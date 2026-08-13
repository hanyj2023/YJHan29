using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAttackStats : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField, Min(0f)]
    private float baseAttackPower = 10f;

    [SerializeField, Min(0)]
    private int attackPercent = 100;

    public float BaseAttackPower => baseAttackPower;
    public int AttackPercent => attackPercent;
    public float CurrentAttackPower => baseAttackPower * attackPercent / 100f;

    public event Action<float> AttackPowerChanged;

    public void SetAttackPercent(int percent)
    {
        percent = Mathf.Max(0, percent);
        if (attackPercent == percent)
        {
            return;
        }

        attackPercent = percent;
        AttackPowerChanged?.Invoke(CurrentAttackPower);
    }

    private void OnValidate()
    {
        baseAttackPower = Mathf.Max(0f, baseAttackPower);
        attackPercent = Mathf.Max(0, attackPercent);
    }
}
