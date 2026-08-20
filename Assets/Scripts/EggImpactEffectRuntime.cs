using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EggImpactEffectRuntime : MonoBehaviour
{
    private const float SecondsPerFrame = 1f / 12f;
    private static readonly string[] FrameNames =
    {
        "EggEffect_0",
        "EggEffect_1",
        "EggEffect_2",
        "EggEffect_3"
    };
    private static Sprite[] cachedFrames;

    private Action completed;
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Vector3 impactPosition;
    private float targetDiameter;
    private float elapsed;
    private int currentFrame = -1;
    private bool initialized;

    public void Initialize(float zoneRadius, Action onCompleted)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            // The supplied clip loops and its source frames have off-centre
            // pivots. Drive the frames here so the burst stays on the hit point.
            animator.enabled = false;
        }

        frames = LoadFrames();
        impactPosition = transform.position;
        targetDiameter = Mathf.Clamp(zoneRadius * 1.2f, 1.5f, 3f);
        completed = onCompleted;
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
        int frameIndex = Mathf.Min(
            frames.Length - 1,
            Mathf.FloorToInt(elapsed / SecondsPerFrame));
        ApplyFrame(frameIndex);

        if (elapsed < frames.Length * SecondsPerFrame)
        {
            return;
        }

        initialized = false;
        Action callback = completed;
        completed = null;
        callback?.Invoke();
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
        spriteRenderer.sortingOrder = 4;

        float largestDimension = Mathf.Max(
            frame.bounds.size.x,
            frame.bounds.size.y);
        float scale = largestDimension > 0f
            ? targetDiameter / largestDimension
            : 1f;
        transform.localScale = new Vector3(scale, scale, 1f);

        // Keep the visible frame centre on the exact impact position even
        // when the imported sprite pivot is at a corner.
        transform.position = impactPosition - frame.bounds.center * scale;
    }

    private static Sprite[] LoadFrames()
    {
        if (cachedFrames != null)
        {
            return cachedFrames;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>("Sprites/EggEffect");
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

        cachedFrames = ordered.ToArray();
        return cachedFrames;
    }
}
