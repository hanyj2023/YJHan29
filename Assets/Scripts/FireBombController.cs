using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats))]
public sealed class FireBombController : MonoBehaviour
{
    [Header("Throw Timing and Targeting")]
    [SerializeField, Min(0.01f)]
    private float throwCooldown = 3f;

    [SerializeField, Min(0f)]
    private float enemySearchRadius = 8f;

    [Header("Projectile Flight")]
    [SerializeField, Min(0f)]
    private float projectileFlightDuration = 0.7f;

    [SerializeField]
    private float projectileRotationSpeed = 360f;

    [SerializeField, Min(0f)]
    private float arcHeight = 2f;

    [Header("폭발 피해")]
    [InspectorName("피격 범위")]
    [Tooltip("착탄 지점을 중심으로 몬스터에게 피해를 적용하는 원형 반경입니다.")]
    [SerializeField, Min(0f)]
    private float explosionRadius = 2.5f;

    [Tooltip("Only Monster layers should be selected. The player is never damaged.")]
    [SerializeField]
    private LayerMask damageLayers;

    [Tooltip("Damage as a percentage of the player's current attack power.")]
    [SerializeField, Min(0f)]
    private float attackPowerPercent = 150f;

    [Header("Prefabs and Effect")]
    [SerializeField]
    private GameObject projectilePrefab;

    [SerializeField]
    private GameObject explosionEffectPrefab;

    [SerializeField, Min(0.0001f)]
    private float effectFlameFillRatio = 0.9f;

    [SerializeField, Min(0.01f)]
    private float effectDuration = 0.5f;

    [Header("Runtime Upgrade")]
    [SerializeField, Min(1)]
    private int simultaneousThrowCount = 1;

    [SerializeField, Min(0f)]
    private float targetDispersionRadius = 1.5f;

    private readonly List<ICombatTarget> targetBuffer =
        new List<ICombatTarget>();
    private PlayerAttackStats attackStats;
    private float cooldownRemaining;
    private bool skillUnlocked;
    private bool hasLastExplosionPosition;
    private Vector3 lastExplosionPosition;

    public int SimultaneousThrowCount => simultaneousThrowCount;
    public float HitRadius => explosionRadius;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        projectilePrefab ??= Resources.Load<GameObject>(
            "Prefabs/FireBombProjectile");
        explosionEffectPrefab ??= Resources.Load<GameObject>(
            "Prefabs/FireBombEffect");

        if (projectilePrefab == null || explosionEffectPrefab == null)
        {
            Debug.LogError(
                "FireBombController requires FireBombProjectile and FireBombEffect prefabs.",
                this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (!skillUnlocked || LevelUpPanelController.IsPaused)
        {
            return;
        }

        cooldownRemaining -= Time.deltaTime;
        if (cooldownRemaining > 0f)
        {
            return;
        }

        cooldownRemaining = throwCooldown;
        ThrowVolley();
    }

    /// <summary>Unlocks FIREBOMB and replaces the previous volley count.</summary>
    public void SetSimultaneousThrowCount(int count)
    {
        simultaneousThrowCount = Mathf.Max(1, count);
        skillUnlocked = true;
        cooldownRemaining = throwCooldown;
    }

    private void ThrowVolley()
    {
        FindNearestTargets();
        if (targetBuffer.Count == 0)
        {
            return;
        }

        Vector3 startPosition = transform.position;
        List<Vector3> targetPositions = new List<Vector3>(
            simultaneousThrowCount);

        int distinctCount = Mathf.Min(
            simultaneousThrowCount,
            targetBuffer.Count);
        for (int index = 0; index < distinctCount; index++)
        {
            targetPositions.Add(targetBuffer[index].TargetTransform.position);
        }

        for (int index = distinctCount;
             index < simultaneousThrowCount;
             index++)
        {
            Vector3 anchor = targetPositions[index % distinctCount];
            Vector2 offset = Random.insideUnitCircle * targetDispersionRadius;
            targetPositions.Add(anchor + (Vector3)offset);
        }

        foreach (Vector3 targetPosition in targetPositions)
        {
            SpawnProjectile(startPosition, targetPosition);
        }
    }

    private void FindNearestTargets()
    {
        targetBuffer.Clear();
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            enemySearchRadius,
            damageLayers);
        HashSet<ICombatTarget> found = new HashSet<ICombatTarget>();

        foreach (Collider2D hit in hits)
        {
            ICombatTarget target = CombatTargetFinder.Find(hit);
            if (target != null && !target.IsDead && found.Add(target))
            {
                targetBuffer.Add(target);
            }
        }

        Vector3 origin = transform.position;
        targetBuffer.Sort((left, right) =>
            (left.TargetTransform.position - origin).sqrMagnitude.CompareTo(
                (right.TargetTransform.position - origin).sqrMagnitude));
    }

    private void SpawnProjectile(Vector3 startPosition, Vector3 targetPosition)
    {
        GameObject projectile = Instantiate(
            projectilePrefab,
            startPosition,
            Quaternion.identity);
        FireBombProjectileController runtime =
            projectile.GetComponent<FireBombProjectileController>();
        if (runtime == null)
        {
            runtime = projectile.AddComponent<FireBombProjectileController>();
        }

        runtime.Initialize(
            startPosition,
            targetPosition,
            projectileFlightDuration,
            projectileRotationSpeed,
            arcHeight,
            TriggerExplosion);
    }

    private void TriggerExplosion(Vector3 position)
    {
        lastExplosionPosition = position;
        hasLastExplosionPosition = true;
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

            // The central confirmation event keeps HEAL and chained EXPLOSION
            // behavior identical to every other player attack route.
            CombatDamage.Apply(target, damage, attackStats);
        }
    }

    private void SpawnEffect(Vector3 position)
    {
        GameObject effect = Instantiate(
            explosionEffectPrefab,
            position,
            Quaternion.identity);
        FireBombEffectRuntime runtime =
            effect.GetComponent<FireBombEffectRuntime>();
        if (runtime == null)
        {
            runtime = effect.AddComponent<FireBombEffectRuntime>();
        }

        runtime.Initialize(
            explosionRadius,
            effectFlameFillRatio,
            effectDuration);
    }

    private void OnValidate()
    {
        throwCooldown = Mathf.Max(0.01f, throwCooldown);
        enemySearchRadius = Mathf.Max(0f, enemySearchRadius);
        projectileFlightDuration = Mathf.Max(0f, projectileFlightDuration);
        arcHeight = Mathf.Max(0f, arcHeight);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        effectFlameFillRatio = Mathf.Max(0.0001f, effectFlameFillRatio);
        effectDuration = Mathf.Max(0.01f, effectDuration);
        simultaneousThrowCount = Mathf.Max(1, simultaneousThrowCount);
        targetDispersionRadius = Mathf.Max(0f, targetDispersionRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, enemySearchRadius);

        if (hasLastExplosionPosition)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.05f, 0.6f);
            Gizmos.DrawWireSphere(lastExplosionPosition, explosionRadius);
        }
    }
}
