using UnityEngine;
using System.Collections.Generic;

public class MapAreaGenerator : MonoBehaviour
{
    public static MapAreaGenerator Instance;
    private CacheManager cacheMgr;
    private MapData mapData;
    private ResourceManager resMgr;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
    }

    private void Start()
    {
        cacheMgr = CacheManager.Instance;
        resMgr = ResourceManager.Instance;
        mapData = cacheMgr.currentMapData;
        GenerateIrregularMap();
        MapManager.Instance.RefreshAllMap();
    }

    // 读取配置生成不规则所有格子
    public void GenerateIrregularMap()
    {
        List<MapAreaConfig> allAreas = ConfigManager.Instance.GetAllAreaList();
        foreach (var areaCfg in allAreas)
        {
            foreach (var grid in areaCfg.areaAllGrids)
            {
                int x = grid.x;
                int y = grid.y;
                string key = MapTool.GetGridKey(x, y);
                LandTile tile;
                if (!mapData.lands.ContainsKey(key))
                {
                    tile = new LandTile();
                    tile.x = x;
                    tile.y = y;
                    tile.landType = "empty";
                    tile.isLocked = true;
                    mapData.lands.Add(key, tile);
                }
                else
                {
                    tile = mapData.lands[key];
                }
                // 草原(0,0)永久解锁
                if (areaCfg.landType == "grass" && x == 0 && y == 0)
                    tile.isLocked = false;
            }
        }
        cacheMgr.SaveMap();
    }

    // 判断是否满足解锁条件
    public bool CheckUnlockCondition(MapAreaConfig areaCfg)
    {
        switch (areaCfg.unlockType)
        {
            case UnlockConditionType.Gold:
                return resMgr.resData["gold"] >= areaCfg.unlockTargetValue;
            case UnlockConditionType.PlayerLevel:
                return resMgr.playerLv >= areaCfg.unlockTargetValue;
            default: return false;
        }
    }

    // 获取解锁提示文本
    public string GetUnlockTip(MapAreaConfig areaCfg)
    {
        if (areaCfg.unlockType == UnlockConditionType.Gold)
            return $"解锁{areaCfg.name}需要{areaCfg.unlockTargetValue}金币";
        else
            return $"解锁{areaCfg.name}需要Lv.{areaCfg.unlockTargetValue}";
    }

    // 解锁整片区域所有格子
    public void UnlockWholeArea(MapAreaConfig areaCfg)
    {
        if (!CheckUnlockCondition(areaCfg))
        {
            TipTool.ShowTip(GetUnlockTip(areaCfg), Color.red);
            return;
        }
        // 金币解锁扣钱
        if (areaCfg.unlockType == UnlockConditionType.Gold)
        {
            Dictionary<string, int> cost = new Dictionary<string, int>() { { "gold", (int)areaCfg.unlockTargetValue } };
            if (!resMgr.CostRes(cost)) return;
        }
        // 批量解锁所有不规则格子
        foreach (var grid in areaCfg.areaAllGrids)
        {
            string key = MapTool.GetGridKey(grid.x, grid.y);
            if (mapData.lands.ContainsKey(key))
                mapData.lands[key].isLocked = false;
        }
        cacheMgr.SaveMap();
        MapManager.Instance.RefreshAllMap();
        TipTool.ShowTip($"{areaCfg.name}解锁成功", Color.green);
    }
}