using System;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCoinWallet : MonoBehaviour
{
    [Header("Coin UI")]
    [SerializeField]
    private TMP_Text coinText;

    private long currentCoins;

    public long CurrentCoins => currentCoins;

    public event Action<long> CoinsChanged;

    private void Awake()
    {
        ResolveCoinText();
        currentCoins = PlayerPreference.Coins;
        PlayerPreference.CoinsChanged += OnPersistentCoinsChanged;
        LocalizationManager.LanguageChanged += UpdateCoinUI;
        UpdateCoinUI();
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        PlayerPreference.AddCoins(amount);
    }

    private void ResolveCoinText()
    {
        if (coinText != null)
        {
            return;
        }

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (TMP_Text text in texts)
        {
            if (text.name == "CoinText")
            {
                coinText = text;
                return;
            }
        }

        Debug.LogWarning("PlayerCoinWallet could not find CoinText.", this);
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
        {
            LocalizedText localized = coinText.GetComponent<LocalizedText>()
                ?? coinText.gameObject.AddComponent<LocalizedText>();
            localized.SetKey("ui.game.coin_format", currentCoins);
        }
    }

    private void OnPersistentCoinsChanged(long coins)
    {
        currentCoins = coins;
        UpdateCoinUI();
        CoinsChanged?.Invoke(currentCoins);
    }

    private void OnDestroy()
    {
        PlayerPreference.CoinsChanged -= OnPersistentCoinsChanged;
        LocalizationManager.LanguageChanged -= UpdateCoinUI;
    }
}
