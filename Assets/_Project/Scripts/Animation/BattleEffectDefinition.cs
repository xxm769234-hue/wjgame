using System;
using UnityEngine;

public enum BattleEffectPlacement { Enemy, Projectile, EnemyGround, CasterGround, CasterAbove, Ally, PartyGround, EnemyFormationGround, FullPartyGround, FullPartyRise, FullPartyTravel, AllyAbove, RowProjectile }

[Serializable]
public sealed class BattleEffectSourceKey
{
    public float frame;
    public Vector2 localPosition;
}

[Serializable]
public sealed class BattleEffectBinding
{
    public BattleEffectDefinition effect;
    public BattleEffectPlacement placement;
    public bool sortWithCombatants;
    public bool IsFullParty => placement == BattleEffectPlacement.FullPartyGround || placement == BattleEffectPlacement.FullPartyRise || placement == BattleEffectPlacement.FullPartyTravel;
    public float startFrame;
    public float launchFrame;
    public float contactFrame;
    public float endFrame;
    public int firstEffectFrame;
    public int launchEffectFrame;
    public int contactEffectFrame;
    public int impactIndex = -1;
    public float width = 3f;
    public float opacity = 1f;
    public bool preserveSourceScale;
    public bool flipX;
    public Vector2 sourceOffset = new Vector2(0.6f, 0.65f);
    public BattleEffectSourceKey[] sourceKeys;
    public Vector2 targetOffset;
    public Vector2 targetBodyOffset;
    public float arcHeight;
    public Vector2 projectileDirection;
    public float projectileDistance;
    public Vector2 formationOffset;
}

public sealed class BattleEffectDefinition : ScriptableObject
{
    public BattleAnimationSequence sequence;
    public Vector2[] frameAnchors;
    public Vector2 projectileForward = Vector2.right;
    public bool projectileForwardFollowsAnchors;
    public float referenceWidth = 1f;
    public float referenceHeight = 1f;
    public bool IsValid => sequence != null && sequence.IsValid && frameAnchors != null && frameAnchors.Length == sequence.FrameCount;

    public Vector2 ProjectileForwardAt(int frame, int launchFrame, int contactFrame)
    {
        if (projectileForwardFollowsAnchors)
        {
            int first = Mathf.Clamp(launchFrame, 0, frameAnchors.Length - 1);
            int last = Mathf.Clamp(contactFrame, first, frameAnchors.Length - 1);
            int current = Mathf.Clamp(frame, first, last);
            Vector2 tangent = frameAnchors[Mathf.Min(current + 1, last)] - frameAnchors[Mathf.Max(current - 1, first)];
            if (tangent.sqrMagnitude > .000001f) return tangent;
        }
        return projectileForward.sqrMagnitude > .000001f ? projectileForward : Vector2.right;
    }
}
