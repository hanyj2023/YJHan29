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

    [Header("On-Hit Healing")]
    [SerializeField, Min(0)]
    private int healPercent;

    private PlayerHealth playerHealth;

    public float BaseAttackPower => baseAttackPower;
    public int AttackPercent => attackPercent;
    public float CurrentAttackPower => baseAttackPower * attackPercent / 100f;
    public int HealPercent => healPercent;

    public event Action<float> AttackPowerChanged;

    private void Awake()
    {
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
    }
}
