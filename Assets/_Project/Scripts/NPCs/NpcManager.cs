using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns backend NPC residence assignment requests.</summary>
public sealed class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

    [SerializeField] private NpcConfig[] availableNpcs;

    private readonly List<NpcConfig> remoteAvailableNpcs = new List<NpcConfig>();
    private readonly Dictionary<NpcConfig, int> playerNpcIds = new Dictionary<NpcConfig, int>();

    public IReadOnlyList<NpcConfig> AvailableNpcs => remoteAvailableNpcs;

    public static NpcManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        NpcManager existing = FindFirstObjectByType<NpcManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("NpcManager");
        return managerObject.AddComponent<NpcManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanAssignToSlot(LandSlotController slot, NpcConfig config, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }
        if (!slot.IsBuilt)
        {
            reason = "请先放置地块，再分配居民。";
            return false;
        }
        if (!slot.HasBuilding)
        {
            reason = "请先建造建筑，再分配居民。";
            return false;
        }
        if (!slot.IsBuildingCompleted)
        {
            reason = "请等待建筑完成，再分配居民。";
            return false;
        }
        if (slot.HasNpc)
        {
            reason = "该地块已有居民。";
            return false;
        }
        if (config == null)
        {
            reason = "暂时无法查看此居民，请重新打开列表。";
            return false;
        }
        if (!config.canAssign)
        {
            reason = FrontendTextLocalization.NormalizePlayerMessage(config.disabledReason, "居民当前不可用。");
            return false;
        }
        if (!CanUseBuildingType(slot.CurrentBuilding, config.requiredBuildingType))
        {
            reason = $"需要建筑：{FrontendTextLocalization.Normalize(config.requiredBuildingType)}。";
            return false;
        }

        return true;
    }

    public void RequestAvailableNpcsForSlot(LandSlotController slot, Action<bool, string, IReadOnlyList<NpcConfig>> onComplete)
    {
        remoteAvailableNpcs.Clear();
        playerNpcIds.Clear();

        if (!CanRequestAvailableNpcs(slot, out string reason))
        {
            onComplete?.Invoke(false, reason, remoteAvailableNpcs);
            return;
        }

        BackendGameService.Instance.GetAvailableNpcs(slot, result =>
        {
            bool success = result != null && result.Success && result.Data != null;
            if (success) StoreRemoteAvailableNpcs(result.Data);
            string message = result == null || string.IsNullOrEmpty(result.Message) ? "连接中断，请稍后重试。" : result.Message;
            onComplete?.Invoke(success, message, remoteAvailableNpcs);
        });
    }

    public bool TryAssignToSlot(LandSlotController slot, NpcConfig config, Action<bool, string> onComplete, out string reason)
    {
        if (!CanAssignToSlot(slot, config, out reason)) return false;

        if (!playerNpcIds.TryGetValue(config, out int playerNpcId) || playerNpcId <= 0)
        {
            reason = "暂时无法分配此居民，请刷新列表。";
            return false;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            reason = "请先登录账号。";
            return false;
        }

        service.SettleNpc(slot, playerNpcId, (success, message) =>
        {
            if (!success) Debug.LogWarning($"Backend NPC settle failed: {message}", slot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.RefreshIfShowing(slot);
            onComplete?.Invoke(success, message);
        });
        return true;
    }

    public bool CanRemoveFromSlot(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }
        if (!slot.HasBuilding)
        {
            reason = "该地块没有建筑。";
            return false;
        }
        if (!slot.IsBuildingCompleted)
        {
            reason = "请等待建筑完成，再移除居民。";
            return false;
        }
        if (!slot.HasNpc)
        {
            reason = "该建筑未分配居民。";
            return false;
        }

        return true;
    }

    public bool TryRemoveFromSlot(LandSlotController slot, Action<bool, string> onComplete, out string reason)
    {
        if (!CanRemoveFromSlot(slot, out reason)) return false;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            reason = "请先登录账号。";
            return false;
        }

        service.RemoveNpc(slot, (success, message) =>
        {
            if (!success) Debug.LogWarning($"Backend NPC remove failed: {message}", slot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.RefreshIfShowing(slot);
            onComplete?.Invoke(success, message);
        });
        return true;
    }

    public static NpcConfig CreateRuntimeNpc(NpcResponse response)
    {
        if (response == null) return null;
        return CreateRuntimeNpc(response.npcId, response.name, response.npcCode, response.produceBonusRate);
    }

    public static NpcConfig CreateRuntimeNpc(SettledNpcResponse response)
    {
        if (response == null) return null;
        return CreateRuntimeNpc(response.npcId, response.name, response.npcCode, response.produceBonusRate);
    }

    private static NpcConfig CreateRuntimeNpc(int npcId, string npcName, string npcCode, float produceBonusRate)
    {
        NpcConfig config = ScriptableObject.CreateInstance<NpcConfig>();
        config.npcId = npcId;
        config.npcName = string.IsNullOrEmpty(npcName) ? npcCode : npcName;
        config.role = produceBonusRate > 0f ? $"+{Mathf.RoundToInt(produceBonusRate * 100f)}%产出" : "居民";
        config.description = npcCode;
        config.fallbackColor = new Color(0.34f, 0.66f, 0.96f, 1f);
        config.visualScale = 0.85f;
        config.visualOffset = new Vector2(0.85f, 0.15f);
        config.canAssign = true;
        return config;
    }

    private bool CanRequestAvailableNpcs(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "未选择地块。";
            return false;
        }
        if (!slot.HasBuilding)
        {
            reason = "请先建造建筑，再分配居民。";
            return false;
        }
        if (!slot.IsBuildingCompleted)
        {
            reason = "请等待建筑完成，再分配居民。";
            return false;
        }
        if (slot.HasNpc)
        {
            reason = "该地块已有居民。";
            return false;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            reason = "请先登录账号。";
            return false;
        }
        return true;
    }

    private void StoreRemoteAvailableNpcs(NpcResponse[] responses)
    {
        remoteAvailableNpcs.Clear();
        playerNpcIds.Clear();
        if (responses == null) return;

        for (int i = 0; i < responses.Length; i++)
        {
            NpcResponse response = responses[i];
            NpcConfig config = ResolveVisualNpc(response);
            if (config == null) continue;

            remoteAvailableNpcs.Add(config);
            playerNpcIds[config] = response.playerNpcId;
        }
    }

    private NpcConfig ResolveVisualNpc(NpcResponse response)
    {
        if (response == null) return null;
        NpcConfig config = FindAuthoredNpc(response.npcId);
        if (config == null) return CreateRuntimeNpc(response);

        NpcConfig runtime = Instantiate(config);
        runtime.npcId = response.npcId;
        runtime.npcName = string.IsNullOrEmpty(response.name) ? config.npcName : response.name;
        runtime.role = response.produceBonusRate > 0f ? $"+{Mathf.RoundToInt(response.produceBonusRate * 100f)}%产出" : config.role;
        runtime.description = response.npcCode;
        runtime.canAssign = true;
        return runtime;
    }

    private NpcConfig FindAuthoredNpc(int npcId)
    {
        if (availableNpcs == null) return null;
        for (int i = 0; i < availableNpcs.Length; i++)
        {
            if (availableNpcs[i] != null && availableNpcs[i].npcId == npcId) return availableNpcs[i];
        }
        return null;
    }

    private static bool CanUseBuildingType(BuildingConfig building, string requiredBuildingType)
    {
        if (IsBlank(requiredBuildingType)) return true;
        return building != null && string.Equals(building.buildingType, requiredBuildingType, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlank(string value)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
    }

}
