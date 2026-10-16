using System;
using System.Text;

[Serializable]
public sealed class ApiResponse<T>
{
    public int code;
    public string message;
    public T data;
}

public sealed class BackendResult<T>
{
    public readonly bool Success;
    public readonly int HttpStatus;
    public readonly int Code;
    public readonly string Message;
    public readonly T Data;

    public BackendResult(bool success, int httpStatus, string message, T data)
        : this(success, httpStatus, 0, message, data)
    {
    }

    public BackendResult(bool success, int httpStatus, int code, string message, T data)
    {
        Success = success;
        HttpStatus = httpStatus;
        Code = code;
        Message = message;
        Data = data;
    }
}

[Serializable]
public sealed class EmptyResponse { }

[Serializable]
public sealed class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public sealed class RegisterRequest
{
    public string username;
    public string password;
    public string nickname;
}

[Serializable]
public sealed class PlayerIdRequest
{
    public int playerId;
    public int sceneId;
}

[Serializable]
public sealed class PlaceLandRequest
{
    public int playerId;
    public int sceneId;
    public int landId;
    public int positionX;
    public int positionY;
}

[Serializable]
public sealed class PurchaseLandSlotRequest
{
    public int playerId;
    public int sceneId;
    public int positionX;
    public int positionY;
    public long expectedSceneVersion;
    public long expectedPolicyRevision;
}

[Serializable]
public sealed class BuildBuildingRequest
{
    public int playerId;
    public int buildingId;
    public int playerLandId;
}

[Serializable]
public sealed class PlayerBuildingRequest
{
    public int playerId;
    public int playerBuildingId;
}

[Serializable]
public sealed class NpcSettleRequest
{
    public int playerId;
    public int playerBuildingId;
    public int playerNpcId;
}

[Serializable]
public sealed class MoveBuildingRequest
{
    public int playerId;
    public int playerBuildingId;
    public int targetPlayerLandId;
}

[Serializable]
public sealed class LoginResponse
{
    public string token;
    public int expiresIn;
    public UserResponse user;
}

[Serializable]
public sealed class UserResponse
{
    public long id;
    public string username;
    public string nickname;
    public long nicknameVersion;
    public string nicknameUpdatedTime;
    public int nicknameChangeCount;
    public bool nicknameChangeRequired;
    public string avatarUrl;
    public string avatarSource;
    public long avatarVersion;
    public string avatarUpdatedTime;
    public int level;
    public long totalExperience;
    public long levelCurveRevision;
    public long currentLevelExperience;
    public long requiredExperienceForNextLevel;
    public int experienceProgressBps;
    public bool maxLevel;
    public bool economyFrozen;
    public long gold;
    public long wood;
    public long stone;
    public long iron;
    public long diamond;
    public InventoryItemResponse[] items;
}

[Serializable]
public sealed class InventoryItemResponse
{
    public long itemId;
    public string itemKey;
    public string name;
    public string itemType;
    public string quality;
    public string icon;
    public long quantity;
    public string description;
}

[Serializable]
public sealed class MapDataResponse
{
    public int playerId;
    public int sceneId;
    public string sceneKey;
    public string sceneName;
    public string backgroundKey;
    public string terrainKey;
    public string bgmKey;
    public long sceneConfigVersion;
    public long landPolicyRevision;
    public int landGridWidth;
    public int landGridHeight;
    public int landSlotCount;
    public int width;
    public int height;
    public LandSlotStateResponse[] landSlots;
    public LandTileResponse[] lands;
    public BuildingResponse[] buildings;
}

[Serializable]
public sealed class LandSlotStateResponse
{
    public int sceneId;
    public int positionX;
    public int positionY;
    public int unitIndex;
    public int requiredLevel;
    public bool levelUnlocked;
    public bool requiresPurification;
    public bool purified;
    public bool purificationAvailable;
    public long purificationItemId;
    public string purificationItemKey;
    public long purificationItemAmount;
    public bool requiresPurchase;
    public bool purchased;
    public bool purchaseAvailable;
    public bool placementUnlocked;
    public long goldCost;
}

[Serializable]
public sealed class LandTileResponse
{
    public int sceneId;
    public int playerLandId;
    public int landId;
    public string landType;
    public string landName;
    public string allowBuildType;
    public int level;
    public int positionX;
    public int positionY;
}

[Serializable]
public sealed class LandConfigResponse
{
    public long landId;
    public string landType;
    public string name;
    public string cost;
    public int unlockLevel;
    public string allowBuildType;
    public long goldCost;
    public long woodCost;
    public long stoneCost;
    public long ironCost;
    public bool canPlace;
    public string cannotPlaceReason;
}

[Serializable]
public sealed class BuildingConfigResponse
{
    public long id;
    public long buildingId;
    public string code;
    public string name;
    public string description;
    public string needLand;
    public string cost;
    public string produce;
    public string buildType;
    public string type;
    public int level;
    public long goldCost;
    public long woodCost;
    public long stoneCost;
    public long ironCost;
    public int buildTime;
    public string modelKey;
    public string icon;
    public int requiredLevel;
    public long prerequisiteBuildingId;
    public string singletonGroupCode;
    public int ownedCount;
    public int maxOwnedCount;
    public bool ownershipLimitMet;
    public bool canBuild;
    public string cannotBuildReason;
}

[Serializable]
public sealed class BuildingConditionResponse
{
    public long buildingId;
    public bool canBuild;
    public bool levelMet;
    public bool resourceMet;
    public bool prerequisiteMet;
    public bool landMet;
    public bool ownershipLimitMet;
    public int ownedCount;
    public int maxOwnedCount;
    public string message;
    public long goldNeeded;
    public long woodNeeded;
    public long stoneNeeded;
    public long ironNeeded;
    public int requiredLevel;
    public string missingPrerequisite;
}

[Serializable]
public sealed class BuildingResponse
{
    public int sceneId;
    public int playerBuildingId;
    public int buildingId;
    public int playerLandId;
    public int landId;
    public string buildingCode;
    public string buildingName;
    public string buildingType;
    public int level;
    public string produce;
    public string modelKey;
    public string icon;
    public string status;
    public int positionX;
    public int positionY;
    public string buildStartTime;
    public string buildEndTime;
    public long remainingTime;
    public int progress;
    public ExperienceGrantResponse experienceGrant;
}

[Serializable]
public sealed class ExperienceGrantResponse
{
    public long playerId;
    public string sourceType;
    public string sourceId;
    public long requestedExperience;
    public long grantedExperience;
    public long beforeExperience;
    public long afterExperience;
    public int beforeLevel;
    public int afterLevel;
    public bool leveledUp;
    public bool maxLevel;
    public long currentLevelExperience;
    public long requiredExperienceForNextLevel;
    public int experienceProgressBps;
    public long curveRevision;
    public bool replayed;
}

[Serializable]
public sealed class ProductionResponse
{
    public int sceneId;
    public int playerLandId;
    public int playerBuildingId;
    public int landId;
    public int buildingId;
    public string buildingName;
    public int level;
    public string status;
    public long itemId;
    public string itemKey;
    public string itemName;
    public string icon;
    public long baseProduceAmount;
    public long produceAmount;
    public float npcProduceBonusRate;
    public SettledNpcResponse settledNpc;
    public int produceIntervalSeconds;
    public long storedAmount;
    public long storageCapacity;
    public bool canCollect;
    public string lastSettleTime;
    public string nextProduceTime;
    public long remainingSeconds;
}

