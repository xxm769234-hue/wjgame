using System.Collections.Generic;
using UnityEngine;

public sealed class IdleAdventureCombatant : MonoBehaviour
{
    private const int MaxVisibleBuffNames = 2;
    private const int MaxVisibleSkillNames = 2;

    private readonly List<IdleAdventureBattleBuff> buffs = new List<IdleAdventureBattleBuff>();
    private IdleAdventureBattleSkill[] skillSlots = new IdleAdventureBattleSkill[0];

    public string CombatantName { get; private set; }
    public string RuntimeId { get; private set; }
    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Speed { get; private set; }
    public IdleAdventureDamageAttribute AttributeType { get; private set; }
    public int EffectiveAttack => Mathf.Max(0, Attack + SumBuffAttack());
    public int EffectiveDefense => Mathf.Max(0, Defense + SumBuffDefense());
    public int EffectiveSpeed => Mathf.Max(1, Speed + SumBuffSpeed());
    public bool IsFriendly { get; private set; }
    public int SlotIndex { get; private set; }
    public Color BaseColor { get; private set; }
    public IReadOnlyList<IdleAdventureBattleBuff> ActiveBuffs => buffs;
    public float HealthRatio => Mathf.Clamp01((float)CurrentHealth / MaxHealth);
    public bool IsDefeated => CurrentHealth <= 0;

    public void Initialize(string combatantName, int maxHealth, int attack, int defense, int speed, bool isFriendly, int slotIndex, IdleAdventureSkillDefinition[] skills = null, int skillSlotCount = 0, IdleAdventureDamageAttribute attributeType = IdleAdventureDamageAttribute.Physical)
    {
        CombatantName = combatantName;
        RuntimeId = string.Empty;
        MaxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = MaxHealth;
        Attack = Mathf.Max(0, attack);
        Defense = Mathf.Max(0, defense);
        Speed = Mathf.Max(1, speed);
        AttributeType = attributeType == IdleAdventureDamageAttribute.None ? IdleAdventureDamageAttribute.Physical : attributeType;
        IsFriendly = isFriendly;
        SlotIndex = slotIndex;
        BaseColor = isFriendly ? new Color(0.18f, 0.62f, 0.93f, 1f) : new Color(0.9f, 0.32f, 0.28f, 1f);
        buffs.Clear();
        SetSkillSlots(skills, skillSlotCount);
    }

    public void ApplyBackendState(string runtimeId, string combatantName, int maxHealth, int currentHealth, int attack, int defense, int speed, bool isFriendly, int slotIndex, IdleAdventureDamageAttribute attributeType, IList<IdleAdventureBattleBuff> backendBuffs)
    {
        RuntimeId = string.IsNullOrEmpty(runtimeId) ? RuntimeId : runtimeId;
        CombatantName = string.IsNullOrEmpty(combatantName) ? CombatantName : combatantName;
        MaxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        Attack = Mathf.Max(0, attack);
        Defense = Mathf.Max(0, defense);
        Speed = Mathf.Max(1, speed);
        AttributeType = attributeType == IdleAdventureDamageAttribute.None ? IdleAdventureDamageAttribute.Physical : attributeType;
        IsFriendly = isFriendly;
        SlotIndex = slotIndex;
        BaseColor = isFriendly ? new Color(0.18f, 0.62f, 0.93f, 1f) : new Color(0.9f, 0.32f, 0.28f, 1f);
        ReplaceBuffs(backendBuffs);
    }

    public void SetCurrentHealth(int currentHealth)
    {
        CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
    }

    public void ReplaceBuffs(IList<IdleAdventureBattleBuff> nextBuffs)
    {
        buffs.Clear();
        if (nextBuffs == null) return;

        for (int i = 0; i < nextBuffs.Count; i++)
        {
            if (nextBuffs[i] != null) buffs.Add(nextBuffs[i].FreshCopy());
        }
    }

