using System;
using UnityEngine;

public enum BattleEffectPlacement { Enemy, Projectile, EnemyGround, CasterGround, CasterAbove, Ally, PartyGround, EnemyFormationGround }

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
    public float referenceWidth = 1f;
    public float referenceHeight = 1f;
    public bool IsValid => sequence != null && sequence.IsValid && frameAnchors != null && frameAnchors.Length == sequence.FrameCount;
}