[Serializable]
public sealed class NpcConfigResponse
{
    public int npcId;
    public string npcCode;
    public string name;
    public string quality;
    public string icon;
    public string modelKey;
    public string description;
    public string combatAttribute;
    public int maxLevel;
    public float produceBonusRate;
    public bool owned;
    public NpcAcquisitionResponse acquisition;
    public int configVersion;
    public int contractVersion;
}

[Serializable]
public sealed class NpcAcquisitionResponse
{
    public string status;
    public string channelType;
    public string displayName;
    public string buttonText;
    public string message;
}

[Serializable]
public sealed class NpcResponse
{
    public int playerNpcId;
    public int npcId;
    public string npcCode;
    public string name;
    public string quality;
    public string icon;
    public string modelKey;
    public string description;
    public string combatAttribute;
    public int maxLevel;
    public int level;
    public string state;
    public int settledPlayerBuildingId;
    public int settledSceneId;
    public int settledPlayerLandId;
    public float produceBonusRate;
}

[Serializable]
public sealed class SettledNpcResponse
{
    public int playerNpcId;
    public int npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string modelKey;
    public float produceBonusRate;
}

[Serializable]
public sealed class ProductionCollectResponse
{
    public int playerId;
    public int sceneId;
    public string collectScope;
    public string idempotencyKey;
    public CollectedItemResponse[] collectedItems;
    public ProductionResponse[] productions;
    public InventoryItemResponse[] items;
}

[Serializable]
public sealed class CollectedItemResponse
{
    public long itemId;
    public string itemKey;
    public string name;
    public long quantity;
}

[Serializable]
public sealed class IdleAdventureAfkRewardStateResponse
{
    public long player_id;
    public long playerId;
    public string item_key;
    public string itemKey;
    public string display_name;
    public string itemName;
    public string displayName;
    public int base_quantity_per_cycle;
    public long baseQuantityPerCycle;
    public int quantity_per_highest_stage;
    public long quantityPerHighestStage;
    public int quantity_per_cycle;
    public long quantityPerCycle;
    public int production_interval_seconds;
    public int productionIntervalSeconds;
    public int intervalSeconds;
    public int max_stored_quantity;
    public long maxStoredQuantity;
    public int stored_quantity;
    public long claimableQuantity;
    public long effectiveElapsedSeconds;
    public int highest_stage;
    public int highestStage;
    public long server_unix_seconds;
    public long serverUnixSeconds;
    public long last_settled_server_unix_seconds;
    public string lastCollectTime;
    public string serverTime;
    public long nextSettlementSeconds;
    public long next_settlement_seconds;
    public long mode_config_id;
    public long modeConfigId;
    public int mode_config_version;
    public int modeConfigVersion;
    public bool acceleration_enabled;
    public bool accelerationEnabled;
    public int acceleration_stamina_cost;
    public int accelerationStaminaCost;
    public long acceleration_reward_quantity;
    public long accelerationRewardQuantity;
    public int acceleration_cycles;
    public int accelerationCycles;
    public long current_stamina;
    public long currentStamina;
    public int max_times_by_current_stamina;
    public int maxTimesByCurrentStamina;

    public int ResolvedPlayerId => playerId > 0L ? (int)playerId : (int)player_id;
    public string ResolvedItemKey => string.IsNullOrEmpty(itemKey) ? item_key : itemKey;
    public string ResolvedDisplayName => string.IsNullOrEmpty(displayName)
        ? (string.IsNullOrEmpty(itemName) ? display_name : itemName)
        : displayName;
    public int ResolvedBaseQuantityPerCycle => baseQuantityPerCycle > 0L
        ? (int)Math.Min(int.MaxValue, baseQuantityPerCycle)
        : base_quantity_per_cycle;
    public int ResolvedQuantityPerHighestStage => quantity_per_highest_stage != 0
        ? quantity_per_highest_stage
        : (int)Math.Min(int.MaxValue, quantityPerHighestStage);
    public int ResolvedQuantityPerCycle => quantityPerCycle > 0L
        ? (int)Math.Min(int.MaxValue, quantityPerCycle)
        : quantity_per_cycle;
    public int ResolvedProductionIntervalSeconds => productionIntervalSeconds > 0
        ? productionIntervalSeconds
        : intervalSeconds > 0 ? intervalSeconds : production_interval_seconds;
    public int ResolvedMaxStoredQuantity => maxStoredQuantity > 0L
        ? (int)Math.Min(int.MaxValue, maxStoredQuantity)
        : max_stored_quantity;
    public int ResolvedStoredQuantity
    {
        get
        {
            if (claimableQuantity > 0L) return (int)Math.Min(int.MaxValue, claimableQuantity);
            return stored_quantity;
        }
    }
    public int ResolvedHighestClearedStage => highestStage > 0 ? highestStage : highest_stage;
    public long ResolvedServerUnixSeconds
    {
        get
        {
            if (serverUnixSeconds > 0L) return serverUnixSeconds;
            if (TryParseUnixSeconds(serverTime, out long parsed)) return parsed;
            return server_unix_seconds;
        }
    }
    public long ResolvedLastSettledServerUnixSeconds
    {
        get
        {
            if (last_settled_server_unix_seconds > 0L) return last_settled_server_unix_seconds;
            return TryParseUnixSeconds(lastCollectTime, out long parsed) ? parsed : 0L;
        }
    }
    public long ResolvedNextSettlementSeconds => nextSettlementSeconds > 0L
        ? nextSettlementSeconds
        : next_settlement_seconds;
    public long ResolvedModeConfigId => modeConfigId > 0L ? modeConfigId : mode_config_id;
    public int ResolvedModeConfigVersion => modeConfigVersion > 0
        ? modeConfigVersion
        : mode_config_version;
    public bool ResolvedAccelerationEnabled => accelerationEnabled || acceleration_enabled;
    public int ResolvedAccelerationStaminaCost => accelerationStaminaCost > 0
        ? accelerationStaminaCost
        : acceleration_stamina_cost;
    public long ResolvedAccelerationRewardQuantity => accelerationRewardQuantity > 0L
        ? accelerationRewardQuantity
        : acceleration_reward_quantity;
    public int ResolvedAccelerationCycles => accelerationCycles > 0 ? accelerationCycles : acceleration_cycles;
    public long ResolvedCurrentStamina => currentStamina > 0L ? currentStamina : current_stamina;
    public int ResolvedMaxTimesByCurrentStamina => maxTimesByCurrentStamina > 0
        ? maxTimesByCurrentStamina
        : max_times_by_current_stamina;

    private static bool TryParseUnixSeconds(string value, out long seconds)
    {
        seconds = 0L;
        if (string.IsNullOrEmpty(value)) return false;
        if (long.TryParse(value, out seconds)) return seconds > 0L;
        if (!DateTimeOffset.TryParse(value, out DateTimeOffset parsed)) return false;
        seconds = parsed.ToUnixTimeSeconds();
        return seconds > 0L;
    }
}

