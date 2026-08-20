using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EggProjectileController : MonoBehaviour
{
    private const float HitCastRadius = 0.15f;
    private const float MinimumVisibleTime = 0.18f;

    private Vector3 startPosition;
    private Vector2 direction;
    private float speed;
    private float remainingDistance;
    private LayerMask hitLayers;
    private Action<Vector3> landed;
    private Vector3 pendingLandingPosition;
    private float elapsed;
    private bool hasPendingLanding;
    private bool initialized;

    public void Initialize(
        Vector3 start,
        Vector2 throwDirection,
        float moveSpeed,
        float maximumRange,
        LayerMask targetLayers,
        Action<Vector3> onLanded)
    {
        startPosition = start;
        transform.position = start;
        direction = throwDirection.sqrMagnitude > 0f
            ? throwDirection.normalized
            : Vector2.right;
        speed = Mathf.Max(0.01f, moveSpeed);
        remainingDistance = Mathf.Max(0f, maximumRange);
        hitLayers = targetLayers;
        landed = onLanded;
        elapsed = 0f;
        hasPendingLanding = false;
        initialized = true;

        EnsureVisibleSprite();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (remainingDistance <= 0f)
        {
            RequestLanding(transform.position);
        }
    }

    private void Update()
    {
        if (!initialized || LevelUpPanelController.IsPaused)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (hasPendingLanding)
        {
            float progress = Mathf.Clamp01(elapsed / MinimumVisibleTime);
            transform.position = Vector3.Lerp(
                startPosition,
                pendingLandingPosition,
                progress);
            RotateVisual();
            if (elapsed >= MinimumVisibleTime)
            {
                Land(pendingLandingPosition);
            }

            return;
        }

        float travelDistance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
        Vector2 origin = transform.position;
        RaycastHit2D hit = Physics2D.CircleCast(
            origin,
            HitCastRadius,
            direction,
            travelDistance,
            hitLayers);

        if (hit.collider != null)
        {
            ICombatTarget target = CombatTargetFinder.Find(hit.collider);
            if (target != null && !target.IsDead)
            {
                RequestLanding(hit.point);
                return;
            }
        }

        transform.position += (Vector3)(direction * travelDistance);
        remainingDistance -= travelDistance;
        RotateVisual();

        if (remainingDistance <= 0f)
        {
            RequestLanding(transform.position);
        }
    }

    private void EnsureVisibleSprite()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        Sprite sprite = Resources.Load<Sprite>("Sprites/Skill_Egg");
        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("Sprites/Skill_Egg");
            if (sprites.Length > 0)
            {
                sprite = sprites[0];
            }
        }

        if (sprite != null)
        {
            renderer.sprite = sprite;
        }

        renderer.enabled = true;
        renderer.color = Color.white;
        renderer.sortingOrder = 5;
    }

    private void RequestLanding(Vector3 position)
    {
        if (elapsed >= MinimumVisibleTime)
        {
            Land(position);
            return;
        }

        pendingLandingPosition = position;
        hasPendingLanding = true;
    }

    private void RotateVisual()
    {
        transform.Rotate(0f, 0f, -540f * Time.deltaTime);
    }

    private void Land(Vector3 position)
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        Action<Vector3> callback = landed;
        landed = null;
        callback?.Invoke(position);
        Destroy(gameObject);
    }
}
