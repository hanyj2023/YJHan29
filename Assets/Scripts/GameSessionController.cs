using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class GameSessionController : MonoBehaviour
{
    [SerializeField] private StageMonsterSpawner spawner;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private GameObject resultCanvas;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject success;
    [SerializeField] private GameObject fail;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button homeButton;

    private static GameSessionController instance;
    private double startTime;
    private int duration;
    private int displayedSeconds = -1;
    private bool started;
    private bool finished;
    private bool loading;

    public static bool HasEnded => instance != null && instance.finished;

    private void Awake()
    {
        instance = this;
        Time.timeScale = 1f;
        resultPanel.SetActive(false);
        resultCanvas.SetActive(true);
        replayButton.onClick.AddListener(Replay);
        homeButton.onClick.AddListener(GoHome);
        playerHealth.Died += OnPlayerDied;
    }

    private void Start()
    {
        if (!TryReadDuration(spawner.CurrentStageId, out duration))
        {
            Debug.LogError($"Stage.csv has no valid Time for StageId {spawner.CurrentStageId}.", this);
            spawner.enabled = false;
            Time.timeScale = 0f;
            enabled = false;
            return;
        }

        startTime = Time.timeAsDouble;
        started = true;
        DisplayTime(duration);
    }

    private void Update()
    {
        if (!started || finished || LevelUpPanelController.IsPaused)
            return;

        int remaining = Math.Max(0, (int)Math.Ceiling(duration - (Time.timeAsDouble - startTime)));
        DisplayTime(remaining);
        if (remaining == 0 && !playerHealth.IsDead && playerHealth.CurrentHP > 0f)
            Finish(true);
    }

    private void DisplayTime(int seconds)
    {
        if (displayedSeconds == seconds)
            return;
        displayedSeconds = seconds;
        LocalizedText localized = timeText.GetComponent<LocalizedText>()
            ?? timeText.gameObject.AddComponent<LocalizedText>();
        localized.SetKey("ui.game.time_format", seconds / 60, seconds % 60);
    }

    private void OnPlayerDied() => Finish(false);

    private void Finish(bool won)
    {
        if (finished)
            return;
        finished = true;

        // Close queued card selections before taking ownership of the pause.
        LevelUpPanelController cards = playerHealth.GetComponent<LevelUpPanelController>();
        if (cards != null)
            cards.enabled = false;
        PlayerMovement movement = playerHealth.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;
        spawner.enabled = false;
        Time.timeScale = 0f;

        success.SetActive(won);
        fail.SetActive(!won);
        replayButton.gameObject.SetActive(!won);
        homeButton.gameObject.SetActive(true);
        resultPanel.SetActive(true);
    }

    public void Replay()
    {
        if (!finished || loading)
            return;
        StageTable.SelectedStageId = spawner.CurrentStageId;
        LoadScene("GameScene");
    }

    public void GoHome()
    {
        if (!finished || loading)
            return;
        LoadScene("HomeScene");
    }

    private void LoadScene(string scene)
    {
        loading = true;
        Time.timeScale = 1f;
        // A fresh scene also resets health, selected cards, drops and spawn schedules.
        SceneManager.LoadScene(scene);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.Died -= OnPlayerDied;
        if (replayButton != null)
            replayButton.onClick.RemoveListener(Replay);
        if (homeButton != null)
            homeButton.onClick.RemoveListener(GoHome);
        if (instance == this)
        {
            instance = null;
            Time.timeScale = 1f;
        }
    }

    private static bool TryReadDuration(int stageId, out int seconds)
    {
        StageData stage = StageTable.Load().Find(entry => entry.Id == stageId);
        seconds = stage != null ? stage.Duration : 0;
        return stage != null;
    }
}
