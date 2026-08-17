using UnityEngine;

[DisallowMultipleComponent]
public sealed class AutoShooter : MonoBehaviour
{
    [Header("Firing")]
    [SerializeField, Min(0.01f)]
    private float fireInterval = 0.5f;

    [SerializeField, Min(0f)]
    private float minTurnCooldown = 0.1f;

    [SerializeField]
    private ProjectileController projectilePrefab = null;

    [Tooltip("If omitted, projectiles spawn at the Player transform.")]
    [SerializeField]
    private Transform spawnPoint = null;

    [Tooltip("Optional rotation that defines the projectile sprite's default right-facing pose.")]
    [SerializeField]
    private Transform baseRotationReference = null;

    [SerializeField, Range(1, 4)]
    private int projectileCount = 1;

    private Vector2 fireDirection = Vector2.right;
    private float nextFireTime;
    private PlayerAttackStats attackStats;
    private PlayerMovement playerMovement;

    public int ProjectileCount => projectileCount;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        playerMovement = GetComponent<PlayerMovement>();
        if (attackStats == null || playerMovement == null)
        {
            Debug.LogError(
                "AutoShooter requires PlayerAttackStats and PlayerMovement on the same object.",
                this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        nextFireTime = Time.time;
    }

    private void LateUpdate()
    {
        if (LevelUpPanelController.IsPaused)
        {
            return;
        }

        Vector2 facingDirection = playerMovement.FacingDirection;
        if (facingDirection.sqrMagnitude > 0f
            && !ApproximatelySameDirection(facingDirection, fireDirection))
        {
            fireDirection = facingDirection.normalized;

            // Turning can only postpone the next shot; it never creates an
            // extra shot earlier than the normal interval.
            nextFireTime = Mathf.Max(
                nextFireTime,
                Time.time + minTurnCooldown);
        }

        if (Time.time >= nextFireTime)
        {
            Fire();
        }
    }

    private void Fire()
    {
        // Advance the timer even if setup is incomplete, avoiding a retry every frame.
        nextFireTime = Time.time + fireInterval;

        if (projectilePrefab == null)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Quaternion baseRotation = baseRotationReference != null
            ? baseRotationReference.rotation
            : projectilePrefab.transform.rotation;

        for (int index = 0; index < projectileCount; index++)
        {
            ProjectileController projectile = Instantiate(
                projectilePrefab,
                origin.position,
                baseRotation);

            projectile.Initialize(
                GetProjectileDirection(index),
                baseRotation,
                attackStats);
        }
    }

    private static bool ApproximatelySameDirection(Vector2 a, Vector2 b)
    {
        return Vector2.Dot(a.normalized, b.normalized) > 0.9999f;
    }

    private Vector2 GetProjectileDirection(int index)
    {
        Vector2 sideDirection = Mathf.Abs(fireDirection.x) > 0.5f
            ? Vector2.up
            : Vector2.right;

        switch (index)
        {
            case 0:
                return fireDirection;
            case 1:
                return -fireDirection;
            case 2:
                // Use world up for horizontal facing. For vertical facing,
                // use world right so the third shot never overlaps shot 1/2.
                return sideDirection;
            case 3:
                return -sideDirection;
            default:
                return fireDirection;
        }
    }

    public void SetProjectileCount(int count)
    {
        projectileCount = Mathf.Clamp(count, 1, 4);

        // Keep the firing schedule valid when the value changes while the
        // level-up panel has frozen scaled time.
        if (!float.IsFinite(nextFireTime))
        {
            nextFireTime = Time.time;
        }
    }

    private void OnValidate()
    {
        fireInterval = Mathf.Max(0.01f, fireInterval);
        minTurnCooldown = Mathf.Max(0f, minTurnCooldown);
        projectileCount = Mathf.Clamp(projectileCount, 1, 4);
    }
}
