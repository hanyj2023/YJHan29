using UnityEngine;
using UnityEngine.InputSystem;

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

    private Vector2 fireDirection = Vector2.right;
    private float nextFireTime;

    private void OnEnable()
    {
        nextFireTime = Time.time;
    }

    private void Update()
    {
        Vector2 inputDirection = ReadKeyboardInput();
        if (inputDirection.sqrMagnitude > 0f
            && !ApproximatelySameDirection(inputDirection, fireDirection))
        {
            fireDirection = inputDirection.normalized;

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

        ProjectileController projectile = Instantiate(
            projectilePrefab,
            origin.position,
            baseRotation);

        projectile.Initialize(fireDirection, baseRotation);
    }

    private static bool ApproximatelySameDirection(Vector2 a, Vector2 b)
    {
        return Vector2.Dot(a.normalized, b.normalized) > 0.9999f;
    }

    private static Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        bool left =
            keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool right =
            keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        bool down =
            keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        bool up =
            keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;

        float x = left == right ? 0f : left ? -1f : 1f;
        float y = down == up ? 0f : down ? -1f : 1f;

        Vector2 input = new Vector2(x, y);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void OnValidate()
    {
        fireInterval = Mathf.Max(0.01f, fireInterval);
        minTurnCooldown = Mathf.Max(0f, minTurnCooldown);
    }
}