[Serializable]
public sealed class IdleAdventureAfkRewardCollectResponse
{
    public long player_id;
    public long playerId;
    public IdleAdventureCollectedItemResponse collected_item;
    public IdleAdventureCollectedItemResponse collected_reward;
    public IdleAdventureCollectedItemResponse[] collected_rewards;
    public IdleAdventureCollectedItemResponse collectedReward;
    public IdleAdventureCollectedItemResponse[] collectedRewards;
    public IdleAdventureInventoryItemResponse[] items;
    public IdleAdventureAfkRewardStateResponse state;
    public bool idempotent_replay;
    public bool idempotentReplay;

    public int ResolvedPlayerId => playerId > 0L ? (int)playerId : (int)player_id;
    public IdleAdventureCollectedItemResponse ResolvedCollectedItem => collected_item;
    public IdleAdventureCollectedItemResponse ResolvedCollectedReward => collectedReward ?? collected_reward;
    public IdleAdventureCollectedItemResponse[] ResolvedCollectedRewards => collectedRewards ?? collected_rewards;
    public bool ResolvedIdempotentReplay => idempotentReplay || idempotent_replay;
}

[Serializable]
public sealed class IdleAdventureFormationSlotRequest
{
    public int slot_index;
    public long definition_id;
    public bool has_definition_id = true;
}

[Serializable]
public sealed class IdleAdventureSaveFormationRequest
{
    public IdleAdventureFormationSlotRequest[] slots;

    public string ToJson()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"slots\":[");
        if (slots != null)
        {
            bool appended = false;
            for (int i = 0; i < slots.Length; i++)
            {
                IdleAdventureFormationSlotRequest slot = slots[i];
                if (slot == null) continue;

                if (appended) builder.Append(',');
                builder.Append("{\"slot_index\":");
                builder.Append(slot.slot_index);
                builder.Append(",\"definition_id\":");
                if (slot.has_definition_id) builder.Append(slot.definition_id);
                else builder.Append("null");
                builder.Append('}');
                appended = true;
            }
        }
        builder.Append("]}");
        return builder.ToString();
    }
}

[Serializable]
public sealed class IdleAdventureStartSessionRequest
{
    public int stage;
    public bool use_saved_formation = true;
    public IdleAdventureFormationSlotRequest[] formation_slots;
}

[Serializable]
public sealed class IdleAdventureResolveRoundRequest
{
    public int client_round;
}

[Serializable]
public sealed class IdleAdventureApplyBuffRequest
{
    public string target_side;
    public int slot_index;
    public long buff_preset_id;
    public string source = "OTHER";
}

[Serializable]
public sealed class IdleAdventureStageClearRequest
{
    public string session_key;
    public int cleared_stage;
}

[Serializable]
public sealed class IdleAdventureHomeResponse
{
    public IdleAdventureModeResponse mode;
    public IdleAdventureProgressSummaryResponse progress;
    public IdleAdventureNpcSummaryResponse[] availableNpcs;
    public IdleAdventureNpcSummaryResponse[] available_npcs;
    public IdleAdventureFormationDataResponse formation;
    public IdleAdventureSessionSummaryResponse activeSession;
    public IdleAdventureSessionSummaryResponse active_session;
    public IdleAdventureAfkRewardStateResponse afkReward;
    public IdleAdventureAfkRewardStateResponse afk_reward;
    public IdleAdventureStaminaResponse stamina;

    // Legacy response names are kept for older backend deployments.
    public long player_id;
    public long playerId;
    public int highest_stage;
    public int highestStage;
    public int next_stage;
    public int nextStage;
    public bool has_running_session;
    public bool hasRunningSession;
    public string running_session_key;
    public string runningSessionKey;
    public IdleAdventureModeConfigResponse mode_config;
    public IdleAdventureFormationDataResponse legacy_formation;
    public IdleAdventurePlayerNpcResponse[] available_npc_list;

    public long ResolvedPlayerId => playerId > 0L ? playerId : player_id;
    public int ResolvedHighestStage => highestStage > 0 ? highestStage : highest_stage;
    public int ResolvedNextStage => nextStage > 0 ? nextStage : next_stage;
    public bool ResolvedHasRunningSession => hasRunningSession || has_running_session
        || ResolvedActiveSession != null || !string.IsNullOrEmpty(ResolvedRunningSessionKey);
    public string ResolvedRunningSessionKey => string.IsNullOrEmpty(runningSessionKey)
        ? running_session_key
        : runningSessionKey;
    public IdleAdventureNpcSummaryResponse[] ResolvedAvailableNpcs => availableNpcs ?? available_npcs;
    public IdleAdventureFormationDataResponse ResolvedFormation => formation ?? legacy_formation;
    public IdleAdventureSessionSummaryResponse ResolvedActiveSession => activeSession ?? active_session;
    public IdleAdventureAfkRewardStateResponse ResolvedAfkReward => afkReward ?? afk_reward;
}

[Serializable]
public sealed class IdleAdventureProgressSummaryResponse
{
    public long playerId;
    public long player_id;
    public int highestStage;
    public int nextStage;
    public int lastStage;
    public long totalWins;
    public long totalFailures;
    public string updatedAt;
    public int highest_stage;
    public int next_stage;
    public int last_stage;
    public long total_wins;
    public long total_failures;
    public string updated_at;

    public long ResolvedPlayerId => playerId > 0L ? playerId : player_id;
    public int ResolvedHighestStage => highestStage > 0 ? highestStage : highest_stage;
    public int ResolvedNextStage => nextStage > 0 ? nextStage : next_stage;
    public int ResolvedLastStage => lastStage > 0 ? lastStage : last_stage;
    public long ResolvedTotalWins => totalWins > 0L ? totalWins : total_wins;
    public long ResolvedTotalFailures => totalFailures > 0L ? totalFailures : total_failures;
    public string ResolvedUpdatedAt => string.IsNullOrEmpty(updatedAt) ? updated_at : updatedAt;
}

[Serializable]
public sealed class IdleAdventureNpcSummaryResponse
{
    public long playerNpcId;
    public long npcId;
    public string npcCode;
    public string name;
    public string quality;
    public string icon;
    public string modelKey;
    public int level;
    public string state;
    public string combatAttribute;
    public long baseHealth;
    public long baseAttack;
    public long baseDefense;
    public long speed;
    public long equipmentHealthBonus;
    public long equipmentAttackBonus;
    public long equipmentDefenseBonus;
    public long totalHealth;
    public long totalAttack;
    public long totalDefense;
    public long player_npc_id;
    public long npc_id;
    public string npc_code;
    public string model_key;
    public string combat_attribute;
    public long base_health;
    public long base_attack;
    public long base_defense;
    public long equipment_health_bonus;
    public long equipment_attack_bonus;
    public long equipment_defense_bonus;
    public long total_health;
    public long total_attack;
    public long total_defense;

