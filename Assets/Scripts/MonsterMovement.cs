using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MonsterMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 2f;

    [Header("Facing")]
    [SerializeField]
    private bool faceTarget;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    private Rigidbody2D body;
    private Transform target;
    private Action onDestroyed;
    private SpawnShape spawnShape;
    private int tornadoDirection = 1;
    private float movementPhase;
    private bool initialized;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    public void Initialize(Transform followTarget, Action destroyedCallback)
    {
        Initialize(followTarget, destroyedCallback, SpawnShape.CIRCLE, 1, 0f);
    }

    public void Initialize(
        Transform followTarget,
        Action destroyedCallback,
        SpawnShape shape,
        int spinDirection,
        float phase)
    {
        target = followTarget;
        onDestroyed = destroyedCallback;
        spawnShape = shape;
        tornadoDirection = spinDirection < 0 ? -1 : 1;
        movementPhase = phase;
        initialized = true;
        UpdateFacingDirection();
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        UpdateFacingDirection();

        if (moveSpeed <= 0f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = (Vector2)target.position - body.position;
        Vector2 direction = CalculateMovementDirection(toTarget);
        body.MovePosition(body.position + direction * (moveSpeed * Time.fixedDeltaTime));
    }

    private Vector2 CalculateMovementDirection(Vector2 toTarget)
    {
        if (toTarget.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector2.zero;
        }

        Vector2 inward = toTarget.normalized;
        if (spawnShape != SpawnShape.TORNADO)
        {
            return inward;
        }

        Vector2 tangent = new Vector2(-inward.y, inward.x) * tornadoDirection;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f + movementPhase);
        return (inward + tangent * pulse).normalized;
    }

    private void UpdateFacingDirection()
    {
        if (!faceTarget || spriteRenderer == null || target == null)
        {
            return;
        }

        float horizontalDifference = target.position.x - transform.position.x;
        if (!Mathf.Approximately(horizontalDifference, 0f))
        {
            // Monster sprites face right by default, so flip only when the target is left.
            spriteRenderer.flipX = horizontalDifference < 0f;
        }
    }

    private void OnDestroy()
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        onDestroyed?.Invoke();
        onDestroyed = null;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
}
