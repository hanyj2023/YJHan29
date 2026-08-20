using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerItemMagnet : MonoBehaviour
{
    public static PlayerItemMagnet Instance { get; private set; }

    [Header("Item Magnet")]
    [Tooltip("Base item absorption radius in world units.")]
    [SerializeField, Min(0f)]
    private float baseAbsorptionRadius = 3f;

    [SerializeField, Min(0)]
    private int radiusPercent = 100;

    private float superMagnetEndTime;
    private float superMagnetSpeedMultiplier = 1f;

    public float BaseAbsorptionRadius => baseAbsorptionRadius;
    public int RadiusPercent => radiusPercent;
    public float CurrentAbsorptionRadius =>
        baseAbsorptionRadius * radiusPercent / 100f;
    public Vector3 CollectionPosition => transform.position;
    public bool IsSuperMagnetActive => Time.time < superMagnetEndTime;
    public float CurrentMoveSpeedMultiplier =>
        IsSuperMagnetActive ? superMagnetSpeedMultiplier : 1f;

    public event Action<float> AbsorptionRadiusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one PlayerItemMagnet can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public void SetRadiusPercent(int percent)
    {
        percent = Mathf.Max(0, percent);
        if (radiusPercent == percent)
        {
            return;
        }

        radiusPercent = percent;
        AbsorptionRadiusChanged?.Invoke(CurrentAbsorptionRadius);
    }

    public void ActivateSuperMagnet(float duration, float moveSpeedMultiplier)
    {
        duration = Mathf.Max(0f, duration);
        superMagnetSpeedMultiplier = Mathf.Max(1f, moveSpeedMultiplier);

        // Picking up another SuperMagnet replaces the remaining time with the
        // latest pickup's full duration.
        superMagnetEndTime = Time.time + duration;
    }

    public bool ShouldAttract(Vector3 collectiblePosition)
    {
        return IsSuperMagnetActive
            || Vector2.Distance(collectiblePosition, CollectionPosition)
                <= CurrentAbsorptionRadius;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        superMagnetEndTime = 0f;
        superMagnetSpeedMultiplier = 1f;
    }

    private void OnValidate()
    {
        baseAbsorptionRadius = Mathf.Max(0f, baseAbsorptionRadius);
        radiusPercent = Mathf.Max(0, radiusPercent);
    }
}
