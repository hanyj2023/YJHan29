using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TreasureBoxManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField]
    private TreasureBoxController treasureBoxPrefab;

    [SerializeField, Min(0.01f)]
    private float spawnCooldown = 30f;

    [SerializeField, Min(0f)]
    private float firstSpawnDelay = 10f;

    [SerializeField, Min(1)]
    private int maxActiveCount = 3;

    [Header("Scene References")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private Camera targetCamera;

    [Header("Distance Outside Camera")]
    [Tooltip("카메라 시야 경계에서 바깥쪽으로 더 떨어질 최소 거리입니다.")]
    [SerializeField, Min(0f)]
    private float minimumSpawnDistance = 1f;

    [Tooltip("카메라 시야 경계에서 바깥쪽으로 더 떨어질 최대 거리입니다.")]
    [SerializeField, Min(0f)]
    private float maximumSpawnDistance = 4f;

    [Tooltip("카메라 시야 밖 후보를 찾기 위해 한 주기마다 시도할 횟수입니다.")]
    [SerializeField, Min(1)]
    private int positionAttempts = 16;

    private readonly HashSet<TreasureBoxController> activeBoxes =
        new HashSet<TreasureBoxController>();
    private float nextSpawnTime;

    public int ActiveCount => activeBoxes.Count;

    private void Start()
    {
        if (!ResolveReferences())
        {
            enabled = false;
            return;
        }

        TreasureBoxController[] existingBoxes =
            FindObjectsByType<TreasureBoxController>(FindObjectsSortMode.None);
        foreach (TreasureBoxController box in existingBoxes)
        {
            activeBoxes.Add(box);
            box.Initialize(OnTreasureBoxDestroyed);
        }

        nextSpawnTime = Time.time + firstSpawnDelay;
    }

    private void Update()
    {
        if (LevelUpPanelController.IsPaused || Time.time < nextSpawnTime)
        {
            return;
        }

        nextSpawnTime = Time.time + spawnCooldown;
        activeBoxes.RemoveWhere(box => box == null);
        if (activeBoxes.Count >= maxActiveCount)
        {
            return;
        }

        TrySpawn();
    }

    private void TrySpawn()
    {
        float outsideViewRadius = CalculateRadiusOutsideCameraView(
            player.position);

        for (int attempt = 0; attempt < positionAttempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float extraDistance = Random.Range(
                minimumSpawnDistance,
                maximumSpawnDistance);
            float distance = outsideViewRadius + extraDistance;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 candidate = player.position + (Vector3)(direction * distance);
            candidate.z = player.position.z;

            if (IsInsideCameraView(candidate))
            {
                continue;
            }

            TreasureBoxController box = Instantiate(
                treasureBoxPrefab,
                candidate,
                Quaternion.identity);
            box.gameObject.SetActive(true);
            activeBoxes.Add(box);
            box.Initialize(OnTreasureBoxDestroyed);
            return;
        }
    }

    private float CalculateRadiusOutsideCameraView(Vector3 center)
    {
        float depth = Mathf.Abs(center.z - targetCamera.transform.position.z);
        float maxSqrDistance = 0f;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Vector3 corner = targetCamera.ViewportToWorldPoint(
                    new Vector3(x, y, depth));
                maxSqrDistance = Mathf.Max(
                    maxSqrDistance,
                    ((Vector2)(corner - center)).sqrMagnitude);
            }
        }

        return Mathf.Sqrt(maxSqrDistance);
    }

    private bool IsInsideCameraView(Vector3 worldPosition)
    {
        Vector3 viewport = targetCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f
            && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void OnTreasureBoxDestroyed(TreasureBoxController box)
    {
        activeBoxes.Remove(box);
    }

    private bool ResolveReferences()
    {
        if (treasureBoxPrefab == null)
        {
            GameObject loaded = Resources.Load<GameObject>("Prefabs/TreasureBox");
            treasureBoxPrefab = loaded != null
                ? loaded.GetComponent<TreasureBoxController>()
                : null;
        }

        if (player == null)
        {
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            player = movement != null ? movement.transform : null;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (treasureBoxPrefab != null && player != null && targetCamera != null)
        {
            return true;
        }

        Debug.LogError(
            "TreasureBoxManager requires a TreasureBox prefab, Player, and Camera.",
            this);
        return false;
    }

    private void OnValidate()
    {
        spawnCooldown = Mathf.Max(0.01f, spawnCooldown);
        firstSpawnDelay = Mathf.Max(0f, firstSpawnDelay);
        maxActiveCount = Mathf.Max(1, maxActiveCount);
        minimumSpawnDistance = Mathf.Max(0f, minimumSpawnDistance);
        maximumSpawnDistance = Mathf.Max(
            minimumSpawnDistance,
            maximumSpawnDistance);
        positionAttempts = Mathf.Max(1, positionAttempts);
    }
}
