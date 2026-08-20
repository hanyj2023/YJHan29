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
        currentCoins = 0;
        UpdateCoinUI();
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentCoins += amount;
        UpdateCoinUI();
        CoinsChanged?.Invoke(currentCoins);
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
            coinText.text = "Coin : " + currentCoins.ToString(
                "N0",
                CultureInfo.InvariantCulture);
        }
    }
}
