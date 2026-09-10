using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAttackStats : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField, Min(0f)]
    private float baseAttackPower = 10f;

    [SerializeField, Min(0)]
    private int attackPercent = 100;

    [Header("Critical")]
    [SerializeField, Range(0f, 100f)]
    private float criticalChance;

    [SerializeField, Min(1f)]
    private float criticalDamageMultiplier = 1.5f;

    [Header("On-Hit Healing")]
    [SerializeField, Min(0)]
    private int healPercent;

    private PlayerHealth playerHealth;

    public float BaseAttackPower => baseAttackPower;
    public int AttackPercent => attackPercent;
    public float CurrentAttackPower => baseAttackPower * attackPercent / 100f;
    public int HealPercent => healPercent;
    public float CriticalChance => criticalChance;
    public float CriticalDamageMultiplier => criticalDamageMultiplier;

    public event Action<float> AttackPowerChanged;

    private void Awake()
    {
        baseAttackPower = StatusUpgradeTable.GetSavedValue(
            PersistentStatusType.AttackPower);
        criticalChance = StatusUpgradeTable.GetSavedValue(
            PersistentStatusType.CriticalChance);
        criticalDamageMultiplier = StatusUpgradeTable.GetSavedValue(
            PersistentStatusType.CriticalDamageMultiplier);

        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogError("PlayerAttackStats requires PlayerHealth on the same object.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        CombatDamage.DamageConfirmed += OnDamageConfirmed;
    }

    private void OnDisable()
    {
        CombatDamage.DamageConfirmed -= OnDamageConfirmed;
    }

    public void SetAttackPercent(int percent)
    {
        percent = Mathf.Max(0, percent);
        if (attackPercent == percent)
        {
            return;
        }

        attackPercent = percent;
        AttackPowerChanged?.Invoke(CurrentAttackPower);
    }

    public void SetHealPercent(int percent)
    {
        healPercent = Mathf.Max(0, percent);
    }

    public float RollCriticalDamage(float damage)
    {
        if (damage <= 0f || criticalChance <= 0f)
        {
            return damage;
        }

        return UnityEngine.Random.value * 100f < criticalChance
            ? damage * criticalDamageMultiplier
            : damage;
    }

    private void OnDamageConfirmed(ConfirmedDamage damage)
    {
        if (damage.Attacker != this || healPercent <= 0 || playerHealth == null)
        {
            return;
        }

        playerHealth.Heal(damage.AppliedDamage * healPercent / 100f);
    }

    private void OnValidate()
    {
        baseAttackPower = Mathf.Max(0f, baseAttackPower);
        attackPercent = Mathf.Max(0, attackPercent);
        healPercent = Mathf.Max(0, healPercent);
        criticalChance = Mathf.Clamp(criticalChance, 0f, 100f);
        criticalDamageMultiplier = Mathf.Max(1f, criticalDamageMultiplier);
    }
}
