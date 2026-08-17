using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FireBombEffectRuntime : MonoBehaviour
{
    private static readonly string[] FrameNames =
    {
        "FireBombEffect_1",
        "oil_explosion_sheet_6",
        "oil_explosion_sheet_0",
        "FireBombEffect_0"
    };
    private static Sprite[] cachedFrames;

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Vector3 explosionPosition;
    private float explosionRadius;
    private float flameFillRatio;
    private float duration;
    private float elapsed;
    private int currentFrame = -1;
    private bool initialized;

    public void Initialize(float radius, float fillRatio, float lifetime)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }

        frames = LoadFrames();
        explosionPosition = transform.position;
        explosionRadius = Mathf.Max(0f, radius);
        flameFillRatio = Mathf.Max(0.0001f, fillRatio);
        duration = Mathf.Max(0.01f, lifetime);
        elapsed = 0f;
        initialized = true;
        ApplyFrame(0);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(elapsed / duration);
        int frameIndex = Mathf.Min(
            frames.Length - 1,
            Mathf.FloorToInt(normalizedTime * frames.Length));
        ApplyFrame(frameIndex);

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyFrame(int frameIndex)
    {
        if (frames.Length == 0 || frameIndex == currentFrame)
        {
            return;
        }

        currentFrame = frameIndex;
        Sprite frame = frames[frameIndex];
        spriteRenderer.sprite = frame;

        // sprite width (units) = pixel width / PPU
        // scale = explosion diameter / (sprite width * flame fill ratio)
        float spriteWidthUnits = frame.rect.width / frame.pixelsPerUnit;
        float scale = spriteWidthUnits > 0f
            ? explosionRadius * 2f / (spriteWidthUnits * flameFillRatio)
            : 1f;
        transform.localScale = new Vector3(scale, scale, 1f);

        // Some supplied frames use a non-centred pivot. Offset the root so the
        // visible sprite remains centred exactly on the explosion point.
        Vector3 scaledCenter = frame.bounds.center * scale;
        transform.position = explosionPosition - scaledCenter;
    }

    private static Sprite[] LoadFrames()
    {
        if (cachedFrames != null)
        {
            return cachedFrames;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>("Sprites/FireBombEffect");
        Dictionary<string, Sprite> byName = new Dictionary<string, Sprite>(
            StringComparer.Ordinal);
        foreach (Sprite sprite in loaded)
        {
            byName[sprite.name] = sprite;
        }

        List<Sprite> ordered = new List<Sprite>(FrameNames.Length);
        foreach (string frameName in FrameNames)
        {
            if (byName.TryGetValue(frameName, out Sprite sprite))
            {
                ordered.Add(sprite);
            }
        }

        if (ordered.Count == 0 && loaded.Length > 0)
        {
            ordered.Add(loaded[0]);
        }

        cachedFrames = ordered.ToArray();
        return cachedFrames;
    }
}