    public long ResolvedNpcId => npcId > 0L ? npcId : npc_id;
    public long ResolvedPlayerNpcId => playerNpcId > 0L ? playerNpcId : player_npc_id;
    public string ResolvedNpcCode => string.IsNullOrEmpty(npcCode) ? npc_code : npcCode;
    public string ResolvedModelKey => string.IsNullOrEmpty(modelKey) ? model_key : modelKey;
    public string ResolvedCombatAttribute => string.IsNullOrEmpty(combatAttribute) ? combat_attribute : combatAttribute;
    public long ResolvedBaseHealth => baseHealth > 0L ? baseHealth : base_health;
    public long ResolvedBaseAttack => baseAttack > 0L ? baseAttack : base_attack;
    public long ResolvedBaseDefense => baseDefense > 0L ? baseDefense : base_defense;
    public long ResolvedTotalHealth => totalHealth > 0L ? totalHealth : total_health;
    public long ResolvedTotalAttack => totalAttack > 0L ? totalAttack : total_attack;
    public long ResolvedTotalDefense => totalDefense > 0L ? totalDefense : total_defense;
}

[Serializable]
public sealed class IdleAdventureFormationResponseV2
{
    public bool initialized;
    public int deployedCount;
    public int deployed_count;
    public IdleAdventureFormationSlotResponse[] slots;

    public int ResolvedDeployedCount => deployedCount > 0 ? deployedCount : deployed_count;
}

[Serializable]
public sealed class IdleAdventureSessionSummaryResponse
{
    public string sessionKey;
    public int stage;
    public int stageAttempt;
    public int round;
    public string status;
    public string session_key;
    public int stage_attempt;
    public int round_before;
    public int round_after;

    public string ResolvedSessionKey => string.IsNullOrEmpty(sessionKey) ? session_key : sessionKey;
    public int ResolvedStageAttempt => stageAttempt > 0 ? stageAttempt : stage_attempt;
    public int ResolvedRound => round;
}

[Serializable]
public sealed class IdleAdventureModeResponse
{
    public long modeConfigId;
    public long mode_config_id;
    public string configCode;
    public string config_code;
    public int version;
    public int maxPartySize;
    public int max_party_size;
    public int startingPartySize;
    public int starting_party_size;
    public int maxRoundsPerStage;
    public int max_rounds_per_stage;
    public float restraintMultiplier;
    public float restraint_multiplier;
    public int turnIntervalMillis;
    public int turn_interval_millis;
    public int attackAnimationMillis;
    public int attack_animation_millis;
    public int hitAnimationMillis;
    public int hit_animation_millis;

    public long ResolvedModeConfigId => modeConfigId > 0L ? modeConfigId : mode_config_id;
    public string ResolvedConfigCode => string.IsNullOrEmpty(configCode) ? config_code : configCode;
    public int ResolvedMaxPartySize => maxPartySize > 0 ? maxPartySize : max_party_size;
    public int ResolvedStartingPartySize => startingPartySize > 0 ? startingPartySize : starting_party_size;
    public int ResolvedMaxRoundsPerStage => maxRoundsPerStage > 0 ? maxRoundsPerStage : max_rounds_per_stage;
    public float ResolvedRestraintMultiplier => restraintMultiplier != 0f ? restraintMultiplier : restraint_multiplier;
    public int ResolvedTurnIntervalMillis => turnIntervalMillis > 0 ? turnIntervalMillis : turn_interval_millis;
    public int ResolvedAttackAnimationMillis => attackAnimationMillis > 0 ? attackAnimationMillis : attack_animation_millis;
    public int ResolvedHitAnimationMillis => hitAnimationMillis > 0 ? hitAnimationMillis : hit_animation_millis;
}

[Serializable]
public sealed class IdleAdventureModeConfigResponse
{
    public int max_party_size;
    public int maxPartySize;
    public int formation_rows;
    public int formationRows;
    public int formation_columns;
    public int formationColumns;
    public int friendly_skill_slot_count;
    public int friendlySkillSlotCount;
    public int enemy_skill_slot_count;
    public int enemySkillSlotCount;
    public int max_buff_icon_slots;
    public int maxBuffIconSlots;
    public int starting_friendly_count;
    public int startingFriendlyCount;
    public int base_enemy_count;
    public int baseEnemyCount;
    public int add_enemy_every_stages;
    public int addEnemyEveryStages;
    public float turn_interval;
    public float turnInterval;
    public float attack_animation_duration;
    public float attackAnimationDuration;
    public float hit_flash_duration;
    public float hitFlashDuration;
    public float attribute_restraint_multiplier;
    public float attributeRestraintMultiplier;
    public string reward_settlement;
    public string rewardSettlement;
    public IdleAdventureCombatRulesResponse combat_rules;
    public IdleAdventureCombatRulesResponse combatRules;

    public int ResolvedMaxPartySize => maxPartySize > 0 ? maxPartySize : max_party_size;
    public int ResolvedFormationRows => formationRows > 0 ? formationRows : formation_rows;
    public int ResolvedFormationColumns => formationColumns > 0 ? formationColumns : formation_columns;
    public int ResolvedFriendlySkillSlotCount => friendlySkillSlotCount > 0
        ? friendlySkillSlotCount
        : friendly_skill_slot_count;
    public int ResolvedEnemySkillSlotCount => enemySkillSlotCount > 0
        ? enemySkillSlotCount
        : enemy_skill_slot_count;
    public int ResolvedMaxBuffIconSlots => maxBuffIconSlots > 0 ? maxBuffIconSlots : max_buff_icon_slots;
    public int ResolvedStartingFriendlyCount => startingFriendlyCount > 0
        ? startingFriendlyCount
        : starting_friendly_count;
    public int ResolvedBaseEnemyCount => baseEnemyCount > 0 ? baseEnemyCount : base_enemy_count;
    public int ResolvedAddEnemyEveryStages => addEnemyEveryStages > 0
        ? addEnemyEveryStages
        : add_enemy_every_stages;
    public float ResolvedTurnInterval => turnInterval > 0f ? turnInterval : turn_interval;
    public float ResolvedAttackAnimationDuration => attackAnimationDuration > 0f
        ? attackAnimationDuration
        : attack_animation_duration;
    public float ResolvedHitFlashDuration => hitFlashDuration > 0f
        ? hitFlashDuration
        : hit_flash_duration;
    public float ResolvedAttributeRestraintMultiplier => attributeRestraintMultiplier != 0f
        ? attributeRestraintMultiplier
        : attribute_restraint_multiplier;
    public string ResolvedRewardSettlement => string.IsNullOrEmpty(rewardSettlement)
        ? reward_settlement
        : rewardSettlement;
    public IdleAdventureCombatRulesResponse ResolvedCombatRules => combatRules ?? combat_rules;
}

[Serializable]
public sealed class IdleAdventureCombatRulesResponse
{
    public string initiative;
    public bool front_column_first;
    public bool frontColumnFirst;
    public bool prefer_same_row;
    public bool preferSameRow;
    public bool prefer_upper_row_when_same_row_empty;
    public bool preferUpperRowWhenSameRowEmpty;

    public string ResolvedInitiative => initiative;
    public bool ResolvedFrontColumnFirst => frontColumnFirst || front_column_first;
    public bool ResolvedPreferSameRow => preferSameRow || prefer_same_row;
    public bool ResolvedPreferUpperRowWhenSameRowEmpty =>
        preferUpperRowWhenSameRowEmpty || prefer_upper_row_when_same_row_empty;
}

[Serializable]
public sealed class IdleAdventureFormationDataResponse
{
    public int deployed_count;
    public int deployedCount;
    public bool initialized;
    public IdleAdventureFormationSlotResponse[] slots;

