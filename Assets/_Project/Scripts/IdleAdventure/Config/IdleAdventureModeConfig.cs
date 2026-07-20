using System;
using UnityEngine;

[Serializable]
public sealed class IdleAdventureCombatantDefinition
{
    [SerializeField] private string combatantName = "Combatant";
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int attack = 10;
    [SerializeField, Min(0)] private int defense = 0;
    [SerializeField, Min(1)] private int speed = 10;
    [SerializeField] private IdleAdventureDamageAttribute attributeType = IdleAdventureDamageAttribute.Physical;
    [SerializeField] private IdleAdventureSkillDefinition[] skillSlots = new IdleAdventureSkillDefinition[0];

    public IdleAdventureCombatantDefinition()
    {
    }

    public IdleAdventureCombatantDefinition(string combatantName, int maxHealth, int attack, int defense, int speed, IdleAdventureSkillDefinition[] skillSlots = null)
        : this(combatantName, maxHealth, attack, defense, speed, IdleAdventureDamageAttribute.Physical, skillSlots)
    {
    }

    public IdleAdventureCombatantDefinition(string combatantName, int maxHealth, int attack, int defense, int speed, IdleAdventureDamageAttribute attributeType, IdleAdventureSkillDefinition[] skillSlots = null)
    {
        this.combatantName = combatantName;
        this.maxHealth = maxHealth;
        this.attack = attack;
        this.defense = defense;
        this.speed = speed;
        this.attributeType = attributeType;
        this.skillSlots = skillSlots ?? new IdleAdventureSkillDefinition[0];
    }

    public string ResolveName(string fallbackName)
    {
        return string.IsNullOrEmpty(combatantName) ? fallbackName : combatantName;
    }

    public int MaxHealth => maxHealth;
    public int Attack => attack;
    public int Defense => defense;
    public int Speed => speed;
    public IdleAdventureDamageAttribute AttributeType => attributeType == IdleAdventureDamageAttribute.None ? IdleAdventureDamageAttribute.Physical : attributeType;
    public IdleAdventureSkillDefinition[] SkillSlots => skillSlots;
}

[Serializable]
public sealed class IdleAdventureBuffDefinition
{
    [SerializeField] private string buffId = "battle_buff";
    [SerializeField] private string displayName = "Battle Buff";
    [SerializeField] private IdleAdventureBuffCategory category = IdleAdventureBuffCategory.Benefit;
    [SerializeField] private IdleAdventureBuffSource source = IdleAdventureBuffSource.CharacterSkill;
    [SerializeField] private IdleAdventureBuffEffectType effectType = IdleAdventureBuffEffectType.NormalDamage;
    [SerializeField, Min(1)] private int durationRounds = 3;
    [SerializeField] private int attackModifier;
    [SerializeField] private int defenseModifier;
    [SerializeField] private int speedModifier;
    [SerializeField] private float normalDamageMultiplier;
    [SerializeField] private float attributeDamageMultiplier;
    [SerializeField] private IdleAdventureDamageAttribute affectedAttribute = IdleAdventureDamageAttribute.None;

    public IdleAdventureBuffDefinition()
    {
    }

