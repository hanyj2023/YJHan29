using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EggDamageZone : MonoBehaviour
{
    private const float DamageInterval = 1f;
    private const float VisualFadeDuration = 0.2f;
    private const int CircleSegments = 64;

    private readonly Dictionary<ICombatTarget, float> nextDamageTimes =
        new Dictionary<ICombatTarget, float>();
    private readonly HashSet<ICombatTarget> targetsInside =
        new HashSet<ICombatTarget>();
    private readonly List<ICombatTarget> removalBuffer =
        new List<ICombatTarget>();

    private PlayerAttackStats attackStats;
    private LayerMask damageLayers;
    private float radius;
    private float duration;
    private float damagePercent;
    private float elapsed;
    private LineRenderer outline;
    private SpriteRenderer fieldRenderer;
    private Color outlineColor;
    private Color fieldColor;
    private bool initialized;

    public void Initialize(
        float zoneRadius,
        float lifetime,
        LayerMask targetLayers,
        float attackPowerPercent,
        PlayerAttackStats ownerAttackStats,
        Sprite finalFrameSprite = null)
    {
        radius = Mathf.Max(0f, zoneRadius);
        duration = Mathf.Max(0.01f, lifetime);
        damageLayers = targetLayers;
        damagePercent = Mathf.Max(0f, attackPowerPercent);
        attackStats = ownerAttackStats;
        elapsed = 0f;
        CreateVisual(finalFrameSprite);
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || LevelUpPanelController.IsPaused)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed <= duration)
        {
            UpdateTargetsAndDamage();
            return;
        }

        UpdateFadeAfterLifetime();
        if (elapsed >= duration + VisualFadeDuration)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateTargetsAndDamage()
    {
        float now = Time.time;
        targetsInside.Clear();
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            radius,
            damageLayers);

        foreach (Collider2D hit in hits)
        {
            ICombatTarget target = CombatTargetFinder.Find(hit);
            if (target == null || target.IsDead || !targetsInside.Add(target))
            {
                continue;
            }

            if (!nextDamageTimes.TryGetValue(target, out float nextDamageTime))
            {
                // Each entrant owns its own cadence. Re-entering after leaving
                // starts a fresh one-second period rather than inheriting one.
                nextDamageTimes[target] = now + DamageInterval;
                continue;
            }

            if (now < nextDamageTime)
            {
                continue;
            }

            float damage = attackStats.CurrentAttackPower * damagePercent / 100f;
            CombatDamage.Apply(target, damage, attackStats);
            nextDamageTimes[target] = now + DamageInterval;
        }

        removalBuffer.Clear();
        foreach (ICombatTarget trackedTarget in nextDamageTimes.Keys)
        {
            if (!targetsInside.Contains(trackedTarget))
            {
                removalBuffer.Add(trackedTarget);
            }
        }

        foreach (ICombatTarget target in removalBuffer)
        {
            nextDamageTimes.Remove(target);
        }
    }

    private void CreateVisual(Sprite finalFrameSprite)
    {
        CreateFieldSprite(finalFrameSprite);

        outline = gameObject.AddComponent<LineRenderer>();
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = CircleSegments;
        outline.widthMultiplier = Mathf.Max(0.04f, radius * 0.04f);
        outline.sortingOrder = 1;
        outline.material = new Material(Shader.Find("Sprites/Default"));
        outlineColor = new Color(1f, 0.88f, 0.22f, 0.72f);
        outline.startColor = outlineColor;
        outline.endColor = outlineColor;

        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / CircleSegments;
            outline.SetPosition(index, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f));
        }
    }

    private void CreateFieldSprite(Sprite suppliedFinalFrame)
    {
        Sprite fieldSprite = suppliedFinalFrame;
        if (fieldSprite == null)
        {
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("Sprites/EggEffect"))
            {
                if (sprite.name == "EggEffect_3")
                {
                    fieldSprite = sprite;
                    break;
                }
            }
        }

        if (fieldSprite == null)
        {
            return;
        }

        GameObject visual = new GameObject("FriedEggFieldVisual");
        visual.transform.SetParent(transform, false);
        fieldRenderer = visual.AddComponent<SpriteRenderer>();

        fieldRenderer.sprite = fieldSprite;
        fieldRenderer.sortingOrder = 0;
        fieldColor = new Color(1f, 1f, 1f, 0.55f);
        fieldRenderer.color = fieldColor;

        // Match the visible field renderer's full width and height to the
        // actual damage-zone diameter configured by EggSkillController.
        float diameter = radius * 2f;
        float scaleX = fieldSprite.bounds.size.x > 0f
            ? diameter / fieldSprite.bounds.size.x
            : 1f;
        float scaleY = fieldSprite.bounds.size.y > 0f
            ? diameter / fieldSprite.bounds.size.y
            : 1f;
        Transform visualTransform = fieldRenderer.transform;
        visualTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        visualTransform.localPosition = new Vector3(
            -fieldSprite.bounds.center.x * scaleX,
            -fieldSprite.bounds.center.y * scaleY,
            0f);
    }

    private void UpdateFadeAfterLifetime()
    {
        // Damage ends at duration. The final frame stays at full opacity for
        // that entire lifetime, then gets a separate short visual-only fade.
        float fadeProgress = Mathf.InverseLerp(
            duration,
            duration + VisualFadeDuration,
            elapsed);
        float alpha = outlineColor.a * (1f - fadeProgress);
        Color color = new Color(
            outlineColor.r,
            outlineColor.g,
            outlineColor.b,
            alpha);
        outline.startColor = color;
        outline.endColor = color;

        if (fieldRenderer != null)
        {
            fieldRenderer.color = new Color(
                fieldColor.r,
                fieldColor.g,
                fieldColor.b,
                fieldColor.a * (alpha / outlineColor.a));
        }
    }

    private void OnDestroy()
    {
        if (outline != null && outline.material != null)
        {
            Destroy(outline.material);
        }
    }
}
