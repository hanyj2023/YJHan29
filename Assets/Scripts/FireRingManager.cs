using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats))]
public sealed class FireRingManager : MonoBehaviour
{
    [SerializeField]
    private FireringController fireringPrefab;

    [SerializeField, Min(0)]
    private int fireballCount;

    private readonly List<FireringController> activeFireballs =
        new List<FireringController>();

    private PlayerAttackStats attackStats;

    public int FireballCount => fireballCount;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        if (fireringPrefab == null)
        {
            fireringPrefab = Resources.Load<FireringController>("Prefabs/Firering");
        }

        if (fireringPrefab == null)
        {
            // Keep compatibility with the existing project asset name.
            fireringPrefab = Resources.Load<FireringController>("Prefabs/FireRing");
        }

        if (fireringPrefab == null)
        {
            Debug.LogError("FireRingManager requires the Firering prefab.", this);
            enabled = false;
            return;
        }

        RebuildFireballs();
    }

    /// <summary>Replaces the current count instead of adding to it.</summary>
    public void SetFireballCount(int count)
    {
        count = Mathf.Max(0, count);
        if (fireballCount == count)
        {
            return;
        }

        fireballCount = count;
        RebuildFireballs();
    }

    private void RebuildFireballs()
    {
        foreach (FireringController fireball in activeFireballs)
        {
            if (fireball != null)
            {
                fireball.gameObject.SetActive(false);
                Destroy(fireball.gameObject);
            }
        }

        activeFireballs.Clear();
        if (!Application.isPlaying || fireringPrefab == null)
        {
            return;
        }

        for (int index = 0; index < fireballCount; index++)
        {
            FireringController fireball = Instantiate(fireringPrefab, transform);
            fireball.Initialize(transform, attackStats, index, fireballCount);
            activeFireballs.Add(fireball);
        }
    }

    private void OnValidate()
    {
        fireballCount = Mathf.Max(0, fireballCount);
    }
}
