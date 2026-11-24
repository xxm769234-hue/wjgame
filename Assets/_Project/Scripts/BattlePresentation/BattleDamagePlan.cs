using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleDamageNumberKind { Damage, Hurt, Heal }

/// <summary>
/// 一次行动的伤害数字计划：服务器只下发每个目标的技能总伤，这里按命中帧把总伤拆成若干段显示。
/// 只影响显示，不改变任何伤害结算；最后一段一律补齐余数，所有段相加严格等于服务器总伤。
/// </summary>
public sealed class BattleDamagePlan
{
    public sealed class Entry
    {
        public BattleActorAnimation anchor;
        public BattleDamageNumberKind kind;
        public long total;
        public bool restrained;
        public string attribute = "physical";
        public bool defeated;
        internal long shown;
        internal long[] parts;
    }

    /// <summary>
    /// 默认分段占比（按 sequenceId）。
    /// 优先级：动画资产里的 impactDamageWeights → 本表 → 按反馈档自动（轻 1 / 中 1.5 / 重 2.5）。
    /// </summary>
    public static readonly Dictionary<string, float[]> DefaultWeights = new Dictionary<string, float[]>
    {
        { "Swordsman_NormalAttack_02", new[] { 40f, 60f } },
        { "Swordsman_SkillAttack_03", new[] { 30f, 25f, 45f } },
        { "Swordsman_SkillAttack_01", new[] { 30f, 20f, 50f } },
        { "Warhammer_SkillAttack_01", new[] { 35f, 65f } },
        { "Assassin_NormalAttack_01", new[] { 30f, 30f, 40f } },
        { "Cangqi_SkillAttack_02", new[] { 15f, 12f, 13f, 17f, 13f, 30f } },
    };

    public readonly List<Entry> entries = new List<Entry>();
    public BattleActorAnimation attackerAnchor;
    private readonly BattleDamageNumbers view;
    private readonly int actionKey;
    private int impactCount = -1;
    private int lastImpact = -1;
    private bool finished;
    private static int nextKey;

    public BattleDamagePlan(BattleDamageNumbers numbers)
    {
        view = numbers;
        actionKey = ++nextKey;
    }

    public bool IsEmpty => entries.Count == 0;

    /// <summary>在 AnimateCombatAction 的命中回调里调用，impactIndex 与播放器回调一致。</summary>
    public void Impact(int impactIndex, BattleAnimationSequence clip)
    {
        if (finished || view == null || entries.Count == 0) return;
        if (impactCount < 0) Prepare(clip);
        int index = Mathf.Clamp(impactIndex, 0, impactCount - 1);
        if (index <= lastImpact) return;
        lastImpact = index;
        bool final = index >= impactCount - 1;
        bool heavy = impactCount > 1 && (final || IsHeavyFeedback(clip, index));
        foreach (Entry entry in entries)
        {
            if (entry.anchor == null) continue;
            if (entry.kind == BattleDamageNumberKind.Heal && !final) continue;
            long amount = final ? entry.total - entry.shown : entry.parts[index];
            if (amount <= 0 && !final) continue;
            entry.shown += amount;
            if (entry.kind != BattleDamageNumberKind.Heal)
                entry.anchor.GetComponent<IdleAdventureActorView>()?.PreviewHealthDrop(amount, final);
            view.Hit(actionKey, this, entry, amount, impactCount, final, heavy);
        }
        if (final) finished = true;
    }

    /// <summary>动画结束时调用：如果命中回调没走到最后一段（例如动画被打断），把余数一次显示完。</summary>
    public void Complete(BattleAnimationSequence clip)
    {
        if (!finished) Impact(int.MaxValue, clip);
    }

    private void Prepare(BattleAnimationSequence clip)
    {
        impactCount = clip != null && clip.impactFrames != null && clip.impactFrames.Length > 0 ? clip.impactFrames.Length : 1;
        float[] weights = ResolveWeights(clip, impactCount);
        foreach (Entry entry in entries) entry.parts = Split(entry.total, weights);
    }

    public static float[] ResolveWeights(BattleAnimationSequence clip, int count)
    {
        if (count <= 1) return new[] { 1f };
        if (clip != null && Valid(clip.impactDamageWeights, count)) return clip.impactDamageWeights;
        if (clip != null && !string.IsNullOrEmpty(clip.sequenceId)
            && DefaultWeights.TryGetValue(clip.sequenceId, out float[] table) && Valid(table, count)) return table;
        var automatic = new float[count];
        for (int i = 0; i < count; i++)
        {
            BattleHitFeedbackProfile feedback = clip != null && clip.impactFeedback != null && i < clip.impactFeedback.Length
                ? clip.impactFeedback[i] : null;
            automatic[i] = feedback == null ? 1.5f : feedback.priority >= 30 ? 2.5f : feedback.priority >= 20 ? 1.5f : 1f;
        }
        return automatic;
    }

