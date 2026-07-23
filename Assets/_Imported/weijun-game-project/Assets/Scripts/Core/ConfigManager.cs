using UnityEngine;
using System.Collections.Generic;

public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance;
    [Header("所有区域配置（草原/森林/海洋/火山/雪地）")]
    public List<MapAreaConfig> allAreaConfig;
    [Header("地块配置")]
    public List<LandConfig> allLandConfig;
    [Header("建筑配置")]
    public List<BuildingConfig> allBuildConfig;
    [Header("资源配置")]
    public List<ResourceConfig> allResConfig;

    private Dictionary<string, MapAreaConfig> areaDict = new Dictionary<string, MapAreaConfig>();
    private Dictionary<string, LandConfig> landDict = new Dictionary<string, LandConfig>();
    private Dictionary<string, BuildingConfig> buildDict = new Dictionary<string, BuildingConfig>();
    private Dictionary<string, ResourceConfig> resDict = new Dictionary<string, ResourceConfig>();

    private void Awake()
    {
        // 单例处理
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // ========== 安全加载区域配置（防重复Key）==========
        foreach (var cfg in allAreaConfig)
        {
            if (string.IsNullOrEmpty(cfg.landType))
            {
                Debug.LogError($"MapAreaConfig {cfg.name} 的 landType 为空，请填写！");
                continue;
            }
            if (areaDict.ContainsKey(cfg.landType))
            {
                Debug.LogWarning($"【重复区域Key】landType:{cfg.landType} 重复，后加载的配置会覆盖前面！检查配置文件");
                areaDict[cfg.landType] = cfg;
            }
            else
            {
                areaDict.Add(cfg.landType, cfg);
            }
        }

        // ========== 安全加载地块配置 ==========
        foreach (var cfg in allLandConfig)
        {
            if (string.IsNullOrEmpty(cfg.id))
            {
                Debug.LogError($"LandConfig {cfg.name} 的 id 为空！");
                continue;
            }
            if (landDict.ContainsKey(cfg.id))
            {
                Debug.LogWarning($"【重复地块Key】id:{cfg.id} 重复");
                landDict[cfg.id] = cfg;
            }
            else
            {
                landDict.Add(cfg.id, cfg);
            }
        }

        // ========== 安全加载建筑配置 ==========
        foreach (var cfg in allBuildConfig)
        {
            if (string.IsNullOrEmpty(cfg.id))
            {
                Debug.LogError($"BuildingConfig {cfg.name} 的 id 为空！");
                continue;
            }
            if (buildDict.ContainsKey(cfg.id))
            {
                Debug.LogWarning($"【重复建筑Key】id:{cfg.id} 重复");
                buildDict[cfg.id] = cfg;
            }
            else
            {
                buildDict.Add(cfg.id, cfg);
            }
        }

        // ========== 安全加载资源配置 ==========
        foreach (var cfg in allResConfig)
        {
            if (string.IsNullOrEmpty(cfg.id))
            {
                Debug.LogError($"ResourceConfig {cfg.name} 的 id 为空！");
                continue;
            }
            if (resDict.ContainsKey(cfg.id))
            {
                Debug.LogWarning($"【重复资源Key】id:{cfg.id} 重复");
                resDict[cfg.id] = cfg;
            }
            else
            {
                resDict.Add(cfg.id, cfg);
            }
        }
    }

    public MapAreaConfig GetAreaByLandType(string landType)
    {
        areaDict.TryGetValue(landType, out var cfg);
        return cfg;
    }
    public LandConfig GetLand(string id)
    {
        landDict.TryGetValue(id, out var cfg);
        return cfg;
    }
    public BuildingConfig GetBuild(string id)
    {
        buildDict.TryGetValue(id, out var cfg);
        return cfg;
    }
    public List<MapAreaConfig> GetAllAreaList() => allAreaConfig;
}