using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class VampireSurvivorsBalanceWindow : EditorWindow
{
    // All editor-only balance tooling lives in Unity's normal editor assembly.
    private const string StageMonsterPath = "Assets/Resources/StageMonster.csv";
    private const string StagePath = "Assets/Resources/Stage.csv";
    private const float PreviewStep = 0.5f;

    [SerializeField] private float playerHealth = 100f;
    [SerializeField] private float playerAttack = 10f;
    [SerializeField] private float survivalSeconds = 60f;
    [SerializeField] private float playerMoveSpeed = 20f;
    [SerializeField] private float monsterMoveSpeed = 2f;
    [SerializeField] private int stageId = 1;
    [SerializeField] private float firstMonsterHealth = 15f;
    [SerializeField] private int monsterTypeCount = 2;
    [SerializeField] private int phaseCount = 6;
    [SerializeField] private float spawnLoad = 1.25f;
    [SerializeField] private float minimumWaveInterval = 1.5f;
    [SerializeField] private float maximumWaveInterval = 5f;
    [SerializeField] private AnimationCurve stageDifficulty = new AnimationCurve(
        new Keyframe(1f, 1f), new Keyframe(5f, 1.45f), new Keyframe(10f, 2.2f));
    [SerializeField] private AnimationCurve pressure = new AnimationCurve(
        new Keyframe(0f, 0.75f), new Keyframe(0.35f, 1.1f),
        new Keyframe(0.72f, 1.9f), new Keyframe(1f, 0.6f));
    [SerializeField] private AnimationCurve skillDpsGrowth = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.5f, 1.35f),
        new Keyframe(0.75f, 1.9f), new Keyframe(1f, 5f));
    [SerializeField] private AnimationCurve monsterHealthGrowth = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.7f, 1.45f), new Keyframe(1f, 1.7f));
    [SerializeField] private AnimationCurve monsterAttackGrowth = new AnimationCurve(
        new Keyframe(0f, 0.8f), new Keyframe(0.7f, 1.25f), new Keyframe(1f, 1.45f));

    private readonly List<BalanceRow> rows = new List<BalanceRow>();
    private readonly List<PreviewPoint> preview = new List<PreviewPoint>();
    private readonly List<MonsterPrefabInfo> availableMonsters = new List<MonsterPrefabInfo>();
    private Vector2 pageScroll;
    private Vector2 tableScroll;
    private bool hasPreview;
    private string previewMessage = "값과 곡선을 조정한 뒤 '미리보기 생성'을 누르세요.";
    private float detectedBaseDps = 10f;

    [MenuItem("Tools/Vampire Survivors/Balance Tool")]
    private static void Open()
    {
        GetWindow<VampireSurvivorsBalanceWindow>("뱀서라이크 밸런스");
    }

    private void OnEnable()
    {
        minSize = new Vector2(820f, 650f);
        EditorApplication.projectChanged -= OnProjectChanged;
        EditorApplication.projectChanged += OnProjectChanged;
        RefreshMonsterPrefabs();
        DetectWeaponDps();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        RefreshMonsterPrefabs();
        hasPreview = false;
        previewMessage = "프로젝트의 몬스터 프리팹 목록이 변경되었습니다. 미리보기를 다시 생성하세요.";
        Repaint();
    }

    private void OnGUI()
    {
        pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("뱀서라이크 스테이지 밸런스", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "미리보기는 파일을 바꾸지 않습니다. OK를 눌렀을 때만 CSV, 현재 열린 씬, 프리팹에 반영됩니다.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        DrawRequiredInputs();
        DrawTuningInputs();
        if (EditorGUI.EndChangeCheck())
        {
            hasPreview = false;
            previewMessage = "설정이 변경되었습니다. 미리보기를 다시 생성하세요.";
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("몬스터 프리팹 다시 찾기", GUILayout.Height(28f)))
            {
                RefreshMonsterPrefabs();
                hasPreview = false;
            }
            if (GUILayout.Button("미리보기 생성", GUILayout.Height(28f))) GeneratePreview();
            using (new EditorGUI.DisabledScope(!hasPreview))
            {
                GUI.backgroundColor = new Color(0.55f, 1f, 0.62f);
                if (GUILayout.Button("OK - CSV / 인스펙터에 반영", GUILayout.Height(28f))) ApplyPreview();
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.HelpBox(previewMessage, hasPreview ? MessageType.None : MessageType.Warning);
        if (hasPreview)
        {
            DrawSummary();
            DrawGraph();
            DrawRows();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawRequiredInputs()
    {
        EditorGUILayout.LabelField("내가 정하는 값", EditorStyles.boldLabel);
        playerHealth = EditorGUILayout.FloatField("플레이어 체력", playerHealth);
        playerAttack = EditorGUILayout.FloatField("플레이어 공격력", playerAttack);
        survivalSeconds = EditorGUILayout.FloatField("버텨야 할 시간 (초)", survivalSeconds);
        playerMoveSpeed = EditorGUILayout.FloatField("플레이어 이동속도", playerMoveSpeed);
        monsterMoveSpeed = EditorGUILayout.FloatField("몬스터 이동속도", monsterMoveSpeed);
        stageId = EditorGUILayout.IntField("스테이지 ID", stageId);
        firstMonsterHealth = EditorGUILayout.FloatField("첫 등장 몬스터 체력", firstMonsterHealth);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.FloatField("현재 무기 기준 시작 DPS", detectedBaseDps);
        EditorGUILayout.LabelField(
            "DPS = 공격력 × 발사체 수 × 발사체 배율 ÷ 발사 간격 (현재 열린 씬 기준)",
            EditorStyles.miniLabel);
    }

    private void DrawTuningInputs()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("밸런스 튜닝", EditorStyles.boldLabel);
        monsterTypeCount = EditorGUILayout.IntSlider(
            "몬스터 종류 수", monsterTypeCount, 1, Mathf.Max(1, availableMonsters.Count));
        phaseCount = EditorGUILayout.IntSlider("시간 구간 수", phaseCount, 3, 12);
        spawnLoad = EditorGUILayout.Slider("처리량 대비 스폰 부하", spawnLoad, 1.02f, 2.5f);
        minimumWaveInterval = EditorGUILayout.Slider("최소 웨이브 간격", minimumWaveInterval, 0.25f, 10f);
        maximumWaveInterval = EditorGUILayout.Slider("최대 웨이브 간격", maximumWaveInterval, 0.5f, 15f);
        stageDifficulty = EditorGUILayout.CurveField(
            new GUIContent("스테이지 난이도 (X=ID)", "스테이지 번호별 체력·공격·스폰 배율"),
            stageDifficulty, Color.red, new Rect(1f, 0.25f, 49f, 4.75f));
        pressure = EditorGUILayout.CurveField(
            new GUIContent("스테이지 내 압박 (0~1)", "시간에 따른 처리량 대비 스폰량"),
            pressure, Color.yellow, new Rect(0f, 0.25f, 1f, 2.75f));
        skillDpsGrowth = EditorGUILayout.CurveField(
            new GUIContent("예상 스킬 DPS 성장 (0~1)", "플레이 중 성장한 공격력을 시뮬레이션에 반영"),
            skillDpsGrowth, Color.cyan, new Rect(0f, 0.5f, 1f, 5.5f));
        monsterHealthGrowth = EditorGUILayout.CurveField(
            "몬스터 체력 성장 (0~1)", monsterHealthGrowth, Color.green,
            new Rect(0f, 0.25f, 1f, 3.75f));
        monsterAttackGrowth = EditorGUILayout.CurveField(
            "몬스터 공격력 성장 (0~1)", monsterAttackGrowth, Color.magenta,
            new Rect(0f, 0.25f, 1f, 3.75f));
        string discoveredNames = availableMonsters.Count == 0 ? "사용 가능한 프리팹 없음" :
            string.Join(", ", availableMonsters.Select(item => item.Id));
        string selectedNames = availableMonsters.Count == 0 ? "없음" :
            string.Join(", ", availableMonsters.Take(monsterTypeCount).Select(item => item.Id));
        EditorGUILayout.LabelField("발견된 몬스터 프리팹", discoveredNames, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("밸런싱에 사용할 몬스터", selectedNames, EditorStyles.wordWrappedMiniLabel);
    }

    private void GeneratePreview()
    {
        ClampInputs();
        RefreshMonsterPrefabs();
        DetectWeaponDps();
        rows.Clear();
        preview.Clear();
        if (availableMonsters.Count == 0)
        {
            previewMessage = "Resources/Prefabs에 필요한 컴포넌트를 가진 몬스터 프리팹이 없습니다.";
            hasPreview = false;
            return;
        }

        int usedTypes = Mathf.Clamp(monsterTypeCount, 1, availableMonsters.Count);
        float difficulty = Mathf.Max(0.1f, stageDifficulty.Evaluate(stageId));
        float segmentDuration = survivalSeconds / phaseCount;
        float previousSpawnRate = 0f;
        for (int phase = 0; phase < phaseCount; phase++)
        {
            float start = phase == 0 ? Mathf.Min(1f, survivalSeconds * 0.02f) : phase * segmentDuration;
            float end = phase == phaseCount - 1 ? survivalSeconds : (phase + 1) * segmentDuration;
            float normalized = Mathf.Clamp01(((start + end) * 0.5f) / survivalSeconds);
            float pressureValue = Mathf.Max(0.25f, pressure.Evaluate(normalized));
            float hp = phase == 0 ? firstMonsterHealth : firstMonsterHealth
                * Mathf.Pow(difficulty, 0.55f)
                * Mathf.Max(0.1f, monsterHealthGrowth.Evaluate(normalized));
            hp = Mathf.Max(1f, hp);
            float dps = detectedBaseDps * Mathf.Max(0.1f, skillDpsGrowth.Evaluate(normalized));
            float desiredSpawnRate = Mathf.Max(0.35f, dps / hp * spawnLoad
                * pressureValue * Mathf.Pow(difficulty, 0.35f));
            // After the peak, never drop faster than 10% per phase. The player's
            // late DPS spike still creates a slaughter phase without making the
            // battlefield suddenly feel empty or the spawner look switched off.
            desiredSpawnRate = Mathf.Max(desiredSpawnRate, previousSpawnRate * 0.9f);
            previousSpawnRate = desiredSpawnRate;
            float interval = Mathf.Clamp(3.2f / Mathf.Sqrt(desiredSpawnRate),
                minimumWaveInterval, maximumWaveInterval);
            interval = Mathf.Min(interval, Mathf.Max(0.25f, end - start));
            int waves = Mathf.Max(1, Mathf.FloorToInt((end - start - 0.01f) / interval) + 1);
            int waveStart = Mathf.Max(1, Mathf.CeilToInt(desiredSpawnRate * interval * 0.9f));
            float endPressure = Mathf.Max(0.25f, pressure.Evaluate(Mathf.Clamp01(end / survivalSeconds)));
            int growth = endPressure > pressureValue && waves > 1
                ? Mathf.Max(0, Mathf.CeilToInt(waveStart * (endPressure / pressureValue - 1f) / (waves - 1))) : 0;
            int waveMax = Mathf.Max(waveStart, Mathf.CeilToInt(desiredSpawnRate * interval * 1.35f));
            int budget = CalculateBudget(waveStart, growth, waveMax, waves);
            int aliveCap = Mathf.Max(waveMax * 3,
                Mathf.CeilToInt(desiredSpawnRate * (4f + 4f * pressureValue)));
            float attack = playerHealth * 0.055f * Mathf.Sqrt(difficulty)
                * Mathf.Max(0.1f, monsterAttackGrowth.Evaluate(normalized));
            SpawnShape spawnShape = (SpawnShape)(phase % Enum.GetValues(typeof(SpawnShape)).Length);
            rows.Add(new BalanceRow(stageId, availableMonsters[phase % usedTypes].Id,
                start, interval, waveStart, growth, waveMax, budget, aliveCap, waves,
                hp, Mathf.Max(0f, attack), spawnShape));
        }

        Simulate();
        int spawned = preview.Count == 0 ? 0 : preview[preview.Count - 1].TotalSpawned;
        int peak = preview.Count == 0 ? 0 : preview.Max(point => point.Alive);
        previewMessage = string.Format(CultureInfo.InvariantCulture,
            "스테이지 {0}: 규칙 {1}개, 총 {2}마리, 예상 최대 잔존 {3}마리, 종료 직전 스폰 공백 {4:0.0}초",
            stageId, rows.Count, spawned, peak, FindFinalSpawnGap());
        hasPreview = true;
    }

    private void Simulate()
    {
        var monsters = new List<SimMonster>();
        var waveIndexes = new int[rows.Count];
        var nextWaves = rows.Select(row => row.SpawnStartSec).ToArray();
        var spawnedByRule = new int[rows.Count];
        var aliveByRule = new int[rows.Count];
        int totalSpawned = 0;
        for (float time = 0f; time <= survivalSeconds + 0.001f; time += PreviewStep)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                BalanceRow row = rows[i];
                while (time + 0.001f >= nextWaves[i] && waveIndexes[i] < row.WaveCount
                    && spawnedByRule[i] < row.TotalBudget)
                {
                    int requested = Mathf.Min(row.WaveSizeStart
                        + waveIndexes[i] * row.WaveSizeGrowth, row.WaveSizeMax);
                    int count = Mathf.Min(requested, row.TotalBudget - spawnedByRule[i],
                        row.MaxAliveCap - aliveByRule[i]);
                    for (int spawn = 0; spawn < count; spawn++)
                        monsters.Add(new SimMonster(i, row.MaxHP));
                    spawnedByRule[i] += count;
                    aliveByRule[i] += count;
                    totalSpawned += count;
                    waveIndexes[i]++;
                    nextWaves[i] += row.WaveIntervalSec;
                }
            }

            float normalized = Mathf.Clamp01(time / survivalSeconds);
            float damage = detectedBaseDps * Mathf.Max(0.1f,
                skillDpsGrowth.Evaluate(normalized)) * PreviewStep;
            int cursor = 0;
            while (damage > 0f && cursor < monsters.Count)
            {
                SimMonster monster = monsters[cursor];
                float applied = Mathf.Min(damage, monster.HP);
                monster.HP -= applied;
                damage -= applied;
                if (monster.HP <= 0.001f)
                {
                    aliveByRule[monster.RuleIndex]--;
                    monsters.RemoveAt(cursor);
                }
                else cursor++;
            }
            preview.Add(new PreviewPoint(time, totalSpawned, monsters.Count));
        }
    }

    private void DrawSummary()
    {
        int peak = preview.Count == 0 ? 0 : preview.Max(point => point.Alive);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("미리보기 요약", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"예상 생성: {rows.Sum(row => row.TotalBudget)} 마리");
            EditorGUILayout.LabelField($"최대 잔존: {peak} 마리");
            EditorGUILayout.LabelField($"종료 시 잔존: {(preview.Count == 0 ? 0 : preview[preview.Count - 1].Alive)} 마리");
            EditorGUILayout.LabelField($"종류: {rows.Select(row => row.MonsterId).Distinct().Count()}개");
        }
    }

    private void DrawGraph()
    {
        Rect rect = GUILayoutUtility.GetRect(100f, 190f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.11f, 0.11f, 0.11f));
        if (preview.Count < 2) return;
        int maxAlive = Mathf.Max(1, preview.Max(point => point.Alive));
        int maxSpawned = Mathf.Max(1, preview.Max(point => point.TotalSpawned));
        Handles.BeginGUI();
        Handles.color = new Color(1f, 0.55f, 0.15f);
        Handles.DrawAAPolyLine(3f, BuildGraphPoints(rect, point => point.Alive / (float)maxAlive));
        Handles.color = new Color(0.2f, 0.75f, 1f);
        Handles.DrawAAPolyLine(2f, BuildGraphPoints(rect, point => point.TotalSpawned / (float)maxSpawned));
        Handles.EndGUI();
        GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 400f, 20f),
            $"주황: 잔존 몬스터 (최대 {maxAlive})   파랑: 누적 스폰 ({maxSpawned})", EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.x + 6f, rect.yMax - 20f, 100f, 18f), "0초", EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.xMax - 80f, rect.yMax - 20f, 75f, 18f),
            $"{survivalSeconds:0}초", EditorStyles.miniLabel);
    }

    private Vector3[] BuildGraphPoints(Rect rect, Func<PreviewPoint, float> selector)
    {
        var result = new Vector3[preview.Count];
        for (int i = 0; i < preview.Count; i++)
        {
            result[i] = new Vector3(
                rect.x + rect.width * preview[i].Time / survivalSeconds,
                rect.yMax - 20f - (rect.height - 28f) * Mathf.Clamp01(selector(preview[i])), 0f);
        }
        return result;
    }

    private void DrawRows()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("StageMonster.csv 적용 예정 행", EditorStyles.boldLabel);
        tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUILayout.Height(210f));
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        string[] headers = { "ID", "Monster", "Start", "Interval", "StartQty", "Growth", "MaxQty", "Budget", "AliveCap", "Waves", "HP", "ATK", "Shape" };
        float[] widths = { 34, 75, 50, 55, 58, 52, 52, 52, 58, 48, 55, 55, 70 };
        for (int i = 0; i < headers.Length; i++) DrawCell(headers[i], widths[i]);
        EditorGUILayout.EndHorizontal();
        foreach (BalanceRow row in rows)
        {
            EditorGUILayout.BeginHorizontal();
            string[] values = row.DisplayValues();
            for (int i = 0; i < values.Length; i++) DrawCell(values[i], widths[i]);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private static void DrawCell(string text, float width)
    {
        EditorGUILayout.SelectableLabel(text, GUILayout.Width(width), GUILayout.Height(18f));
    }

    private void ApplyPreview()
    {
        if (!hasPreview || rows.Count == 0) return;
        try
        {
            WriteStageMonsterCsv();
            WriteStageDuration();
            int sceneChanges = ApplyOpenSceneValues();
            int prefabChanges = ApplyPrefabValues();
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            previewMessage = $"적용 완료: CSV 2개, 열린 씬 컴포넌트 {sceneChanges}개, 몬스터 프리팹 {prefabChanges}개 갱신";
            ShowNotification(new GUIContent("밸런스 적용 완료"));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("밸런스 적용 실패", exception.Message, "확인");
        }
    }

    private void WriteStageMonsterCsv()
    {
        var preserved = new List<string>();
        if (File.Exists(StageMonsterPath))
        {
            string[] oldLines = File.ReadAllLines(StageMonsterPath);
            for (int i = 1; i < oldLines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(oldLines[i])) continue;
                string first = oldLines[i].Split(',')[0].Trim().TrimStart('\uFEFF');
                if (!int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int oldStage)
                    || oldStage != stageId) preserved.Add(oldLines[i]);
            }
        }
        var lines = new List<string>
        {
            "StageId,MonsterId,SpawnStartSec,WaveIntervalSec,WaveSizeStart,WaveSizeGrowth,WaveSizeMax,TotalBudget,MaxAliveCap,WaveCount,MaxHP,AttackDamage,SpawnShape"
        };
        lines.AddRange(preserved);
        lines.AddRange(rows.Select(row => row.ToCsv()));
        File.WriteAllText(StageMonsterPath, string.Join("\n", lines) + "\n");
    }

    private void WriteStageDuration()
    {
        if (!File.Exists(StagePath)) return;
        string[] lines = File.ReadAllLines(StagePath);
        if (lines.Length == 0) return;
        string[] headers = lines[0].Split(',');
        int stageColumn = Array.FindIndex(headers, value => value.Trim().TrimStart('\uFEFF').Equals("StageId", StringComparison.OrdinalIgnoreCase));
        int timeColumn = Array.FindIndex(headers, value => value.Trim().Equals("Time", StringComparison.OrdinalIgnoreCase));
        if (stageColumn < 0 || timeColumn < 0) return;
        bool found = false;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            if (values.Length <= Mathf.Max(stageColumn, timeColumn)) continue;
            if (int.TryParse(values[stageColumn], out int parsedStage) && parsedStage == stageId)
            {
                values[timeColumn] = survivalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
                lines[i] = string.Join(",", values);
                found = true;
            }
        }
        if (!found)
        {
            string[] values = Enumerable.Repeat(string.Empty, headers.Length).ToArray();
            values[stageColumn] = stageId.ToString(CultureInfo.InvariantCulture);
            values[timeColumn] = survivalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            lines = lines.Concat(new[] { string.Join(",", values) }).ToArray();
        }
        File.WriteAllText(StagePath, string.Join("\n", lines) + "\n");
    }

    private int ApplyOpenSceneValues()
    {
        return SetSceneFloat<PlayerHealth>("maxHP", playerHealth)
            + SetSceneFloat<PlayerAttackStats>("baseAttackPower", playerAttack)
            + SetSceneFloat<PlayerMovement>("moveSpeed", playerMoveSpeed)
            + SetSceneInt<StageMonsterSpawner>("currentStageId", stageId);
    }

    private static int SetSceneFloat<T>(string name, float value) where T : Component
    {
        int changed = 0;
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid()) continue;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) continue;
            Undo.RecordObject(component, "Apply Vampire Survivors Balance");
            property.floatValue = value;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            changed++;
        }
        return changed;
    }

    private static int SetSceneInt<T>(string name, int value) where T : Component
    {
        int changed = 0;
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid()) continue;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) continue;
            Undo.RecordObject(component, "Apply Vampire Survivors Balance");
            property.intValue = value;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            changed++;
        }
        return changed;
    }

    private int ApplyPrefabValues()
    {
        int changes = 0;
        foreach (MonsterPrefabInfo info in availableMonsters.Take(monsterTypeCount))
        {
            BalanceRow row = rows.FirstOrDefault(item => item.MonsterId == info.Id);
            if (row == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(info.Path);
            try
            {
                MonsterController controller = root.GetComponent<MonsterController>();
                MonsterMovement movement = root.GetComponent<MonsterMovement>();
                if (controller == null || movement == null) continue;
                var controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("maxHP").floatValue = row.MaxHP;
                controllerObject.FindProperty("attackDamge").floatValue = row.AttackDamage;
                controllerObject.ApplyModifiedPropertiesWithoutUndo();
                var movementObject = new SerializedObject(movement);
                movementObject.FindProperty("moveSpeed").floatValue = monsterMoveSpeed;
                movementObject.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, info.Path);
                changes++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return changes;
    }

    private void RefreshMonsterPrefabs()
    {
        availableMonsters.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<MonsterController>() != null
                && prefab.GetComponent<MonsterMovement>() != null)
                availableMonsters.Add(new MonsterPrefabInfo(Path.GetFileNameWithoutExtension(path), path));
        }
        availableMonsters.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
        monsterTypeCount = Mathf.Clamp(monsterTypeCount, 1, Mathf.Max(1, availableMonsters.Count));
    }

    private void DetectWeaponDps()
    {
        float interval = 1f;
        int projectileCount = 1;
        float projectilePercent = 100f;
        AutoShooter shooter = Resources.FindObjectsOfTypeAll<AutoShooter>()
            .FirstOrDefault(item => !EditorUtility.IsPersistent(item) && item.gameObject.scene.IsValid());
        if (shooter != null)
        {
            var serialized = new SerializedObject(shooter);
            interval = Mathf.Max(0.01f, serialized.FindProperty("fireInterval").floatValue);
            projectileCount = Mathf.Max(1, serialized.FindProperty("projectileCount").intValue);
            var projectile = serialized.FindProperty("projectilePrefab").objectReferenceValue as ProjectileController;
            if (projectile != null)
            {
                var projectileObject = new SerializedObject(projectile);
                projectilePercent = projectileObject.FindProperty("attackPowerPercent").floatValue;
            }
        }
        detectedBaseDps = Mathf.Max(0.01f,
            playerAttack * projectileCount * projectilePercent / 100f / interval);
    }

    private void ClampInputs()
    {
        playerHealth = Mathf.Max(1f, playerHealth);
        playerAttack = Mathf.Max(0.01f, playerAttack);
        survivalSeconds = Mathf.Max(5f, survivalSeconds);
        playerMoveSpeed = Mathf.Max(0f, playerMoveSpeed);
        monsterMoveSpeed = Mathf.Max(0f, monsterMoveSpeed);
        stageId = Mathf.Max(1, stageId);
        firstMonsterHealth = Mathf.Max(1f, firstMonsterHealth);
        phaseCount = Mathf.Clamp(phaseCount, 3, 12);
        spawnLoad = Mathf.Max(1.02f, spawnLoad);
        minimumWaveInterval = Mathf.Max(0.25f, minimumWaveInterval);
        maximumWaveInterval = Mathf.Max(minimumWaveInterval, maximumWaveInterval);
    }

    private float FindFinalSpawnGap()
    {
        float last = rows.Max(row => row.SpawnStartSec + (row.WaveCount - 1) * row.WaveIntervalSec);
        return Mathf.Max(0f, survivalSeconds - last);
    }

    private static int CalculateBudget(int start, int growth, int max, int waves)
    {
        int total = 0;
        for (int wave = 0; wave < waves; wave++) total += Mathf.Min(start + wave * growth, max);
        return total;
    }

    private sealed class MonsterPrefabInfo
    {
        public readonly string Id;
        public readonly string Path;
        public MonsterPrefabInfo(string id, string path) { Id = id; Path = path; }
    }

    private sealed class BalanceRow
    {
        public readonly int StageId, WaveSizeStart, WaveSizeGrowth, WaveSizeMax, TotalBudget, MaxAliveCap, WaveCount;
        public readonly string MonsterId;
        public readonly float SpawnStartSec, WaveIntervalSec, MaxHP, AttackDamage;
        public readonly SpawnShape SpawnShape;
        public BalanceRow(int stageId, string monsterId, float start, float interval,
            int waveStart, int growth, int waveMax, int budget, int aliveCap, int waves,
            float hp, float attack, SpawnShape spawnShape)
        {
            StageId = stageId; MonsterId = monsterId; SpawnStartSec = start;
            WaveIntervalSec = interval; WaveSizeStart = waveStart; WaveSizeGrowth = growth;
            WaveSizeMax = waveMax; TotalBudget = budget; MaxAliveCap = aliveCap;
            WaveCount = waves; MaxHP = hp; AttackDamage = attack;
            SpawnShape = spawnShape;
        }
        public string[] DisplayValues() => new[]
        {
            StageId.ToString(), MonsterId, SpawnStartSec.ToString("0.##"),
            WaveIntervalSec.ToString("0.##"), WaveSizeStart.ToString(), WaveSizeGrowth.ToString(),
            WaveSizeMax.ToString(), TotalBudget.ToString(), MaxAliveCap.ToString(),
            WaveCount.ToString(), MaxHP.ToString("0.##"), AttackDamage.ToString("0.##"),
            SpawnShape.ToString().ToUpperInvariant()
        };
        public string ToCsv() => string.Join(",", new[]
        {
            StageId.ToString(CultureInfo.InvariantCulture), MonsterId,
            SpawnStartSec.ToString("0.###", CultureInfo.InvariantCulture),
            WaveIntervalSec.ToString("0.###", CultureInfo.InvariantCulture),
            WaveSizeStart.ToString(CultureInfo.InvariantCulture),
            WaveSizeGrowth.ToString(CultureInfo.InvariantCulture),
            WaveSizeMax.ToString(CultureInfo.InvariantCulture),
            TotalBudget.ToString(CultureInfo.InvariantCulture),
            MaxAliveCap.ToString(CultureInfo.InvariantCulture),
            WaveCount.ToString(CultureInfo.InvariantCulture),
            MaxHP.ToString("0.###", CultureInfo.InvariantCulture),
            AttackDamage.ToString("0.###", CultureInfo.InvariantCulture),
            SpawnShape.ToString().ToUpperInvariant()
        });
    }

    private sealed class SimMonster
    {
        public readonly int RuleIndex;
        public float HP;
        public SimMonster(int ruleIndex, float hp) { RuleIndex = ruleIndex; HP = hp; }
    }

    private readonly struct PreviewPoint
    {
        public readonly float Time;
        public readonly int TotalSpawned;
        public readonly int Alive;
        public PreviewPoint(float time, int totalSpawned, int alive)
        { Time = time; TotalSpawned = totalSpawned; Alive = alive; }
    }
}
