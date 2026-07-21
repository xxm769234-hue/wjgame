using UnityEngine;

public class TileClick : MonoBehaviour
{
    private LandTile tileData;
    private MapAreaConfig belongAreaCfg;

    public void SetData(LandTile tile, MapAreaConfig areaCfg)
    {
        tileData = tile;
        belongAreaCfg = areaCfg;
    }

    private void OnMouseDown()
    {
        Debug.Log("鼠标点击到");
        UIPanelManager.Instance.CloseAllBuildPanels();
        // 锁定格子：弹出解锁弹窗
        if (tileData.isLocked)
        {
            UIPanelManager.Instance.ShowUnlockPop(belongAreaCfg);
            return;
        }
        // 空白地块：打开地块建造面板
        if (tileData.landType == "empty")
        {
            UIPanelManager.Instance.landBuildPanel.SetActive(true);
        }
        // 已有地形：打开建筑建造面板
        else
        {
            UIPanelManager.Instance.buildPanel.SetActive(true);
        }
    }
}
