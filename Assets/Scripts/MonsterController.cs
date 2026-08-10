using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MonsterController : MonoBehaviour, IDamageable
{
    [Header("Monster Stats")]
    [SerializeField, Min(1f)]
    private float maxHP = 30f;

    [SerializeField, Min(0f)]
    private float attackDamge = 10f;

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
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        maxHP = Mathf.Max(1f, maxHP);
        attackDamge = Mathf.Max(0f, attackDamge);
    }
}
