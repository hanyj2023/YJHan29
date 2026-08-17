using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireBombProjectileController : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float flightDuration;
    private float rotationSpeed;
    private float arcHeight;
    private float elapsed;
    private float rotationDirection;
    private Action<Vector3> arrived;
    private bool initialized;

    public void Initialize(
        Vector3 start,
        Vector3 target,
        float duration,
        float degreesPerSecond,
        float height,
        Action<Vector3> onArrived)
    {
        startPosition = start;
        targetPosition = target;
        flightDuration = Mathf.Max(0f, duration);
        rotationSpeed = degreesPerSecond;
        arcHeight = Mathf.Max(0f, height);
        arrived = onArrived;
        elapsed = 0f;

        // Unity's positive Z rotation is counter-clockwise. A target on the
        // right therefore uses -1, while a target on the left uses +1.
        rotationDirection = targetPosition.x >= startPosition.x ? -1f : 1f;
        transform.position = startPosition;
        initialized = true;

        if (flightDuration <= 0f)
        {
            CompleteFlight();
        }
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / flightDuration);
        Vector3 linearPosition = Vector3.Lerp(
            startPosition,
            targetPosition,
            progress);
        float verticalOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
        transform.position = linearPosition + Vector3.up * verticalOffset;

        // Keep the inspector value's sign meaningful: do not normalize it
        // with Mathf.Abs before multiplying by the calculated direction.
        transform.Rotate(
            0f,
            0f,
            rotationSpeed * rotationDirection * Time.deltaTime);

        if (progress >= 1f)
        {
            CompleteFlight();
        }
    }

    private void CompleteFlight()
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        transform.position = targetPosition;
        Action<Vector3> callback = arrived;
        arrived = null;
        callback?.Invoke(targetPosition);
        Destroy(gameObject);
    }
}
