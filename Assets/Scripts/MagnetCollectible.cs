using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for every drop that can be pulled toward the player. New drop
/// types inherit this class and implement TryCollect to join the same system.
/// </summary>
public abstract class MagnetCollectible : MonoBehaviour
{
    public const string CollectibleTag = "MagnetCollectible";

    private static readonly List<MagnetCollectible> ActiveCollectibles =
        new List<MagnetCollectible>();

    [Header("Magnet Movement")]
    [SerializeField, Min(0f)]
    private float magnetMoveSpeed = 10f;

    [SerializeField, Min(0f)]
    private float collectDistance = 0.1f;

    [Tooltip("SuperMagnet 발동 중 목표 속도까지 증가하는 가속도입니다.")]
    [SerializeField, Min(0f)]
    private float superMagnetAcceleration = 30f;

    private bool isCollected;
    private float currentMoveSpeed;

    protected virtual void Awake()
    {
        if (!CompareTag(CollectibleTag))
        {
            gameObject.tag = CollectibleTag;
        }

        currentMoveSpeed = magnetMoveSpeed;
    }

    protected virtual void OnEnable()
    {
        if (!ActiveCollectibles.Contains(this))
        {
            ActiveCollectibles.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        ActiveCollectibles.Remove(this);
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
        if (!magnet.ShouldAttract(transform.position))
        {
            currentMoveSpeed = magnetMoveSpeed;
            return;
        }

        float targetMoveSpeed = magnetMoveSpeed
            * magnet.CurrentMoveSpeedMultiplier;
        currentMoveSpeed = magnet.IsSuperMagnetActive
            ? Mathf.MoveTowards(
                currentMoveSpeed,
                targetMoveSpeed,
                superMagnetAcceleration * Time.deltaTime)
            : magnetMoveSpeed;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            currentMoveSpeed * Time.deltaTime);

        // Do not rely solely on trigger callbacks. Some projects disable
        // contacts between the collectible and player layers, and checking
        // only the two transform centers makes tall player colliders miss.
        TryCollectIfReached(magnet);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TryCollectFromPlayerCollider(other);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        TryCollectFromPlayerCollider(other);
    }

    private void TryCollectFromPlayerCollider(Collider2D other)
    {
        if (isCollected || LevelUpPanelController.IsPaused || other == null)
        {
            return;
        }

        PlayerItemMagnet magnet = other.GetComponentInParent<PlayerItemMagnet>();
        if (magnet == null || !magnet.isActiveAndEnabled)
        {
            return;
        }

        TryCollectOnce(magnet);
    }

    private void TryCollectOnce(PlayerItemMagnet magnet)
    {
        if (!isCollected)
        {
            isCollected = TryCollect(magnet);
        }
    }

    public void TryCollectIfReached(PlayerItemMagnet magnet)
    {
        if (isCollected || magnet == null || !isActiveAndEnabled
            || LevelUpPanelController.IsPaused)
        {
            return;
        }

        if (magnet.IsWithinCollectionDistance(
            transform.position,
            collectDistance))
        {
            TryCollectOnce(magnet);
        }
    }

    public static void CollectAllReachedItems(PlayerItemMagnet magnet)
    {
        for (int i = ActiveCollectibles.Count - 1; i >= 0; i--)
        {
            if (i >= ActiveCollectibles.Count)
            {
                continue;
            }

            MagnetCollectible collectible = ActiveCollectibles[i];
            if (collectible == null)
            {
                ActiveCollectibles.RemoveAt(i);
                continue;
            }

            collectible.TryCollectIfReached(magnet);
        }
    }

    protected abstract bool TryCollect(PlayerItemMagnet magnet);

    protected virtual void OnValidate()
    {
        magnetMoveSpeed = Mathf.Max(0f, magnetMoveSpeed);
        collectDistance = Mathf.Max(0f, collectDistance);
        superMagnetAcceleration = Mathf.Max(0f, superMagnetAcceleration);
    }
}
