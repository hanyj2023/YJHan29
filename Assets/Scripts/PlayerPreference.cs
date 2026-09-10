using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class PlayerPreference
{
    [Serializable]
    private sealed class SaveData
    {
        public long coins;
        public List<StatusLevelData> statusLevels = new List<StatusLevelData>();
    }

    [Serializable]
    private sealed class StatusLevelData
    {
        public string statusName;
        public int level;
    }

    private static SaveData data;

    public static event Action<long> CoinsChanged;
    public static event Action<string, int> StatusLevelChanged;

    public static string SavePath => Path.Combine(
        Application.persistentDataPath,
        "PlayerPreference.json");

    public static long Coins
    {
        get
        {
            EnsureLoaded();
            return data.coins;
        }
    }

    public static int GetStatusLevel(string statusName)
    {
        EnsureLoaded();
        StatusLevelData saved = FindStatus(statusName);
        return saved == null ? 0 : Mathf.Clamp(saved.level, 0, 5);
    }

    public static void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        EnsureLoaded();
        data.coins = amount > long.MaxValue - data.coins
            ? long.MaxValue
            : data.coins + amount;
        Save();
        CoinsChanged?.Invoke(data.coins);
    }

    public static bool TryPurchaseUpgrade(
        string statusName,
        int cost,
        int maxLevel,
        out int newLevel)
    {
        EnsureLoaded();
        StatusLevelData saved = FindStatus(statusName);
        int currentLevel = saved == null ? 0 : Mathf.Clamp(saved.level, 0, maxLevel);
        newLevel = currentLevel;
        if (cost < 0 || currentLevel >= maxLevel || data.coins < cost)
        {
            return false;
        }

        if (saved == null)
        {
            saved = new StatusLevelData { statusName = statusName };
            data.statusLevels.Add(saved);
        }

        data.coins -= cost;
        saved.level = currentLevel + 1;
        newLevel = saved.level;
        Save();
        CoinsChanged?.Invoke(data.coins);
        StatusLevelChanged?.Invoke(statusName, newLevel);
        return true;
    }

    private static StatusLevelData FindStatus(string statusName)
    {
        return data.statusLevels.Find(saved =>
            string.Equals(saved.statusName, statusName, StringComparison.Ordinal));
    }

    private static void EnsureLoaded()
    {
        if (data != null)
        {
            return;
        }

        data = new SaveData();
        try
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath, Encoding.UTF8);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded != null)
                {
                    data = loaded;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPreference could not be loaded: {exception.Message}");
            data = new SaveData();
        }

        data.coins = Math.Max(0L, data.coins);
        if (data.statusLevels == null)
        {
            data.statusLevels = new List<StatusLevelData>();
        }

        foreach (StatusLevelData status in data.statusLevels)
        {
            status.statusName = NormalizeLegacyStatusKey(status.statusName);
        }
    }

    private static string NormalizeLegacyStatusKey(string statusKey)
    {
        switch (statusKey)
        {
            case "공격력": return "status.attack_power";
            case "이동 속도": return "status.move_speed";
            case "자석 반경": return "status.magnet_radius";
            case "치명타율": return "status.critical_chance";
            case "치명타 대미지 배율": return "status.critical_damage";
            case "부활": return "status.revive";
            default: return statusKey;
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogError($"PlayerPreference could not be saved: {exception.Message}");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        CoinsChanged = null;
        StatusLevelChanged = null;
    }
}
