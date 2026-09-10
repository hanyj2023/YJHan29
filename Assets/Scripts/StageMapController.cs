using UnityEngine;
using UnityEngine.Tilemaps;

// Choose the map before any repeater creates its eight runtime copies.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class StageMapController : MonoBehaviour
{
    [SerializeField] private StageMonsterSpawner spawner;
    [SerializeField] private Transform mapRoot;

    private void Awake()
    {
        int stageId = StageTable.SelectedStageId > 0 ? StageTable.SelectedStageId : spawner.CurrentStageId;
        StageData stage = StageTable.Load().Find(entry => entry.Id == stageId);
        Tilemap selected = null;
        Tilemap[] maps = mapRoot.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap map in maps)
        {
            bool matches = stage != null && map.name == stage.TilemapName;
            map.gameObject.SetActive(matches);
            if (matches)
                selected = map;
        }
        if (selected == null)
        {
            Debug.LogError($"StageId {stageId} has no matching Tilemap in GameScene.", this);
            spawner.enabled = false;
            return;
        }
        spawner.SetStage(stage.Id);
        InfiniteTilemapRepeater repeater = selected.GetComponent<InfiniteTilemapRepeater>();
        if (repeater == null)
            selected.gameObject.AddComponent<InfiniteTilemapRepeater>();
        else
            repeater.enabled = true;
    }
}
