using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum PersistentStatusType
{
    AttackPower,
    MoveSpeed,
    MagnetRadius,
    CriticalChance,
    CriticalDamageMultiplier,
    Revive
}

public sealed class StatusUpgradeData
{
    private readonly float[] levelValues;

    public PersistentStatusType Type { get; }
    public string StatusNameKey { get; }
    public string StatusName => LocalizationManager.Get(StatusNameKey);
    public int UpgradeCost { get; }
    public int MaxLevel => levelValues.Length - 1;

    public StatusUpgradeData(
        PersistentStatusType type,
        string statusNameKey,
        float[] values,
        int upgradeCost)
    {
        Type = type;
        StatusNameKey = statusNameKey;
        levelValues = values;
        UpgradeCost = upgradeCost;
    }

    public float GetValue(int level)
    {
        return levelValues[Mathf.Clamp(level, 0, MaxLevel)];
    }
}

public static class StatusUpgradeTable
{
    private static readonly string[] RequiredColumns =
    {
        "StatusNameKey", "Level0", "Level1", "Level2", "Level3", "Level4", "Level5", "UpgradeCost"
    };

    private static List<StatusUpgradeData> cachedRows;

    public static IReadOnlyList<StatusUpgradeData> Load()
    {
        if (cachedRows != null)
        {
            return cachedRows;
        }

        cachedRows = new List<StatusUpgradeData>();
        TextAsset csv = Resources.Load<TextAsset>("StatusUpgrade");
        if (csv == null)
        {
            Debug.LogError("Resources/StatusUpgrade.csv was not found.");
            return cachedRows;
        }

        string[] lines = csv.text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            Debug.LogError("StatusUpgrade.csv does not contain any status data.");
            return cachedRows;
        }

        string[] headers = StageMonsterSpawner.ParseCsvLine(lines[0]);
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < headers.Length; index++)
        {
            columns[headers[index].Trim().TrimStart('\uFEFF')] = index;
        }

        foreach (string column in RequiredColumns)
        {
            if (!columns.ContainsKey(column))
            {
                Debug.LogError($"StatusUpgrade.csv is missing column '{column}'.");
                cachedRows.Clear();
                return cachedRows;
            }
        }

        var loadedTypes = new HashSet<PersistentStatusType>();
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            try
            {
                string[] values = StageMonsterSpawner.ParseCsvLine(lines[lineIndex]);
                string statusNameKey = GetValue(values, columns, "StatusNameKey").Trim();
                if (!TryGetType(statusNameKey, out PersistentStatusType type))
                {
                    throw new FormatException($"Unknown status string key '{statusNameKey}'.");
                }

                if (!loadedTypes.Add(type))
                {
                    throw new FormatException($"Status '{statusNameKey}' is duplicated.");
                }

                var levelValues = new float[6];
                for (int level = 0; level <= 5; level++)
                {
                    levelValues[level] = float.Parse(
                        GetValue(values, columns, $"Level{level}").Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture);
                    if (levelValues[level] < 0f)
                    {
                        throw new FormatException("Status values cannot be negative.");
                    }
                }

                int cost = int.Parse(
                    GetValue(values, columns, "UpgradeCost").Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                if (cost < 0)
                {
                    throw new FormatException("Upgrade cost cannot be negative.");
                }

                cachedRows.Add(new StatusUpgradeData(type, statusNameKey, levelValues, cost));
            }
            catch (Exception exception) when (
                exception is FormatException
                || exception is IndexOutOfRangeException
                || exception is OverflowException)
            {
                Debug.LogError(
                    $"StatusUpgrade.csv line {lineIndex + 1} is invalid: {exception.Message}");
                cachedRows.Clear();
                return cachedRows;
            }
        }

        if (cachedRows.Count != Enum.GetValues(typeof(PersistentStatusType)).Length)
        {
            Debug.LogError("StatusUpgrade.csv must contain exactly one row for every status type.");
            cachedRows.Clear();
        }

        return cachedRows;
    }

    public static StatusUpgradeData Get(PersistentStatusType type)
    {
        foreach (StatusUpgradeData row in Load())
        {
            if (row.Type == type)
            {
                return row;
            }
        }

        return null;
    }

    public static float GetSavedValue(PersistentStatusType type)
    {
        StatusUpgradeData row = Get(type);
        return row == null ? 0f : row.GetValue(PlayerPreference.GetStatusLevel(row.StatusNameKey));
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

    private static bool TryGetType(string name, out PersistentStatusType type)
    {
        switch (name)
        {
            case "status.attack_power": type = PersistentStatusType.AttackPower; return true;
            case "status.move_speed": type = PersistentStatusType.MoveSpeed; return true;
            case "status.magnet_radius": type = PersistentStatusType.MagnetRadius; return true;
            case "status.critical_chance": type = PersistentStatusType.CriticalChance; return true;
            case "status.critical_damage": type = PersistentStatusType.CriticalDamageMultiplier; return true;
            case "status.revive": type = PersistentStatusType.Revive; return true;
            default: type = default; return false;
        }
    }
}
