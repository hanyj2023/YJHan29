using System;
using UnityEngine;

public interface IDamageable
{
    /// <summary>Applies damage and returns the amount actually removed from HP.</summary>
    float ApplyDamage(float damage);
}

/// <summary>Common targeting contract for monsters and monster-like objects.</summary>
public interface ICombatTarget : IDamageable
{
    bool IsDead { get; }
    Transform TargetTransform { get; }
}

public static class CombatTargetFinder
{
    public static ICombatTarget Find(Collider2D collider)
    {
        if (collider == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours =
            collider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ICombatTarget target)
            {
                return target;
            }
        }

        return null;
    }
}

public readonly struct ConfirmedDamage
{
    public IDamageable Target { get; }
    public PlayerAttackStats Attacker { get; }
    public float RequestedDamage { get; }
    public float AppliedDamage { get; }

    public ConfirmedDamage(
        IDamageable target,
        PlayerAttackStats attacker,
        float requestedDamage,
        float appliedDamage)
    {
        Target = target;
        Attacker = attacker;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
    }
}

/// <summary>
/// The single damage-confirmation path for projectiles, melee attacks, damage
/// over time, and future player skills.
/// </summary>
public static class CombatDamage
{
    public static event Action<ConfirmedDamage> DamageConfirmed;

    public static float Apply(
        IDamageable target,
        float requestedDamage,
        PlayerAttackStats attacker = null)
    {
        if (target == null || requestedDamage <= 0f)
        {
            return 0f;
        }

        float appliedDamage = Mathf.Clamp(
            target.ApplyDamage(requestedDamage),
            0f,
            requestedDamage);

        if (appliedDamage > 0f)
        {
            DamageConfirmed?.Invoke(new ConfirmedDamage(
                target,
                attacker,
                requestedDamage,
                appliedDamage));
        }

        return appliedDamage;
    }
}
