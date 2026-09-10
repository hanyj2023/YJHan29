using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class StageData
{
    public int Id { get; }
    public string NameKey { get; }
    public string Name => LocalizationManager.Get(NameKey);
    public string TilemapName { get; }
    public int Duration { get; }

    public StageData(int id, string nameKey, string tilemapName, int duration)
    {
        Id = id;
        NameKey = nameKey;
        TilemapName = tilemapName;
        Duration = duration;
    }
}

public static class StageTable
{
    public static int SelectedStageId { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection() => SelectedStageId = 0;

    public static List<StageData> Load()
    {
        var stages = new List<StageData>();
        TextAsset csv = Resources.Load<TextAsset>("Stage");
        if (csv == null)
        {
            Debug.LogError("Resources/Stage.csv was not found.");
            return stages;
        }

        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return stages;
        string[] headers = StageMonsterSpawner.ParseCsvLine(lines[0]);
        int idColumn = Array.FindIndex(headers, h => h.TrimStart('\uFEFF').Equals("StageId", StringComparison.OrdinalIgnoreCase));
        int nameColumn = Array.FindIndex(headers, h => h.Equals("StageNameKey", StringComparison.OrdinalIgnoreCase));
        int mapColumn = Array.FindIndex(headers, h => h.Equals("Tilemapname", StringComparison.OrdinalIgnoreCase));
        int timeColumn = Array.FindIndex(headers, h => h.Equals("Time", StringComparison.OrdinalIgnoreCase));
        if (idColumn < 0 || nameColumn < 0 || mapColumn < 0 || timeColumn < 0)
        {
            Debug.LogError("Stage.csv requires StageId, StageNameKey, Tilemapname and Time columns.");
            return stages;
        }

        int lastColumn = Math.Max(Math.Max(idColumn, nameColumn), Math.Max(mapColumn, timeColumn));
        var ids = new HashSet<int>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            string[] values = StageMonsterSpawner.ParseCsvLine(lines[i]);
            if (values.Length <= lastColumn
                || !int.TryParse(values[idColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id < 1
                || !int.TryParse(values[timeColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out int time) || time < 0
                || string.IsNullOrWhiteSpace(values[mapColumn]) || !ids.Add(id))
            {
                Debug.LogError($"Stage.csv row {i + 1} is invalid or has a duplicate StageId.");
                continue;
            }
            stages.Add(new StageData(id, values[nameColumn], values[mapColumn], time));
        }
        return stages;
    }
}
