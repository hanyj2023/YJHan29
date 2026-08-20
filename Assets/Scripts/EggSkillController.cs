using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats))]
public sealed class EggSkillController : MonoBehaviour
{
    [Header("투척")]
    [InspectorName("투척 쿨타임")]
    [SerializeField, Min(0.01f)]
    private float throwCooldown = 3f;

    [InspectorName("계란 투사체 프리팹")]
    [SerializeField]
    private GameObject projectilePrefab;

    [InspectorName("투사체 이동 속도")]
    [SerializeField, Min(0.01f)]
    private float projectileSpeed = 8f;

    [InspectorName("최대 사거리")]
    [SerializeField, Min(0f)]
    private float maximumRange = 8f;

    [Header("착탄 및 장판")]
    [InspectorName("착탄 이펙트 프리팹")]
    [SerializeField]
    private GameObject impactEffectPrefab;

    [InspectorName("장판 반경 크기")]
    [SerializeField, Min(0f)]
    private float zoneRadius = 2.5f;

    [InspectorName("장판 지속 시간")]
    [SerializeField, Min(0.01f)]
    private float zoneDuration = 5f;

    [InspectorName("도트 판정 대상 레이어")]
    [Tooltip("Monster 레이어만 선택합니다. 플레이어는 이 마스크와 무관하게 공격 대상이 아닙니다.")]
    [SerializeField]
    private LayerMask damageLayers;

    [InspectorName("공격력 비례 초당 피해 배율 (%)")]
    [SerializeField, Min(0f)]
    private float damagePercent = 30f;

    private PlayerAttackStats attackStats;
    private float cooldownRemaining;
    private bool skillUnlocked;

    // These active values form a complete snapshot. Applying a later card
    // replaces the previous level instead of accumulating any field.
    private EggSkillStats activeStats;

    public bool IsUnlocked => skillUnlocked;
    public float DamagePercent => activeStats.DamagePercent;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        projectilePrefab ??= Resources.Load<GameObject>("Prefabs/EggProjectile");
        impactEffectPrefab ??= Resources.Load<GameObject>("Prefabs/EggEffect_0");

        if (projectilePrefab == null || impactEffectPrefab == null)
        {
            Debug.LogError(
                "EggSkillController requires EggProjectile and EggEffect_0 prefabs.",
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

        if (TryThrowAtNearestEnemy())
        {
            cooldownRemaining = activeStats.ThrowCooldown;
        }
    }

    /// <summary>Unlocks the skill and wholly replaces the prior card level.</summary>
    public void ApplyUpgrade(float newDamagePercent)
    {
        damagePercent = Mathf.Max(0f, newDamagePercent);
        activeStats = new EggSkillStats(
            throwCooldown,
            projectileSpeed,
            maximumRange,
            zoneRadius,
            zoneDuration,
            damageLayers,
            damagePercent);
        skillUnlocked = true;
        cooldownRemaining = activeStats.ThrowCooldown;
    }

    private bool TryThrowAtNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            activeStats.MaximumRange,
            activeStats.DamageLayers);
        ICombatTarget nearestTarget = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (Collider2D hit in hits)
        {
            ICombatTarget target = CombatTargetFinder.Find(hit);
            if (target == null || target.IsDead)
            {
                continue;
            }

            float distanceSquared =
                (target.TargetTransform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestTarget = target;
            }
        }

        if (nearestTarget == null)
        {
            return false;
        }

        Vector3 start = transform.position;
        Vector2 direction = nearestTarget.TargetTransform.position - start;
        GameObject projectile = Instantiate(
            projectilePrefab,
            start,
            Quaternion.identity);
        EggProjectileController runtime =
            projectile.GetComponent<EggProjectileController>();
        if (runtime == null)
        {
            runtime = projectile.AddComponent<EggProjectileController>();
        }

        EggSkillStats throwStats = activeStats;
        runtime.Initialize(
            start,
            direction,
            throwStats.ProjectileSpeed,
            throwStats.MaximumRange,
            throwStats.DamageLayers,
            position => SpawnImpactEffect(position, throwStats));
        return true;
    }

    private void SpawnImpactEffect(Vector3 position, EggSkillStats throwStats)
    {
        GameObject effect = Instantiate(
            impactEffectPrefab,
            position,
            Quaternion.identity);
        EggImpactEffectRuntime runtime =
            effect.GetComponent<EggImpactEffectRuntime>();
        if (runtime == null)
        {
            runtime = effect.AddComponent<EggImpactEffectRuntime>();
        }

        runtime.Initialize(
            throwStats.ZoneRadius,
            () =>
            {
                SpriteRenderer effectRenderer =
                    effect != null ? effect.GetComponent<SpriteRenderer>() : null;
                Sprite finalFrame =
                    effectRenderer != null ? effectRenderer.sprite : null;
                SpawnDamageZone(position, throwStats, finalFrame);
                if (effect != null)
                {
                    Destroy(effect);
                }
            });
    }

    private void SpawnDamageZone(
        Vector3 position,
        EggSkillStats throwStats,
        Sprite finalEffectFrame)
    {
        GameObject zoneObject = new GameObject("EggDamageZone");
        zoneObject.transform.position = position;
        EggDamageZone zone = zoneObject.AddComponent<EggDamageZone>();
        zone.Initialize(
            throwStats.ZoneRadius,
            throwStats.ZoneDuration,
            throwStats.DamageLayers,
            throwStats.DamagePercent,
            attackStats,
            finalEffectFrame);
    }

    private void OnValidate()
    {
        throwCooldown = Mathf.Max(0.01f, throwCooldown);
        projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
        maximumRange = Mathf.Max(0f, maximumRange);
        zoneRadius = Mathf.Max(0f, zoneRadius);
        zoneDuration = Mathf.Max(0.01f, zoneDuration);
        damagePercent = Mathf.Max(0f, damagePercent);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, maximumRange);
    }

    private readonly struct EggSkillStats
    {
        public float ThrowCooldown { get; }
        public float ProjectileSpeed { get; }
        public float MaximumRange { get; }
        public float ZoneRadius { get; }
        public float ZoneDuration { get; }
        public LayerMask DamageLayers { get; }
        public float DamagePercent { get; }

        public EggSkillStats(
            float cooldown,
            float speed,
            float range,
            float radius,
            float duration,
            LayerMask layers,
            float percent)
        {
            ThrowCooldown = cooldown;
            ProjectileSpeed = speed;
            MaximumRange = range;
            ZoneRadius = radius;
            ZoneDuration = duration;
            DamageLayers = layers;
            DamagePercent = percent;
        }
    }
}
