using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public sealed class InfiniteTilemapRepeater : MonoBehaviour
{
    private const int GridDiameter = 3;
    private const int RuntimeCopyCount = GridDiameter * GridDiameter - 1;

    [SerializeField]
    [Tooltip("반복 타일맵의 중심을 따라갈 대상입니다. 비어 있으면 PlayerMovement를 자동으로 찾습니다.")]
    private Transform target;

    private readonly List<Transform> repeatedTilemaps = new(GridDiameter * GridDiameter);

    private Tilemap sourceTilemap;
    private TilemapRenderer sourceRenderer;
    private Transform tilemapParent;
    private Matrix4x4 templateLocalToParent;
    private Matrix4x4 templateParentToLocal;
    private Vector2 repeatSize;
    private Vector2 patternCenter;
    private Vector2Int currentCenterChunk = new(int.MinValue, int.MinValue);

    private void Awake()
    {
        sourceTilemap = GetComponent<Tilemap>();
        sourceRenderer = GetComponent<TilemapRenderer>();
        tilemapParent = transform.parent;

        if (target == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            target = player != null ? player.transform : Camera.main?.transform;
        }

        Bounds visualBounds = sourceRenderer.localBounds;
        repeatSize = new Vector2(visualBounds.size.x, visualBounds.size.y);
        patternCenter = new Vector2(visualBounds.center.x, visualBounds.center.y);

        if (target == null || repeatSize.x <= Mathf.Epsilon || repeatSize.y <= Mathf.Epsilon)
        {
            Debug.LogError(
                $"{nameof(InfiniteTilemapRepeater)} requires a target and a non-empty Tilemap.",
                this);
            enabled = false;
            return;
        }

        templateLocalToParent = Matrix4x4.TRS(
            transform.localPosition,
            transform.localRotation,
            transform.localScale);
        templateParentToLocal = templateLocalToParent.inverse;

        repeatedTilemaps.Add(transform);
        for (int i = 0; i < RuntimeCopyCount; i++)
        {
            repeatedTilemaps.Add(CreateRuntimeCopy(i + 1));
        }

        RefreshLayout(force: true);
    }

    private void LateUpdate()
    {
        RefreshLayout(force: false);
    }

    private Transform CreateRuntimeCopy(int copyNumber)
    {
        GameObject copyObject = new($"{gameObject.name}_RuntimeCopy_{copyNumber}");
        copyObject.layer = gameObject.layer;

        Transform copyTransform = copyObject.transform;
        copyTransform.SetParent(tilemapParent, worldPositionStays: false);
        copyTransform.SetLocalPositionAndRotation(transform.localPosition, transform.localRotation);
        copyTransform.localScale = transform.localScale;

        Tilemap copyTilemap = copyObject.AddComponent<Tilemap>();
        CopyTilemap(sourceTilemap, copyTilemap);

        TilemapRenderer copyRenderer = copyObject.AddComponent<TilemapRenderer>();
        CopyRenderer(sourceRenderer, copyRenderer);

        return copyTransform;
    }

    private void RefreshLayout(bool force)
    {
        Vector3 targetInParent = tilemapParent != null
            ? tilemapParent.InverseTransformPoint(target.position)
            : target.position;
        Vector3 targetInTemplate = templateParentToLocal.MultiplyPoint3x4(targetInParent);

        Vector2Int centerChunk = new(
            GetChunkIndex(targetInTemplate.x, patternCenter.x, repeatSize.x),
            GetChunkIndex(targetInTemplate.y, patternCenter.y, repeatSize.y));

        if (!force && centerChunk == currentCenterChunk)
        {
            return;
        }

        currentCenterChunk = centerChunk;
        int mapIndex = 0;

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3 repeatedOrigin = new(
                    (centerChunk.x + x) * repeatSize.x,
                    (centerChunk.y + y) * repeatSize.y,
                    0f);

                repeatedTilemaps[mapIndex].localPosition =
                    templateLocalToParent.MultiplyPoint3x4(repeatedOrigin);
                mapIndex++;
            }
        }
    }

    private static int GetChunkIndex(float targetPosition, float patternCenterPosition, float size)
    {
        return Mathf.FloorToInt((targetPosition - patternCenterPosition) / size + 0.5f);
    }

    private static void CopyTilemap(Tilemap source, Tilemap destination)
    {
        destination.color = source.color;
        destination.tileAnchor = source.tileAnchor;
        destination.orientation = source.orientation;
        destination.orientationMatrix = source.orientationMatrix;
        destination.animationFrameRate = source.animationFrameRate;

        BoundsInt bounds = source.cellBounds;
        destination.SetTilesBlock(bounds, source.GetTilesBlock(bounds));

        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            if (!source.HasTile(position))
            {
                continue;
            }

            TileFlags flags = source.GetTileFlags(position);
            destination.SetTileFlags(position, TileFlags.None);
            destination.SetColor(position, source.GetColor(position));
            destination.SetTransformMatrix(position, source.GetTransformMatrix(position));
            destination.SetTileFlags(position, flags);
        }
    }

    private static void CopyRenderer(TilemapRenderer source, TilemapRenderer destination)
    {
        destination.enabled = source.enabled;
        destination.sharedMaterials = source.sharedMaterials;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.mode = source.mode;
        destination.sortOrder = source.sortOrder;
        destination.maskInteraction = source.maskInteraction;
        destination.renderingLayerMask = source.renderingLayerMask;
    }
}
