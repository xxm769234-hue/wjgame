using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns local NPC residence assignment requests. API integration can replace this later.</summary>
public sealed class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

    [SerializeField] private NpcConfig[] availableNpcs;

    private readonly List<NpcConfig> runtimeNpcs = new List<NpcConfig>();
    private readonly Dictionary<NpcConfig, LandSlotController> assignedSlots = new Dictionary<NpcConfig, LandSlotController>();

    public IReadOnlyList<NpcConfig> AvailableNpcs
    {
        get
        {
            EnsureRuntimeNpcs();
            return runtimeNpcs;
        }
    }

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
        EnsureRuntimeNpcs();
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
            reason = "No selected land slot.";
            return false;
        }
        if (!slot.IsBuilt)
        {
            reason = "Land must be placed before assigning an NPC.";
            return false;
        }
        if (!slot.HasBuilding)
        {
            reason = "Build a building before assigning an NPC.";
            return false;
        }
        if (!slot.IsBuildingCompleted)
        {
            reason = "Wait for the building to complete before assigning an NPC.";
            return false;
        }
        if (slot.HasNpc)
        {
            reason = "This land already has an NPC.";
            return false;
        }
        if (config == null)
        {
            reason = "NPC config is missing.";
            return false;
        }
        if (!config.canAssign)
        {
            reason = string.IsNullOrEmpty(config.disabledReason) ? "NPC is not available." : config.disabledReason;
            return false;
        }
        if (assignedSlots.TryGetValue(config, out LandSlotController assignedSlot) && assignedSlot != null && assignedSlot != slot)
        {
            reason = "NPC is already assigned to another land.";
            return false;
        }
        if (!CanUseBuildingType(slot.CurrentBuilding, config.requiredBuildingType))
        {
            reason = $"Requires building type: {config.requiredBuildingType}.";
            return false;
        }

        return true;
    }

    public bool TryAssignToSlot(LandSlotController slot, NpcConfig config, out string reason)
    {
        if (!CanAssignToSlot(slot, config, out reason)) return false;
        if (!slot.AssignNpc(config))
        {
            reason = "Land slot refused the NPC assignment.";
            return false;
        }
        assignedSlots[config] = slot;
        return true;
    }

    public bool CanRemoveFromSlot(LandSlotController slot, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "No selected land slot.";
            return false;
        }
        if (!slot.HasBuilding)
        {
            reason = "No building on this land.";
            return false;
        }
        if (!slot.IsBuildingCompleted)
        {
            reason = "Wait for the building to complete before removing an NPC.";
            return false;
        }
        if (!slot.HasNpc)
        {
            reason = "No NPC is assigned to this building.";
            return false;
        }

        return true;
    }

    public bool TryRemoveFromSlot(LandSlotController slot, out string reason)
    {
        if (!CanRemoveFromSlot(slot, out reason)) return false;
        NpcConfig removedNpc = slot.CurrentNpc;
        if (!slot.RemoveNpc())
        {
            reason = "Land slot refused the NPC removal.";
            return false;
        }

        Debug.Log($"POST /api/production/npc/remove local fallback: playerBuildingId={slot.PlayerBuildingId}, npcId={removedNpc.npcId}", slot);
        return true;
    }

    public void RegisterAssignment(NpcConfig config, LandSlotController slot)
    {
        if (config == null || slot == null) return;
        assignedSlots[config] = slot;
    }

    public void UnregisterAssignment(NpcConfig config, LandSlotController slot)
    {
        if (config == null) return;
        if (!assignedSlots.TryGetValue(config, out LandSlotController assignedSlot)) return;
        if (assignedSlot == slot || assignedSlot == null) assignedSlots.Remove(config);
    }

    private void EnsureRuntimeNpcs()
    {
        runtimeNpcs.Clear();
        if (availableNpcs != null)
        {
            for (int i = 0; i < availableNpcs.Length; i++)
            {
                if (availableNpcs[i] != null) runtimeNpcs.Add(availableNpcs[i]);
            }
        }

        if (runtimeNpcs.Count == 0) runtimeNpcs.Add(CreateDefaultNpc());
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

    private static NpcConfig CreateDefaultNpc()
    {
        NpcConfig config = ScriptableObject.CreateInstance<NpcConfig>();
        config.npcId = 1;
        config.npcName = "Mira";
        config.role = "Caretaker";
        config.description = "Demo NPC";
        config.fallbackColor = new Color(0.34f, 0.66f, 0.96f, 1f);
        config.visualScale = 0.85f;
        config.visualOffset = new Vector2(0.85f, 0.15f);
        config.canAssign = true;
        return config;
    }
}
