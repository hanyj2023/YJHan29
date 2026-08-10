using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageMonsterSpawner : MonoBehaviour
{
    [Header("Stage")]
    [SerializeField, Min(1)]
    private int currentStageId = 1;

    [Tooltip("If omitted, Resources/StageMonster.csv is loaded automatically.")]
    [SerializeField]
    private TextAsset stageMonsterCsv;

    [Header("Scene References")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private Camera targetCamera;

    [Header("Spawn Area")]
    [Tooltip("Minimum world-space distance beyond the camera view boundary.")]
    [SerializeField, Min(0.01f)]
    private float outsidePadding = 1f;

    [Tooltip("Adds a random radial offset so monsters do not form a perfect ring.")]
    [SerializeField, Min(0f)]
    private float randomExtraDistance = 2f;

    private readonly List<SpawnSchedule> schedules = new List<SpawnSchedule>();
    private readonly Dictionary<string, int> aliveByMonsterId =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private float stageStartTime;

    private void Start()
    {
        if (!ResolveSceneReferences())
        {
            enabled = false;
            return;
        }

        TextAsset csv = stageMonsterCsv != null
            ? stageMonsterCsv
            : Resources.Load<TextAsset>("StageMonster");

        if (csv == null)
        {
            Debug.LogError("StageMonster.csv could not be found in Resources.", this);
            enabled = false;
            return;
        }

        LoadSchedules(csv.text);
        stageStartTime = Time.time;

        if (schedules.Count == 0)
        {
            Debug.LogWarning(
                $"No valid monster spawn rules were found for StageId {currentStageId}.",
                this);
        }
    }

    private void Update()
    {
        float elapsed = Time.time - stageStartTime;

        for (int i = 0; i < schedules.Count; i++)
        {
            SpawnSchedule schedule = schedules[i];
            while (elapsed >= schedule.NextWaveTime
                && schedule.SpawnedTotal < schedule.Rule.TotalBudget)
            {
                RunWave(schedule);
                schedule.WaveIndex++;
                schedule.NextWaveTime += schedule.Rule.WaveIntervalSec;
            }
        }
    }

    private void RunWave(SpawnSchedule schedule)
    {
        StageMonsterRule rule = schedule.Rule;
        int requested = Mathf.Min(
            rule.WaveSizeStart + (schedule.WaveIndex * rule.WaveSizeGrowth),
            rule.WaveSizeMax);
        int remainingBudget = rule.TotalBudget - schedule.SpawnedTotal;
        int alive = GetAliveCount(rule.MonsterId);
        int remainingAliveSlots = rule.MaxAliveCap - alive;
        int spawnCount = Mathf.Min(requested, remainingBudget, remainingAliveSlots);

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnMonster(schedule);
        }
    }

    private void SpawnMonster(SpawnSchedule schedule)
    {
        Vector3 spawnPosition = FindSpawnPositionOutsideView();
        GameObject instance = Instantiate(schedule.Prefab, spawnPosition, Quaternion.identity);
        MonsterMovement movement = instance.GetComponent<MonsterMovement>();

        if (movement == null)
        {
            Debug.LogError(
                $"Prefab '{schedule.Rule.MonsterId}' needs a MonsterMovement component.",
                instance);
            Destroy(instance);
            return;
        }

        string monsterId = schedule.Rule.MonsterId;
        schedule.SpawnedTotal++;
        aliveByMonsterId[monsterId] = GetAliveCount(monsterId) + 1;
        movement.Initialize(player, () => DecreaseAliveCount(monsterId));
    }

    private Vector3 FindSpawnPositionOutsideView()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 playerPosition = player.position;
        float circleRadius = CalculateRadiusOutsideCameraView(playerPosition);

        float extraDistance = outsidePadding
            + UnityEngine.Random.Range(0f, randomExtraDistance);
        Vector3 result = playerPosition
            + (Vector3)(direction * (circleRadius + extraDistance));
        result.z = playerPosition.z;
        return result;
    }

    private float CalculateRadiusOutsideCameraView(Vector3 center)
    {
        float depth = Mathf.Abs(center.z - targetCamera.transform.position.z);
        float maxSqrDistance = 0f;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Vector3 corner = targetCamera.ViewportToWorldPoint(
                    new Vector3(x, y, depth));
                maxSqrDistance = Mathf.Max(
                    maxSqrDistance,
                    ((Vector2)(corner - center)).sqrMagnitude);
            }
        }

        return Mathf.Sqrt(maxSqrDistance);
    }

    private void LoadSchedules(string csvText)
    {
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return;
        }

        Dictionary<string, int> columns = BuildColumnMap(ParseCsvLine(lines[0]));
        string[] requiredColumns =
        {
            "StageId", "MonsterId", "SpawnStartSec", "WaveIntervalSec",
            "WaveSizeStart", "WaveSizeGrowth", "WaveSizeMax", "TotalBudget",
            "MaxAliveCap"
        };

        for (int i = 0; i < requiredColumns.Length; i++)
        {
            if (!columns.ContainsKey(requiredColumns[i]))
            {
                Debug.LogError($"StageMonster.csv is missing column '{requiredColumns[i]}'.", this);
                return;
            }
        }

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string[] values = ParseCsvLine(lines[lineIndex]);
            try
            {
                StageMonsterRule rule = ParseRule(values, columns);
                if (rule.StageId != currentStageId)
                {
                    continue;
                }

                if (!rule.IsValid(out string reason))
                {
                    Debug.LogWarning($"StageMonster.csv line {lineIndex + 1} was skipped: {reason}", this);
                    continue;
                }

                GameObject prefab = Resources.Load<GameObject>("Prefabs/" + rule.MonsterId);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"Monster prefab 'Resources/Prefabs/{rule.MonsterId}' was not found.",
                        this);
                    continue;
                }

                schedules.Add(new SpawnSchedule(rule, prefab));
            }
            catch (Exception exception) when (
                exception is FormatException
                || exception is IndexOutOfRangeException
                || exception is OverflowException)
            {
                Debug.LogWarning(
                    $"StageMonster.csv line {lineIndex + 1} was skipped: {exception.Message}",
                    this);
            }
        }
    }

    private bool ResolveSceneReferences()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            player = playerMovement != null ? playerMovement.transform : null;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (player != null && targetCamera != null)
        {
            return true;
        }

        Debug.LogError("StageMonsterSpawner requires a Player and a Camera.", this);
        return false;
    }

    private int GetAliveCount(string monsterId)
    {
        return aliveByMonsterId.TryGetValue(monsterId, out int count) ? count : 0;
    }

    private void DecreaseAliveCount(string monsterId)
    {
        int nextCount = Mathf.Max(0, GetAliveCount(monsterId) - 1);
        aliveByMonsterId[monsterId] = nextCount;
    }

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            result[headers[i].Trim().TrimStart('\uFEFF')] = i;
        }

        return result;
    }

    private static StageMonsterRule ParseRule(
        string[] values,
        Dictionary<string, int> columns)
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        return new StageMonsterRule
        {
            StageId = int.Parse(GetValue(values, columns, "StageId"), culture),
            MonsterId = GetValue(values, columns, "MonsterId").Trim(),
            SpawnStartSec = float.Parse(GetValue(values, columns, "SpawnStartSec"), culture),
            WaveIntervalSec = float.Parse(GetValue(values, columns, "WaveIntervalSec"), culture),
            WaveSizeStart = int.Parse(GetValue(values, columns, "WaveSizeStart"), culture),
            WaveSizeGrowth = int.Parse(GetValue(values, columns, "WaveSizeGrowth"), culture),
            WaveSizeMax = int.Parse(GetValue(values, columns, "WaveSizeMax"), culture),
            TotalBudget = int.Parse(GetValue(values, columns, "TotalBudget"), culture),
            MaxAliveCap = int.Parse(GetValue(values, columns, "MaxAliveCap"), culture)
        };
    }

    private static string GetValue(
        string[] values,
        Dictionary<string, int> columns,
        string column)
    {
        return values[columns[column]];
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        bool inQuotes = false;
        int valueStart = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                values.Add(Unquote(line.Substring(valueStart, i - valueStart)));
                valueStart = i + 1;
            }
        }

        values.Add(Unquote(line.Substring(valueStart)));
        return values.ToArray();
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
        }

        return value;
    }

    private void OnValidate()
    {
        currentStageId = Mathf.Max(1, currentStageId);
        outsidePadding = Mathf.Max(0.01f, outsidePadding);
        randomExtraDistance = Mathf.Max(0f, randomExtraDistance);
    }

    private sealed class SpawnSchedule
    {
        public readonly StageMonsterRule Rule;
        public readonly GameObject Prefab;
        public int WaveIndex;
        public int SpawnedTotal;
        public float NextWaveTime;

        public SpawnSchedule(StageMonsterRule rule, GameObject prefab)
        {
            Rule = rule;
            Prefab = prefab;
            NextWaveTime = rule.SpawnStartSec;
        }
    }

    private sealed class StageMonsterRule
    {
        public int StageId;
        public string MonsterId;
        public float SpawnStartSec;
        public float WaveIntervalSec;
        public int WaveSizeStart;
        public int WaveSizeGrowth;
        public int WaveSizeMax;
        public int TotalBudget;
        public int MaxAliveCap;

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(MonsterId))
            {
                reason = "MonsterId is empty.";
                return false;
            }

            if (SpawnStartSec < 0f || WaveIntervalSec <= 0f)
            {
                reason = "SpawnStartSec must be non-negative and WaveIntervalSec must be positive.";
                return false;
            }

            if (WaveSizeStart < 0 || WaveSizeGrowth < 0 || WaveSizeMax < 0
                || TotalBudget < 0 || MaxAliveCap < 0)
            {
                reason = "Spawn counts cannot be negative.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
