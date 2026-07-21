using UnityEngine;

public class BuildPanel : MonoBehaviour
{
    public BuildingConfig selectCfg;
    public GameObject previewBuildPrefab;
    private GameObject previewObj;
    private bool buildMode = false;

    void Update()
    {
        if (!buildMode) return;
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int grid = MapTool.WorldToGrid(mouseWorld);
        bool canPlace = BuildingManager.Instance.CheckCanBuild(grid.x, grid.y, selectCfg);
        Vector2 worldPos = MapTool.GridToWorld(grid.x, grid.y);
        previewObj.transform.position = worldPos;
        BuildingRenderer render = previewObj.GetComponent<BuildingRenderer>();
        render.spr.color = canPlace ? new Color(1, 1, 1, 0.6f) : new Color(1, 0.3f, 0.3f, 0.6f);

        if (Input.GetMouseButtonDown(0))
        {
            if (canPlace) BuildingManager.Instance.CreateBuildLocal(grid.x, grid.y, selectCfg);
            else TipTool.ShowTip("不能在此建造", Color.red);
        }
        if (Input.GetMouseButtonDown(1)) ExitBuildMode();
    }

    public void EnterBuildMode(BuildingConfig cfg)
    {
        UIPanelManager.Instance.CloseAllBuildPanels();
        selectCfg = cfg;
        buildMode = true;
        previewObj = Instantiate(previewBuildPrefab, MapManager.Instance.buildRoot);
    }

    public void ExitBuildMode()
    {
        buildMode = false;
        if (previewObj != null) Destroy(previewObj);
    }
}