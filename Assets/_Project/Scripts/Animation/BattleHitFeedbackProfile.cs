using UnityEngine;

public enum BattleHitShakeMode { Directional, DualAxis }
public enum BattleHitRecoilMode { SnapReturn, SoftArc }

public struct BattleHitStopWindow
{
    private float windowStart;
    private float used;
    private float lastStop;
    private int lastPriority;

    public float Take(float now, BattleHitFeedbackProfile profile)
    {
        if (profile == null || profile.hitStop <= 0f) return 0f;
        if (used == 0f || now - windowStart >= .25f)
        {
            windowStart = now;
            used = 0f;
        }
        if (lastPriority > 0 && now - lastStop < .08f && profile.priority <= lastPriority) return 0f;
        float seconds = Mathf.Min(profile.hitStop, Mathf.Max(0f, .1f - used));
        if (seconds <= 0f) return 0f;
        used += seconds;
        lastPriority = profile.priority;
        lastStop = now;
        return seconds;
    }
}

public sealed class BattleHitFeedbackProfile : ScriptableObject
{
    public int priority = 20;
    public float hitStop = 0.05f;
    public float shakePixels = 2.5f;
    public float shakeDuration = 0.12f;
    public BattleHitShakeMode shakeMode;
    public float recoilPixels = 3f;
    public float recoilAttack = 0.035f;
    public float recoilReturn = 0.12f;
    public BattleHitRecoilMode recoilMode;
    public bool horizontalRecoil;
    public float flashAmount = 0.45f;
    public Color flashColor = Color.white;
    public float flashDuration = 0.04f;
    public float afterglowDuration = 0.08f;
    public float afterglowAmount = 0.12f;
    public Color afterglowColor = new Color(1f, 0.78f, 0.37f);
    public float flashBodyFraction = 0.28f;
    public float sparkPixels = 18f;
    public float sparkDuration = 0.18f;
    public float sparkWidthPixels = 2.2f;
    public float sparkRayWidthPixels = 1.8f;
    public Color contactColor = new Color(1f, 0.78f, 0.37f);
    public float contactGain = 1f;
    public AudioClip[] windupSounds;
    public AudioClip[] contactSounds;
    public AudioClip[] tailSounds;
    public AudioClip sustainSound;
    public float windupLead = 0.12f;
}
