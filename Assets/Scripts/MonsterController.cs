using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MonsterController : MonoBehaviour, IDamageable
{
    [Serializable]
    private sealed class ExpDropEntry
    {
        [SerializeField]
        private ExpOrbController orbPrefab;

        [SerializeField, Range(0f, 100f)]
        private float dropChance;

        public ExpOrbController OrbPrefab => orbPrefab;
        public float DropChance => dropChance;

        public void Validate()
        {
            dropChance = Mathf.Clamp(dropChance, 0f, 100f);
        }
    }

    [Header("Monster Stats")]
    [SerializeField, Min(1f)]
    private float maxHP = 30f;

    [SerializeField, Min(0f)]
    private float attackDamge = 10f;

    [Header("Experience Drops")]
    [Tooltip("One roll is made on death. Entries are checked in order; any chance left over means no orb drops.")]
    [SerializeField]
    private ExpDropEntry[] expDrops = Array.Empty<ExpDropEntry>();

    private float currentHP;
    private bool isDead;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public float AttackDamage => attackDamge;

    private void Awake()
    {
        currentHP = maxHP;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || isDead)
        {
            return;
        }

        currentHP = Mathf.Max(0f, currentHP - damage);
        if (currentHP <= 0f)
        {
            Die();
        }
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (isDead || attackDamge <= 0f)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        playerHealth?.TakeDamage(attackDamge);
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        DropExperienceOrb();
        Destroy(gameObject);
    }

    private void DropExperienceOrb()
    {
        if (expDrops == null)
        {
            return;
        }

        float roll = UnityEngine.Random.Range(0f, 100f);
        float cumulativeChance = 0f;

        foreach (ExpDropEntry drop in expDrops)
        {
            if (drop == null || drop.OrbPrefab == null || drop.DropChance <= 0f)
            {
                continue;
            }

            cumulativeChance += drop.DropChance;
            if (roll < cumulativeChance)
            {
                Instantiate(drop.OrbPrefab, transform.position, Quaternion.identity);
                return;
            }
        }
    }

    private void OnValidate()
    {
        maxHP = Mathf.Max(1f, maxHP);
        attackDamge = Mathf.Max(0f, attackDamge);

        if (expDrops == null)
        {
            return;
        }

        foreach (ExpDropEntry drop in expDrops)
        {
            drop?.Validate();
        }
    }
}
