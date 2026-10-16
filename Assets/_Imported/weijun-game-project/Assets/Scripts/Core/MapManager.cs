using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    public Transform tileRoot;
    public Transform buildRoot;
    public GameObject tilePrefab;
    public GameObject buildPrefab;

    public Dictionary<string, LandRenderer> tileObjDic = new Dictionary<string, LandRenderer>();
    public Dictionary<long, BuildingRenderer> buildObjDic = new Dictionary<long, BuildingRenderer>();

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        tileRoot = transform.Find("TileRoot");
        buildRoot = transform.Find("BuildRoot");
        if (tileRoot == null) tileRoot = new GameObject("TileRoot").transform;
        if (buildRoot == null) buildRoot = new GameObject("BuildRoot").transform;
        tileRoot.SetParent(transform);
        buildRoot.SetParent(transform);
    }

    // 完整刷新整张地图
    public void RefreshAllMap()
    {
        RenderAllLand();
        RenderAllBuilding();
    }

    // 根据坐标获取地块
    public LandTile GetTile(int x, int y)
    {
        string key = MapTool.GetGridKey(x, y);
        CacheManager cache = CacheManager.Instance;
        if (cache.currentMapData.lands.ContainsKey(key))
            return cache.currentMapData.lands[key];
        return null;
    }

    // 渲染所有地块
    public void RenderAllLand()
    {
        foreach (var t in tileObjDic.Values) Destroy(t.gameObject);
        tileObjDic.Clear();
        foreach (var pair in CacheManager.Instance.currentMapData.lands)
        {
            string[] xy = pair.Key.Split('_');
            int x = int.Parse(xy[0]);
            int y = int.Parse(xy[1]);
            LandManager.Instance.RenderSingleTile(x, y);
        }
    }

    // 渲染所有建筑
    public void RenderAllBuilding()
    {
        foreach (var b in buildObjDic.Values) Destroy(b.gameObject);
        buildObjDic.Clear();
        foreach (var pair in CacheManager.Instance.currentMapData.buildings)
        {
            BuildingManager.Instance.RenderSingleBuild(pair.Key);
        }
    }
}