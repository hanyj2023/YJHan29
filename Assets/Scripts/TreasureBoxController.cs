using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TreasureBoxController : MonoBehaviour, ICombatTarget
{
    [Serializable]
    private sealed class ItemDropEntry
    {
        [SerializeField]
        private GameObject itemPrefab;

        [SerializeField, Range(0f, 100f)]
        private float dropChance;

        public GameObject ItemPrefab => itemPrefab;
        public float DropChance => dropChance;

        public void Validate()
        {
            dropChance = Mathf.Clamp(dropChance, 0f, 100f);
        }
    }

    [Header("Treasure Box Stats")]
    [SerializeField, Min(1f)]
    private float maxHP = 100f;

    [Header("Item Drops")]
    [Tooltip("파괴 시 한 번 추첨합니다. 위에서부터 누적 확률로 확인하며, 남는 확률은 아이템이 나오지 않습니다.")]
    [SerializeField]
    private ItemDropEntry[] itemDrops = Array.Empty<ItemDropEntry>();

    private float currentHP;
    private bool isDead;
    private Action<TreasureBoxController> destroyedCallback;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public bool IsDead => isDead;
    public Transform TargetTransform => transform;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void Initialize(Action<TreasureBoxController> onDestroyed)
    {
        destroyedCallback = onDestroyed;
    }

    public float ApplyDamage(float damage)
    {
        if (damage <= 0f || isDead || LevelUpPanelController.IsPaused)
        {
            return 0f;
        }

        float appliedDamage = Mathf.Min(currentHP, damage);
        currentHP -= appliedDamage;
        if (currentHP <= 0f)
        {
            Die();
        }

        return appliedDamage;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        DropItem();
        Destroy(gameObject);
    }

    private void DropItem()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        float cumulativeChance = 0f;

        foreach (ItemDropEntry drop in itemDrops)
        {
            if (drop == null || drop.ItemPrefab == null || drop.DropChance <= 0f)
            {
                continue;
            }

            cumulativeChance += drop.DropChance;
            if (roll < cumulativeChance)
            {
                Instantiate(drop.ItemPrefab, transform.position, Quaternion.identity);
                return;
            }
        }
    }

    private void OnDestroy()
    {
        Action<TreasureBoxController> callback = destroyedCallback;
        destroyedCallback = null;
        callback?.Invoke(this);
    }

    private void OnValidate()
    {
        maxHP = Mathf.Max(1f, maxHP);
        if (itemDrops == null)
        {
            return;
        }

        foreach (ItemDropEntry drop in itemDrops)
        {
            drop?.Validate();
        }
    }
}