    public IdleAdventureBuffDefinition(string buffId, string displayName, IdleAdventureBuffCategory category, IdleAdventureBuffSource source, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
        : this(buffId, displayName, category, source, IdleAdventureBuffEffectType.NormalDamage, durationRounds, attackModifier, defenseModifier, speedModifier, 0f, 0f, IdleAdventureDamageAttribute.None)
    {
    }

    public IdleAdventureBuffDefinition(string buffId, string displayName, IdleAdventureBuffCategory category, IdleAdventureBuffSource source, IdleAdventureBuffEffectType effectType, int durationRounds, int attackModifier, int defenseModifier, int speedModifier, float normalDamageMultiplier, float attributeDamageMultiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        this.buffId = buffId;
        this.displayName = displayName;
        this.category = category;
        this.source = source;
        this.effectType = effectType;
        this.durationRounds = durationRounds;
        this.attackModifier = attackModifier;
        this.defenseModifier = defenseModifier;
        this.speedModifier = speedModifier;
        this.normalDamageMultiplier = normalDamageMultiplier;
        this.attributeDamageMultiplier = attributeDamageMultiplier;
        this.affectedAttribute = affectedAttribute;
    }

    public IdleAdventureBattleBuff CreateRuntimeBuff()
    {
        return IdleAdventureBattleBuff.Create(buffId, displayName, category, source, effectType, durationRounds, attackModifier, defenseModifier, speedModifier, SignedMultiplier(normalDamageMultiplier), SignedMultiplier(attributeDamageMultiplier), affectedAttribute);
    }

    private float SignedMultiplier(float value)
    {
        return category == IdleAdventureBuffCategory.Detriment ? -Mathf.Abs(value) : Mathf.Abs(value);
    }
}

[Serializable]
public sealed class IdleAdventureFieldBuffDefinition
{
    [SerializeField] private string buffId = "field_buff";
    [SerializeField] private string displayName = "Field Buff";
    [SerializeField] private IdleAdventureBuffCategory category = IdleAdventureBuffCategory.Benefit;
    [SerializeField] private bool affectFriendlies = true;
    [SerializeField] private bool affectEnemies = true;
    [SerializeField] private IdleAdventureDamageAttribute affectedAttribute = IdleAdventureDamageAttribute.Physical;
    [SerializeField] private float fieldDamageMultiplier = 0.15f;

    public IdleAdventureFieldBuffDefinition()
    {
    }

    public IdleAdventureFieldBuffDefinition(string buffId, string displayName, IdleAdventureBuffCategory category, bool affectFriendlies, bool affectEnemies, IdleAdventureDamageAttribute affectedAttribute, float fieldDamageMultiplier)
    {
        this.buffId = buffId;
        this.displayName = displayName;
        this.category = category;
        this.affectFriendlies = affectFriendlies;
        this.affectEnemies = affectEnemies;
        this.affectedAttribute = affectedAttribute;
        this.fieldDamageMultiplier = fieldDamageMultiplier;
    }

    public string BuffId => string.IsNullOrEmpty(buffId) ? "field_buff" : buffId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? BuffId : displayName;
    public IdleAdventureBuffCategory Category => category;
    public bool AffectFriendlies => affectFriendlies;
    public bool AffectEnemies => affectEnemies;
    public IdleAdventureDamageAttribute AffectedAttribute => affectedAttribute;
    public float FieldDamageMultiplier => category == IdleAdventureBuffCategory.Detriment ? -Mathf.Abs(fieldDamageMultiplier) : Mathf.Abs(fieldDamageMultiplier);
}

[Serializable]
public sealed class IdleAdventureAttributeRestraintDefinition
{
    [SerializeField] private IdleAdventureDamageAttribute attackerAttribute = IdleAdventureDamageAttribute.Fire;
    [SerializeField] private IdleAdventureDamageAttribute defenderAttribute = IdleAdventureDamageAttribute.Wood;

    public IdleAdventureAttributeRestraintDefinition()
    {
    }

    public IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute attackerAttribute, IdleAdventureDamageAttribute defenderAttribute)
    {
        this.attackerAttribute = attackerAttribute;
        this.defenderAttribute = defenderAttribute;
    }

    public IdleAdventureDamageAttribute AttackerAttribute => attackerAttribute;
    public IdleAdventureDamageAttribute DefenderAttribute => defenderAttribute;
}

[Serializable]
public sealed class IdleAdventureRewardItemDefinition
{
    [SerializeField] private string itemKey = "gold";
    [SerializeField] private string displayName = "Gold";
    [SerializeField, Min(1)] private int quantity = 100;

    public IdleAdventureRewardItemDefinition()
    {
    }

    public IdleAdventureRewardItemDefinition(string itemKey, string displayName, int quantity)
    {
        this.itemKey = itemKey;
        this.displayName = displayName;
        this.quantity = quantity;
    }

