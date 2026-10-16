using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//功能：全局资源数值管理，建造 / 升级 / 收取资源统一入口，自动刷新 UI。
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;
    public Dictionary<string, long> resData = new Dictionary<string, long>();
    public int playerLv = 1;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        // 初始化金币
        if (!resData.ContainsKey("gold")) resData["gold"] = 1000;
        // 读取存档等级
        if (PlayerPrefs.HasKey("PlayerLv"))
            playerLv = PlayerPrefs.GetInt("PlayerLv");
    }

    public void SaveLevel()
    {
        PlayerPrefs.SetInt("PlayerLv", playerLv);
        PlayerPrefs.Save();
    }

    public void AddLevel(int add = 1)
    {
        playerLv += add;
        SaveLevel();
        UIPanelManager.Instance.RefreshTopBar();
        TipTool.ShowTip($"升级！当前Lv.{playerLv}", Color.yellow);
    }

    // 检查资源是否足够
    public bool CheckCost(Dictionary<string, int> cost)
    {
        foreach (var pair in cost)
        {
            if (!resData.ContainsKey(pair.Key) || resData[pair.Key] < pair.Value)
                return false;
        }
        return true;
    }

    // 扣除资源
    public bool CostRes(Dictionary<string, int> cost)
    {
        if (!CheckCost(cost)) return false;
        foreach (var p in cost) resData[p.Key] -= p.Value;
        UIPanelManager.Instance.RefreshTopBar();
        return true;
    }

    // 增加资源
    public void AddRes(Dictionary<string, int> add)
    {
        foreach (var p in add)
        {
            if (!resData.ContainsKey(p.Key)) resData[p.Key] = 0;
            resData[p.Key] += p.Value;
        }
        UIPanelManager.Instance.RefreshTopBar();
    }
}