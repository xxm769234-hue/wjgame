using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleActorAnimation : MonoBehaviour
{
    public const float ReferenceBodyHeight = 124f;
    private static readonly int[] AssassinPreparation = { 0, 4, 8, 12, 16, 24, 32, 35 };
    private static readonly int[] AssassinRecovery = { 72, 74, 76, 80, 84, 88, 90, 92, 96 };

    private Image body;
    private RectTransform actorRoot;
    private BattleAnimationCatalog.Entry entry;
    private BattleAnimationSequence sequence;
    private BattleAnimationAudio audioPlayer;
    private BattleActionEffects effects;
    private string identity;
    private Vector2 homePosition;
    private float elapsed;
    private float duration;
    private bool looping;
    private bool completed;
    private bool dying;
    private bool facingRight;
    private int generation;
    private int impactCursor;
    private Action<int> onImpact;
    private Func<bool> isPaused;
    private BattleHitFeedbackProfile feedback;
    private float feedbackElapsed;
    private float feedbackDirection;
    private Image movementTrail;
    private float hitStopRemaining;
    private RectTransform presentationCanvas;
    private float renderedPresentationScale = 1f;
    private bool externalSampling;
    private bool fallbackDeath;
    private float fallbackDeathElapsed;
    private Vector2 fallbackDeathOrigin;
    private Vector3 fallbackDeathScale;
    private BattleActorHitVisual hitVisual;

    private Vector2 BaseBodyPosition => new Vector2(0f, -8f) + entry.offset * UnitsToPixels;

    public bool IsBound => entry != null && body != null;
    public bool IsPlayingAction => IsBound && sequence != null && !looping && !completed;
    public bool IsPlayingDeath => dying && IsPlayingAction;
    public bool IsDead => dying && completed;
    public bool Paused => isPaused != null && isPaused();
    public float PresentationScale => presentationCanvas != null
        ? Mathf.Min(presentationCanvas.rect.width / 1920f, presentationCanvas.rect.height / 1080f) : 1f;
    public float BodyHeight => ReferenceBodyHeight * PresentationScale * (entry != null ? entry.visualScale : 1f);
    public float UnitsToPixels => BodyHeight / Mathf.Max(0.01f, entry != null ? entry.referenceBodyHeight : 3f);
    public float FacingSign => facingRight ? 1f : -1f;
    public float SourceMirror => entry != null && entry.sourceFacingRight != facingRight ? -1f : 1f;
    public int CurrentFrame => sequence != null ? FrameIndex(sequence, Mathf.Min(elapsed, duration)) : 0;
    public float CurrentSourceFrame => sequence != null ? SourceFrame(sequence, Mathf.Min(elapsed, duration)) : 0f;
    public BattleAnimationSequence CurrentSequence => sequence;
    public RectTransform ActorRoot => actorRoot;
    public Vector3 FootPosition => body != null ? body.rectTransform.position : transform.position;
    public Vector3 BodyCenter => FootPosition + transform.TransformVector(Vector3.up * BodyHeight * 0.5f);

    public bool Bind(Image target, RectTransform visualRoot, string runtimeIdentity, bool friendly, Func<bool> pauseQuery, params string[] keys)
    {
        BattleAnimationCatalog.Entry nextEntry = friendly ? BattleAnimationCatalog.Find(keys) : FindEnemyEntry(keys);
        isPaused = pauseQuery;
        if (nextEntry == null || !nextEntry.HasAction(BattleAnimationAction.Idle) && !nextEntry.HasAction(BattleAnimationAction.NormalAttack)
            && !nextEntry.HasAction(BattleAnimationAction.SkillAttack) && !nextEntry.HasAction(BattleAnimationAction.Buff))
        {
            Cancel();
            body = target;
            actorRoot = visualRoot;
            facingRight = friendly;
            return false;
        }
        bool unchanged = entry == nextEntry && identity == runtimeIdentity && body == target;
        if (unchanged && sequence != null) return true;
        Cancel();
        body = target;
        actorRoot = visualRoot;
        Canvas owningCanvas = target != null ? target.canvas : null;
        presentationCanvas = owningCanvas != null ? owningCanvas.rootCanvas.transform as RectTransform : null;
        entry = nextEntry;
        identity = runtimeIdentity;
        facingRight = friendly;
        homePosition = actorRoot.anchoredPosition;
        audioPlayer = GetComponent<BattleAnimationAudio>();
        if (audioPlayer == null) audioPlayer = gameObject.AddComponent<BattleAnimationAudio>();
        effects = GetComponent<BattleActionEffects>();
        if (effects == null) effects = gameObject.AddComponent<BattleActionEffects>();
        hitVisual = GetComponent<BattleActorHitVisual>();
        if (hitVisual == null) hitVisual = gameObject.AddComponent<BattleActorHitVisual>();
        hitVisual.Bind(body, actorRoot);
        CharacterIdleImage.Stop(body);
        dying = false;
        Rest();
        if (sequence != null) return true;
        Cancel();
        return false;
    }

    public static BattleAnimationCatalog.Entry FindEnemyEntry(params string[] keys)
    {
        if (keys == null) return null;
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || long.TryParse(key.Trim(), out _)) continue;
            BattleAnimationCatalog.Entry candidate = BattleAnimationCatalog.Find(key);
            if (candidate == null || string.IsNullOrEmpty(candidate.key)) continue;
            if (candidate.key.StartsWith("Monster", StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.key, "Whale", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    public BattleAnimationSequence Resolve(BattleAnimationAction action, string skillKey, int variantSeed)
    {
        if (entry == null) return null;
        BattleAnimationSequence selected = entry.Resolve(action, skillKey, variantSeed);
        if (selected == null && action == BattleAnimationAction.SkillAttack)
            selected = entry.Resolve(BattleAnimationAction.Buff, skillKey, variantSeed);
        if (selected == null && (action == BattleAnimationAction.SkillAttack || action == BattleAnimationAction.Buff))
            selected = entry.Resolve(BattleAnimationAction.NormalAttack, skillKey, variantSeed);
        return selected != null && selected.IsValid ? selected : null;
    }

    public IEnumerator PlayAction(BattleAnimationSequence selected, BattleActorAnimation target, BattleActorAnimation[] allies, BattleActorAnimation[] opponents, int variantSeed, Action<int> impact, BattleActorAnimation[] affectedTargets = null)
    {
        if (!IsBound || selected == null || !selected.IsValid || dying) yield break;
        int operation = ++generation;
        Vector2 origin = actorRoot.anchoredPosition;
        bool approach = target != null && (selected.approachDistance > 0f || selected.approachContact != Vector2.zero)
            && (selected.action == BattleAnimationAction.NormalAttack || selected.action == BattleAnimationAction.SkillAttack || selected.targetsEnemy);
        if (approach)
        {
            Vector3 contact;
            if (selected.approachContact != Vector2.zero)
            {
                Vector2 contactOffset = selected.approachContact * UnitsToPixels;
                contactOffset.x *= SourceMirror;
                Vector2 targetOffset = selected.attackTargetBodyOffset * target.BodyHeight;
                targetOffset.x *= SourceMirror;
                contact = target.BodyCenter + target.transform.TransformVector(targetOffset) - transform.TransformVector(contactOffset);
            }
            else
                contact = target.FootPosition - transform.TransformVector(Vector3.right * FacingSign * selected.approachDistance * UnitsToPixels);
            Vector2 destination = origin + (Vector2)actorRoot.parent.InverseTransformVector(contact - FootPosition);
            if (selected.blinkApproach)
                actorRoot.anchoredPosition = destination;
            else
                yield return Travel(destination, variantSeed, operation);
        }
        if (operation != generation || !isActiveAndEnabled) yield break;
        Begin(selected, false, impact);
        effects.Begin(this, selected, target, allies, opponents, variantSeed, affectedTargets);
        while (!completed && operation == generation && isActiveAndEnabled) yield return null;
        if (operation != generation || !isActiveAndEnabled) yield break;
        effects.Clear();
        if (!dying && selected.sequenceId == "Monster4_Spider_Attack") yield return SettleSpider(operation);
        if (approach && !dying) yield return Travel(origin, variantSeed, operation, true);
        if (operation == generation && !dying) Rest();
    }

    private IEnumerator Travel(Vector2 destination, int variantSeed, int operation, bool returning = false)
    {
        BattleAnimationSequence movement = Resolve(BattleAnimationAction.Move, null, variantSeed);
        Vector2 origin = actorRoot.anchoredPosition;
        // The imported clips include several seconds of staging. Use their authored
        // preparation / dash / recovery poses, without delaying every combat turn.
        float travelDuration = movement != null
            ? Mathf.Clamp(Vector2.Distance(origin, destination) / Mathf.Max(1f, 1300f * PresentationScale),
                returning ? .38f : .46f, returning ? .52f : .64f) : .18f;
        bool originalFacing = facingRight;
        if (Mathf.Abs(destination.x - origin.x) > 0.01f) facingRight = destination.x > origin.x;
        try
        {
            if (movement != null) Begin(movement, false, null);
            externalSampling = true;
            float travelElapsed = 0f;
            while (travelElapsed < travelDuration && operation == generation && isActiveAndEnabled)
            {
                if (!Paused && hitStopRemaining <= 0f)
                {
                    travelElapsed += Time.deltaTime;
                    float phase = Mathf.Clamp01(travelElapsed / travelDuration);
                    float progress = phase;
                    if (movement != null)
                    {
                        bool staged = HasMovementStages(movement);
                        progress = staged ? Mathf.InverseLerp(.2f, .76f, phase) : phase;
                        float frame = MovementFrame(movement, phase);
                        elapsed = SourceTime(movement, frame);
                        Sample(elapsed);
                    }
                    actorRoot.anchoredPosition = Vector2.Lerp(origin, destination, Mathf.SmoothStep(0f, 1f, progress));
                }
                yield return null;
            }
            if (operation == generation) actorRoot.anchoredPosition = destination;
        }
        finally
        {
            if (operation == generation)
            {
                facingRight = originalFacing;
                externalSampling = false;
                completed = true;
                audioPlayer.Clear();
                if (movementTrail != null) movementTrail.enabled = false;
            }
        }
    }

    private static float MovementFrame(BattleAnimationSequence movement, float phase)
    {
        bool legacyAssassin = movement.sequenceId == "Assassin_Move_01" && movement.movementTravelLastFrame <= movement.movementTravelFirstFrame;
        int first = Mathf.Clamp(legacyAssassin ? 36 : movement.movementTravelFirstFrame, 0, movement.FrameCount - 1);
        int last = Mathf.Clamp(legacyAssassin ? 71 : movement.movementTravelLastFrame, first, movement.FrameCount - 1);
        if (last <= first) return phase * (movement.FrameCount - 1);
        if (phase < .2f) return StageFrame(legacyAssassin ? AssassinPreparation : movement.movementPreparationFrames, phase / .2f, 0, first);
        if (phase <= .76f) return Mathf.Lerp(first, last, Mathf.InverseLerp(.2f, .76f, phase));
        return StageFrame(legacyAssassin ? AssassinRecovery : movement.movementRecoveryFrames, Mathf.InverseLerp(.76f, 1f, phase), last, movement.FrameCount - 1);
    }

    private static bool HasMovementStages(BattleAnimationSequence movement) => movement.movementTravelLastFrame > movement.movementTravelFirstFrame
        || movement.sequenceId == "Assassin_Move_01";

    private static float StageFrame(int[] frames, float progress, int first, int last)
    {
        if (frames == null || frames.Length == 0) return Mathf.Lerp(first, last, progress);
        // Hold each selected pose briefly; interpolating back through skipped frames
        // would reintroduce the source's lengthy preparation.
        return frames[Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * frames.Length), 0, frames.Length - 1)];
    }

    private IEnumerator SettleSpider(int operation)
    {
        BattleAnimationSequence idle = entry.Resolve(BattleAnimationAction.Idle);
        if (idle == null || !idle.IsValid) yield break;
        externalSampling = true;
        completed = false;
        float settleElapsed = 0f;
        Sprite finalPose = body.sprite;
        Sprite idlePose = idle.presentationFrames != null && idle.presentationFrames.Length == idle.FrameCount
            && idle.presentationFrames[0] != null ? idle.presentationFrames[0] : idle.frames[0];
        try
        {
            while (settleElapsed < .16f && operation == generation && isActiveAndEnabled)
            {
                if (!Paused && hitStopRemaining <= 0f)
                {
                    settleElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(settleElapsed / .16f);
                    // There is no authored claw-retraction frame. A single-sprite
                    // settle hides the hard cut without cross-fade ghost images.
                    Sprite pose = progress < .4f ? finalPose : idlePose;
                    SetSprite(body, pose, UnitsToPixels, SourceMirror);
                    float compression = Mathf.Sin(progress * Mathf.PI);
                    body.rectTransform.localScale = new Vector3(SourceMirror * (1f + compression * .025f), 1f - compression * .06f, 1f);
                    body.rectTransform.anchoredPosition = BaseBodyPosition;
                }
                yield return null;
            }
        }
        finally
        {
            if (operation == generation)
            {
                externalSampling = false;
                completed = true;
                body.rectTransform.localScale = new Vector3(SourceMirror, 1f, 1f);
            }
        }
    }

    public void React(bool defeated, int variantSeed, BattleHitFeedbackProfile profile = null, float direction = 1f)
    {
        if (!IsBound || dying) return;
        if (defeated)
        {
            generation++;
            dying = true;
            effects.Clear();
            externalSampling = false;
            if (hitVisual != null) hitVisual.Clear();
            BattleAnimationSequence death = Resolve(BattleAnimationAction.Death, null, variantSeed);
            if (death != null) Begin(death, false, null);
            else
            {
                fallbackDeath = true;
                fallbackDeathElapsed = 0f;
                fallbackDeathOrigin = body.rectTransform.anchoredPosition;
                fallbackDeathScale = body.rectTransform.localScale;
                looping = false;
                completed = false;
                onImpact = null;
                feedback = null;
                audioPlayer.Clear();
                if (movementTrail != null) movementTrail.enabled = false;
            }
            return;
        }
        BattleAnimationSequence hit = Resolve(BattleAnimationAction.Hit, null, variantSeed);
        if (hit != null && (!IsPlayingAction || sequence.action == BattleAnimationAction.Hit)) Begin(hit, false, null);
        feedback = profile;
        feedbackElapsed = 0f;
        feedbackDirection = direction;
        if (hitVisual != null) hitVisual.Show(profile, 0f, BodyHeight, PresentationScale, SourceMirror);
        if (profile != null) HoldFor(profile.hitStop);
    }

    public void HoldFor(float seconds) { hitStopRemaining = Mathf.Max(hitStopRemaining, seconds); }

    public BattleHitFeedbackProfile FeedbackAt(int index)
    {
        return sequence != null && sequence.impactFeedback != null && index >= 0 && index < sequence.impactFeedback.Length
            ? sequence.impactFeedback[index] : null;
    }

    public void Rest()
    {
        if (!IsBound || dying) return;
        actorRoot.anchoredPosition = homePosition;
        BattleAnimationSequence idle = entry.Resolve(BattleAnimationAction.Idle);
        if (idle == null)
        {
            idle = Resolve(BattleAnimationAction.NormalAttack, null, 0) ?? Resolve(BattleAnimationAction.SkillAttack, null, 0);
            if (idle == null) return;
            Begin(idle, false, null);
            completed = true;
            audioPlayer.Clear();
            return;
        }
        Begin(idle, true, null);
    }

    private void Begin(BattleAnimationSequence next, bool repeat, Action<int> impact)
    {
        sequence = next;
        elapsed = 0f;
        duration = Mathf.Max(0.01f, next.Duration);
        looping = repeat;
        completed = false;
        impactCursor = 0;
        onImpact = impact;
        externalSampling = false;
        fallbackDeath = false;
        feedback = null;
        if (hitVisual != null) hitVisual.Clear();
        body.enabled = true;
        body.color = Color.white;
        audioPlayer.Begin(next, duration);
        Sample(0f);
    }

    private void Update()
    {
        if (!IsBound || sequence == null) return;
        bool pausedNow = Paused;
        if (!pausedNow && hitStopRemaining > 0f)
        {
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - Time.deltaTime);
            audioPlayer.Sample(SourceFrame(sequence, Mathf.Min(elapsed, duration)), true);
            return;
        }
        if (pausedNow)
        {
            audioPlayer.Sample(SourceFrame(sequence, Mathf.Min(elapsed, duration)), true);
            return;
        }
        if (fallbackDeath)
        {
            fallbackDeathElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(fallbackDeathElapsed / .32f);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            body.rectTransform.anchoredPosition = fallbackDeathOrigin + Vector2.down * (BodyHeight * .08f * eased);
            body.rectTransform.localScale = fallbackDeathScale * Mathf.Lerp(1f, .96f, eased);
            body.color = new Color(1f, 1f, 1f, 1f - eased);
            if (progress >= 1f)
            {
                fallbackDeath = false;
                completed = true;
                body.enabled = false;
            }
            return;
        }
        if (!completed && !externalSampling)
        {
            elapsed += Time.deltaTime;
            if (looping && elapsed >= duration)
            {
                elapsed %= duration;
                audioPlayer.Begin(sequence, duration);
            }
            Sample(Mathf.Min(elapsed, duration));
            if (!looping && elapsed >= duration)
            {
                completed = true;
                audioPlayer.Clear();
                effects.Clear();
                if (dying) body.enabled = false;
                else if (sequence.action == BattleAnimationAction.Hit) Rest();
            }
        }
        ApplyFeedback();
    }

    private void Sample(float time)
    {
        int frame = FrameIndex(sequence, time);
        Sprite[] presentation = sequence.presentationFrames;
        Sprite sprite = presentation != null && presentation.Length == sequence.FrameCount && presentation[frame] != null
            ? presentation[frame] : sequence.frames[frame];
        if (sequence.action == BattleAnimationAction.Move && sequence.movementBodyFrames != null && sequence.movementBodyFrames.Length == sequence.FrameCount && sequence.movementBodyFrames[frame] != null)
            sprite = sequence.movementBodyFrames[frame];
        SetSprite(body, sprite, UnitsToPixels, SourceMirror);
        body.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        body.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        body.rectTransform.anchoredPosition = BaseBodyPosition + TailFootCorrection(time);
        SampleMovementTrail(frame);
        renderedPresentationScale = PresentationScale;
        float sourceFrame = SourceFrame(sequence, time);
        audioPlayer.Sample(sourceFrame, false);
        effects.Sample(sourceFrame);
        if (onImpact == null) return;
        int count = sequence.impactFrames != null && sequence.impactFrames.Length > 0 ? sequence.impactFrames.Length : 1;
        while (impactCursor < count)
        {
            int impactFrame = sequence.impactFrames != null && sequence.impactFrames.Length > 0
                ? sequence.impactFrames[impactCursor] : sequence.FrameCount / 2;
            if (frame < Mathf.Clamp(impactFrame, 0, sequence.FrameCount - 1)) break;
            int index = impactCursor++;
            onImpact(index);
        }
    }

    private void LateUpdate()
    {
        if (!IsBound || sequence == null || body.sprite == null) return;
        float scale = PresentationScale;
        if (Mathf.Approximately(renderedPresentationScale, scale)) return;
        Vector2 footOffset = new Vector2(0f, -8f);
        Vector2 previousOffset = body.rectTransform.anchoredPosition - footOffset;
        SetSprite(body, body.sprite, UnitsToPixels, SourceMirror);
        body.rectTransform.anchoredPosition = footOffset + previousOffset * (scale / Mathf.Max(0.01f, renderedPresentationScale));
        SampleMovementTrail(CurrentFrame);
        renderedPresentationScale = scale;
        if (effects != null) effects.Sample(CurrentSourceFrame);
    }

    private void ApplyFeedback()
    {
        if (feedback == null || body == null || dying) return;
        feedbackElapsed += Time.deltaTime;
        float remaining = 1f - Mathf.Clamp01(feedbackElapsed / Mathf.Max(0.01f, feedback.shakeDuration));
        float shake = Mathf.Sin(feedbackElapsed * 110f) * feedback.shakePixels * remaining;
        float recoilDuration = Mathf.Max(0.01f, feedback.recoilAttack + feedback.recoilReturn);
        float recoilProgress = Mathf.Clamp01(feedbackElapsed / recoilDuration);
        float recoil = feedback.recoilMode == BattleHitRecoilMode.SoftArc
            ? Mathf.Sin(recoilProgress * Mathf.PI) * (1f - recoilProgress)
            : feedbackElapsed < feedback.recoilAttack
                ? Mathf.Sin(Mathf.Clamp01(feedbackElapsed / Mathf.Max(.001f, feedback.recoilAttack)) * Mathf.PI * .5f)
                : Mathf.Pow(1f - Mathf.Clamp01((feedbackElapsed - feedback.recoilAttack) / Mathf.Max(.001f, feedback.recoilReturn)), 2f);
        float scale = PresentationScale;
        body.rectTransform.anchoredPosition = BaseBodyPosition + TailFootCorrection(Mathf.Min(elapsed, duration))
            + new Vector2((shake + recoil * feedback.recoilPixels) * feedbackDirection * scale,
                (feedback.shakeMode == BattleHitShakeMode.DualAxis ? shake * .45f : 0f) * scale);
        if (hitVisual != null) hitVisual.Show(feedback, feedbackElapsed, BodyHeight, scale, SourceMirror);
        float visualDuration = Mathf.Max(feedback.flashDuration + feedback.afterglowDuration, feedback.sparkDuration);
        if (remaining <= 0f && feedbackElapsed >= Mathf.Max(recoilDuration, visualDuration))
        {
            body.color = Color.white;
            body.rectTransform.anchoredPosition = BaseBodyPosition + TailFootCorrection(Mathf.Min(elapsed, duration));
            if (hitVisual != null) hitVisual.Clear();
            feedback = null;
        }
    }

    private Vector2 TailFootCorrection(float time)
    {
        if (sequence == null || externalSampling) return Vector2.zero;
        // Measurements from the imported poses, in source world units. This affects
        // only the landing / final recovery; jumping attacks retain their full arc.
        float offset = sequence.sequenceId == "Swordsman_Hit_01" ? .51f
            : sequence.sequenceId == "Swordsman_SkillAttack_03" ? -.37f : 0f;
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Max(0f, duration - .18f), duration, time));
        return Vector2.up * (offset * UnitsToPixels * weight);
    }

    private void SampleMovementTrail(int frame)
    {
        bool visible = sequence.action == BattleAnimationAction.Move && sequence.movementTrailFrames != null
            && sequence.movementTrailFrames.Length == sequence.FrameCount && sequence.movementTrailFrames[frame] != null;
        if (!visible)
        {
            if (movementTrail != null) movementTrail.enabled = false;
            return;
        }
        if (movementTrail == null)
        {
            GameObject visual = new GameObject("Battle Movement Trail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visual.transform.SetParent(actorRoot, false);
            movementTrail = visual.GetComponent<Image>();
            movementTrail.rectTransform.SetSiblingIndex(body.rectTransform.GetSiblingIndex());
        }
        movementTrail.enabled = true;
        movementTrail.color = Color.white;
        SetSprite(movementTrail, sequence.movementTrailFrames[frame], UnitsToPixels, SourceMirror);
        movementTrail.rectTransform.anchorMin = body.rectTransform.anchorMin;
        movementTrail.rectTransform.anchorMax = body.rectTransform.anchorMax;
        movementTrail.rectTransform.anchoredPosition = body.rectTransform.anchoredPosition;
    }

    public static int FrameIndex(BattleAnimationSequence selected, float time)
    {
        if (selected == null || !selected.IsValid) return 0;
        if (time >= selected.Duration) return selected.FrameCount - 1;
        if (selected.frameTimes != null && selected.frameTimes.Length == selected.FrameCount)
        {
            int low = 0;
            int high = selected.FrameCount - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (selected.frameTimes[middle] <= time) low = middle;
                else high = middle - 1;
            }
            return low;
        }
        return Mathf.Clamp(Mathf.FloorToInt(time / Mathf.Max(0.01f, selected.Duration) * selected.FrameCount), 0, selected.FrameCount - 1);
    }

    public static float SourceFrame(BattleAnimationSequence selected, float time)
    {
        if (selected == null || !selected.IsValid || time <= 0f) return 0f;
        if (time >= selected.Duration) return selected.FrameCount;
        if (selected.frameTimes == null || selected.frameTimes.Length != selected.FrameCount)
            return time / Mathf.Max(0.01f, selected.Duration) * selected.FrameCount;
        int frame = FrameIndex(selected, time);
        float start = selected.frameTimes[frame];
        float end = frame + 1 < selected.FrameCount ? selected.frameTimes[frame + 1] : selected.Duration;
        return frame + (end > start ? Mathf.Clamp01((time - start) / (end - start)) : 0f);
    }

    public static float SourceTime(BattleAnimationSequence selected, float sourceFrame)
    {
        if (selected == null || !selected.IsValid || sourceFrame <= 0f) return 0f;
        if (sourceFrame >= selected.FrameCount) return selected.Duration;
        if (selected.frameTimes == null || selected.frameTimes.Length != selected.FrameCount)
            return sourceFrame / selected.FrameCount * selected.Duration;
        int frame = Mathf.Clamp(Mathf.FloorToInt(sourceFrame), 0, selected.FrameCount - 1);
        float start = selected.frameTimes[frame];
        float end = frame + 1 < selected.FrameCount ? selected.frameTimes[frame + 1] : selected.Duration;
        return Mathf.Lerp(start, end, sourceFrame - frame);
    }

    public static void SetSprite(Image image, Sprite sprite, float unitsToPixels, float mirror)
    {
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
        rect.sizeDelta = sprite.rect.size / Mathf.Max(0.01f, sprite.pixelsPerUnit) * unitsToPixels;
        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(mirror, 1f, 1f);
    }

    public void Cancel()
    {
        generation++;
        if (effects != null) effects.Clear();
        if (audioPlayer != null) audioPlayer.Clear();
        if (hitVisual != null) hitVisual.Clear();
        if (movementTrail != null)
        {
            movementTrail.enabled = false;
            movementTrail.sprite = null;
        }
        if (actorRoot != null && entry != null) actorRoot.anchoredPosition = homePosition;
        if (body != null)
        {
            body.enabled = true;
            body.color = Color.white;
            body.sprite = null;
        }
        sequence = null;
        entry = null;
        onImpact = null;
        feedback = null;
        externalSampling = false;
        fallbackDeath = false;
        hitStopRemaining = 0f;
        dying = false;
        completed = true;
    }

    private void OnDisable() { Cancel(); }
}
