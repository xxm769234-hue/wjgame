using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//功能：全局 MapData 唯一持有者，本地 JSON 存档读写，联网同步服务端地图数据。
[System.Serializable]
public class MapData
{
    public Dictionary<string, LandTile> lands = new Dictionary<string, LandTile>();
    public Dictionary<long, BuildingData> buildings = new Dictionary<long, BuildingData>();
    public int mapMaxWidth = 15;
    public int mapMaxHeight = 15;
}

public class CacheManager : MonoBehaviour
{
    public static CacheManager Instance;
    public MapData currentMapData;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        LoadMapSave();
    }

    public void LoadMapSave()
    {
        string json = PlayerPrefs.GetString("MapSave");
        if (!string.IsNullOrEmpty(json))
        {
            currentMapData = JsonUtility.FromJson<MapData>(json);
        }
        else
        {
            currentMapData = new MapData();
            currentMapData.lands = new Dictionary<string, LandTile>();
            currentMapData.buildings = new Dictionary<long, BuildingData>();
        }
    }

    public void SaveMap()
    {
        string json = JsonUtility.ToJson(currentMapData);
        PlayerPrefs.SetString("MapSave", json);
        PlayerPrefs.Save();
    }
}