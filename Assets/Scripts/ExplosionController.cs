using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats))]
public sealed class ExplosionController : MonoBehaviour
{
    [Header("Explosion Damage")]
    [SerializeField, Min(0f)]
    private float explosionRadius = 3f;

    [Tooltip("Only colliders on these layers can receive explosion damage.")]
    [SerializeField]
    private LayerMask damageLayers;

    [Tooltip("Damage as a percentage of the player's current attack power.")]
    [SerializeField, Min(0f)]
    private float attackPowerPercent = 30f;

    [Header("Explosion Visual")]
    [SerializeField]
    private GameObject explosionEffectPrefab;

    [SerializeField, Min(0f)]
    private float effectSize = 0.6f;

    [SerializeField, Min(0.01f)]
    private float effectDuration = 0.5f;

    [Header("Runtime Upgrade")]
    [SerializeField, Range(0, 100)]
    private int activationChancePercent;

    private PlayerAttackStats attackStats;

    public float ExplosionRadius => explosionRadius;
    public LayerMask DamageLayers => damageLayers;
    public float AttackPowerPercent => attackPowerPercent;
    public int ActivationChancePercent => activationChancePercent;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        if (explosionEffectPrefab == null)
        {
            explosionEffectPrefab = Resources.Load<GameObject>(
                "Prefabs/ExplosionEffect");
        }

        if (explosionEffectPrefab == null)
        {
            Debug.LogError(
                "ExplosionController requires the ExplosionEffect prefab.",
                this);
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

    /// <summary>Replaces the previous activation chance instead of adding to it.</summary>
    public void SetActivationChancePercent(int percent)
    {
        activationChancePercent = Mathf.Clamp(percent, 0, 100);
    }

    private void OnDamageConfirmed(ConfirmedDamage damage)
    {
        if (activationChancePercent <= 0 || damage.Attacker != attackStats)
        {
            return;
        }

        ICombatTarget killedTarget = damage.Target as ICombatTarget;
        if (killedTarget == null || !killedTarget.IsDead)
        {
            return;
        }

        if (Random.Range(0f, 100f) >= activationChancePercent)
        {
            return;
        }

        TriggerExplosion(killedTarget.TargetTransform.position);
    }

    private void TriggerExplosion(Vector3 position)
    {
        SpawnEffect(position);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            explosionRadius,
            damageLayers);
        HashSet<ICombatTarget> damagedTargets =
            new HashSet<ICombatTarget>();

        float damage = attackStats.CurrentAttackPower
            * attackPowerPercent / 100f;

        foreach (Collider2D hit in hits)
        {
            ICombatTarget target = CombatTargetFinder.Find(hit);
            if (target == null || target.IsDead || !damagedTargets.Add(target))
            {
                continue;
            }

            // Explosion kills travel through the same confirmation event, so
            // every killed monster can independently trigger another explosion.
            CombatDamage.Apply(target, damage, attackStats);
        }
    }

    private void SpawnEffect(Vector3 position)
    {
        GameObject effect = Instantiate(
            explosionEffectPrefab,
            position,
            Quaternion.identity);
        ExplosionEffectRuntime runtime =
            effect.GetComponent<ExplosionEffectRuntime>();
        if (runtime == null)
        {
            runtime = effect.AddComponent<ExplosionEffectRuntime>();
        }

        runtime.Initialize(effectSize, effectDuration);
    }

    private void OnValidate()
    {
        explosionRadius = Mathf.Max(0f, explosionRadius);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        effectSize = Mathf.Max(0f, effectSize);
        effectDuration = Mathf.Max(0.01f, effectDuration);
        activationChancePercent = Mathf.Clamp(activationChancePercent, 0, 100);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
