using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExpOrbController : MagnetCollectible
{
    [Header("Experience Orb")]
    [SerializeField, Min(1)]
    private int expValue = 1;

    public int ExpValue => expValue;

    protected override bool TryCollect(PlayerItemMagnet magnet)
    {
        ExpDropManager manager = ExpDropManager.Instance;
        if (manager == null || !manager.isActiveAndEnabled)
        {
            return false;
        }

        manager.AddExperience(expValue);
        Destroy(gameObject);
        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        expValue = Mathf.Max(1, expValue);
    }
}
