using System;
using UnityEngine;

[Serializable]
public sealed class IdleAdventureSkillDefinition
{
    [SerializeField] private string skillName = "Skill";
    [SerializeField, Min(0)] private int cooldownRounds = 2;
    [SerializeField, Min(0f)] private float skillMultiplier = 1.2f;
    [SerializeField] private IdleAdventureDamageAttribute damageAttribute = IdleAdventureDamageAttribute.Physical;
    [SerializeField] private int selfBuffPresetIndex = -1;
    [SerializeField] private int targetBuffPresetIndex = -1;

    public IdleAdventureSkillDefinition()
    {
    }

    public IdleAdventureSkillDefinition(string skillName, int cooldownRounds, int damagePercent, int flatDamage, int selfBuffPresetIndex = -1, int targetBuffPresetIndex = -1)
    {
        this.skillName = skillName;
        this.cooldownRounds = cooldownRounds;
        skillMultiplier = Mathf.Max(0f, damagePercent / 100f);
        damageAttribute = IdleAdventureDamageAttribute.Physical;
        this.selfBuffPresetIndex = selfBuffPresetIndex;
        this.targetBuffPresetIndex = targetBuffPresetIndex;
    }

    public IdleAdventureSkillDefinition(string skillName, int cooldownRounds, float skillMultiplier, IdleAdventureDamageAttribute damageAttribute = IdleAdventureDamageAttribute.Physical, int selfBuffPresetIndex = -1, int targetBuffPresetIndex = -1)
    {
        this.skillName = skillName;
        this.cooldownRounds = cooldownRounds;
        this.skillMultiplier = skillMultiplier;
        this.damageAttribute = damageAttribute;
        this.selfBuffPresetIndex = selfBuffPresetIndex;
        this.targetBuffPresetIndex = targetBuffPresetIndex;
    }

    public string SkillName => string.IsNullOrEmpty(skillName) ? "Skill" : skillName;
    public int CooldownRounds => Mathf.Max(0, cooldownRounds);
    public float SkillMultiplier => Mathf.Max(0f, skillMultiplier);
    public IdleAdventureDamageAttribute DamageAttribute => damageAttribute == IdleAdventureDamageAttribute.None ? IdleAdventureDamageAttribute.Physical : damageAttribute;
    public int SelfBuffPresetIndex => selfBuffPresetIndex;
    public int TargetBuffPresetIndex => targetBuffPresetIndex;
    public bool HasSelfBuff => selfBuffPresetIndex >= 0;
    public bool HasTargetBuff => targetBuffPresetIndex >= 0;
}

public sealed class IdleAdventureBattleSkill
{
    public IdleAdventureBattleSkill(IdleAdventureSkillDefinition definition)
    {
        Definition = definition;
    }

    public IdleAdventureSkillDefinition Definition { get; }
    public int CooldownRemaining { get; private set; }
    public bool IsEmpty => Definition == null;
    public bool CanUse => Definition != null && CooldownRemaining <= 0;

    public void TriggerCooldown()
    {
        if (Definition == null) return;
        CooldownRemaining = Definition.CooldownRounds;
    }

    public void TickCooldown()
    {
        if (CooldownRemaining > 0) CooldownRemaining--;
    }

    public string FormatSummary(int slotNumber)
    {
        if (Definition == null) return $"S{slotNumber}:Empty";
        return CooldownRemaining > 0 ? $"S{slotNumber}:{Definition.SkillName}({CooldownRemaining})" : $"S{slotNumber}:{Definition.SkillName}";
    }
}
