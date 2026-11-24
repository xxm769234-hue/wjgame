using System;
using System.Collections.Generic;

// Converts server results to presentation recipients. Animation metadata never adds targets.
public static class BattleActionContract
{
    public static IdleAdventureBattleTargetResponse[] Targets(IdleAdventureBattleActionResponse action)
    {
        if (action == null) return Array.Empty<IdleAdventureBattleTargetResponse>();
        if (action.targets != null) return action.targets;
        if (string.IsNullOrEmpty(action.target_runtime_id)) return Array.Empty<IdleAdventureBattleTargetResponse>();
        return new[] { new IdleAdventureBattleTargetResponse
        {
            runtime_id = action.target_runtime_id,
            target_name = action.target_name,
            health_before = action.target_health_before,
            health_after = action.target_health_after,
            has_health_after = action.has_target_health_after,
            damage = action.damage,
            defeated = Contains(action.defeated_runtime_ids, action.target_runtime_id),
            has_defeated = action.defeated_runtime_ids != null
        } };
    }

    public static bool IsHealing(IdleAdventureBattleActionResponse action)
    {
        return action != null && string.Equals(action.effect_type, "HEAL", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBenefit(IdleAdventureBattleActionResponse action)
    {
        return IsHealing(action) || action != null && string.Equals(action.effect_type, "BUFF", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasDamage(IdleAdventureBattleActionResponse action)
    {
        foreach (IdleAdventureBattleTargetResponse target in Targets(action))
            if (target != null && target.damage > 0) return true;
        return false;
    }

    public static bool HasSameSideDamage(IdleAdventureBattleActionResponse action)
    {
        if (action == null) return false;
        foreach (IdleAdventureBattleTargetResponse target in Targets(action))
            if (target != null && target.damage > 0 && SameSide(action.actor_runtime_id, target.runtime_id)) return true;
        return false;
    }

    public static bool SameSide(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return false;
        return (first[0] == 'F' || first[0] == 'E') && first[0] == second[0];
    }

    public static bool Contains(string[] values, string value)
    {
        return values != null && !string.IsNullOrEmpty(value) && Array.IndexOf(values, value) >= 0;
    }
}

// The action's aggregate authoritative result is committed only once, at its last contact.
// The player clamps all configured impact frames into the clip, so the final callback is
// the last array entry (or index zero for a clip without an explicit impact array).
public sealed class BattleActionImpactGate
{
    private readonly int finalImpact;
    private bool committed;
    public bool Committed => committed;
    public BattleActionImpactGate(int impactCount) { finalImpact = Math.Max(0, impactCount - 1); }
    public bool TryCommit(int impactIndex, Action apply)
    {
        if (committed || impactIndex < finalImpact) return false;
        committed = true;
        apply?.Invoke();
        return true;
    }
    public bool Complete(Action apply) { return TryCommit(finalImpact, apply); }
}