    public void TakeDamage(int damage)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, damage));
    }

    public void HealToFull()
    {
        CurrentHealth = MaxHealth;
    }

    public void AddBuff(IdleAdventureBattleBuff buff)
    {
        if (buff == null) return;

        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].CanRefreshFrom(buff))
            {
                buffs[i].RefreshFrom(buff);
                return;
            }
        }

        buffs.Add(buff.FreshCopy());
    }

    public void TickBuffs()
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            buffs[i].TickRound();
            if (buffs[i].RemainingRounds <= 0) buffs.RemoveAt(i);
        }
    }

    public IdleAdventureBattleSkill FirstReadySkill()
    {
        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] != null && skillSlots[i].CanUse) return skillSlots[i];
        }

        return null;
    }

    public void TickSkillCooldowns()
    {
        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] != null) skillSlots[i].TickCooldown();
        }
    }

    public string FormatBuffSummary()
    {
        if (buffs.Count == 0) return "Buff 0  Debuff 0  Skill 0  Other 0";

        int benefitCount = 0;
        int detrimentCount = 0;
        int skillSourceCount = 0;
        int otherSourceCount = 0;
        int normalDamageCount = 0;
        int attributeDamageCount = 0;
        int fieldCount = 0;
        List<string> visibleNames = new List<string>();

        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].Category == IdleAdventureBuffCategory.Benefit) benefitCount++;
            else detrimentCount++;

            if (buffs[i].Source == IdleAdventureBuffSource.CharacterSkill) skillSourceCount++;
            else otherSourceCount++;

            if (buffs[i].EffectType == IdleAdventureBuffEffectType.AttributeDamage) attributeDamageCount++;
            else if (buffs[i].EffectType == IdleAdventureBuffEffectType.Field) fieldCount++;
            else normalDamageCount++;

            if (visibleNames.Count < MaxVisibleBuffNames)
            {
                visibleNames.Add($"{buffs[i].EffectShortLabel}{buffs[i].SourceShortLabel}:{buffs[i].DisplayName} {buffs[i].RemainingRounds}");
            }
        }

        string names = visibleNames.Count > 0 ? $"  {string.Join(", ", visibleNames)}" : string.Empty;
        return $"Buff {benefitCount}  Debuff {detrimentCount}  Skill {skillSourceCount}  Other {otherSourceCount}  N {normalDamageCount}  A {attributeDamageCount}  F {fieldCount}{names}";
    }

    public string FormatSkillSummary()
    {
        if (skillSlots == null || skillSlots.Length == 0) return "Skill Slot 0";

        List<string> visibleNames = new List<string>();
        int count = Mathf.Min(MaxVisibleSkillNames, skillSlots.Length);
        for (int i = 0; i < count; i++)
        {
            visibleNames.Add(skillSlots[i] != null ? skillSlots[i].FormatSummary(i + 1) : $"S{i + 1}:Empty");
        }

        return string.Join("  ", visibleNames);
    }

    private void SetSkillSlots(IdleAdventureSkillDefinition[] skills, int skillSlotCount)
    {
        int slotCount = Mathf.Max(0, skillSlotCount);
        skillSlots = new IdleAdventureBattleSkill[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            IdleAdventureSkillDefinition definition = skills != null && i < skills.Length ? skills[i] : null;
            skillSlots[i] = new IdleAdventureBattleSkill(definition);
        }
    }

    private int SumBuffAttack()
    {
        int value = 0;
        for (int i = 0; i < buffs.Count; i++) value += buffs[i].AttackModifier;
        return value;
    }

    private int SumBuffDefense()
    {
        int value = 0;
        for (int i = 0; i < buffs.Count; i++) value += buffs[i].DefenseModifier;
        return value;
    }

    private int SumBuffSpeed()
    {
        int value = 0;
        for (int i = 0; i < buffs.Count; i++) value += buffs[i].SpeedModifier;
        return value;
    }

    public float SumNormalDamageBuffMultiplier()
    {
        float value = 0f;
        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].EffectType == IdleAdventureBuffEffectType.NormalDamage)
            {
                value += buffs[i].NormalDamageMultiplier;
            }
        }

        return value;
    }

    public float SumAttributeDamageBuffMultiplier()
    {
        float value = 0f;
        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].EffectType != IdleAdventureBuffEffectType.AttributeDamage) continue;
            if (!AttributeMatches(AttributeType, buffs[i].AffectedAttribute)) continue;
            value += buffs[i].AttributeDamageMultiplier;
        }

        return value;
    }

    public static bool AttributeMatches(IdleAdventureDamageAttribute actual, IdleAdventureDamageAttribute affected)
    {
        return affected == IdleAdventureDamageAttribute.None || actual == affected;
    }
}
