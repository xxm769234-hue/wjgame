using UnityEngine;
using System.Collections.Generic;

public class LandManager : MonoBehaviour
{
    public static LandManager Instance;
    public GameObject tilePrefab;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
    }

    // 校验是否能铺设地块
    public bool CheckCanPlaceTile(int x, int y, LandConfig cfg)
    {
        MapData map = CacheManager.Instance.currentMapData;
        LandTile tile = MapManager.Instance.GetTile(x, y);
        if (tile == null) return false;
        if (tile.isLocked || tile.landType != "empty") return false;
        if (!ResourceManager.Instance.CheckCost(cfg.buildCost)) return false;
        return true;
    }

    // 本地铺设地块
    public void CreateTileLocal(int x, int y, LandConfig cfg)
    {
        string key = MapTool.GetGridKey(x, y);
        LandTile tile = CacheManager.Instance.currentMapData.lands[key];
        tile.landType = cfg.id;
        ResourceManager.Instance.CostRes(cfg.buildCost);
        RenderSingleTile(x, y);
        CacheManager.Instance.SaveMap();
        TipTool.ShowTip("地块铺设成功", Color.green);
    }

    // 渲染单个地块
    // 渲染单个地块
    public void RenderSingleTile(int x, int y)
    {
        string key = MapTool.GetGridKey(x, y);
        LandTile tile = MapManager.Instance.GetTile(x, y);

        // 1、地块数据为空直接退出
        if (tile == null)
        {
            Debug.LogError($"坐标{x},{y} 无地块数据，跳过渲染");
            return;
        }

        Vector2 worldPos = MapTool.GridToWorld(x, y);
        LandRenderer render;

        if (MapManager.Instance.tileObjDic.ContainsKey(key))
        {
            render = MapManager.Instance.tileObjDic[key];
        }
        else
        {
            // 校验父物体是否赋值
            if (MapManager.Instance.tileRoot == null)
            {
                Debug.LogError("MapManager 的 TileRoot 未拖拽赋值！");
                return;
            }
            if (tilePrefab == null)
            {
                Debug.LogError("LandManager 的 tilePrefab 预制体未赋值！");
                return;
            }

            GameObject tileObj = Instantiate(tilePrefab, MapManager.Instance.tileRoot);
            tileObj.transform.position = worldPos;
            render = tileObj.GetComponent<LandRenderer>();
            MapManager.Instance.tileObjDic.Add(key, render);

            // 获取点击组件
            TileClick click = tileObj.GetComponent<TileClick>();

            // 匹配区域key
            string tempLandType = tile.landType == "empty" ? "grass" : tile.landType;
            MapAreaConfig areaCfg = ConfigManager.Instance.GetAreaByLandType(tempLandType);

            // 兜底：查询不到强制使用草原
            if (areaCfg == null)
            {
                areaCfg = ConfigManager.Instance.GetAreaByLandType("grass");
                if (areaCfg == null)
                {
                    Debug.LogError("草原基础配置缺失，无法获取区域！");
                    return;
                }
            }

            // 双重判空再执行57行代码
            if (click != null && areaCfg != null)
            {
                click.SetData(tile, areaCfg);
            }
            else
            {
                if (click == null)
                    Debug.LogError($"坐标{x},{y} Tile预制体缺少 TileClick 脚本组件");
                if (areaCfg == null)
                    Debug.LogError($"坐标{x},{y} 区域配置 {tempLandType} 读取失败");
            }
        }

        LandConfig landCfg = ConfigManager.Instance.GetLand(tile.landType);
        if (landCfg == null)
        {
            landCfg = ConfigManager.Instance.GetLand("grass");
        }
        render.RefreshTileView(tile, landCfg);
    }
}