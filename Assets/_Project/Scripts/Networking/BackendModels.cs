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
    public readonly string Message;
    public readonly T Data;

    public BackendResult(bool success, int httpStatus, string message, T data)
    {
        Success = success;
        HttpStatus = httpStatus;
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
}

[Serializable]
public sealed class PlaceLandRequest
{
    public int playerId;
    public int landId;
    public int positionX;
    public int positionY;
}

[Serializable]
public sealed class BuildBuildingRequest
{
    public int playerId;
    public int buildingId;
    public int landId;
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
    public int targetLandId;
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
    public int id;
    public string username;
    public string nickname;
    public int level;
    public long totalExperience;
    public int levelCurveRevision;
    public int currentLevelExperience;
    public int requiredExperienceForNextLevel;
    public int experienceProgressBps;
    public bool maxLevel;
    public bool economyFrozen;
    public int gold;
    public int wood;
    public int stone;
    public int iron;
    public int diamond;
    public InventoryItemResponse[] items;
}

[Serializable]
public sealed class InventoryItemResponse
{
    public int itemId;
    public string itemKey;
    public string name;
    public string itemType;
    public string icon;
    public int quantity;
}

[Serializable]
public sealed class MapDataResponse
{
    public int playerId;
    public int width;
    public int height;
    public LandTileResponse[] lands;
    public BuildingResponse[] buildings;
}

[Serializable]
public sealed class LandTileResponse
{
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
    public int landId;
    public string landType;
    public string name;
    public string cost;
    public int unlockLevel;
    public string allowBuildType;
    public int goldCost;
    public int woodCost;
    public int stoneCost;
    public int ironCost;
    public bool canPlace;
    public string cannotPlaceReason;
}

[Serializable]
public sealed class BuildingConfigResponse
{
    public int id;
    public int buildingId;
    public string code;
    public string name;
    public string description;
    public string needLand;
    public string cost;
    public string produce;
    public string buildType;
    public string type;
    public int level;
    public int goldCost;
    public int woodCost;
    public int stoneCost;
    public int ironCost;
    public int buildTime;
    public string modelKey;
    public string icon;
    public int requiredLevel;
    public int prerequisiteBuildingId;
    public bool canBuild;
    public string cannotBuildReason;
}

[Serializable]
public sealed class BuildingConditionResponse
{
    public int buildingId;
    public bool canBuild;
    public bool levelMet;
    public bool resourceMet;
    public bool prerequisiteMet;
    public bool landMet;
    public string message;
    public int goldNeeded;
    public int woodNeeded;
    public int stoneNeeded;
    public int ironNeeded;
    public int requiredLevel;
    public string missingPrerequisite;
}

[Serializable]
public sealed class BuildingResponse
{
    public int playerBuildingId;
    public int buildingId;
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
    public int remainingTime;
    public float progress;
    public ExperienceGrantResponse experienceGrant;
}

[Serializable]
public sealed class ExperienceGrantResponse
{
    public int playerId;
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
    public int currentLevelExperience;
    public int requiredExperienceForNextLevel;
    public int experienceProgressBps;
    public int curveRevision;
}

[Serializable]
public sealed class ProductionResponse
{
    public int playerBuildingId;
    public int landId;
    public int buildingId;
    public string buildingName;
    public int level;
    public string status;
    public int itemId;
    public string itemKey;
    public string itemName;
    public string icon;
    public int baseProduceAmount;
    public int produceAmount;
    public float npcProduceBonusRate;
    public SettledNpcResponse settledNpc;
    public int produceIntervalSeconds;
    public int storedAmount;
    public int storageCapacity;
    public bool canCollect;
    public string lastSettleTime;
    public string nextProduceTime;
    public int remainingSeconds;
}

[Serializable]
public sealed class NpcConfigResponse
{
    public int npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string modelKey;
    public float produceBonusRate;
}

[Serializable]
public sealed class NpcResponse
{
    public int playerNpcId;
    public int npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string modelKey;
    public int level;
    public string state;
    public int settledPlayerBuildingId;
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
    public CollectedItemResponse[] collectedItems;
    public ProductionResponse[] productions;
    public InventoryItemResponse[] items;
}

[Serializable]
public sealed class CollectedItemResponse
{
    public int itemId;
    public string itemKey;
    public string name;
    public int quantity;
}

[Serializable]
public sealed class IdleAdventureAfkRewardStateResponse
{
    public long player_id;
    public string item_key;
    public string display_name;
    public int base_quantity_per_cycle;
    public int quantity_per_highest_stage;
    public int quantity_per_cycle;
    public int production_interval_seconds;
    public int max_stored_quantity;
    public int stored_quantity;
    public int highest_stage;
    public long server_unix_seconds;
    public long last_settled_server_unix_seconds;

