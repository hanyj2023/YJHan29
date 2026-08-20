using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinPocketController : MagnetCollectible
{
    [Header("Coin Pocket")]
    [SerializeField, Min(0)]
    private int minimumCoins = 100;

    [SerializeField, Min(0)]
    private int maximumCoins = 500;

    public int MinimumCoins => minimumCoins;
    public int MaximumCoins => maximumCoins;

    protected override bool TryCollect(PlayerItemMagnet magnet)
    {
        PlayerCoinWallet wallet = magnet.GetComponent<PlayerCoinWallet>();
        if (wallet == null)
        {
            wallet = magnet.GetComponentInParent<PlayerCoinWallet>();
        }

        if (wallet == null)
        {
            return false;
        }

        int amount = RollInclusive(minimumCoins, maximumCoins);
        wallet.AddCoins(amount);
        Destroy(gameObject);
        return true;
    }

    private static int RollInclusive(int minimum, int maximum)
    {
        long range = (long)maximum - minimum + 1L;
        long offset = (long)(Random.value * range);
        long rolled = minimum + offset;
        return rolled > maximum ? maximum : (int)rolled;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        minimumCoins = Mathf.Max(0, minimumCoins);
        maximumCoins = Mathf.Max(minimumCoins, maximumCoins);
    }
}