    public int ResolvedDeployedCount => deployedCount > 0 ? deployedCount : deployed_count;
}

[Serializable]
public sealed class IdleAdventureFormationResponse
{
    public long player_id;
    public long playerId;
    public int deployed_count;
    public int deployedCount;
    public IdleAdventureFormationSlotResponse[] slots;

    public long ResolvedPlayerId => playerId > 0L ? playerId : player_id;
    public int ResolvedDeployedCount => deployedCount > 0 ? deployedCount : deployed_count;
}

[Serializable]
public sealed class IdleAdventureFormationSlotResponse
{
    public int slot_index;
    public int slotIndex;
    public int column;
    public int row;
    public bool front_column;
    public bool frontColumn;
    public long definition_id;
    public long definitionId;
    public string combatant_name;
    public string combatantName;
    public string quality;

    public int ResolvedSlotIndex => slot_index != 0 ? slot_index : slotIndex;
    public bool ResolvedFrontColumn => frontColumn || front_column;
    public long ResolvedDefinitionId => definitionId > 0L ? definitionId : definition_id;
    public string ResolvedCombatantName => string.IsNullOrEmpty(combatantName)
        ? combatant_name
        : combatantName;
}

[Serializable]
public sealed class IdleAdventurePlayerNpcResponse
{
    public long player_npc_id;
    public long playerNpcId;
    public long npc_id;
    public long npcId;
    public string npc_code;
    public string npcCode;
    public string name;
    public long definition_id;
    public long definitionId;
    public string combatant_key;
    public string combatantKey;
    public string combatant_name;
    public string combatantName;
    public string quality;
    public string icon;
    public string model_key;
    public string modelKey;
    public int level;
    public string state;
    public string side;
    public long max_health;
    public long maxHealth;
    public long attack;
    public long defense;
    public long speed;
    public string attribute_type;
    public string attributeType;
    public string combat_attribute;
    public string combatAttribute;
    public long base_health;
    public long baseHealth;
    public long base_attack;
    public long baseAttack;
    public long base_defense;
    public long baseDefense;
    public long equipment_health_bonus;
    public long equipmentHealthBonus;
    public long equipment_attack_bonus;
    public long equipmentAttackBonus;
    public long equipment_defense_bonus;
    public long equipmentDefenseBonus;
    public long total_health;
    public long totalHealth;
    public long total_attack;
    public long totalAttack;
    public long total_defense;
    public long totalDefense;
    public bool assigned;
    public IdleAdventureSkillSlotResponse[] skill_slots;
    public IdleAdventureSkillSlotResponse[] skillSlots;

    public long ResolvedPlayerNpcId => playerNpcId > 0L ? playerNpcId : player_npc_id;
    public long ResolvedNpcId => npcId > 0L ? npcId : npc_id;
    public string ResolvedNpcCode => string.IsNullOrEmpty(npcCode) ? npc_code : npcCode;
    public long ResolvedDefinitionId
    {
        get
        {
            if (definitionId > 0L) return definitionId;
            if (definition_id > 0L) return definition_id;
            if (npcId > 0L) return npcId;
            return npc_id;
        }
    }
    public string ResolvedCombatantKey
    {
        get
        {
            if (!string.IsNullOrEmpty(combatantKey)) return combatantKey;
            if (!string.IsNullOrEmpty(combatant_key)) return combatant_key;
            return ResolvedNpcCode;
        }
    }
    public string ResolvedCombatantName
    {
        get
        {
            if (!string.IsNullOrEmpty(combatantName)) return combatantName;
            if (!string.IsNullOrEmpty(combatant_name)) return combatant_name;
            return name;
        }
    }
    public string ResolvedModelKey => string.IsNullOrEmpty(modelKey) ? model_key : modelKey;
    public long ResolvedMaxHealth
    {
        get
        {
            if (maxHealth > 0L) return maxHealth;
            if (max_health > 0L) return max_health;
            if (totalHealth > 0L) return totalHealth;
            if (total_health > 0L) return total_health;
            return baseHealth > 0L ? baseHealth : base_health;
        }
    }
    public long ResolvedAttack
    {
        get
        {
            if (attack > 0L) return attack;
            if (totalAttack > 0L) return totalAttack;
            if (total_attack > 0L) return total_attack;
            return baseAttack > 0L ? baseAttack : base_attack;
        }
    }
    public long ResolvedDefense
    {
        get
        {
            if (defense > 0L) return defense;
            if (totalDefense > 0L) return totalDefense;
            if (total_defense > 0L) return total_defense;
            return baseDefense > 0L ? baseDefense : base_defense;
        }
    }
    public long ResolvedSpeed => speed;
    public string ResolvedAttributeType
    {
        get
        {
            if (!string.IsNullOrEmpty(attributeType)) return attributeType;
            if (!string.IsNullOrEmpty(attribute_type)) return attribute_type;
            return string.IsNullOrEmpty(combatAttribute) ? combat_attribute : combatAttribute;
        }
    }
    public IdleAdventureSkillSlotResponse[] ResolvedSkillSlots => skillSlots ?? skill_slots;
}

[Serializable]
public sealed class IdleAdventureSkillDefinitionResponse
{
    public long skill_id;
    public long skillId;
    public string skill_key;
    public string skillKey;
    public string skill_name;
    public string skillName;
    public int cooldown_rounds;
    public int cooldownRounds;
    public float skill_multiplier;
    public float skillMultiplier;
    public string damage_attribute;
    public string damageAttribute;
    public long self_buff_preset_id;
    public long selfBuffPresetId;
    public long target_buff_preset_id;
    public long targetBuffPresetId;

    public long ResolvedSkillId => skillId > 0L ? skillId : skill_id;
    public string ResolvedSkillKey => string.IsNullOrEmpty(skillKey) ? skill_key : skillKey;
    public string ResolvedSkillName => string.IsNullOrEmpty(skillName) ? skill_name : skillName;
    public int ResolvedCooldownRounds => cooldownRounds > 0 ? cooldownRounds : cooldown_rounds;
    public float ResolvedSkillMultiplier => skillMultiplier != 0f ? skillMultiplier : skill_multiplier;
    public string ResolvedDamageAttribute => string.IsNullOrEmpty(damageAttribute)
        ? damage_attribute
        : damageAttribute;
    public long ResolvedSelfBuffPresetId => selfBuffPresetId > 0L
        ? selfBuffPresetId
        : self_buff_preset_id;
    public long ResolvedTargetBuffPresetId => targetBuffPresetId > 0L
        ? targetBuffPresetId
        : target_buff_preset_id;
}

[Serializable]
public sealed class IdleAdventureSkillSlotResponse
{
    public int slot_index;
    public int slotIndex;
    public IdleAdventureSkillDefinitionResponse skill;

    public int ResolvedSlotIndex => slot_index != 0 ? slot_index : slotIndex;
}