    public string ItemKey => string.IsNullOrEmpty(itemKey) ? "reward" : itemKey;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? ItemKey : displayName;
    public int Quantity => Mathf.Max(1, quantity);
}

[Serializable]
public sealed class IdleAdventureStageRewardDefinition
{
    [SerializeField, Min(1)] private int stage = 1;
    [SerializeField] private IdleAdventureRewardItemDefinition[] rewards = new IdleAdventureRewardItemDefinition[0];

    public IdleAdventureStageRewardDefinition()
    {
    }

    public IdleAdventureStageRewardDefinition(int stage, IdleAdventureRewardItemDefinition[] rewards)
    {
        this.stage = stage;
        this.rewards = rewards ?? new IdleAdventureRewardItemDefinition[0];
    }

    public int Stage => Mathf.Max(1, stage);
    public IdleAdventureRewardItemDefinition[] Rewards => rewards;
}

[CreateAssetMenu(menuName = "Idle Adventure/Mode Config", fileName = "IdleAdventureModeConfig")]
public sealed class IdleAdventureModeConfig : ScriptableObject
{
    [Header("Party Size")]
    [SerializeField, Range(1, 6)] private int startingFriendlyCount = 3;
    [SerializeField, Range(1, 6)] private int baseEnemyCount = 2;
    [SerializeField, Min(0)] private int addEnemyEveryStages = 2;

    [Header("Friendly Stats")]
    [SerializeField, Min(1)] private int friendlyBaseHealth = 160;
    [SerializeField, Min(0)] private int friendlyBaseAttack = 28;
    [SerializeField, Min(0)] private int friendlyBaseDefense = 5;
    [SerializeField, Min(1)] private int friendlyBaseSpeed = 12;

    [Header("Enemy Stats")]
    [SerializeField, Min(1)] private int enemyBaseHealth = 95;
    [SerializeField, Min(0)] private int enemyBaseAttack = 15;
    [SerializeField, Min(0)] private int enemyBaseDefense = 1;
    [SerializeField, Min(1)] private int enemyBaseSpeed = 9;
    [SerializeField, Min(0)] private int enemyHealthPerStage = 22;
    [SerializeField, Min(0)] private int enemyAttackPerStage = 4;
    [SerializeField, Min(0)] private int enemySpeedPerStage = 1;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float turnInterval = 0.75f;
    [SerializeField, Min(0.05f)] private float attackAnimationDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.18f;

    [Header("Default Lineups")]
    [SerializeField] private IdleAdventureCombatantDefinition[] friendlyLineup =
    {
        new IdleAdventureCombatantDefinition("Hero 1", 160, 28, 5, 18, IdleAdventureDamageAttribute.Fire, new[]
        {
            new IdleAdventureSkillDefinition("Power Strike", 2, 1.35f, IdleAdventureDamageAttribute.Fire),
            new IdleAdventureSkillDefinition("Battle Focus", 3, 1f, IdleAdventureDamageAttribute.Fire, 0)
        }),
        new IdleAdventureCombatantDefinition("Hero 2", 174, 31, 6, 17, IdleAdventureDamageAttribute.Water, new[]
        {
            new IdleAdventureSkillDefinition("Piercing Hit", 2, 1.25f, IdleAdventureDamageAttribute.Water),
            new IdleAdventureSkillDefinition("Guard Break", 3, 1f, IdleAdventureDamageAttribute.Water, -1, 1)
        }),
        new IdleAdventureCombatantDefinition("Hero 3", 188, 34, 7, 16, IdleAdventureDamageAttribute.Wood, new[]
        {
            new IdleAdventureSkillDefinition("Heavy Blow", 2, 1.45f, IdleAdventureDamageAttribute.Wood),
            new IdleAdventureSkillDefinition("Quick Tempo", 3, 1f, IdleAdventureDamageAttribute.Wood, 0)
        })
    };

