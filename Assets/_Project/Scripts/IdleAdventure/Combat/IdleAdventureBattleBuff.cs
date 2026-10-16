using System;
using UnityEngine;

public enum IdleAdventureBuffCategory
{
    Benefit,
    Detriment
}

public enum IdleAdventureBuffSource
{
    CharacterSkill,
    Other
}

public enum IdleAdventureBuffEffectType
{
    NormalDamage,
    AttributeDamage,
    Field
}

public enum IdleAdventureDamageAttribute
{
    None,
    Physical,
    Fire,
    Water,
    Wood,
    Earth,
    Light,
    Dark
}

[Serializable]
public sealed class IdleAdventureBattleBuff
{
    private IdleAdventureBattleBuff(string id, string displayName, IdleAdventureBuffCategory category, IdleAdventureBuffSource source, IdleAdventureBuffEffectType effectType, int durationRounds, int attackModifier, int defenseModifier, int speedModifier, float normalDamageMultiplier, float attributeDamageMultiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        Id = string.IsNullOrEmpty(id) ? "battle_buff" : id;
        DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
        Category = category;
        Source = source;
        EffectType = effectType;
        RemainingRounds = Mathf.Max(1, durationRounds);
        AttackModifier = attackModifier;
        DefenseModifier = defenseModifier;
        SpeedModifier = speedModifier;
        NormalDamageMultiplier = normalDamageMultiplier;
        AttributeDamageMultiplier = attributeDamageMultiplier;
        AffectedAttribute = affectedAttribute;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public IdleAdventureBuffCategory Category { get; }
    public IdleAdventureBuffSource Source { get; }
    public IdleAdventureBuffEffectType EffectType { get; }
    public int RemainingRounds { get; private set; }
    public int AttackModifier { get; }
    public int DefenseModifier { get; }
    public int SpeedModifier { get; }
    public float NormalDamageMultiplier { get; }
    public float AttributeDamageMultiplier { get; }
    public IdleAdventureDamageAttribute AffectedAttribute { get; }
    public string SourceLabel => Source == IdleAdventureBuffSource.CharacterSkill ? "Skill" : "Other";
    public string SourceShortLabel => Source == IdleAdventureBuffSource.CharacterSkill ? "S" : "O";
    public string EffectShortLabel
    {
        get
        {
            switch (EffectType)
            {
                case IdleAdventureBuffEffectType.AttributeDamage:
                    return "A";
                case IdleAdventureBuffEffectType.Field:
                    return "F";
                default:
                    return "N";
            }
        }
    }

    public static IdleAdventureBattleBuff StatBuff(string id, string displayName, IdleAdventureBuffCategory category, IdleAdventureBuffSource source, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return new IdleAdventureBattleBuff(id, displayName, category, source, IdleAdventureBuffEffectType.NormalDamage, durationRounds, attackModifier, defenseModifier, speedModifier, 0f, 0f, IdleAdventureDamageAttribute.None);
    }

    public static IdleAdventureBattleBuff DamageMultiplierBuff(string id, string displayName, IdleAdventureBuffCategory category, IdleAdventureBuffSource source, IdleAdventureBuffEffectType effectType, int durationRounds, float normalDamageMultiplier, float attributeDamageMultiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return new IdleAdventureBattleBuff(id, displayName, category, source, effectType, durationRounds, 0, 0, 0, normalDamageMultiplier, attributeDamageMultiplier, affectedAttribute);
    }

    public static IdleAdventureBattleBuff Create(string id, string displayName, IdleAdventureBuffCategory category, IdleAdventureBuffSource source, IdleAdventureBuffEffectType effectType, int durationRounds, int attackModifier, int defenseModifier, int speedModifier, float normalDamageMultiplier, float attributeDamageMultiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return new IdleAdventureBattleBuff(id, displayName, category, source, effectType, durationRounds, attackModifier, defenseModifier, speedModifier, normalDamageMultiplier, attributeDamageMultiplier, affectedAttribute);
    }

    public IdleAdventureBattleBuff FreshCopy()
    {
        return new IdleAdventureBattleBuff(Id, DisplayName, Category, Source, EffectType, RemainingRounds, AttackModifier, DefenseModifier, SpeedModifier, NormalDamageMultiplier, AttributeDamageMultiplier, AffectedAttribute);
    }

    public bool CanRefreshFrom(IdleAdventureBattleBuff source)
    {
        return source != null && Id == source.Id && Source == source.Source && EffectType == source.EffectType;
    }

    public void RefreshFrom(IdleAdventureBattleBuff source)
    {
        RemainingRounds = Mathf.Max(RemainingRounds, source.RemainingRounds);
    }

    public void TickRound()
    {
        RemainingRounds--;
    }
}
