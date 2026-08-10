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
        target = followTarget;
        onDestroyed = destroyedCallback;
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

        Vector2 direction = ((Vector2)target.position - body.position).normalized;
        body.MovePosition(body.position + direction * (moveSpeed * Time.fixedDeltaTime));
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
