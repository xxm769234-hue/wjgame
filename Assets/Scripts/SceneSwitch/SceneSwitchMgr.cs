using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneSwitchMgr : MonoBehaviour
{
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
    public MapAreaConfig snowArea;

    public void Awake()
    {
        // 代码自动绑定所有按钮点击事件，不用手动拖拽OnClick
        GrassBtn.onClick.AddListener(() => EnterMapScene(grassArea));
        ForestBtn.onClick.AddListener(() => EnterMapScene(forestArea));
        SeaBtn.onClick.AddListener(() => EnterMapScene(seaArea));
        VolcanoBtn.onClick.AddListener(() => EnterMapScene(volcanoArea));
        SnowBtn.onClick.AddListener(() => EnterMapScene(snowArea));
    }

    private void EnterMapScene(object grassAreaCfg)
    {
        throw new NotImplementedException();
    }

    // 点击首页草原按钮
    public void ClickGrassMap()
    {
        Debug.Log("点击草原");
        PlayerPrefs.SetString("CurrentAreaLandType", grassArea.landType);
        SceneManager.LoadScene("MapScene");
    }
    public void ClickForestMap()
    {
        PlayerPrefs.SetString("CurrentAreaLandType", forestArea.landType);
        SceneManager.LoadScene("MapScene");
    }
    public void ClickSeaMap()
    {
        PlayerPrefs.SetString("CurrentAreaLandType", seaArea.landType);
        SceneManager.LoadScene("MapScene");
    }
    public void ClickVolcanoMap()
    {
        PlayerPrefs.SetString("CurrentAreaLandType", volcanoArea.landType);
        SceneManager.LoadScene("MapScene");
    }
    public void ClickSnowMap()
    {
        PlayerPrefs.SetString("CurrentAreaLandType", snowArea.landType);
        SceneManager.LoadScene("MapScene");
    }

    /// <summary>
    /// 统一跳转逻辑：保存当前进入的区域类型，加载经营地图场景
    /// </summary>
    private void EnterMapScene(MapAreaConfig targetArea)
    {
        // 把当前选择的区域landType存入本地，进地图后读取
        PlayerPrefs.SetString("OpenAreaLandType", targetArea.landType);
        // 切换到你的经营地图场景（场景名必须和Build Settings里一致）
        SceneManager.LoadScene("MapScene");
    }


}