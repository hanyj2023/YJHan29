using UnityEngine;

/// <summary>
/// Implement this interface on an Enemy component that owns health.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class ProjectileController : MonoBehaviour
{
    [Header("Projectile Stats")]
    [SerializeField, Min(0f)]
    private float damage = 1f;

    [SerializeField, Min(0f)]
    private float speed = 10f;

    [SerializeField, Min(0f)]
    private float lifetime = 3f;

    [SerializeField, Min(0.01f)]
    private float scale = 1f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveDirection = Vector2.right;
    private Vector3 originalScale;
    private bool initialized;
    private bool hasHitEnemy;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        originalScale = transform.localScale;

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.angularVelocity = 0f;
        ApplyScale();
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize(Vector2.right, transform.rotation);
        }
    }

    private void FixedUpdate()
    {
        // Reapply the velocity so contacts cannot change the requested constant speed.
        body.linearVelocity = moveDirection * speed;
    }

    /// <summary>
    /// Sets the projectile's immutable travel direction for this shot.
    /// baseRotation is the rotation at which the source sprite faces right.
    /// </summary>
    public void Initialize(Vector2 direction, Quaternion baseRotation)
    {
        moveDirection = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector2.right;

        initialized = true;
        hasHitEnemy = false;

        ApplyVisualDirection(baseRotation);
        ApplyScale();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.angularVelocity = 0f;
        body.linearVelocity = moveDirection * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamageEnemy(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamageEnemy(collision.collider);
    }

    private void TryDamageEnemy(Collider2D other)
    {
        if (hasHitEnemy || !IsOnEnemyLayer(other.transform))
        {
            return;
        }

        hasHitEnemy = true;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(damage);

        // This projectile is intentionally non-piercing.
        Destroy(gameObject);
    }

    private static bool IsOnEnemyLayer(Transform target)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
        {
            return false;
        }

        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.gameObject.layer == enemyLayer)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyVisualDirection(Quaternion baseRotation)
    {
        Vector3 localDirection3D =
            Quaternion.Inverse(baseRotation)
            * new Vector3(moveDirection.x, moveDirection.y, 0f);
        Vector2 localDirection = new Vector2(localDirection3D.x, localDirection3D.y);

        bool fireLeft = localDirection.x < 0f;
        Vector2 spriteForward = fireLeft ? Vector2.left : Vector2.right;
        float rotationOffset = Vector2.SignedAngle(spriteForward, localDirection);

        transform.rotation =
            baseRotation * Quaternion.Euler(0f, 0f, rotationOffset);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = fireLeft;
        }
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(
            originalScale.x * scale,
            originalScale.y * scale,
            originalScale.z);
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
        speed = Mathf.Max(0f, speed);
        lifetime = Mathf.Max(0f, lifetime);
        scale = Mathf.Max(0.01f, scale);
    }
}
