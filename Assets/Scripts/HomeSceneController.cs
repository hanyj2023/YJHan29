using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HomeSceneController : MonoBehaviour
{
    [Header("Stage Selection")]
    [SerializeField] private Button exitButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject stagePanel;
    [SerializeField] private Transform content;
    [SerializeField] private StagePreview[] previews;

    [Header("Language")]
    [SerializeField] private Button koreanButton;
    [SerializeField] private Button englishButton;

    [Header("Persistent Upgrade")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button upgradeCloseButton;
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private Transform upgradeContent;
    [SerializeField] private GameObject upgradeTemplate;
    [SerializeField] private TMP_Text upgradeCoinText;
    [SerializeField] private Sprite inactiveDotSprite;
    [SerializeField] private Sprite activeDotSprite;

    private readonly List<UpgradeRow> upgradeRows = new List<UpgradeRow>();
    private bool loading;

    [Serializable]
    private struct StagePreview
    {
        public string tilemapName;
        public Sprite sprite;
    }

    private sealed class UpgradeRow
    {
        public StatusUpgradeData Data;
        public Button BuyButton;
        public LocalizedText StatusText;
        public TMP_Text CostText;
        public LocalizedText LocalizedCostText;
        public Image[] Dots;
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        InitializeLanguageSelection();
        InitializeStageSelection();
        InitializePersistentUpgrades();
    }

    private void InitializeLanguageSelection()
    {
        koreanButton ??= FindSceneTransform("BTN_Korean")?.GetComponent<Button>();
        englishButton ??= FindSceneTransform("BTN_English")?.GetComponent<Button>();
        if (koreanButton == null || englishButton == null)
        {
            CreateLanguageButtons();
        }

        if (koreanButton == null || englishButton == null)
        {
            Debug.LogError("HomeScene requires BTN_Korean and BTN_English.", this);
            return;
        }

        koreanButton.onClick.AddListener(SelectKorean);
        englishButton.onClick.AddListener(SelectEnglish);
        LocalizationManager.LanguageChanged += OnLanguageChanged;
    }

    private void InitializeStageSelection()
    {
        stagePanel.SetActive(false);
        exitButton.onClick.AddListener(Exit);
        startButton.onClick.AddListener(OpenStages);
        closeButton.onClick.AddListener(CloseStages);

        Transform template = content.Find("BTN_StagePrefab");
        if (template == null)
        {
            Debug.LogError("Stage Content requires BTN_StagePrefab.", this);
            return;
        }

        template.gameObject.SetActive(false);
        foreach (StageData stage in StageTable.Load())
        {
            GameObject item = Instantiate(template.gameObject, content, false);
            item.name = $"BTN_Stage_{stage.Id}";
            TMP_Text label = item.transform.Find("TXT_Stage").GetComponent<TMP_Text>();
            label.richText = false;
            label.GetComponent<LocalizedText>()?.SetKey(stage.NameKey);
            if (label.GetComponent<LocalizedText>() == null)
            {
                label.gameObject.AddComponent<LocalizedText>().SetKey(stage.NameKey);
            }
            Image preview = item.transform.Find("Image_Stage").GetComponent<Image>();
            preview.sprite = FindPreview(stage.TilemapName);
            preview.preserveAspect = true;
            preview.enabled = preview.sprite != null;
            if (preview.sprite == null)
            {
                Debug.LogWarning($"No stage preview is assigned for '{stage.TilemapName}'.", this);
            }

            item.GetComponent<Button>().onClick.AddListener(() => OpenStage(stage.Id));
            item.SetActive(true);
        }

        Destroy(template.gameObject);
    }

    private void InitializePersistentUpgrades()
    {
        ResolveUpgradeReferences();
        if (upgradeButton == null || upgradeCloseButton == null
            || upgradePanel == null || upgradeContent == null
            || upgradeTemplate == null || upgradeCoinText == null)
        {
            Debug.LogError("HomeScene upgrade UI references could not be resolved.", this);
            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
            }
            return;
        }

        LoadDotSprites();
        upgradeButton.onClick.AddListener(OpenUpgrades);
        upgradeCloseButton.onClick.AddListener(CloseUpgrades);
        PlayerPreference.CoinsChanged += OnCoinsChanged;

        upgradeTemplate.SetActive(false);
        foreach (StatusUpgradeData data in StatusUpgradeTable.Load())
        {
            CreateUpgradeRow(data);
        }

        Destroy(upgradeTemplate);
        RefreshUpgradeUI();
        upgradePanel.SetActive(false);
    }

    private void ResolveUpgradeReferences()
    {
        upgradePanel = upgradePanel != null
            ? upgradePanel
            : FindSceneTransform("Panel_Upgrade")?.gameObject;
        upgradeButton = upgradeButton != null
            ? upgradeButton
            : FindSceneTransform("BTN_Upgrade")?.GetComponent<Button>();

        if (upgradePanel == null)
        {
            return;
        }

        Transform panelTransform = upgradePanel.transform;
        upgradeCloseButton = upgradeCloseButton != null
            ? upgradeCloseButton
            : FindDescendant(panelTransform, "BTN_X")?.GetComponent<Button>();
        upgradeCoinText = upgradeCoinText != null
            ? upgradeCoinText
            : FindDescendant(panelTransform, "TXT_Coin")?.GetComponent<TMP_Text>();

        Transform scrollView = FindDescendant(panelTransform, "Scroll View_Upgrade");
        upgradeContent = upgradeContent != null
            ? upgradeContent
            : FindDescendant(scrollView, "Content");
        Transform template = upgradeContent == null
            ? null
            : FindDescendant(upgradeContent, "UpgradePrefab");
        upgradeTemplate = upgradeTemplate != null
            ? upgradeTemplate
            : template?.gameObject;
    }

    private void LoadDotSprites()
    {
        foreach (Sprite sprite in Resources.LoadAll<Sprite>("Sprites/Dot"))
        {
            if (sprite.name == "Dot_0" && inactiveDotSprite == null)
            {
                inactiveDotSprite = sprite;
            }
            else if (sprite.name == "Dot_1" && activeDotSprite == null)
            {
                activeDotSprite = sprite;
            }
        }

        if (inactiveDotSprite == null || activeDotSprite == null)
        {
            Debug.LogError("Sprites/Dot must contain Dot_0 and Dot_1 sprites.", this);
        }
    }

    private void CreateUpgradeRow(StatusUpgradeData data)
    {
        GameObject item = Instantiate(upgradeTemplate, upgradeContent, false);
        item.name = $"Upgrade_{data.Type}";
        LayoutElement layout = item.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = item.AddComponent<LayoutElement>();
        }
        layout.minHeight = 100f;
        layout.preferredHeight = 100f;

        TMP_Text statusText = FindDescendant(item.transform, "TXT_Status")
            ?.GetComponent<TMP_Text>();
        Transform buyTransform = FindDescendant(item.transform, "BTN_Buy")
            ?? FindDescendant(item.transform, "Button_Buy");
        Button buyButton = buyTransform?.GetComponent<Button>();
        TMP_Text costText = buyTransform?.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        var dots = new Image[5];
        for (int index = 0; index < dots.Length; index++)
        {
            dots[index] = FindDescendant(item.transform, $"Dot_{index + 1}")
                ?.GetComponent<Image>();
        }

        if (statusText == null || buyButton == null || costText == null
            || Array.Exists(dots, dot => dot == null))
        {
            Debug.LogError($"UpgradePrefab is missing UI children for '{data.StatusName}'.", item);
            Destroy(item);
            return;
        }

        statusText.richText = false;
        LocalizedText localizedStatus = statusText.GetComponent<LocalizedText>()
            ?? statusText.gameObject.AddComponent<LocalizedText>();
        localizedStatus.SetKey(data.StatusNameKey);
        var row = new UpgradeRow
        {
            Data = data,
            BuyButton = buyButton,
            StatusText = localizedStatus,
            CostText = costText,
            LocalizedCostText = costText.GetComponent<LocalizedText>()
                ?? costText.gameObject.AddComponent<LocalizedText>(),
            Dots = dots
        };
        buyButton.onClick.AddListener(() => BuyUpgrade(row));
        upgradeRows.Add(row);
        item.SetActive(true);
    }

    private void BuyUpgrade(UpgradeRow row)
    {
        PlayerPreference.TryPurchaseUpgrade(
            row.Data.StatusNameKey,
            row.Data.UpgradeCost,
            row.Data.MaxLevel,
            out _);
        RefreshUpgradeUI();
    }

    private void RefreshUpgradeUI()
    {
        long coins = PlayerPreference.Coins;
        LocalizedText localizedCoins = upgradeCoinText.GetComponent<LocalizedText>()
            ?? upgradeCoinText.gameObject.AddComponent<LocalizedText>();
        localizedCoins.SetKey("ui.common.number_format", coins);
        foreach (UpgradeRow row in upgradeRows)
        {
            row.StatusText.SetKey(row.Data.StatusNameKey);
            int level = PlayerPreference.GetStatusLevel(row.Data.StatusNameKey);
            bool isMaxLevel = level >= row.Data.MaxLevel;
            if (isMaxLevel)
            {
                row.LocalizedCostText.SetKey("ui.common.max");
            }
            else
            {
                row.LocalizedCostText.SetKey("ui.common.number_format", row.Data.UpgradeCost);
            }
            row.BuyButton.interactable = !isMaxLevel && coins >= row.Data.UpgradeCost;

            for (int index = 0; index < row.Dots.Length; index++)
            {
                row.Dots[index].sprite = index < level
                    ? activeDotSprite
                    : inactiveDotSprite;
            }
        }
    }

    private void OnCoinsChanged(long _) => RefreshUpgradeUI();
    private void OnLanguageChanged() => RefreshUpgradeUI();
    private void SelectKorean() => LocalizationManager.SetLanguage(GameLanguage.KOR);
    private void SelectEnglish() => LocalizationManager.SetLanguage(GameLanguage.ENG);

    private void CreateLanguageButtons()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        TMP_Text sample = startButton != null
            ? startButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        koreanButton ??= CreateLanguageButton(canvas.transform, "BTN_Korean",
            new Vector2(-220f, -45f), "ui.language.korean", sample);
        englishButton ??= CreateLanguageButton(canvas.transform, "BTN_English",
            new Vector2(-70f, -45f), "ui.language.english", sample);
    }

    private static Button CreateLanguageButton(Transform parent, string name,
        Vector2 anchoredPosition, string key, TMP_Text sample)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = LayerMask.NameToLayer("UI");
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(130f, 52f);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

        GameObject textObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = buttonObject.layer;
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        if (sample != null)
        {
            label.font = sample.font;
        }
        textObject.AddComponent<LocalizedText>().SetKey(key);
        return buttonObject.GetComponent<Button>();
    }

    private Sprite FindPreview(string tilemapName)
    {
        foreach (StagePreview preview in previews)
        {
            if (preview.tilemapName == tilemapName)
            {
                return preview.sprite;
            }
        }

        return null;
    }

    private void OpenStages() => stagePanel.SetActive(true);
    private void CloseStages() => stagePanel.SetActive(false);

    private void OpenUpgrades()
    {
        RefreshUpgradeUI();
        upgradePanel.SetActive(true);
    }

    private void CloseUpgrades() => upgradePanel.SetActive(false);

    private void OpenStage(int stageId)
    {
        if (loading)
        {
            return;
        }

        loading = true;
        StageTable.SelectedStageId = stageId;
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
    }

    private void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == objectName)
            {
                return child;
            }

            Transform nested = FindDescendant(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        PlayerPreference.CoinsChanged -= OnCoinsChanged;
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
        if (koreanButton != null)
        {
            koreanButton.onClick.RemoveListener(SelectKorean);
        }
        if (englishButton != null)
        {
            englishButton.onClick.RemoveListener(SelectEnglish);
        }
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Exit);
        }
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OpenStages);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseStages);
        }
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OpenUpgrades);
        }
        if (upgradeCloseButton != null)
        {
            upgradeCloseButton.onClick.RemoveListener(CloseUpgrades);
        }
    }
}
