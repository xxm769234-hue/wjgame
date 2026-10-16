using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneSwitchMgr : MonoBehaviour
{
    private const string SimulationSceneName = "Simulation management mode";
    [Header("五大区域跳转按钮")]
    public Button GrassBtn;
    public Button ForestBtn;
    public Button SeaBtn;
    public Button VolcanoBtn;
    public Button SnowBtn;

    [Header("对应区域配置")]
    public MapAreaConfig grassArea;
    public MapAreaConfig forestArea;
    public MapAreaConfig seaArea;
    public MapAreaConfig volcanoArea;

    public void Awake()
    {
        BindButton(GrassBtn, grassArea);
        BindButton(ForestBtn, forestArea);
        BindButton(SeaBtn, seaArea);
        BindButton(VolcanoBtn, volcanoArea);
        BindButton(SnowBtn, "snow");
    }

    // 点击首页草原按钮
    public void ClickGrassMap()
    {
        Debug.Log("点击草原");
        EnterMapScene(grassArea);
    }
    public void ClickForestMap()
    {
        EnterMapScene(forestArea);
    }
    public void ClickSeaMap()
    {
        EnterMapScene(seaArea);
    }
    public void ClickVolcanoMap()
    {
        EnterMapScene(volcanoArea);
    }
    public void ClickSnowMap()
    {
        EnterMapScene("snow");
    }

    /// <summary>
    /// 统一跳转逻辑：保存当前进入的区域类型，加载经营地图场景
    /// </summary>
    private void EnterMapScene(MapAreaConfig targetArea)
    {
        if (targetArea == null)
        {
            Debug.LogWarning("地图按钮缺少 MapAreaConfig，已取消场景跳转。", this);
            return;
        }

        EnterMapScene(targetArea.landType);
    }

    private void EnterMapScene(string landType)
    {
        if (string.IsNullOrWhiteSpace(landType))
        {
            Debug.LogWarning("地图按钮缺少地块类型，已取消场景跳转。", this);
            return;
        }

        PlayerPrefs.SetString("OpenAreaLandType", landType);
        PlayerPrefs.SetString("CurrentAreaLandType", landType);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SimulationSceneName);
    }

    private void BindButton(Button button, MapAreaConfig area)
    {
        if (button != null)
        {
            button.onClick.AddListener(() => EnterMapScene(area));
        }
    }

    private void BindButton(Button button, string landType)
    {
        if (button != null)
        {
            button.onClick.AddListener(() => EnterMapScene(landType));
        }
    }

}
