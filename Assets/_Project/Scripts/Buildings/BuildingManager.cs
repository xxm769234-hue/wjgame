using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns backend building placement requests.</summary>
public sealed class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    [SerializeField] private BuildingConfig[] availableBuildings;

    private readonly List<BuildingConfig> runtimeBuildings = new List<BuildingConfig>();
    private LandSlotController movingSource;
    private bool remoteActionPending;
    private bool hasRemoteAvailableBuildings;

    public IReadOnlyList<BuildingConfig> AvailableBuildings
    {
        get
        {
            EnsureRuntimeBuildings();
            return runtimeBuildings;
        }
    }

    public bool IsMoveMode => movingSource != null;
    public LandSlotController MovingSource => movingSource;

    public static BuildingManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        BuildingManager existing = FindFirstObjectByType<BuildingManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("BuildingManager");
        return managerObject.AddComponent<BuildingManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureRuntimeBuildings();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanBuildOnSlot(LandSlotController slot, BuildingConfig config, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }
        if (!slot.IsBuilt)
        {
            reason = "请先放置地块再建造。";
            return false;
        }
        if (slot.HasBuilding)
        {
            reason = "该地块已有建筑。";
            return false;
        }
        if (config == null)
        {
            reason = "建筑配置缺失。";
            return false;
        }
        if (!config.canBuild)
        {
            reason = string.IsNullOrEmpty(config.disabledReason) ? "该建筑当前不可用。" : config.disabledReason;
            return false;
        }
        if (!slot.AllowsBuildingType(config))
        {
            reason = "该地块不允许建造此类型建筑。";
            return false;
        }

        return true;
    }

    public bool TryBuildOnSlot(LandSlotController slot, BuildingConfig config, out string reason)
    {
        if (!CanBuildOnSlot(slot, config, out reason)) return false;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            reason = "服务器会话未认证。";
            return false;
        }

        if (remoteActionPending)
        {
            reason = "建筑操作正在进行中。";
            return false;
        }

        Debug.Log($"BuildingManager build click accepted. Checking backend conditions for {config.buildingName} on playerLandId={slot.PlayerLandId}.", slot);
        remoteActionPending = true;
        service.CheckBuildingForLand(slot, config, result =>
        {
            if (result == null || !result.Success || result.Data == null)
            {
                remoteActionPending = false;
                string message = ResultMessage(result);
                Debug.LogWarning($"Backend building check failed: {message}", slot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.SetBuildingMessage($"建筑操作失败：{message}");
                return;
            }

            Debug.Log($"BuildingManager backend check result for {config.buildingName}: canBuild={result.Data.canBuild}, message={result.Data.message}", slot);
            if (!result.Data.canBuild)
            {
                remoteActionPending = false;
                string message = string.IsNullOrEmpty(result.Data.message) ? "服务器拒绝了该建造操作。" : result.Data.message;
                Debug.LogWarning($"Backend building check rejected: {message}", slot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.SetBuildingMessage($"建筑操作失败：{message}");
                return;
            }

            service.BuildBuilding(slot, config, (success, message) =>
            {
                remoteActionPending = false;
                Debug.Log($"BuildingManager backend build callback for {config.buildingName}: success={success}, message={message}", slot);
                if (!success) Debug.LogWarning($"Backend building build failed: {message}", slot);
                if (LandInfoPanel.Instance != null)
                {
            if (!success) LandInfoPanel.Instance.SetBuildingMessage($"建筑操作失败：{message}");
                    LandInfoPanel.Instance.RefreshIfShowing(slot);
                }
            });
        });
        return true;
    }

    public bool TryStartUpgrade(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }

        if (!slot.IsBuildingCompleted)
        {
            reason = "只有已完成的建筑可以升级。";
            return false;
        }
        if (slot.BuildingLevel >= slot.CurrentBuilding.maxLevel)
        {
            reason = "建筑已达到最高等级。";
            return false;
        }
        return RunRemoteAction(slot, service => service.UpgradeBuilding(slot, RemoteRefresh(slot, "upgrade")), out reason);
    }

    public bool TryCancelBuildingAction(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }

        if (!slot.IsBuildingInProgress)
        {
            reason = "只有建造中或升级中的建筑可以取消。";
            return false;
        }
        return RunRemoteAction(slot, service => service.CancelBuildingAction(slot, RemoteRefresh(slot, "cancel")), out reason);
    }

    public bool TryCompleteBuildingAction(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }

        if (!slot.IsBuildingInProgress)
        {
            reason = "该建筑当前不在等待完成状态。";
            return false;
        }
        if (slot.BuildingRemainingSeconds > 0f)
        {
            reason = $"还需 {Mathf.CeilToInt(slot.BuildingRemainingSeconds)}s。";
            return false;
        }
        return RunRemoteAction(slot, service => service.CompleteBuildingAction(slot, RemoteRefresh(slot, "complete")), out reason);
    }

    public bool TryDemolishBuilding(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }

        if (!slot.IsBuildingCompleted)
        {
            reason = "建造中或升级中的建筑不能拆除。";
            return false;
        }
        return RunRemoteAction(slot, service => service.DemolishBuilding(slot, RemoteRefresh(slot, "demolish")), out reason);
    }

    public bool BeginMoveBuilding(LandSlotController source, out string reason)
    {
        reason = string.Empty;
        if (source == null)
        {
            reason = "未选择地块。";
            return false;
        }
        if (!source.IsBuildingCompleted)
        {
            reason = "只有已完成的建筑可以移动。";
            return false;
        }

        movingSource = source;
        return true;
    }

    public void CancelMoveBuilding()
    {
        movingSource = null;
    }

    public bool HandleLandSlotClicked(LandSlotController clickedSlot)
    {
        if (movingSource == null) return false;

        LandSlotController source = movingSource;
        movingSource = null;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            Debug.LogWarning("移动建筑失败：服务器会话未认证。", clickedSlot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(source);
            return true;
        }

        if (!clickedSlot.CanReceiveMovedBuilding(source.CurrentBuilding, out string moveReason))
        {
            Debug.LogWarning($"Building move failed: {moveReason}", clickedSlot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(source);
            return true;
        }

        if (remoteActionPending)
        {
            Debug.LogWarning("建筑操作正在进行中。", clickedSlot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(source);
            return true;
        }

        remoteActionPending = true;
        service.MoveBuilding(source, clickedSlot, (success, message) =>
        {
            remoteActionPending = false;
            if (!success)
            {
                Debug.LogWarning($"Backend building move failed: {message}", clickedSlot);
                if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(source);
            }
            else if (LandInfoPanel.Instance != null)
            {
                LandInfoPanel.Instance.Show(clickedSlot);
            }
        });
        return true;
    }

    public void SetRemoteAvailableBuildings(IReadOnlyList<BuildingConfig> configs)
    {
        runtimeBuildings.Clear();
        hasRemoteAvailableBuildings = true;
        if (configs != null)
        {
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i] != null) runtimeBuildings.Add(configs[i]);
            }
        }

    }

    private void EnsureRuntimeBuildings()
    {
        if (hasRemoteAvailableBuildings) return;

        runtimeBuildings.Clear();
    }

    private bool RunRemoteAction(LandSlotController slot, System.Action<BackendGameService> action, out string reason)
    {
        reason = string.Empty;
        if (remoteActionPending)
        {
            reason = "建筑操作正在进行中。";
            return false;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            reason = "服务器会话未认证。";
            return false;
        }

        remoteActionPending = true;
        action(service);
        return true;
    }

    private System.Action<bool, string> RemoteRefresh(LandSlotController slot, string actionName)
    {
        return (success, message) =>
        {
            remoteActionPending = false;
            if (!success) Debug.LogWarning($"Backend building {actionName} failed: {message}", slot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.RefreshIfShowing(slot);
        };
    }

    private static string ResultMessage<T>(BackendResult<T> result)
    {
        if (result == null) return "未收到服务器响应。";
        return FrontendTextLocalization.Normalize(string.IsNullOrEmpty(result.Message) ? $"请求状态：{result.HttpStatus}" : result.Message);
    }
}
