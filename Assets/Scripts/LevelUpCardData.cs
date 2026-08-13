using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum LevelUpCardEffect
{
    ATKUp
}

public sealed class LevelUpCardData
{
    public int Id { get; }
    public int Rate { get; }
    public int Required { get; }
    public string Icon { get; }
    public string Description { get; }
    public LevelUpCardEffect Effect { get; }
    public int Value { get; }

    public LevelUpCardData(
        int id,
        int rate,
        int required,
        string icon,
        string description,
        LevelUpCardEffect effect,
        int value)
    {
        Id = id;
        Rate = rate;
        Required = required;
        Icon = icon;
        Description = description;
        Effect = effect;
        Value = value;
    }
}

public static class LevelUpCardTable
{
    private static readonly string[] RequiredColumns =
    {
        "ID", "Rate", "Required", "Icon", "Desc", "Effect", "Value"
    };

    public static string Decode(TextAsset csv)
    {
        byte[] bytes = csv.bytes;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Korean Windows spreadsheet/text editors frequently save CSV as
            // CP949. TextAsset.text assumes UTF-8, so decode the original bytes.
            return Encoding.GetEncoding(949).GetString(bytes);
        }
    }

    public static bool TryLoad(
        string csvText,
        UnityEngine.Object logContext,
        out List<LevelUpCardData> cards)
    {
        cards = new List<LevelUpCardData>();
        string[] lines = csvText.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            Debug.LogError("LevelUpCard.csv does not contain any card data.", logContext);
            return false;
        }

        Dictionary<string, int> columns = BuildColumnMap(ParseCsvLine(lines[0]));
        foreach (string requiredColumn in RequiredColumns)
        {
            if (!columns.ContainsKey(requiredColumn))
            {
                Debug.LogError(
                    $"LevelUpCard.csv is missing column '{requiredColumn}'.",
                    logContext);
                return false;
            }
        }

        HashSet<int> ids = new HashSet<int>();
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string[] values = ParseCsvLine(lines[lineIndex]);
            try
            {
                int id = ParseInt(values, columns, "ID");
                int rate = ParseInt(values, columns, "Rate");
                int required = ParseInt(values, columns, "Required");
                int value = ParseInt(values, columns, "Value");
                string icon = GetValue(values, columns, "Icon").Trim();
                string description = GetValue(values, columns, "Desc").Trim();
                string effectName = GetValue(values, columns, "Effect").Trim();

                if (id <= 0 || rate <= 0 || required < 0 || value < 0)
                {
                    throw new FormatException(
                        "ID and Rate must be positive; Required and Value cannot be negative.");
                }

                if (!ids.Add(id))
                {
                    throw new FormatException($"ID {id} is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(icon)
                    || string.IsNullOrWhiteSpace(description))
                {
                    throw new FormatException("Icon and Desc cannot be empty.");
                }

                if (!Enum.TryParse(effectName, true, out LevelUpCardEffect effect))
                {
                    throw new FormatException($"Effect '{effectName}' is not supported.");
                }

                cards.Add(new LevelUpCardData(
                    id, rate, required, icon, description, effect, value));
            }
            catch (Exception exception) when (
                exception is FormatException
                || exception is IndexOutOfRangeException
                || exception is OverflowException)
            {
                Debug.LogError(
                    $"LevelUpCard.csv line {lineIndex + 1} is invalid: {exception.Message}",
                    logContext);
                cards.Clear();
                return false;
            }
        }

        if (cards.Count < 3)
        {
            Debug.LogError("LevelUpCard.csv needs at least three valid cards.", logContext);
            cards.Clear();
            return false;
        }

        int initiallyEligibleCount = 0;
        foreach (LevelUpCardData card in cards)
        {
            if (card.Required == 0)
            {
                initiallyEligibleCount++;
            }
        }

        if (initiallyEligibleCount < 3)
        {
            Debug.LogError(
                "LevelUpCard.csv needs at least three cards with Required=0 for the first level-up.",
                logContext);
            cards.Clear();
            return false;
        }

        foreach (LevelUpCardData card in cards)
        {
            if (card.Required != 0 && !ids.Contains(card.Required))
            {
                Debug.LogError(
                    $"Card ID {card.Id} requires missing card ID {card.Required}.",
                    logContext);
                cards.Clear();
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        Dictionary<string, int> columns =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < headers.Length; index++)
        {
            columns[headers[index].Trim().TrimStart('\uFEFF')] = index;
        }

        return columns;
    }

    private static int ParseInt(
        string[] values,
        Dictionary<string, int> columns,
        string column)
    {
        return int.Parse(
            GetValue(values, columns, column).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture);
    }

    private static string GetValue(
        string[] values,
        Dictionary<string, int> columns,
        string column)
    {
        int index = columns[column];
        if (index >= values.Length)
        {
            throw new IndexOutOfRangeException($"Column '{column}' has no value.");
        }

        return values[index];
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        bool inQuotes = false;
        int valueStart = 0;

        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
            }
            else if (line[index] == ',' && !inQuotes)
            {
                values.Add(Unquote(line.Substring(valueStart, index - valueStart)));
                valueStart = index + 1;
            }
        }

        if (inQuotes)
        {
            throw new FormatException("A quoted CSV value is not closed.");
        }

        values.Add(Unquote(line.Substring(valueStart)));
        return values.ToArray();
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
        }

        return value;
    }
}
