using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum SpawnShape
{
    CIRCLE,
    LINE,
    CROWD,
    SQUARE,
    TORNADO
}

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
    private float stageStartTime;

    public int CurrentStageId => currentStageId;

    // Called during scene initialization, before Start builds the spawn schedules.
    public void SetStage(int stageId) => currentStageId = stageId;

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
        if (LevelUpPanelController.IsPaused)
            return;

        float elapsed = Time.time - stageStartTime;

        for (int i = 0; i < schedules.Count; i++)
        {
            SpawnSchedule schedule = schedules[i];
            while (elapsed >= schedule.NextWaveTime
                && schedule.WaveIndex < schedule.Rule.WaveCount
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
        int remainingAliveSlots = rule.MaxAliveCap - schedule.AliveCount;
        int spawnCount = Mathf.Min(requested, remainingBudget, remainingAliveSlots);

        if (spawnCount <= 0)
        {
            return;
        }

        WaveFormation formation = CreateWaveFormation();

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnMonster(schedule, formation, i, spawnCount);
        }
    }

    private void SpawnMonster(
        SpawnSchedule schedule,
        WaveFormation formation,
        int index,
        int count)
    {
        SpawnInstruction instruction = CreateSpawnInstruction(
            schedule.Rule.SpawnShape, formation, index, count);
        Vector3 spawnPosition = instruction.Position;
        GameObject instance = Instantiate(schedule.Prefab, spawnPosition, Quaternion.identity);
        MonsterMovement movement = instance.GetComponent<MonsterMovement>();
        MonsterController controller = instance.GetComponent<MonsterController>();

        if (movement == null || controller == null)
        {
            Debug.LogError(
                $"Prefab '{schedule.Rule.MonsterId}' needs MonsterMovement and MonsterController components.",
                instance);
            Destroy(instance);
            return;
        }

        float maxHP = schedule.Rule.MaxHP > 0f
            ? schedule.Rule.MaxHP
            : controller.MaxHP;
        float attackDamage = schedule.Rule.AttackDamage >= 0f
            ? schedule.Rule.AttackDamage
            : controller.AttackDamage;
        controller.InitializeStats(maxHP, attackDamage);
        schedule.SpawnedTotal++;
        schedule.AliveCount++;
        movement.Initialize(
            player,
            () => DecreaseAliveCount(schedule),
            schedule.Rule.SpawnShape,
            instruction.TornadoDirection,
            instruction.Phase);
    }

    private WaveFormation CreateWaveFormation()
    {
        Vector3 playerPosition = player.position;
        float circleRadius = CalculateRadiusOutsideCameraView(playerPosition);
        float extraDistance = outsidePadding
            + UnityEngine.Random.Range(0f, randomExtraDistance);
        int tornadoDirection = UnityEngine.Random.value < 0.5f ? -1 : 1;
        return new WaveFormation(
            playerPosition,
            circleRadius + extraDistance,
            UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            tornadoDirection);
    }

    private static SpawnInstruction CreateSpawnInstruction(
        SpawnShape shape,
        WaveFormation formation,
        int index,
        int count)
    {
        float phase = count <= 0 ? 0f : index * Mathf.PI * 2f / count;
        Vector2 offset;

        switch (shape)
        {
            case SpawnShape.LINE:
            {
                Vector2 inwardAxis = DirectionFromAngle(formation.Rotation);
                Vector2 lineAxis = new Vector2(-inwardAxis.y, inwardAxis.x);
                const float lineSpacing = 1.35f;
                offset = inwardAxis * formation.Radius
                    + lineAxis * ((index - ((count - 1) * 0.5f)) * lineSpacing);
                break;
            }
            case SpawnShape.CROWD:
            {
                Vector2 crowdCenter = DirectionFromAngle(formation.Rotation) * formation.Radius;
                float clusterAngle = index * 2.399963f;
                float clusterRadius = 0.55f * Mathf.Sqrt(index);
                offset = crowdCenter + DirectionFromAngle(clusterAngle) * clusterRadius;
                break;
            }
            case SpawnShape.SQUARE:
            {
                Vector2 perimeter = PointOnSquarePerimeter((index + 0.5f) / count);
                offset = Rotate(perimeter, formation.Rotation) * formation.Radius;
                break;
            }
            case SpawnShape.TORNADO:
            case SpawnShape.CIRCLE:
            default:
            {
                float angle = formation.Rotation + phase;
                offset = DirectionFromAngle(angle) * formation.Radius;
                break;
            }
        }

        Vector3 position = formation.Center + (Vector3)offset;
        position.z = formation.Center.z;
        return new SpawnInstruction(position, formation.TornadoDirection, phase);
    }

    private static Vector2 PointOnSquarePerimeter(float progress)
    {
        float sideProgress = Mathf.Repeat(progress, 1f) * 4f;
        int side = Mathf.FloorToInt(sideProgress);
        float t = sideProgress - side;

        switch (side)
        {
            case 0: return new Vector2(-1f + (2f * t), 1f);
            case 1: return new Vector2(1f, 1f - (2f * t));
            case 2: return new Vector2(1f - (2f * t), -1f);
            default: return new Vector2(-1f, -1f + (2f * t));
        }
    }

    private static Vector2 DirectionFromAngle(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static Vector2 Rotate(Vector2 point, float angle)
    {
        float cosine = Mathf.Cos(angle);
        float sine = Mathf.Sin(angle);
        return new Vector2(
            point.x * cosine - point.y * sine,
            point.x * sine + point.y * cosine);
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
            "MaxAliveCap", "SpawnShape"
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

    private static void DecreaseAliveCount(SpawnSchedule schedule)
    {
        schedule.AliveCount = Mathf.Max(0, schedule.AliveCount - 1);
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
            MaxAliveCap = int.Parse(GetValue(values, columns, "MaxAliveCap"), culture),
            SpawnShape = ParseSpawnShape(GetValue(values, columns, "SpawnShape")),
            WaveCount = GetOptionalInt(values, columns, "WaveCount", int.MaxValue, culture),
            MaxHP = GetOptionalFloat(values, columns, "MaxHP", -1f, culture),
            AttackDamage = GetOptionalFloat(values, columns, "AttackDamage", -1f, culture)
        };
    }

    private static SpawnShape ParseSpawnShape(string value)
    {
        if (Enum.TryParse(value.Trim(), true, out SpawnShape result))
        {
            return result;
        }

        throw new FormatException(
            $"SpawnShape '{value}' is invalid. Use CIRCLE, LINE, CROWD, SQUARE, or TORNADO.");
    }

    private static int GetOptionalInt(
        string[] values, Dictionary<string, int> columns, string column,
        int fallback, CultureInfo culture)
    {
        return columns.ContainsKey(column)
            ? int.Parse(GetValue(values, columns, column), culture)
            : fallback;
    }

    private static float GetOptionalFloat(
        string[] values, Dictionary<string, int> columns, string column,
        float fallback, CultureInfo culture)
    {
        return columns.ContainsKey(column)
            ? float.Parse(GetValue(values, columns, column), culture)
            : fallback;
    }

    private static string GetValue(
        string[] values,
        Dictionary<string, int> columns,
        string column)
    {
        return values[columns[column]];
    }

    internal static string[] ParseCsvLine(string line)
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
        public int AliveCount;
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
        public SpawnShape SpawnShape;
        public int WaveCount;
        public float MaxHP;
        public float AttackDamage;

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
                || TotalBudget < 0 || MaxAliveCap < 0 || WaveCount < 0)
            {
                reason = "Spawn counts cannot be negative.";
                return false;
            }

            if ((MaxHP >= 0f && MaxHP < 1f) || AttackDamage < -1f)
            {
                reason = "MaxHP must be at least 1 and AttackDamage cannot be negative.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    private readonly struct WaveFormation
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Rotation;
        public readonly int TornadoDirection;

        public WaveFormation(Vector3 center, float radius, float rotation, int tornadoDirection)
        {
            Center = center;
            Radius = radius;
            Rotation = rotation;
            TornadoDirection = tornadoDirection;
        }
    }

    private readonly struct SpawnInstruction
    {
        public readonly Vector3 Position;
        public readonly int TornadoDirection;
        public readonly float Phase;

        public SpawnInstruction(Vector3 position, int tornadoDirection, float phase)
        {
            Position = position;
            TornadoDirection = tornadoDirection;
            Phase = phase;
        }
    }
}
