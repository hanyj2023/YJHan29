using UnityEngine;

[DisallowMultipleComponent]
public sealed class PotionController : MagnetCollectible
{
    [Header("Potion")]
    [SerializeField, Min(0f)]
    private float healAmount = 20f;

    public float HealAmount => healAmount;

    protected override bool TryCollect(PlayerItemMagnet magnet)
    {
        PlayerHealth health = magnet.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = magnet.GetComponentInParent<PlayerHealth>();
        }

        if (health == null || health.IsDead)
        {
            return false;
        }

        health.Heal(healAmount);
        Destroy(gameObject);
        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        healAmount = Mathf.Max(0f, healAmount);
    }
}
