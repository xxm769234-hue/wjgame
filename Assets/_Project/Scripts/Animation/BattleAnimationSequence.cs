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

[Serializable]
public struct BattleFootContactKey
{
    public int frame;
    public Vector2 left;
    public Vector2 right;
}

public sealed class BattleAnimationSequence : ScriptableObject
{
    [Tooltip("Sole positions relative to the sprite pivot in unmirrored reference body heights.")]
    public BattleFootContactKey[] footContacts;

    public bool TryGetFootContacts(int frame, out Vector2 left, out Vector2 right)
    {
        left = right = Vector2.zero;
        if (footContacts == null || footContacts.Length == 0) return false;
        BattleFootContactKey previous = footContacts[0];
        for (int index = 1; index < footContacts.Length; index++)
        {
            BattleFootContactKey next = footContacts[index];
            if (frame <= next.frame)
            {
                float progress = Mathf.InverseLerp(previous.frame, next.frame, frame);
                left = Vector2.Lerp(previous.left, next.left, progress);
                right = Vector2.Lerp(previous.right, next.right, progress);
                return true;
            }
            previous = next;
        }
        left = previous.left;
        right = previous.right;
        return true;
    }
    public string sequenceId;
    public AudioClip environmentMusic;
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
    public bool authoredCrossThrough;
    public Vector2 approachContact;
    public float recoveryFootX;
    public float departureFootX;
    public Vector2 attackTargetBodyOffset;
    public bool restartTargetHitOnImpact;
    [Tooltip("Optional source frames for full recoil during a multi-hit attack. Other impacts still play contact feedback.")]
    public int[] targetHitRestartFrames;

    public bool RestartsTargetHit(int impactIndex)
    {
        if (impactIndex == 0) return true;
        if (!restartTargetHitOnImpact) return false;
        if (targetHitRestartFrames == null || targetHitRestartFrames.Length == 0) return true;
        return impactFrames != null && impactIndex >= 0 && impactIndex < impactFrames.Length
            && Array.IndexOf(targetHitRestartFrames, impactFrames[impactIndex]) >= 0;
    }

    public Sprite[] presentationFrames;
    public Sprite[] movementBodyFrames;
    public Sprite[] movementTrailFrames;
    public int[] movementPreparationFrames;
    public int[] movementRecoveryFrames;
    public int movementTravelFirstFrame;
    public int movementTravelLastFrame;
    public BattleEffectBinding[] effects;
    public BattleAudioCue[] audioCues;
    [Tooltip("The authored track already contains the sound of each impact.")]
    public bool audioCuesIncludeImpacts;
    public int[] impactFrames;
    [Tooltip("每个命中帧的飘字权重，仅影响显示。留空时使用默认分配。")]
    public float[] impactDamageWeights;
    [Tooltip("Source frame for an additional reaction on surviving targets. Zero disables the cue.")]
    public int followUpTargetReactionFrame;
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
