using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 地块建造右侧面板：选择地块、鼠标预览红绿、左键发包、右键退出
/// </summary>
public class LandBuildPanel : MonoBehaviour
{
    [Header("地块预览预制体 PreviewTile")]
    public GameObject previewTilePrefab;
    [Header("关闭面板按钮")]
    public Button closePanelBtn;

    // 当前选中要建造的地块配置
    public LandConfig selectLandConfig;
    // 预览方块实例
    private GameObject previewObj;
    // 是否处于建造模式
    private bool isBuildMode = false;

    private void Awake()
    {
        // 绑定关闭按钮事件
        if (closePanelBtn != null)
        {
            closePanelBtn.onClick.AddListener(ExitBuildMode);
        }
    }

    private void Update()
    {
        // 非建造模式直接返回
        if (!isBuildMode || selectLandConfig == null || Camera.main == null)
            return;

        // 鼠标屏幕坐标转世界坐标
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = MapTool.WorldToGrid(mouseWorldPos);

        // 校验该格子是否允许放置
        bool canPlace = LandManager.Instance.CheckCanPlaceTile(gridPos.x, gridPos.y, selectLandConfig);

        // 更新预览方块位置与颜色
        if (previewObj != null)
        {
            Vector2 worldGridPos = MapTool.GridToWorld(gridPos.x, gridPos.y);
            previewObj.transform.position = worldGridPos;
            LandRenderer render = previewObj.GetComponent<LandRenderer>();
            if (render != null)
            {
                render.SetPreviewColor(canPlace);
            }
        }

        // 鼠标左键：确认放置，发送网络请求
        if (Input.GetMouseButtonDown(0))
        {
            ConfirmPlaceLand(gridPos.x, gridPos.y);
        }

        // 鼠标右键：退出建造模式
        if (Input.GetMouseButtonDown(1))
        {
            ExitBuildMode();
        }
    }

    /// <summary>
    /// 【关键！必须是public，带LandConfig参数，Inspector才能搜到】
    /// 点击地块按钮进入对应地形建造模式
    /// </summary>
    /// <param name="cfg">选中的地块配置文件</param>
    public void EnterBuildMode(LandConfig cfg)
    {
        if (cfg == null)
        {
            TipTool.ShowTip("地块配置不能为空！", Color.red);
            return;
        }

        // 关闭其他所有建造面板
        UIPanelManager.Instance.CloseAllBuildPanels();
        // 显示当前地块面板
        gameObject.SetActive(true);

        selectLandConfig = cfg;
        isBuildMode = true;

        // 生成预览方块
        if (previewTilePrefab != null && MapManager.Instance.tileRoot != null)
        {
            previewObj = Instantiate(previewTilePrefab, MapManager.Instance.tileRoot);
        }
    }

    /// <summary>
    /// 左键确认放置，发送网络请求给服务端
    /// </summary>
    private void ConfirmPlaceLand(int x, int y)
    {
        // 本地预校验，不满足直接弹窗，不发包
        if (!LandManager.Instance.CheckCanPlaceTile(x, y, selectLandConfig))
        {
            TipTool.ShowTip("无法放置：格子锁定/已有地形/金币不足", Color.red);
            return;
        }

        // 发送网络请求
        NetRequest.SendBuildLandRequest(
            x,
            y,
            selectLandConfig.id,
            // 服务端成功回调
            () =>
            {
                LandManager.Instance.CreateTileLocal(x, y, selectLandConfig);
                TipTool.ShowTip("地块建造成功！", Color.green);
            },
            // 服务端失败回调
            (errorMsg) =>
            {
                TipTool.ShowTip(errorMsg, Color.red);
            }
        );
    }

    /// <summary>
    /// 退出建造模式，销毁预览方块，隐藏面板
    /// </summary>
    public void ExitBuildMode()
    {
        isBuildMode = false;
        selectLandConfig = null;
        gameObject.SetActive(false);

        if (previewObj != null)
        {
            Destroy(previewObj);
            previewObj = null;
        }
    }
}
