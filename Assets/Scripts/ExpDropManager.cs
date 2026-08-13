using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ExpDropManager : MonoBehaviour
{
    public static ExpDropManager Instance { get; private set; }

    [Header("Experience Magnet")]
    [SerializeField, Min(0f)]
    private float magnetRange = 3f;

    [Header("Experience")]
    [Tooltip("Total experience earned across every level.")]
    [SerializeField, Min(0)]
    private int currentExperience;

    [SerializeField, Min(1)]
    private int currentLevel = 1;

    [Tooltip("If omitted, Resources/LevelXP.csv is loaded automatically.")]
    [SerializeField]
    private TextAsset levelXpCsv;

    [Header("Experience UI")]
    [SerializeField]
    private Image expBarFill;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField, Min(0f)]
    private float barAnimationDuration = 0.25f;

    private readonly Dictionary<int, int> needXpByLevel = new Dictionary<int, int>();
    private int experienceRequiredForCurrentLevel;
    private Coroutine barAnimationRoutine;

    public float MagnetRange => magnetRange;
    public int CurrentExperience => currentExperience;
    public int CurrentLevel => currentLevel;
    public int NeedExperience => TryGetNeedExperience(currentLevel, out int needXp) ? needXp : 0;
    public int ExperienceInCurrentLevel =>
        Mathf.Max(0, currentExperience - experienceRequiredForCurrentLevel);
    public bool IsMaxLevel => !TryGetNeedExperience(currentLevel, out _);

    public event Action<int> ExperienceChanged;
    public event Action<int> LevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one ExpDropManager can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
        ResolveUiReferences();

        TextAsset csv = levelXpCsv != null
            ? levelXpCsv
            : Resources.Load<TextAsset>("LevelXP");

        if (csv == null)
        {
            Debug.LogError("LevelXP.csv could not be found in Resources.", this);
            enabled = false;
            return;
        }

        if (!LoadLevelXpTable(csv.text))
        {
            enabled = false;
            return;
        }

        RecalculateLevel();
        UpdateLevelText();
        SetExperienceBarImmediate(CalculateBarRatio());
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 || !enabled)
        {
            return;
        }

        if (IsMaxLevel)
        {
            SetExperienceBarImmediate(1f);
            return;
        }

        currentExperience = currentExperience > int.MaxValue - amount
            ? int.MaxValue
            : currentExperience + amount;

        int previousLevel = currentLevel;
        ProcessLevelUps();

        if (currentLevel != previousLevel)
        {
            // A level-up resets the visible bar before displaying carried experience.
            SetExperienceBarImmediate(0f);
        }

        AnimateExperienceBar(CalculateBarRatio());
        ExperienceChanged?.Invoke(currentExperience);
    }

    private void ProcessLevelUps()
    {
        while (TryGetNeedExperience(currentLevel, out int needXp)
            && currentExperience >= experienceRequiredForCurrentLevel + needXp)
        {
            experienceRequiredForCurrentLevel += needXp;
            currentLevel++;

            if (IsMaxLevel)
            {
                // Discard overflow at the level cap and keep the completed bar full.
                currentExperience = experienceRequiredForCurrentLevel;
            }

            UpdateLevelText();
            LevelChanged?.Invoke(currentLevel);
        }
    }

    private void RecalculateLevel()
    {
        currentLevel = 1;
        experienceRequiredForCurrentLevel = 0;
        ProcessLevelUps();
    }

    private float CalculateBarRatio()
    {
        if (!TryGetNeedExperience(currentLevel, out int needXp))
        {
            return 1f;
        }

        int experienceIntoLevel = currentExperience - experienceRequiredForCurrentLevel;
        return Mathf.Clamp01((float)experienceIntoLevel / needXp);
    }

    private bool TryGetNeedExperience(int level, out int needXp)
    {
        return needXpByLevel.TryGetValue(level, out needXp);
    }

    private bool LoadLevelXpTable(string csvText)
    {
        needXpByLevel.Clear();
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            Debug.LogError("LevelXP.csv does not contain any level data.", this);
            return false;
        }

        string[] headers = lines[0].TrimStart('\uFEFF').Split(',');
        int levelColumn = FindColumn(headers, "Level");
        int needXpColumn = FindColumn(headers, "NeedXP");
        if (levelColumn < 0 || needXpColumn < 0)
        {
            Debug.LogError("LevelXP.csv must contain Level and NeedXP columns.", this);
            return false;
        }

        int requiredValueCount = Mathf.Max(levelColumn, needXpColumn) + 1;
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string[] values = line.Split(',');
            if (values.Length < requiredValueCount
                || !int.TryParse(values[levelColumn].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int level)
                || !int.TryParse(values[needXpColumn].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int needXp)
                || level < 1
                || needXp < 1)
            {
                Debug.LogWarning($"LevelXP.csv line {lineIndex + 1} was skipped: values must be positive integers.", this);
                continue;
            }

            if (!needXpByLevel.TryAdd(level, needXp))
            {
                Debug.LogWarning($"LevelXP.csv line {lineIndex + 1} was skipped: Level {level} is duplicated.", this);
            }
        }

        if (!needXpByLevel.ContainsKey(1))
        {
            Debug.LogError("LevelXP.csv must contain data for Level 1.", this);
            return false;
        }

        return true;
    }

    private static int FindColumn(string[] headers, string columnName)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i].Trim(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void ResolveUiReferences()
    {
        if (expBarFill == null)
        {
            Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Image image in images)
            {
                if (image.name == "ExpBar_Fill")
                {
                    expBarFill = image;
                    break;
                }
            }
        }

        if (levelText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                if (text.name == "LevelText")
                {
                    levelText = text;
                    break;
                }
            }
        }
    }

    private void UpdateLevelText()
    {
        if (levelText != null)
        {
            levelText.text = $"Level : {currentLevel}";
        }
    }

    private void AnimateExperienceBar(float targetRatio)
    {
        if (expBarFill == null)
        {
            return;
        }

        if (barAnimationRoutine != null)
        {
            StopCoroutine(barAnimationRoutine);
        }

        barAnimationRoutine = StartCoroutine(AnimateBarRoutine(targetRatio));
    }

    private IEnumerator AnimateBarRoutine(float targetRatio)
    {
        float startRatio = expBarFill.fillAmount;
        float elapsed = 0f;

        while (elapsed < barAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = barAnimationDuration > 0f
                ? Mathf.Clamp01(elapsed / barAnimationDuration)
                : 1f;
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            expBarFill.fillAmount = Mathf.LerpUnclamped(startRatio, targetRatio, easedProgress);
            yield return null;
        }

        expBarFill.fillAmount = targetRatio;
        barAnimationRoutine = null;
    }

    private void SetExperienceBarImmediate(float ratio)
    {
        if (barAnimationRoutine != null)
        {
            StopCoroutine(barAnimationRoutine);
            barAnimationRoutine = null;
        }

        if (expBarFill != null)
        {
            expBarFill.fillAmount = Mathf.Clamp01(ratio);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        magnetRange = Mathf.Max(0f, magnetRange);
        currentExperience = Mathf.Max(0, currentExperience);
        currentLevel = Mathf.Max(1, currentLevel);
        barAnimationDuration = Mathf.Max(0f, barAnimationDuration);
    }
}
