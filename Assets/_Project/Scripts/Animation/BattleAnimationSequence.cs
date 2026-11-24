using System;
using UnityEngine;

public enum BattleAnimationAction { Idle, Move, NormalAttack, SkillAttack, Buff, Hit, Death, Effect }

[Serializable]
public sealed class BattleAudioCue
{
    public AudioClip clip;
    public int startFrame;
    public int endFrame;
    public float volume = 0.8f;
    public bool IsValid => clip != null && endFrame > startFrame;
}

public sealed class BattleAnimationSequence : ScriptableObject
{
    public string sequenceId;
    public string sourceFolder;
    public BattleAnimationAction action;
    public float frameRate = 24f;
    public float playbackDuration = 1f;
    public bool loop;
    public bool targetsEnemy;
    public bool targetsAllEnemies;
    public Sprite[] frames;
    public float[] frameTimes;
    public float approachDistance;
    public bool blinkApproach;
    public Vector2 approachContact;
    public Vector2 attackTargetBodyOffset;
    public bool restartTargetHitOnImpact;
    public Sprite[] presentationFrames;
    public Sprite[] movementBodyFrames;
    public Sprite[] movementTrailFrames;
    public int[] movementPreparationFrames;
    public int[] movementRecoveryFrames;
    public int movementTravelFirstFrame;
    public int movementTravelLastFrame;
    public BattleEffectBinding[] effects;
    public BattleAudioCue[] audioCues;
    public int[] impactFrames;
    public BattleHitFeedbackProfile[] impactFeedback;
    public int defenseFrame;
    public int releaseFrame;
    public float deathBlendDuration = 0.08f;
    public bool IsValid => frames != null && frames.Length > 0;
    public int FrameCount => frames != null ? frames.Length : 0;
    public int DefenseFrame => IsValid ? Mathf.Clamp(defenseFrame, 0, frames.Length - 1) : 0;
    public int ReleaseFrame => IsValid ? Mathf.Clamp(releaseFrame, DefenseFrame, frames.Length - 1) : 0;
    public float NaturalDuration => FrameCount / Mathf.Max(1f, frameRate);
    public float Duration => playbackDuration > 0f ? playbackDuration : NaturalDuration;
    public bool HasAudio => audioCues != null && Array.Exists(audioCues, cue => cue != null && cue.IsValid);
}
