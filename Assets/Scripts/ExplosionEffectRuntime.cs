using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExplosionEffectRuntime : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private Vector3 targetScale;
    private float duration;
    private float elapsed;

    public void Initialize(float sizeMultiplier, float lifetime)
    {
        duration = Mathf.Max(0.01f, lifetime);
        targetScale = transform.localScale * Mathf.Max(0f, sizeMultiplier);
        transform.localScale = targetScale * 0.05f;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            originalColors[index] = renderers[index].color;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float expandProgress = 1f - Mathf.Pow(1f - progress, 3f);
        transform.localScale = Vector3.LerpUnclamped(
            targetScale * 0.05f,
            targetScale,
            expandProgress);

        float alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
        for (int index = 0; index < renderers.Length; index++)
        {
            Color color = originalColors[index];
            color.a *= alpha;
            renderers[index].color = color;
        }

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
