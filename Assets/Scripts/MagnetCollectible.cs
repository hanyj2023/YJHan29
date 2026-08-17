using UnityEngine;

/// <summary>
/// Base class for every drop that can be pulled toward the player. New drop
/// types inherit this class and implement TryCollect to join the same system.
/// </summary>
public abstract class MagnetCollectible : MonoBehaviour
{
    public const string CollectibleTag = "MagnetCollectible";

    [Header("Magnet Movement")]
    [SerializeField, Min(0f)]
    private float magnetMoveSpeed = 10f;

    [SerializeField, Min(0f)]
    private float collectDistance = 0.1f;

    private bool isCollected;

    protected virtual void Awake()
    {
        if (!CompareTag(CollectibleTag))
        {
            gameObject.tag = CollectibleTag;
        }
    }

    protected virtual void Update()
    {
        if (isCollected || LevelUpPanelController.IsPaused)
        {
            return;
        }

        PlayerItemMagnet magnet = PlayerItemMagnet.Instance;
        if (magnet == null || !magnet.isActiveAndEnabled)
        {
            return;
        }

        Vector3 targetPosition = magnet.CollectionPosition;
        if (Vector2.Distance(transform.position, targetPosition)
            > magnet.CurrentAbsorptionRadius)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            magnetMoveSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) <= collectDistance)
        {
            isCollected = TryCollect(magnet);
        }
    }

    protected abstract bool TryCollect(PlayerItemMagnet magnet);

    protected virtual void OnValidate()
    {
        magnetMoveSpeed = Mathf.Max(0f, magnetMoveSpeed);
        collectDistance = Mathf.Max(0f, collectDistance);
    }
}
