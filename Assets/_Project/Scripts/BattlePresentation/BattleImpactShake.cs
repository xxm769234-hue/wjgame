using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleImpactShake : MonoBehaviour
{
    private RectTransform field;
    private RectTransform background;
    private Func<bool> paused;
    private BattleHitFeedbackProfile active;
    private Vector2 direction;
    private Vector2 appliedOffset;
    private Vector2 backgroundOffset;
    private float elapsed;
    private int startFrame;

    public void Bind(RectTransform battlefield, RectTransform scenery, Func<bool> pauseQuery)
    {
        ResetShake();
        field = battlefield;
        background = scenery;
        paused = pauseQuery;
    }

    public void Play(BattleHitFeedbackProfile profile, Vector2 impactDirection)
    {
        if (profile == null || profile.shakePixels <= 0f || profile.shakeDuration <= 0f || field == null) return;
        if (active != null && profile.priority <= active.priority && profile.shakePixels <= active.shakePixels) return;
        active = profile;
        direction = impactDirection.normalized;
        elapsed = 0f;
        startFrame = Time.frameCount;
        Sample();
    }

    private void LateUpdate()
    {
        if (active == null || (paused != null && paused()) || Time.frameCount == startFrame) return;
        elapsed += Time.deltaTime;
        if (elapsed >= active.shakeDuration) ResetShake();
        else Sample();
    }

    private void Sample()
    {
        Vector2 displacement;
        if (active.shakeMode == BattleHitShakeMode.DualAxis)
        {
            float sample = elapsed + Mathf.Min(Time.deltaTime, 1f / 60f);
            float envelope = Mathf.Pow(1f - Mathf.Clamp01(sample / active.shakeDuration), 2f);
            displacement = new Vector2(Mathf.Sin(sample * 115f), Mathf.Sin(sample * 157f) * .55f) * envelope;
        }
        else
        {
            float progress = Mathf.Clamp01(elapsed / active.shakeDuration);
            float envelope = (1f - progress) * (1f - progress);
            displacement = direction * (Mathf.Cos(progress * Mathf.PI * 2f) * envelope);
        }
        Vector2 offset = -displacement * active.shakePixels / 720f;
        SetOffset(field, ref appliedOffset, offset);
        SetOffset(background, ref backgroundOffset, offset);
    }

    private static void SetOffset(RectTransform surface, ref Vector2 previous, Vector2 heightFraction)
    {
        if (surface == null || !(surface.parent is RectTransform parent)) return;
        Vector2 next = heightFraction * parent.rect.height;
        surface.anchoredPosition += next - previous;
        previous = next;
    }

    public void ResetShake()
    {
        SetOffset(field, ref appliedOffset, Vector2.zero);
        SetOffset(background, ref backgroundOffset, Vector2.zero);
        active = null;
    }

    private void OnDisable() => ResetShake();
}