[Serializable]
public sealed class IdleAdventureSessionResponse
{
    public string session_key;
    public long player_id;
    public int stage;
    public string stage_name;
    public int stage_attempt;
    public int round;
    public int round_before;
    public int round_after;
    public string status;
    public bool paused;
    public bool stage_choice_pending;
    public int pending_next_stage;
    public IdleAdventureInitiativeEntryResponse[] initiative_order;
    public IdleAdventureBattleActionResponse[] actions;
    public IdleAdventureCombatantStateResponse[] friendlies;
    public IdleAdventureCombatantStateResponse[] enemies;
    public string[] logs;
    public IdleAdventureRewardItemResponse[] pending_rewards;
    public IdleAdventureRewardItemResponse[] random_drops;
    public int[] settled_stages;
    public IdleAdventureRewardItemResponse[] granted_rewards;
    public IdleAdventureInventoryItemResponse[] items;
    public string message;
    public bool idempotent_replay;
}

[Serializable]
public sealed class IdleAdventureCombatantStateResponse
{
    public string runtime_id;
    public long definition_id;
    public string combatant_name;
    public string side;
    public int slot_index;
    public long max_health;
    public long current_health;
    public long attack;
    public long defense;
    public long speed;
    public string attribute_type;
    public long effective_attack;
    public long effective_defense;
    public long effective_speed;
    public bool defeated;
    public IdleAdventureActiveBuffResponse[] active_buffs;
    public IdleAdventureSkillStateResponse[] skill_states;
}

[Serializable]
public sealed class IdleAdventureActiveBuffResponse
{
    public string buff_key;
    public string display_name;
    public string category;
    public string source;
    public string effect_type;
    public int remaining_rounds;
    public long attack_modifier;
    public long defense_modifier;
    public long speed_modifier;
    public float normal_damage_multiplier;
    public float attribute_damage_multiplier;
    public string affected_attribute;
}

[Serializable]
public sealed class IdleAdventureSkillStateResponse
{
    public int slot_index;
    public long skill_id;
    public string skill_name;
    public int cooldown_remaining;
    public bool can_use;
}

[Serializable]
public sealed class IdleAdventureInitiativeEntryResponse
{
    public string runtime_id;
    public string combatant_name;
    public string side;
    public int slot_index;
    public long effective_speed;
}

[Serializable]
public sealed class IdleAdventureBattleActionResponse
{
    public int action_order;
    public int round;
    public string action_type;
    public string actor_runtime_id;
    public string actor_name;
    public string target_runtime_id;
    public string target_name;
    public long skill_id;
    public string skill_name;
    public long damage;
    public long target_health_before;
    public long target_health_after;
    public IdleAdventureDamageFormulaResponse damage_formula;
    public IdleAdventureActiveBuffResponse[] applied_buffs;
    public string[] defeated_runtime_ids;
    public IdleAdventureAnimationResponse animation;
    public string log;
}

[Serializable]
public sealed class IdleAdventureDamageFormulaResponse
{
    public long base_damage;
    public float skill_multiplier;
    public float normal_buff_multiplier;
    public float attribute_buff_multiplier;
    public float field_buff_multiplier;
    public float attribute_restraint_multiplier;
    public float bonus_multiplier;
    public string attacker_attribute;
    public string defender_attribute;
}

[Serializable]
public sealed class IdleAdventureAnimationResponse
{
    public int attack_direction;
    public float attack_duration;
    public float hit_flash_duration;
}

[Serializable]
public sealed class IdleAdventureRewardItemResponse
{
    public string item_key;
    public string itemKey;
    public string display_name;
    public string displayName;
    public string reward_source;
    public string rewardSource;
    public string reward_type;
    public string rewardType;
    public long asset_id;
    public long assetId;
    public string reward_key;
    public string rewardKey;
    public string reward_name;
    public string rewardName;
    public string reward_icon;
    public string rewardIcon;
    public string reward_category;
    public string rewardCategory;
    public long item_id;
    public long itemId;
    public string item_name;
    public string itemName;
    public string grant_scope;
    public string grantScope;
    public bool fallback_applied;
    public bool fallbackApplied;
    public long quantity;

    public string ResolvedItemKey
    {
        get
        {
            if (!string.IsNullOrEmpty(itemKey)) return itemKey;
            if (!string.IsNullOrEmpty(item_key)) return item_key;
            if (!string.IsNullOrEmpty(rewardKey)) return rewardKey;
            return reward_key;
        }
    }

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            if (!string.IsNullOrEmpty(display_name)) return display_name;
            if (!string.IsNullOrEmpty(itemName)) return itemName;
            if (!string.IsNullOrEmpty(item_name)) return item_name;
            if (!string.IsNullOrEmpty(rewardName)) return rewardName;
            return reward_name;
        }
    }

    public string ResolvedRewardType => string.IsNullOrEmpty(rewardType) ? reward_type : rewardType;
    public long ResolvedAssetId => assetId > 0L ? assetId : asset_id;
}

[Serializable]
public sealed class IdleAdventureInventoryItemResponse
{
    public long item_id;
    public long itemId;
    public string item_key;
    public string itemKey;
    public string name;
    public string display_name;
    public string displayName;
    public string item_type;
    public string itemType;
    public string icon;
    public long quantity;

    public long ResolvedItemId => itemId > 0L ? itemId : item_id;
    public string ResolvedItemKey => string.IsNullOrEmpty(itemKey) ? item_key : itemKey;
    public string ResolvedName => string.IsNullOrEmpty(displayName)
        ? (string.IsNullOrEmpty(display_name) ? name : display_name)
        : displayName;
    public string ResolvedItemType => string.IsNullOrEmpty(itemType) ? item_type : itemType;
}

[Serializable]
public sealed class IdleAdventureCollectedItemResponse
{
    public long item_id;
    public long itemId;
    public string item_key;
    public string itemKey;
    public string name;
    public string display_name;
    public string displayName;
    public long quantity;

    public string ResolvedItemKey => string.IsNullOrEmpty(itemKey) ? item_key : itemKey;
    public string ResolvedName => string.IsNullOrEmpty(displayName)
        ? (string.IsNullOrEmpty(display_name) ? name : display_name)
        : displayName;
}

[Serializable]
public sealed class IdleAdventureProgressResponse
{
    public long player_id;
    public long playerId;
    public int highest_stage;
    public int highestStage;
    public int last_stage;
    public int lastStage;
    public string running_session_key;
    public string runningSessionKey;
    public long total_wins;
    public long totalWins;
    public long total_failures;
    public long totalFailures;
    public string updated_at;
    public string updatedAt;

    public int ResolvedHighestStage => highestStage > 0 ? highestStage : highest_stage;
    public int ResolvedLastStage => lastStage > 0 ? lastStage : last_stage;
    public string ResolvedRunningSessionKey => string.IsNullOrEmpty(runningSessionKey) ? running_session_key : runningSessionKey;
}

[Serializable]
public sealed class IdleAdventureBuffAppliedResponse
{
    public IdleAdventureCombatantStateResponse target;
    public string log;
}

