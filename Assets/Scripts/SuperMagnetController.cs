using UnityEngine;

[DisallowMultipleComponent]
public sealed class SuperMagnetController : MagnetCollectible
{
    [Header("Super Magnet")]
    [Tooltip("모든 MagnetCollectible을 끌어당기는 지속 시간(초)입니다.")]
    [SerializeField, Min(0f)]
    private float effectDuration = 8f;

    [Tooltip("효과 중 각 흡수 대상의 기존 이동 속도에 적용할 배율입니다.")]
    [SerializeField, Min(1f)]
    private float moveSpeedMultiplier = 3f;

    public float EffectDuration => effectDuration;

    protected override bool TryCollect(PlayerItemMagnet magnet)
    {
        magnet.ActivateSuperMagnet(effectDuration, moveSpeedMultiplier);
        Destroy(gameObject);
        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        effectDuration = Mathf.Max(0f, effectDuration);
        moveSpeedMultiplier = Mathf.Max(1f, moveSpeedMultiplier);
    }
}
