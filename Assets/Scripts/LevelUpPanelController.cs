using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelUpPanelController : MonoBehaviour
{
    private const int CardChoiceCount = 3;

    [Header("References")]
    [SerializeField]
    private ExpDropManager experienceManager;

    [SerializeField]
    private PlayerAttackStats playerAttackStats;

    [SerializeField]
    private GameObject levelUpPanel;

    [SerializeField]
    private Button card1;

    [SerializeField]
    private Button card2;

    [SerializeField]
    private Button card3;

    [Header("Card Data")]
    [Tooltip("If omitted, Resources/LevelUpCard.csv is loaded automatically.")]
    [SerializeField]
    private TextAsset levelUpCardCsv;

    private readonly Queue<int> pendingLevels = new Queue<int>();
    private readonly HashSet<int> selectedCardIds = new HashSet<int>();
    private readonly List<LevelUpCardData> allCards = new List<LevelUpCardData>();
    private readonly CardView[] cardViews = new CardView[CardChoiceCount];

    private Coroutine showNextRoutine;
    private float timeScaleBeforePause = 1f;
    private int presentedLevel;

    public static bool IsPaused { get; private set; }
    public bool IsPanelOpen => levelUpPanel != null && levelUpPanel.activeSelf;
    public int PendingLevelUpCount => pendingLevels.Count + (IsPanelOpen ? 1 : 0);
    public IReadOnlyCollection<int> SelectedCardIds => selectedCardIds;

    public event Action<int, int> CardSelected;
    public event Action<LevelUpCardData, int> CardEffectApplied;

    private void Awake()
    {
        ResolveReferences();

        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }

        TextAsset csv = levelUpCardCsv != null
            ? levelUpCardCsv
            : Resources.Load<TextAsset>("LevelUpCard");

        if (csv == null)
        {
            Debug.LogError("LevelUpCard.csv could not be found in Resources.", this);
            enabled = false;
            return;
        }

        string csvText = LevelUpCardTable.Decode(csv);
        if (!LevelUpCardTable.TryLoad(csvText, this, out List<LevelUpCardData> loadedCards))
        {
            enabled = false;
            return;
        }

        allCards.AddRange(loadedCards);
    }

    private void Start()
    {
        if (experienceManager == null || playerAttackStats == null
            || levelUpPanel == null || cardViews[0] == null
            || cardViews[1] == null || cardViews[2] == null)
        {
            Debug.LogError(
                "LevelUpPanelController requires ExpDropManager, PlayerAttackStats, Panel_Levelup, and valid Card1~3 UI.",
                this);
            enabled = false;
            return;
        }

        experienceManager.LevelChanged += OnLevelChanged;
        card1.onClick.AddListener(OnCard1Selected);
        card2.onClick.AddListener(OnCard2Selected);
        card3.onClick.AddListener(OnCard3Selected);
    }

    private void OnLevelChanged(int newLevel)
    {
        pendingLevels.Enqueue(newLevel);
        ShowNextLevelUp();
    }

    private void ShowNextLevelUp()
    {
        if (IsPanelOpen || pendingLevels.Count == 0)
        {
            return;
        }

        List<LevelUpCardData> choices = DrawCards();
        if (choices.Count != CardChoiceCount)
        {
            Debug.LogError(
                "There are not enough eligible LevelUpCard rows to display three cards.",
                this);
            return;
        }

        presentedLevel = pendingLevels.Dequeue();
        for (int index = 0; index < CardChoiceCount; index++)
        {
            cardViews[index].Display(choices[index], this);
        }

        PauseGame();
        levelUpPanel.SetActive(true);
    }

    private List<LevelUpCardData> DrawCards()
    {
        List<LevelUpCardData> pool = new List<LevelUpCardData>();
        foreach (LevelUpCardData card in allCards)
        {
            if (selectedCardIds.Contains(card.Id))
            {
                continue;
            }

            if (card.Required == 0 || selectedCardIds.Contains(card.Required))
            {
                pool.Add(card);
            }
        }

        List<LevelUpCardData> choices = new List<LevelUpCardData>(CardChoiceCount);
        while (choices.Count < CardChoiceCount && pool.Count > 0)
        {
            int selectedIndex = DrawWeightedIndex(pool);
            choices.Add(pool[selectedIndex]);
            pool.RemoveAt(selectedIndex);
        }

        return choices;
    }

    private static int DrawWeightedIndex(List<LevelUpCardData> pool)
    {
        long totalRate = 0;
        foreach (LevelUpCardData card in pool)
        {
            totalRate += card.Rate;
        }

        double roll = UnityEngine.Random.value * totalRate;
        long cumulativeRate = 0;
        for (int index = 0; index < pool.Count; index++)
        {
            cumulativeRate += pool[index].Rate;
            if (roll < cumulativeRate)
            {
                return index;
            }
        }

        return pool.Count - 1;
    }

    private void OnCard1Selected() => SelectCard(0);
    private void OnCard2Selected() => SelectCard(1);
    private void OnCard3Selected() => SelectCard(2);

    private void SelectCard(int cardIndex)
    {
        if (!IsPanelOpen || cardIndex < 0 || cardIndex >= cardViews.Length)
        {
            return;
        }

        LevelUpCardData selectedCard = cardViews[cardIndex].Data;
        if (selectedCard == null)
        {
            return;
        }

        ApplyEffect(selectedCard);
        selectedCardIds.Add(selectedCard.Id);
        CardSelected?.Invoke(cardIndex + 1, presentedLevel);
        CardEffectApplied?.Invoke(selectedCard, presentedLevel);
        levelUpPanel.SetActive(false);

        if (pendingLevels.Count > 0)
        {
            showNextRoutine = StartCoroutine(ShowNextQueuedLevelUp());
            return;
        }

        ResumeGame();
    }

    private void ApplyEffect(LevelUpCardData card)
    {
        switch (card.Effect)
        {
            case LevelUpCardEffect.ATKUp:
                // Replacement semantics: the newest Value becomes the active
                // attack percentage instead of being added to the old value.
                playerAttackStats.SetAttackPercent(card.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(card.Effect), card.Effect, "Unsupported card effect.");
        }
    }

    private IEnumerator ShowNextQueuedLevelUp()
    {
        yield return null;
        showNextRoutine = null;
        ShowNextLevelUp();
    }

    private void PauseGame()
    {
        if (IsPaused)
        {
            return;
        }

        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
    }

    private void ResumeGame()
    {
        if (!IsPaused)
        {
            return;
        }

        Time.timeScale = timeScaleBeforePause;
        IsPaused = false;
    }

    private void ResolveReferences()
    {
        experienceManager ??= GetComponent<ExpDropManager>();
        playerAttackStats ??= GetComponent<PlayerAttackStats>();

        if (levelUpPanel == null)
        {
            GameObject[] sceneObjects = FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (GameObject sceneObject in sceneObjects)
            {
                if (sceneObject.name == "Panel_Levelup")
                {
                    levelUpPanel = sceneObject;
                    break;
                }
            }
        }

        if (levelUpPanel == null)
        {
            return;
        }

        Button[] buttons = levelUpPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Card1":
                    card1 ??= button;
                    break;
                case "Card2":
                    card2 ??= button;
                    break;
                case "Card3":
                    card3 ??= button;
                    break;
            }
        }

        cardViews[0] = CardView.TryCreate(card1);
        cardViews[1] = CardView.TryCreate(card2);
        cardViews[2] = CardView.TryCreate(card3);
    }

    private void OnDisable()
    {
        if (experienceManager != null)
        {
            experienceManager.LevelChanged -= OnLevelChanged;
        }

        card1?.onClick.RemoveListener(OnCard1Selected);
        card2?.onClick.RemoveListener(OnCard2Selected);
        card3?.onClick.RemoveListener(OnCard3Selected);

        if (showNextRoutine != null)
        {
            StopCoroutine(showNextRoutine);
            showNextRoutine = null;
        }

        pendingLevels.Clear();
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }

        ResumeGame();
    }

    private sealed class CardView
    {
        private readonly Image iconImage;
        private readonly TMP_Text descriptionText;

        public LevelUpCardData Data { get; private set; }

        private CardView(Image iconImage, TMP_Text descriptionText)
        {
            this.iconImage = iconImage;
            this.descriptionText = descriptionText;
        }

        public static CardView TryCreate(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Image icon = null;
            TMP_Text description = null;

            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "CardImage")
                {
                    icon = image;
                    break;
                }
            }

            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "CardDesc")
                {
                    description = text;
                    break;
                }
            }

            return icon != null && description != null
                ? new CardView(icon, description)
                : null;
        }

        public void Display(LevelUpCardData card, UnityEngine.Object logContext)
        {
            Data = card;
            descriptionText.text = card.Description;
            iconImage.sprite = Resources.Load<Sprite>($"Sprites/{card.Icon}");
            iconImage.enabled = iconImage.sprite != null;

            if (iconImage.sprite == null)
            {
                Debug.LogError(
                    $"Card ID {card.Id} icon 'Resources/Sprites/{card.Icon}' was not found.",
                    logContext);
            }
        }
    }
}