[Serializable]
public sealed class LandSceneResponse
{
    public int sceneId;
    public int scene_id;
    public string sceneKey;
    public string scene_key;
    public string sceneName;
    public string scene_name;
    public int sortOrder;
    public int sort_order;
    public int[] requiredLevels;
    public int[] required_levels;
    public int paidSlotGoldCost;
    public int paid_slot_gold_cost;
    public string backgroundKey;
    public string background_key;
    public string terrainKey;
    public string terrain_key;
    public string bgmKey;
    public string bgm_key;
    public string icon;
    public string status;
    public bool current;
    public bool canEnter;
    public bool can_enter;
    public int unlockedSlotCount;
    public int unlocked_slot_count;
    public int totalSlotCount;
    public int total_slot_count;
    public bool completed;
    public int nextSceneId;
    public int next_scene_id;
    public string unlockedTime;
    public string unlocked_time;
    public string completedTime;
    public string completed_time;
    public string lastEnteredTime;
    public string last_entered_time;
    public long configVersion;
    public long config_version;
    public long landPolicyRevision;
    public long land_policy_revision;

    public int ResolvedSceneId => sceneId > 0 ? sceneId : scene_id;
    public string ResolvedSceneKey => string.IsNullOrEmpty(sceneKey) ? scene_key : sceneKey;
    public string ResolvedSceneName => string.IsNullOrEmpty(sceneName) ? scene_name : sceneName;
    public bool IsEnterable => canEnter || can_enter;
    public long ResolvedConfigVersion => configVersion > 0L ? configVersion : config_version;
    public long ResolvedPolicyRevision => landPolicyRevision > 0L ? landPolicyRevision : land_policy_revision;
}

[Serializable]
public sealed class SceneSwitchRequest
{
    public int playerId;
    public int sceneId;
}

[Serializable]
public sealed class SceneSwitchResponse
{
    public int sceneId;
    public string sceneKey;
    public string sceneName;
    public string status;
    public long configVersion;
    public long landPolicyRevision;
    public long land_policy_revision;

    public long ResolvedPolicyRevision => landPolicyRevision > 0L ? landPolicyRevision : land_policy_revision;
}

[Serializable]
public sealed class IdleAdventureStaminaResponse
{
    public bool enabled;
    public bool enabledValue;
    public long current_stamina;
    public long currentStamina;
    public long max_stamina;
    public int maxStamina;
    public bool over_cap;
    public bool overCap;
    public long overflow_amount;
    public long overflowAmount;
    public int recovery_interval_seconds;
    public int recoveryIntervalSeconds;
    public string next_recovery_at;
    public string nextRecoveryAt;
    public long seconds_to_next_recovery;
    public long secondsToNextRecovery;
    public long seconds_to_full;
    public long secondsToFull;
    public string server_time;
    public string serverTime;
    public long config_version;
    public int configVersion;
    public long policy_mode_config_id;
    public long policyModeConfigId;
    public int policy_version;
    public int policyVersion;

    public long CurrentStamina => Math.Max(0L, currentStamina > 0L ? currentStamina : current_stamina);
    public long MaxStamina => Math.Max(0L, maxStamina > 0 ? maxStamina : max_stamina);
    public int SecondsToNextRecovery => (int)Math.Min(
        int.MaxValue,
        Math.Max(0L, secondsToNextRecovery > 0L ? secondsToNextRecovery : seconds_to_next_recovery));
    public bool ResolvedEnabled => enabledValue || enabled;
    public long ResolvedOverflowAmount => Math.Max(0L, overflowAmount > 0L ? overflowAmount : overflow_amount);
    public int ResolvedRecoveryIntervalSeconds => recoveryIntervalSeconds > 0
        ? recoveryIntervalSeconds
        : recovery_interval_seconds;
    public string ResolvedNextRecoveryAt => string.IsNullOrEmpty(nextRecoveryAt)
        ? next_recovery_at
        : nextRecoveryAt;
    public long ResolvedSecondsToFull => Math.Max(0L, secondsToFull > 0L ? secondsToFull : seconds_to_full);
    public string ResolvedServerTime => string.IsNullOrEmpty(serverTime) ? server_time : serverTime;
    public long ResolvedPolicyModeConfigId => policyModeConfigId > 0L
        ? policyModeConfigId
        : policy_mode_config_id;
    public int ResolvedPolicyVersion
    {
        get
        {
            if (policyVersion > 0) return policyVersion;
            if (policy_version > 0) return policy_version;
            if (configVersion > 0) return configVersion;
            return config_version > 0L
                ? (int)Math.Min(int.MaxValue, config_version)
                : 0;
        }
    }
}

[Serializable]
public sealed class IdleAdventureAccelerateResponse
{
    public long player_id;
    public long playerId;
    public long mode_config_id;
    public long modeConfigId;
    public int mode_config_version;
    public int modeConfigVersion;
    public int highest_stage;
    public int highestStage;
    public long stamina_before;
    public long staminaBefore;
    public long stamina_cost;
    public int staminaCost;
    public long stamina_after;
    public long staminaAfter;
    public int acceleration_cycles;
    public int accelerationCycles;
    public IdleAdventureRewardItemResponse[] natural_rewards;
    public IdleAdventureRewardItemResponse[] naturalRewards;
    public IdleAdventureRewardItemResponse[] acceleration_rewards;
    public IdleAdventureRewardItemResponse[] accelerationRewards;
    public IdleAdventureRewardItemResponse[] total_rewards;
    public IdleAdventureRewardItemResponse[] totalRewards;
    public IdleAdventureStaminaResponse stamina;
    public IdleAdventureProgressResponse progress;
    public IdleAdventureAfkRewardStateResponse state;
    public IdleAdventureInventoryItemResponse[] items;
    public bool idempotent_replay;
    public bool idempotentReplay;

    public long ResolvedStaminaBefore => staminaBefore > 0L ? staminaBefore : stamina_before;
    public long ResolvedStaminaCost => staminaCost > 0 ? staminaCost : stamina_cost;
    public long ResolvedStaminaAfter
    {
        get
        {
            if (staminaAfter > 0L) return staminaAfter;
            if (stamina_after > 0L) return stamina_after;
            long before = ResolvedStaminaBefore;
            long cost = ResolvedStaminaCost;
            return before >= cost && cost > 0L ? before - cost : 0L;
        }
    }
    public int ResolvedAccelerationCycles => accelerationCycles > 0
        ? accelerationCycles
        : acceleration_cycles;
    public bool ResolvedIdempotentReplay => idempotentReplay || idempotent_replay;
}

[Serializable]
public sealed class IdleAdventureStageClearResponse
{
    public long player_id;
    public long playerId;
    public int highest_stage;
    public int highestStage;
    public int last_stage;
    public int lastStage;
    public string running_session_key;
    public string runningSessionKey;
    public long total_wins;
    public long totalWins;
    public long total_failures;
    public long totalFailures;
    public string updated_at;
    public string updatedAt;
    public IdleAdventureProgressResponse progress;
    public IdleAdventureRewardItemResponse[] random_drops;
    public IdleAdventureRewardItemResponse[] randomDrops;
    public IdleAdventureRewardItemResponse[] granted_rewards;
    public IdleAdventureRewardItemResponse[] grantedRewards;
    public IdleAdventureInventoryItemResponse[] items;
    public IdleAdventureStaminaResponse stamina;
    public bool idempotent_replay;
    public bool idempotentReplay;

