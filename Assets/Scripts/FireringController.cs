using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FireringController : MonoBehaviour
{
    [Header("Orbit")]
    [Tooltip("Diameter of the circular orbit around the player.")]
    [SerializeField, Min(0f)]
    private float orbitDiameter = 4f;

    [Tooltip("Orbital rotation speed in degrees per second.")]
    [SerializeField]
    private float degreesPerSecond = 90f;

    [Header("Damage")]
    [Tooltip("Multiplier applied to the player's current attack power.")]
    [SerializeField, Min(0f)]
    private float attackPowerMultiplier = 1f;

    [Tooltip("Minimum interval before this fireball can damage the same monster again.")]
    [SerializeField, Min(0.01f)]
    private float hitCooldown = 0.5f;

    private readonly Dictionary<int, float> nextHitTimeByTarget =
        new Dictionary<int, float>();

    private Transform orbitCenter;
    private PlayerAttackStats attackStats;
    private int orbitIndex;
    private int orbitCount = 1;

    public float OrbitDiameter => orbitDiameter;
    public float DegreesPerSecond => degreesPerSecond;
    public float AttackPowerMultiplier => attackPowerMultiplier;
    public float HitCooldown => hitCooldown;

    private void Awake()
    {
        Collider2D hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;
    }

    public void Initialize(
        Transform center,
        PlayerAttackStats ownerAttackStats,
        int index,
        int totalCount)
    {
        orbitCenter = center;
        attackStats = ownerAttackStats;
        SetOrbitSlot(index, totalCount);
        UpdateOrbitPosition();
    }

    public void SetOrbitSlot(int index, int totalCount)
    {
        orbitIndex = Mathf.Max(0, index);
        orbitCount = Mathf.Max(1, totalCount);
    }

    private void Update()
    {
        if (orbitCenter == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateOrbitPosition();
    }

    private void UpdateOrbitPosition()
    {
        float spacing = 360f / orbitCount;
        float angle = (Time.time * degreesPerSecond) + (spacing * orbitIndex);
        float radians = angle * Mathf.Deg2Rad;
        float radius = orbitDiameter * 0.5f;
        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius,
            0f);

        transform.position = orbitCenter.position + offset;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (LevelUpPanelController.IsPaused || attackStats == null)
        {
            return;
        }

        ICombatTarget target = CombatTargetFinder.Find(other);
        if (target == null || target.IsDead)
        {
            return;
        }

        int targetId = target.TargetTransform.GetInstanceID();
        if (nextHitTimeByTarget.TryGetValue(targetId, out float nextHitTime)
            && Time.time < nextHitTime)
        {
            return;
        }

        nextHitTimeByTarget[targetId] = Time.time + hitCooldown;
        float damage = attackStats.CurrentAttackPower * attackPowerMultiplier;
        CombatDamage.Apply(target, damage, attackStats);
    }

    private void OnValidate()
    {
        orbitDiameter = Mathf.Max(0f, orbitDiameter);
        attackPowerMultiplier = Mathf.Max(0f, attackPowerMultiplier);
        hitCooldown = Mathf.Max(0.01f, hitCooldown);
    }
}