    private static bool Valid(float[] values, int count)
    {
        if (values == null || values.Length != count) return false;
        bool anyPositive = false;
        foreach (float value in values)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return false;
            anyPositive |= value > 0f;
        }
        return anyPositive;
    }

    /// <summary>按占比拆分整数伤害：前几段向下取整（总伤够分时每段至少 1），最后一段补齐余数。</summary>
    public static long[] Split(long total, float[] ratio)
    {
        int count = ratio.Length;
        var parts = new long[count];
        if (total <= 0 || count == 0) return parts;
        float largest = Mathf.Max(ratio);
        float unit = largest > 1000000f || (largest > 0f && largest < .000001f) ? largest : 1f;
        decimal sum = 0m;
        foreach (float value in ratio) sum += (decimal)(Mathf.Max(0f, value) / unit);
        if (sum == 0m) sum = 1m;
        long used = 0;
        for (int i = 0; i < count - 1; i++)
        {
            decimal weight = (decimal)(Mathf.Max(0f, ratio[i]) / unit);
            long part = (long)(total * weight / sum);
            if (total >= count && part < 1) part = 1;
            long reserved = total >= count ? count - i - 1 : 0;
            part = Math.Min(part, total - used - reserved);
            parts[i] = part;
            used += part;
        }
        parts[count - 1] = total - used;
        return parts;
    }

    private static bool IsHeavyFeedback(BattleAnimationSequence clip, int index)
    {
        return clip != null && clip.impactFeedback != null && index >= 0 && index < clip.impactFeedback.Length
            && clip.impactFeedback[index] != null && clip.impactFeedback[index].priority >= 30;
    }

    /// <summary>由服务器的一次行动结果生成计划；没有可显示的数字时返回 null。</summary>
    public static BattleDamagePlan FromBackendAction(BattleDamageNumbers numbers, IdleAdventureBattleActionResponse action,
        BattleActorAnimation attacker, string attackerAttribute, Func<string, BattleActorAnimation> anchorOf, Func<string, bool> isFriendly)
    {
        if (numbers == null || action == null || anchorOf == null) return null;
        var plan = new BattleDamagePlan(numbers) { attackerAnchor = attacker };
        bool healing = BattleActionContract.IsHealing(action);
        IdleAdventureDamageFormulaResponse formula = action.damage_formula;
        string attribute = NormalizeAttribute(formula != null && !string.IsNullOrEmpty(formula.attacker_attribute)
            ? formula.attacker_attribute : attackerAttribute);
        foreach (IdleAdventureBattleTargetResponse target in BattleActionContract.Targets(action))
        {
            if (target == null || string.IsNullOrEmpty(target.runtime_id)) continue;
            BattleActorAnimation anchor = anchorOf(target.runtime_id);
            if (anchor == null) continue;
            var entry = new Entry { anchor = anchor, attribute = attribute };
            if (healing)
            {
                long healed = target.healed_amount > 0 ? target.healed_amount
                    : target.has_health_after ? target.health_after - target.health_before : 0;
                if (healed <= 0) continue;
                entry.kind = BattleDamageNumberKind.Heal;
                entry.total = healed;
            }
            else
            {
                if (target.damage <= 0) continue;
                entry.kind = isFriendly != null && isFriendly(target.runtime_id) ? BattleDamageNumberKind.Hurt : BattleDamageNumberKind.Damage;
                entry.total = target.damage;
                entry.restrained = IsRestrained(formula, target);
                entry.defeated = (target.has_defeated && target.defeated) || BattleActionContract.Contains(action.defeated_runtime_ids, target.runtime_id);
            }
            plan.entries.Add(entry);
        }
        return plan.IsEmpty ? null : plan;
    }

    // 服务器的克制倍率是行动级的；多目标时用目标自身属性与公式里的防守属性对一下，对不上的目标按非克制显示。
    // 如果后端以后在 targets[] 里直接给出每个目标是否克制，改这里即可。
    private static bool IsRestrained(IdleAdventureDamageFormulaResponse formula, IdleAdventureBattleTargetResponse target)
    {
        if (formula == null || formula.attribute_restraint_multiplier <= 0.0001f) return false;
        if (string.IsNullOrEmpty(formula.defender_attribute) || string.IsNullOrEmpty(target.defender_attribute)) return true;
        return string.Equals(formula.defender_attribute.Trim(), target.defender_attribute.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeAttribute(string value)
    {
        switch ((value ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "FIRE": return "fire";
            case "WATER": return "water";
            case "WOOD": return "wood";
            case "EARTH": return "earth";
            case "LIGHT": return "light";
            case "DARK": return "dark";
            case "POISON": return "poison";   // 后端暂无，预留
            default: return "physical";
        }
    }
}