    [SerializeField] private IdleAdventureCombatantDefinition[] enemyLineup =
    {
        new IdleAdventureCombatantDefinition("Enemy 1", 95, 15, 1, 15, IdleAdventureDamageAttribute.Wood, new[] { new IdleAdventureSkillDefinition("Claw", 2, 1.2f, IdleAdventureDamageAttribute.Wood) }),
        new IdleAdventureCombatantDefinition("Enemy 2", 107, 17, 1, 14, IdleAdventureDamageAttribute.Fire, new[] { new IdleAdventureSkillDefinition("Rend", 2, 1.15f, IdleAdventureDamageAttribute.Fire) }),
        new IdleAdventureCombatantDefinition("Enemy 3", 119, 19, 2, 13, IdleAdventureDamageAttribute.Earth, new[] { new IdleAdventureSkillDefinition("Intimidate", 3, 1f, IdleAdventureDamageAttribute.Earth, -1, 1) }),
        new IdleAdventureCombatantDefinition("Enemy 4", 131, 21, 2, 12, IdleAdventureDamageAttribute.Water, new[] { new IdleAdventureSkillDefinition("Claw", 2, 1.2f, IdleAdventureDamageAttribute.Water) }),
        new IdleAdventureCombatantDefinition("Enemy 5", 143, 23, 3, 11, IdleAdventureDamageAttribute.Light, new[] { new IdleAdventureSkillDefinition("Rend", 2, 1.15f, IdleAdventureDamageAttribute.Light) }),
        new IdleAdventureCombatantDefinition("Enemy 6", 155, 25, 3, 10, IdleAdventureDamageAttribute.Dark, new[] { new IdleAdventureSkillDefinition("Intimidate", 3, 1f, IdleAdventureDamageAttribute.Dark, -1, 1) })
    };

    [Header("Buff Presets")]
    [SerializeField] private IdleAdventureBuffDefinition[] buffPresets =
    {
        new IdleAdventureBuffDefinition("normal_damage_up", "Damage Up", IdleAdventureBuffCategory.Benefit, IdleAdventureBuffSource.CharacterSkill, IdleAdventureBuffEffectType.NormalDamage, 3, 0, 0, 0, 0.15f, 0f, IdleAdventureDamageAttribute.None),
        new IdleAdventureBuffDefinition("fire_damage_down", "Fire Damage Down", IdleAdventureBuffCategory.Detriment, IdleAdventureBuffSource.CharacterSkill, IdleAdventureBuffEffectType.AttributeDamage, 3, 0, 0, 0, 0f, -0.15f, IdleAdventureDamageAttribute.Fire)
    };

    [Header("Field Buffs")]
    [SerializeField] private IdleAdventureFieldBuffDefinition[] fieldBuffs =
    {
        new IdleAdventureFieldBuffDefinition("fire_field_up", "Fire Field Up", IdleAdventureBuffCategory.Benefit, true, true, IdleAdventureDamageAttribute.Fire, 0.15f)
    };