    public IdleAdventureProgressResponse ResolvedProgress
    {
        get
        {
            if (progress != null) return progress;
            if (playerId <= 0L && player_id <= 0L
                && highestStage <= 0 && highest_stage <= 0
                && lastStage <= 0 && last_stage <= 0
                && totalWins <= 0L && total_wins <= 0L
                && totalFailures <= 0L && total_failures <= 0L
                && string.IsNullOrEmpty(runningSessionKey)
                && string.IsNullOrEmpty(running_session_key)
                && string.IsNullOrEmpty(updatedAt)
                && string.IsNullOrEmpty(updated_at))
            {
                return null;
            }

            return new IdleAdventureProgressResponse
            {
                player_id = player_id,
                playerId = playerId,
                highest_stage = highest_stage,
                highestStage = highestStage,
                last_stage = last_stage,
                lastStage = lastStage,
                running_session_key = running_session_key,
                runningSessionKey = runningSessionKey,
                total_wins = total_wins,
                totalWins = totalWins,
                total_failures = total_failures,
                totalFailures = totalFailures,
                updated_at = updated_at,
                updatedAt = updatedAt
            };
        }
    }
}

[Serializable]
public sealed class ProductionFuelResponse
{
    public long itemId;
    public string itemKey;
    public string itemName;
    public string itemType;
    public string icon;
    public long workSecondsPerItem;
    public long ownedQuantity;
    public long maxConsumableQuantity;
    public long configVersion;
}

[Serializable]
public sealed class ProductionWorkStateResponse
{
    public long currentWorkClockSeconds;
    public long remainingWorkSeconds;
    public long maxWorkSeconds;
    public string workUntil;
    public string anchorTime;
    public string serverTime;
    public string usageDate;
    public long dailyGainedWorkSeconds;
    public long dailyRemainingGainSeconds;
    public int activeBuildingCount;
    public bool productionRunning;
    public long configVersion;
    public ProductionFuelResponse[] fuels;
}

[Serializable]
public sealed class ProductionWorkTimeRequest
{
    public long itemId;
    public long quantity;
    public long expectedConfigVersion;
}

[Serializable]
public sealed class ProductionWorkTimePreviewResponse
{
    public long itemId;
    public string itemKey;
    public string itemName;
    public long requestedQuantity;
    public long ownedQuantity;
    public long workSecondsPerItem;
    public long requestedWorkSeconds;
    public long currentRemainingWorkSeconds;
    public long afterRemainingWorkSeconds;
    public long maxWorkSeconds;
    public long dailyRemainingGainSeconds;
    public long maxConsumableQuantity;
    public bool canConsume;
    public string unavailableReason;
    public long configVersion;
}

[Serializable]
public sealed class ProductionWorkTimeConsumeResponse
{
    public string idempotencyKey;
    public long itemId;
    public string itemKey;
    public string itemName;
    public long consumedQuantity;
    public long grantedWorkSeconds;
    public long itemRemainingQuantity;
    public ProductionWorkStateResponse workState;
}

[Serializable]
public sealed class ProductionRecipeResponse
{
    public long recipeId;
    public string recipeCode;
    public string displayName;
    public int cycleSeconds;
    public long storageCapacity;
    public bool defaultRecipe;
    public bool active;
    public long configVersion;
    public ProductionRecipeLineResponse[] inputs;
    public ProductionRecipeLineResponse[] outputs;
}

[Serializable]
public sealed class ProductionRecipeLineResponse
{
    public long itemId;
    public string itemKey;
    public string itemName;
    public string itemType;
    public string icon;
    public long amount;
    public int probabilityBasisPoints;
}

[Serializable]
public sealed class ProductionRecipesResponse
{
    public long playerBuildingId;
    public long activeRecipeId;
    public long activeRecipeVersion;
    public ProductionRecipeResponse[] recipes;
}

[Serializable]
public sealed class ProductionRecipeSwitchRequest
{
    public long playerBuildingId;
    public long recipeId;
    public long expectedRecipeVersion;
}

[Serializable]
public sealed class ProductionRecipeSwitchResponse
{
    public string idempotencyKey;
    public long playerBuildingId;
    public long previousRecipeId;
    public ProductionRecipeResponse activeRecipe;
    public ProductionResponse[] productions;
}

[Serializable]
public sealed class AutoUpgradeBlockedRequirementResponse
{
    public string requirementType;
    public string itemKey;
    public long itemId;
    public int requiredAmount;
    public int availableAmount;
    public string message;
}

[Serializable]
public sealed class AutoUpgradeStepResponse
{
    public int targetLevel;
    public int beforeHealth;
    public int afterHealth;
    public int beforeAttack;
    public int afterAttack;
    public int beforeDefense;
    public int afterDefense;
    public int beforeSpeed;
    public int afterSpeed;
    public int health;
    public int attack;
    public int defense;
    public int speed;
    public int goldCost;
    public AutoUpgradeBlockedRequirementResponse[] requirements;
}

[Serializable]
public sealed class AutoUpgradePreviewResponse
{
    public long playerNpcId;
    public long playerEquipmentId;
    public int currentLevel;
    public int requestedTargetLevel;
    public int payableTargetLevel;
    public int upgradeLevelCount;
    public bool targetReached;
    public string outcome;
    public string stopReason;
    public int blockedLevel;
    public AutoUpgradeBlockedRequirementResponse[] blockedRequirements;
    public string planFingerprint;
    public AutoUpgradeStepResponse[] upgradeSteps;
}

[Serializable]
public sealed class AutoUpgradeRequest
{
    public long playerNpcId;
    public long playerEquipmentId;
    public int expectedCurrentLevel;
    public int targetLevel;
    public string planFingerprint;
}

[Serializable]
public sealed class AutoUpgradeResponse
{
    public long playerNpcId;
    public long playerEquipmentId;
    public int fromLevel;
    public int reachedLevel;
    public int level;
    public bool targetReached;
    public string outcome;
    public AutoUpgradeStepResponse[] upgradeSteps;
    public InventoryItemResponse[] items;
    public NpcResponse[] npcs;
    public long remainingGold;
    public bool idempotent_replay;
}

[Serializable]
public sealed class OnboardingCurrentResponse
{
    public bool available;
    public int contractVersion;
    public long minimumClientVersionCode;
    public string guideCode;
    public int guideVersion;
    public long instanceId;
    public long stateVersion;
    public string status;
    public bool allowSkip;
    public OnboardingStepResponse currentStep;
    public string serverTime;
}

[Serializable]
public sealed class OnboardingStepResponse
{
    public string stepCode;
    public int sequence;
    public string sceneCode;
    public string presentationType;
    public string uiTargetKey;
    public string inputMode;
    public string actionKey;
    public string completionType;
    public string title;
    public string content;
    public OnboardingPresentationParamsResponse presentationParams;
    public OnboardingBusinessContextResponse businessContext;
}

[Serializable]
public sealed class OnboardingPresentationParamsResponse
{
    public string highlightColor;
    public string arrowDirection;
    public string animation;
}

[Serializable]
public sealed class OnboardingBusinessContextResponse
{
    public string eventType;
    public int landConfigId;
    public int buildingConfigId;
    public int playerLandId;
    public int playerBuildingId;
}

[Serializable]
public sealed class OnboardingMutationRequest
{
    public long expectedGuideVersion;
    public long expectedStateVersion;
}

[Serializable]
public sealed class OnboardingMutationResponse
{
    public bool available;
    public int guideVersion;
    public long stateVersion;
    public string status;
    public OnboardingStepResponse currentStep;
    public string serverTime;
}
