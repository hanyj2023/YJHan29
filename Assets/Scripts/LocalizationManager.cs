using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum GameLanguage
{
    KOR,
    ENG
}

public static class LocalizationManager
{
    private const string LanguagePreferenceKey = "GameLanguage";
    private const string StringTableResourceName = "StringTable";

    private static readonly Dictionary<string, LocalizedRow> Rows =
        new Dictionary<string, LocalizedRow>(StringComparer.Ordinal);
    private static readonly HashSet<string> ReportedMissingKeys = new HashSet<string>();
    private static bool loaded;
    private static GameLanguage currentLanguage;

    public static event Action LanguageChanged;

    public static GameLanguage CurrentLanguage
    {
        get
        {
            EnsureLoaded();
            return currentLanguage;
        }
    }

    public static void SetLanguage(GameLanguage language)
    {
        EnsureLoaded();
        if (currentLanguage == language)
        {
            return;
        }

        currentLanguage = language;
        PlayerPrefs.SetString(LanguagePreferenceKey, language.ToString());
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static string Get(string stringKey, params object[] arguments)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(stringKey))
        {
            return string.Empty;
        }

        if (!Rows.TryGetValue(stringKey, out LocalizedRow row))
        {
            if (ReportedMissingKeys.Add(stringKey))
            {
                Debug.LogWarning($"StringTable has no entry for '{stringKey}'.");
            }
            return $"[{stringKey}]";
        }

        string value = currentLanguage == GameLanguage.KOR ? row.Korean : row.English;
        if (arguments == null || arguments.Length == 0)
        {
            return value;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, value, arguments);
        }
        catch (FormatException exception)
        {
            Debug.LogError($"StringTable entry '{stringKey}' has an invalid format: {exception.Message}");
            return value;
        }
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        currentLanguage = ParseSavedLanguage();
        TextAsset csv = Resources.Load<TextAsset>(StringTableResourceName);
        if (csv == null)
        {
            Debug.LogError($"Resources/{StringTableResourceName}.csv was not found.");
            return;
        }

        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            Debug.LogError("StringTable.csv is empty.");
            return;
        }

        string[] headers = ParseCsvLine(lines[0]);
        int keyColumn = FindColumn(headers, "StringKey");
        int koreanColumn = FindColumn(headers, "KOR");
        int englishColumn = FindColumn(headers, "ENG");
        if (keyColumn < 0 || koreanColumn < 0 || englishColumn < 0)
        {
            Debug.LogError("StringTable.csv requires StringKey, KOR and ENG columns.");
            return;
        }

        int lastColumn = Math.Max(keyColumn, Math.Max(koreanColumn, englishColumn));
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string[] values = ParseCsvLine(lines[lineIndex]);
            if (values.Length <= lastColumn)
            {
                Debug.LogWarning($"StringTable.csv line {lineIndex + 1} was skipped because it is incomplete.");
                continue;
            }

            string key = values[keyColumn].Trim();
            if (key.Length == 0 || Rows.ContainsKey(key))
            {
                Debug.LogWarning($"StringTable.csv line {lineIndex + 1} has an empty or duplicate key.");
                continue;
            }

            Rows.Add(key, new LocalizedRow(values[koreanColumn], values[englishColumn]));
        }
    }

    private static GameLanguage ParseSavedLanguage()
    {
        return Enum.TryParse(PlayerPrefs.GetString(LanguagePreferenceKey, GameLanguage.KOR.ToString()),
            true, out GameLanguage language)
            ? language
            : GameLanguage.KOR;
    }

    private static int FindColumn(string[] headers, string name)
    {
        for (int index = 0; index < headers.Length; index++)
        {
            if (string.Equals(headers[index].Trim().TrimStart('\uFEFF'), name,
                StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new System.Text.StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }
        values.Add(value.ToString());
        return values.ToArray();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Rows.Clear();
        ReportedMissingKeys.Clear();
        loaded = false;
        LanguageChanged = null;
    }

    private readonly struct LocalizedRow
    {
        public readonly string Korean;
        public readonly string English;

        public LocalizedRow(string korean, string english)
        {
            Korean = korean;
            English = english;
        }
    }
}