    [Header("Attribute Restraints")]
    [SerializeField] private IdleAdventureAttributeRestraintDefinition[] attributeRestraints =
    {
        new IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute.Fire, IdleAdventureDamageAttribute.Wood),
        new IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute.Water, IdleAdventureDamageAttribute.Fire),
        new IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute.Wood, IdleAdventureDamageAttribute.Earth),
        new IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute.Earth, IdleAdventureDamageAttribute.Water),
        new IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute.Light, IdleAdventureDamageAttribute.Dark),
        new IdleAdventureAttributeRestraintDefinition(IdleAdventureDamageAttribute.Dark, IdleAdventureDamageAttribute.Light)
    };

    [Header("Stage Rewards")]
    [SerializeField] private IdleAdventureStageRewardDefinition[] stageRewards =
    {
        new IdleAdventureStageRewardDefinition(1, new[]
        {
            new IdleAdventureRewardItemDefinition("gold", "Gold", 120),
            new IdleAdventureRewardItemDefinition("adventure_token", "Adventure Token", 1)
        }),
        new IdleAdventureStageRewardDefinition(2, new[]
        {
            new IdleAdventureRewardItemDefinition("gold", "Gold", 180),
            new IdleAdventureRewardItemDefinition("wood", "Wood", 60),
            new IdleAdventureRewardItemDefinition("adventure_token", "Adventure Token", 1)
        }),
        new IdleAdventureStageRewardDefinition(3, new[]
        {
            new IdleAdventureRewardItemDefinition("gold", "Gold", 260),
            new IdleAdventureRewardItemDefinition("stone", "Stone", 80),
            new IdleAdventureRewardItemDefinition("adventure_token", "Adventure Token", 2)
        }),
        new IdleAdventureStageRewardDefinition(4, new[]
        {
            new IdleAdventureRewardItemDefinition("gold", "Gold", 360),
            new IdleAdventureRewardItemDefinition("iron", "Iron", 80),
            new IdleAdventureRewardItemDefinition("adventure_token", "Adventure Token", 2)
        }),
        new IdleAdventureStageRewardDefinition(5, new[]
        {
            new IdleAdventureRewardItemDefinition("gold", "Gold", 480),
            new IdleAdventureRewardItemDefinition("stone", "Stone", 120),
            new IdleAdventureRewardItemDefinition("iron", "Iron", 100),
            new IdleAdventureRewardItemDefinition("adventure_token", "Adventure Token", 3)
        })
    };

    public int StartingFriendlyCount => startingFriendlyCount;
    public int BaseEnemyCount => baseEnemyCount;
    public int AddEnemyEveryStages => addEnemyEveryStages;
    public int FriendlyBaseHealth => friendlyBaseHealth;
    public int FriendlyBaseAttack => friendlyBaseAttack;
    public int FriendlyBaseDefense => friendlyBaseDefense;
    public int FriendlyBaseSpeed => friendlyBaseSpeed;
    public int EnemyBaseHealth => enemyBaseHealth;
    public int EnemyBaseAttack => enemyBaseAttack;
    public int EnemyBaseDefense => enemyBaseDefense;
    public int EnemyBaseSpeed => enemyBaseSpeed;
    public int EnemyHealthPerStage => enemyHealthPerStage;
    public int EnemyAttackPerStage => enemyAttackPerStage;
    public int EnemySpeedPerStage => enemySpeedPerStage;
    public float TurnInterval => turnInterval;
    public float AttackAnimationDuration => attackAnimationDuration;
    public float HitFlashDuration => hitFlashDuration;
    public IdleAdventureCombatantDefinition[] FriendlyLineup => friendlyLineup;
    public IdleAdventureCombatantDefinition[] EnemyLineup => enemyLineup;
    public IdleAdventureBuffDefinition[] BuffPresets => buffPresets;
    public IdleAdventureFieldBuffDefinition[] FieldBuffs => fieldBuffs;
    public IdleAdventureAttributeRestraintDefinition[] AttributeRestraints => attributeRestraints;
    public IdleAdventureStageRewardDefinition[] StageRewards => stageRewards;

    public IdleAdventureRewardItemDefinition[] RewardsForStage(int clearedStage)
    {
        if (stageRewards == null) return DefaultRewardsForStage(clearedStage);

        for (int i = 0; i < stageRewards.Length; i++)
        {
            IdleAdventureStageRewardDefinition reward = stageRewards[i];
            if (reward != null && reward.Stage == clearedStage) return reward.Rewards;
        }

        return DefaultRewardsForStage(clearedStage);
    }

    private static IdleAdventureRewardItemDefinition[] DefaultRewardsForStage(int clearedStage)
    {
        int stageValue = Mathf.Max(1, clearedStage);
        return new[]
        {
            new IdleAdventureRewardItemDefinition("gold", "Gold", 100 + stageValue * 40),
            new IdleAdventureRewardItemDefinition("adventure_token", "Adventure Token", Mathf.Max(1, stageValue / 2))
        };
    }
}
