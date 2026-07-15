using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns local building placement requests. Network calls can replace this later.</summary>
public sealed class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    [SerializeField] private BuildingConfig[] availableBuildings;

    private readonly List<BuildingConfig> runtimeBuildings = new List<BuildingConfig>();
    private LandSlotController movingSource;

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
            reason = "No selected land slot.";
            return false;
        }
        if (!slot.IsBuilt)
        {
            reason = "Land must be placed before building.";
            return false;
        }
        if (slot.HasBuilding)
        {
            reason = "This land already has a building.";
            return false;
        }
        if (config == null)
        {
            reason = "Building config is missing.";
            return false;
        }
        if (!config.canBuild)
        {
            reason = string.IsNullOrEmpty(config.disabledReason) ? "Building is not available." : config.disabledReason;
            return false;
        }
        if (!slot.AllowsBuildingType(config))
        {
            reason = $"Land does not allow building type {config.buildingType}.";
            return false;
        }

        return true;
    }

    public bool TryBuildOnSlot(LandSlotController slot, BuildingConfig config, out string reason)
    {
        if (!CanBuildOnSlot(slot, config, out reason)) return false;
        if (!slot.BuildBuilding(config))
        {
            reason = "Land slot refused the building request.";
            return false;
        }
        return true;
    }

    public bool TryStartUpgrade(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "No selected land slot.";
            return false;
        }
        if (!slot.TryStartUpgrade(out reason)) return false;
        Debug.Log($"POST /api/building/upgrade local fallback: playerBuildingId={slot.PlayerBuildingId}", slot);
        return true;
    }

    public bool TryCancelBuildingAction(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "No selected land slot.";
            return false;
        }
        if (!slot.TryCancelBuildingAction(out reason)) return false;
        Debug.Log($"POST /api/building/cancel local fallback: playerBuildingId={slot.PlayerBuildingId}", slot);
        return true;
    }

    public bool TryCompleteBuildingAction(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "No selected land slot.";
            return false;
        }
        if (!slot.TryCompleteBuildingAction(out reason)) return false;
        Debug.Log($"POST /api/building/complete local fallback: playerBuildingId={slot.PlayerBuildingId}", slot);
        return true;
    }

    public bool TryDemolishBuilding(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "No selected land slot.";
            return false;
        }
        int playerBuildingId = slot.PlayerBuildingId;
        if (!slot.TryDemolishBuilding(out reason)) return false;
        Debug.Log($"POST /api/building/demolish local fallback: playerBuildingId={playerBuildingId}", slot);
        return true;
    }

    public bool BeginMoveBuilding(LandSlotController source, out string reason)
    {
        reason = string.Empty;
        if (source == null)
        {
            reason = "No selected land slot.";
            return false;
        }
        if (!source.IsBuildingCompleted)
        {
            reason = "Only completed buildings can be moved.";
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
        if (!source.TryMoveBuildingTo(clickedSlot, out string reason))
        {
            Debug.LogWarning($"Building move failed: {reason}", clickedSlot);
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(source);
            return true;
        }

        Debug.Log($"POST /api/building/move local fallback: playerBuildingId={clickedSlot.PlayerBuildingId}, targetLandId={clickedSlot.PlayerLandId}", clickedSlot);
        if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(clickedSlot);
        return true;
    }

    private void EnsureRuntimeBuildings()
    {
        runtimeBuildings.Clear();
        if (availableBuildings != null)
        {
            for (int i = 0; i < availableBuildings.Length; i++)
            {
                if (availableBuildings[i] != null) runtimeBuildings.Add(availableBuildings[i]);
            }
        }

        if (runtimeBuildings.Count == 0) runtimeBuildings.Add(CreateDefaultBuilding());
    }

    private static BuildingConfig CreateDefaultBuilding()
    {
        BuildingConfig config = ScriptableObject.CreateInstance<BuildingConfig>();
        config.buildingId = 1;
        config.buildingName = "Workshop";
        config.description = "Demo building";
        config.cost = 0;
        config.buildSeconds = 0;
        config.buildingType = "Production";
        config.fallbackColor = new Color(0.95f, 0.68f, 0.26f, 1f);
        config.visualScale = 1.35f;
        config.canBuild = true;
        config.maxLevel = 3;
        config.upgradeCost = 50;
        config.upgradeSeconds = 10;
        config.resourceAmountPerLevel = 5;
        config.canProduceResource = true;
        config.requireNpcForProduction = false;
        config.showResourceBubbleImmediately = true;
        config.resourceName = "Gold";
        config.resourceAmount = 10;
        config.productionSeconds = 10f;
        config.resourceColor = new Color(1f, 0.78f, 0.2f, 1f);
        return config;
    }
}
