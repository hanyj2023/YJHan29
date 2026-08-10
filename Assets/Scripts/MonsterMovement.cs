using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MonsterMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 2f;

    private Rigidbody2D body;
    private Transform target;
    private Action onDestroyed;
    private bool initialized;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
    }

    public void Initialize(Transform followTarget, Action destroyedCallback)
    {
        target = followTarget;
        onDestroyed = destroyedCallback;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (target == null || moveSpeed <= 0f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = ((Vector2)target.position - body.position).normalized;
        body.MovePosition(body.position + direction * (moveSpeed * Time.fixedDeltaTime));
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
