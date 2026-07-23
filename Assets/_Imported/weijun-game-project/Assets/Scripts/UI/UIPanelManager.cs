using UnityEngine;
using TMPro;

public class UIPanelManager : MonoBehaviour
{
    public static UIPanelManager Instance;
    public TMP_Text goldText;
    public TMP_Text levelText;
    public GameObject landBuildPanel;
    public GameObject buildPanel;
    public GameObject unlockPop;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
    }

    // 刷新顶部金币等级
    public void RefreshTopBar()
    {
        long gold = ResourceManager.Instance.resData["gold"];
        goldText.text = $"金币：{gold}";
        levelText.text = $"Lv.{ResourceManager.Instance.playerLv}";
    }

    // 关闭所有建造面板
    public void CloseAllBuildPanels()
    {
        landBuildPanel.SetActive(false); 
        buildPanel.SetActive(false);
    }

    // 弹出解锁弹窗
    public void ShowUnlockPop(MapAreaConfig areaCfg)
    {
        unlockPop.SetActive(true);
        UnlockPop pop = unlockPop.GetComponent<UnlockPop>();
        pop.SetAreaData(areaCfg);
    }
}