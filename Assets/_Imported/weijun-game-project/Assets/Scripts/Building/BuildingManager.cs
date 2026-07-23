using UnityEngine;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;
    public GameObject buildPrefab;
    private long autoId = 10000;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
    }

    // 获取建筑占用全部格子
    public List<Vector2Int> GetOccupyGrids(int baseX, int baseY, int w, int h)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                list.Add(new Vector2Int(baseX + x, baseY + y));
        return list;
    }

    // 校验是否可建造
    public bool CheckCanBuild(int baseX, int baseY, BuildingConfig cfg)
    {
        MapData map = CacheManager.Instance.currentMapData;
        List<Vector2Int> grids = GetOccupyGrids(baseX, baseY, cfg.width, cfg.height);
        foreach (var g in grids)
        {
            LandTile tile = MapManager.Instance.GetTile(g.x, g.y);
            if (tile == null || tile.landType != cfg.limitLandType) return false;
            // 检测格子是否被其他建筑占用
            foreach (var buildPair in map.buildings)
            {
                BuildingData bd = buildPair.Value;
                var occupy = GetOccupyGrids(bd.posX, bd.posY, bd.occupyW, bd.occupyH);
                if (occupy.Contains(g)) return false;
            }
        }
        if (!ResourceManager.Instance.CheckCost(cfg.buildCost)) return false;
        return true;
    }

    // 本地创建建筑
    public void CreateBuildLocal(int x, int y, BuildingConfig cfg)
    {
        BuildingData newBuild = new BuildingData();
        newBuild.buildingId = autoId++;
        newBuild.buildingType = cfg.id;
        newBuild.posX = x;
        newBuild.posY = y;
        newBuild.occupyW = cfg.width;
        newBuild.occupyH = cfg.height;
        newBuild.produceEndTime = (long)(Time.time * 1000) + cfg.produceSecond * 1000;
        CacheManager.Instance.currentMapData.buildings.Add(newBuild.buildingId, newBuild);
        ResourceManager.Instance.CostRes(cfg.buildCost);
        RenderSingleBuild(newBuild.buildingId);
        CacheManager.Instance.SaveMap();
        TipTool.ShowTip("建造完成", Color.green);
    }

    // 渲染单个建筑
    public void RenderSingleBuild(long buildId)
    {
        MapData map = CacheManager.Instance.currentMapData;
        BuildingData bd = map.buildings[buildId];
        BuildingConfig cfg = ConfigManager.Instance.GetBuild(bd.buildingType);
        Vector2 worldPos = MapTool.GridToWorld(bd.posX, bd.posY);
        BuildingRenderer render;
        if (MapManager.Instance.buildObjDic.ContainsKey(buildId))
        {
            render = MapManager.Instance.buildObjDic[buildId];
        }
        else
        {
            GameObject obj = Instantiate(buildPrefab, MapManager.Instance.buildRoot);
            obj.transform.position = worldPos;
            render = obj.GetComponent<BuildingRenderer>();
            MapManager.Instance.buildObjDic.Add(buildId, render);
        }
        render.RefreshView(bd, cfg);
    }
}