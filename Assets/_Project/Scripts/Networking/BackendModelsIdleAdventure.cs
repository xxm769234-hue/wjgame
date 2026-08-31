using System;

[Serializable]
public sealed class IdleAdventureBuffPresetResponse
{
    public long buff_preset_id;
    public long buffPresetId;
    public string buff_key;
    public string buffKey;
    public string display_name;
    public string displayName;
    public string category;
    public string source;
    public string effect_type;
    public string effectType;
    public int duration_rounds;
    public int durationRounds;
    public long attack_modifier;
    public long attackModifier;
    public long defense_modifier;
    public long defenseModifier;
    public long speed_modifier;
    public long speedModifier;
    public float normal_damage_multiplier;
    public float normalDamageMultiplier;
    public float attribute_damage_multiplier;
    public float attributeDamageMultiplier;
    public string affected_attribute;
    public string affectedAttribute;

    public long ResolvedBuffPresetId => buffPresetId > 0L ? buffPresetId : buff_preset_id;
    public string ResolvedBuffKey => string.IsNullOrEmpty(buffKey) ? buff_key : buffKey;
    public string ResolvedDisplayName => string.IsNullOrEmpty(displayName) ? display_name : displayName;
}

[Serializable]
public sealed class IdleAdventureFieldBuffResponse
{
    public long field_buff_id;
    public long fieldBuffId;
    public int stage;
    public string field_buff_key;
    public string fieldBuffKey;
    public string display_name;
    public string displayName;
    public string category;
    public string effect_type;
    public string effectType;
    public string target_side;
    public string targetSide;
    public string affected_attribute;
    public string affectedAttribute;
    public float field_damage_multiplier;
    public float fieldDamageMultiplier;

    public long ResolvedFieldBuffId => fieldBuffId > 0L ? fieldBuffId : field_buff_id;
    public string ResolvedFieldBuffKey => string.IsNullOrEmpty(fieldBuffKey) ? field_buff_key : fieldBuffKey;
    public string ResolvedDisplayName => string.IsNullOrEmpty(displayName) ? display_name : displayName;
}

[Serializable]
public sealed class IdleAdventureAttributeRestraintResponse
{
    public string attacker_attribute;
    public string attackerAttribute;
    public string defender_attribute;
    public string defenderAttribute;

    public string ResolvedAttackerAttribute => string.IsNullOrEmpty(attackerAttribute) ? attacker_attribute : attackerAttribute;
    public string ResolvedDefenderAttribute => string.IsNullOrEmpty(defenderAttribute) ? defender_attribute : defenderAttribute;
}

[Serializable]
public sealed class IdleAdventureAttributeRestraintConfigResponse
{
    public float restraint_multiplier;
    public float restraintMultiplier;
    public IdleAdventureAttributeRestraintResponse[] relations;

    public float ResolvedRestraintMultiplier => restraintMultiplier != 0f ? restraintMultiplier : restraint_multiplier;
}

[Serializable]
public sealed class IdleAdventureStageRewardResponse
{
    public int stage;
    public IdleAdventureRewardItemResponse[] rewards;
}

[Serializable]
public sealed class IdleAdventureEnemyResponse
{
    public long definition_id;
    public long definitionId;
    public string combatant_key;
    public string combatantKey;
    public string combatant_name;
    public string combatantName;
    public string icon;
    public string model_key;
    public string modelKey;
    public int level;
    public string side;
    public long max_health;
    public long maxHealth;
    public long attack;
    public long attackValue;
    public long attack_value;
    public long defense;
    public long defenseValue;
    public long defense_value;
    public long speed;
    public long speedValue;
    public long speed_value;
    public string attribute_type;
    public string attributeType;
    public IdleAdventureSkillSlotResponse[] skill_slots;
    public IdleAdventureSkillSlotResponse[] skillSlots;

    public long ResolvedDefinitionId => definitionId > 0L ? definitionId : definition_id;
    public string ResolvedCombatantKey => string.IsNullOrEmpty(combatantKey) ? combatant_key : combatantKey;
    public string ResolvedCombatantName => string.IsNullOrEmpty(combatantName) ? combatant_name : combatantName;
    public string ResolvedModelKey => string.IsNullOrEmpty(modelKey) ? model_key : modelKey;
    public long ResolvedMaxHealth => maxHealth > 0L ? maxHealth : max_health;
    public long ResolvedAttack => attack > 0L ? attack : attackValue > 0L ? attackValue : attack_value;
    public long ResolvedDefense => defense > 0L ? defense : defenseValue > 0L ? defenseValue : defense_value;
    public long ResolvedSpeed => speed > 0L ? speed : speedValue > 0L ? speedValue : speed_value;
    public string ResolvedAttributeType => string.IsNullOrEmpty(attributeType) ? attribute_type : attributeType;
    public IdleAdventureSkillSlotResponse[] ResolvedSkillSlots => skillSlots ?? skill_slots;
}

[Serializable]
public sealed class IdleAdventureEnemySlotResponse
{
    public int slot_index;
    public int slotIndex;
    public int column;
    public int row;
    public bool front_column;
    public bool frontColumn;
    public IdleAdventureEnemyResponse enemy;

    public int ResolvedSlotIndex => slot_index != 0 ? slot_index : slotIndex;
    public bool ResolvedFrontColumn => frontColumn || front_column;
}

[Serializable]
public sealed class IdleAdventureStageEnemiesResponse
{
    public int stage;
    public string stage_name;
    public string stageName;
    public int enemy_count;
    public int enemyCount;
    public IdleAdventureEnemySlotResponse[] slots;

    public int ResolvedEnemyCount => enemyCount > 0 ? enemyCount : enemy_count;
    public string ResolvedStageName => string.IsNullOrEmpty(stageName) ? stage_name : stageName;
}

[Serializable]
public sealed class IdleAdventureReferenceDataResponse
{
    public IdleAdventureModeConfigResponse config;
    public IdleAdventureBuffPresetResponse[] buff_presets;
    public IdleAdventureSkillDefinitionResponse[] skills;
    public IdleAdventureFieldBuffResponse[] field_buffs;
    public IdleAdventureAttributeRestraintConfigResponse attribute_restraints;
    public IdleAdventureStageRewardResponse[] stage_rewards;
    public IdleAdventurePlayerNpcResponse[] player_npcs;
    public IdleAdventureStageEnemiesResponse stage_enemies;
}