    public int playerId => (int)player_id;
    public string itemKey => item_key;
    public string displayName => display_name;
    public int baseQuantityPerCycle => base_quantity_per_cycle;
    public int quantityPerHighestStage => quantity_per_highest_stage;
    public int quantityPerCycle => quantity_per_cycle;
    public int productionIntervalSeconds => production_interval_seconds;
    public int maxStoredQuantity => max_stored_quantity;
    public int storedQuantity => stored_quantity;
    public int highestClearedStage => highest_stage;
    public long serverUnixSeconds => server_unix_seconds;
    public long lastSettledServerUnixSeconds => last_settled_server_unix_seconds;
}

[Serializable]
public sealed class IdleAdventureAfkRewardCollectResponse
{
    public long player_id;
    public IdleAdventureCollectedItemResponse collected_item;
    public IdleAdventureInventoryItemResponse[] items;
    public IdleAdventureAfkRewardStateResponse state;
    public bool idempotent_replay;

    public int playerId => (int)player_id;
    public IdleAdventureCollectedItemResponse collectedItem => collected_item;
    public bool idempotentReplay => idempotent_replay;
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
    public long player_id;
    public int highest_stage;
    public int next_stage;
    public bool has_running_session;
    public string running_session_key;
    public IdleAdventureModeConfigResponse mode_config;
    public IdleAdventureFormationDataResponse formation;
    public IdleAdventurePlayerNpcResponse[] available_npc_list;
}

[Serializable]
public sealed class IdleAdventureModeConfigResponse
{
    public int max_party_size;
    public int formation_rows;
    public int formation_columns;
    public int friendly_skill_slot_count;
    public int enemy_skill_slot_count;
    public int max_buff_icon_slots;
    public int starting_friendly_count;
    public int base_enemy_count;
    public int add_enemy_every_stages;
    public float turn_interval;
    public float attack_animation_duration;
    public float hit_flash_duration;
    public float attribute_restraint_multiplier;
    public string reward_settlement;
    public IdleAdventureCombatRulesResponse combat_rules;
}

[Serializable]
public sealed class IdleAdventureCombatRulesResponse
{
    public string initiative;
    public bool front_column_first;
    public bool prefer_same_row;
    public bool prefer_upper_row_when_same_row_empty;
}

[Serializable]
public sealed class IdleAdventureFormationDataResponse
{
    public int deployed_count;
    public IdleAdventureFormationSlotResponse[] slots;
}

[Serializable]
public sealed class IdleAdventureFormationResponse
{
    public long player_id;
    public int deployed_count;
    public IdleAdventureFormationSlotResponse[] slots;
}

[Serializable]
public sealed class IdleAdventureFormationSlotResponse
{
    public int slot_index;
    public int column;
    public int row;
    public bool front_column;
    public long definition_id;
    public string combatant_name;
}

[Serializable]
public sealed class IdleAdventurePlayerNpcResponse
{
    public long definition_id;
    public string combatant_key;
    public string combatant_name;
    public string side;
    public long max_health;
    public long attack;
    public long defense;
    public long speed;
    public string attribute_type;
    public bool assigned;
    public IdleAdventureSkillSlotResponse[] skill_slots;
}

[Serializable]
public sealed class IdleAdventureSkillDefinitionResponse
{
    public long skill_id;
    public string skill_key;
    public string skill_name;
    public int cooldown_rounds;
    public float skill_multiplier;
    public string damage_attribute;
    public long self_buff_preset_id;
    public long target_buff_preset_id;
}

[Serializable]
public sealed class IdleAdventureSkillSlotResponse
{
    public int slot_index;
    public IdleAdventureSkillDefinitionResponse skill;
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
    public string display_name;
    public long quantity;
}

[Serializable]
public sealed class IdleAdventureInventoryItemResponse
{
    public long item_id;
    public string item_key;
    public string name;
    public string display_name;
    public string item_type;
    public string icon;
    public long quantity;
}

[Serializable]
public sealed class IdleAdventureCollectedItemResponse
{
    public long item_id;
    public string item_key;
    public string display_name;
    public long quantity;

    public string itemKey => item_key;
    public string name => display_name;
}

[Serializable]
public sealed class IdleAdventureProgressResponse
{
    public long player_id;
    public int highest_stage;
    public int last_stage;
    public string running_session_key;
    public long total_wins;
    public long total_failures;
    public string updated_at;
}

[Serializable]
public sealed class IdleAdventureBuffAppliedResponse
{
    public IdleAdventureCombatantStateResponse target;
    public string log;
}
