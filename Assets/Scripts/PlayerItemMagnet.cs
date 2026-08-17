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

    public float BaseAbsorptionRadius => baseAbsorptionRadius;
    public int RadiusPercent => radiusPercent;
    public float CurrentAbsorptionRadius =>
        baseAbsorptionRadius * radiusPercent / 100f;
    public Vector3 CollectionPosition => transform.position;

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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        baseAbsorptionRadius = Mathf.Max(0f, baseAbsorptionRadius);
        radiusPercent = Mathf.Max(0, radiusPercent);
    }
}
