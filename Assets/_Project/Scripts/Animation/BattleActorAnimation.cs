using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleActorAnimation : MonoBehaviour
{
    public const float ReferenceBodyHeight = 124f;
    private const float BattleBodyScale = 1.10f;
    public float SideSizeMultiplier { get; private set; } = 1f;

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
    private bool friendlyActor;
    private int friendlyHitIndex;
    private float hitPreparationSpeed = 1f;
    private bool hasPreparedHit;
    private int generation;
    private int impactCursor;
    private Action<int> onImpact;
    private Func<bool> isPaused;
    private BattleHitFeedbackProfile feedback;
    private float feedbackElapsed;
    private float contactEffectScale = 1f;
    private float feedbackSpeed = 1f;
    private Vector2 feedbackDirection;
    private Vector2 contactDirection;
    private Vector2 staticBodyRest;
    private int contactFrame;
    private Image movementTrail;
    private float hitStopRemaining;
    private int holdStartFrame;
    public bool HitStopped => hitStopRemaining > 0f;
    private RectTransform presentationCanvas;
    private float renderedPresentationScale = 1f;
    private bool externalSampling;
    private float travelPoseOffsetX;
    private BattleActorAnimation meleeTarget;
    private Vector2 crossThroughSoleRange;
    private float crossThroughStride;
    private bool fallbackDeath;
    private float fallbackDeathElapsed;
    private Vector2 fallbackDeathOrigin;
    private Vector3 fallbackDeathScale;
    private BattleActorHitVisual hitVisual;

    internal Vector2 BaseBodyPosition => new Vector2(0f, -8f) + entry.offset * UnitsToPixels;

    public bool IsBound => entry != null && body != null;
    public bool IsPlayingAction => fallbackDeath || (IsBound && sequence != null && !looping && !completed);
    public bool IsPlayingDeath => dying && IsPlayingAction;
    public bool IsDead => dying && completed;
    public bool Paused => isPaused != null && isPaused();
    public float PresentationScale => presentationCanvas != null
        ? Mathf.Min(presentationCanvas.rect.width / 1920f, presentationCanvas.rect.height / 1080f) : 1f;
    public float BodyHeight => ReferenceBodyHeight * SideSizeMultiplier * PresentationScale * (entry != null ? entry.visualScale : 1f);
    public float UnitsToPixels => BodyHeight / Mathf.Max(0.01f, entry != null ? entry.referenceBodyHeight : 3f);
    public BattleAnimationCatalog.Entry Appearance => entry;
    public float FacingSign => facingRight ? 1f : -1f;
    public float SourceMirror => entry != null && entry.sourceFacingRight != facingRight ? -1f : 1f;
    public int CurrentFrame => sequence != null ? FrameIndex(sequence, Mathf.Min(elapsed, duration)) : 0;
    public float CurrentSourceFrame => sequence != null ? SourceFrame(sequence, Mathf.Min(elapsed, duration)) : 0f;
    public BattleAnimationSequence CurrentSequence => sequence;
    public BattleAnimationSequence DisplayedPoseSequence { get; private set; }
    public int DisplayedPoseFrame { get; private set; }
    public RectTransform ActorRoot => actorRoot;
    internal bool InMeleeContact => meleeTarget != null && !dying && IsPlayingAction;
    public Vector3 FootPosition => body != null ? body.rectTransform.position : transform.position;
    public Vector3 BodyCenter => FootPosition + transform.TransformVector(Vector3.up * BodyHeight * 0.5f);

    public bool Bind(Image target, RectTransform visualRoot, string runtimeIdentity, bool friendly, Func<bool> pauseQuery, params string[] keys)
    {
        friendlyActor = friendly;
        SideSizeMultiplier = BattleBodyScale * (friendly ? 1.10f : 1f);
        BattleAnimationCatalog.Entry nextEntry = friendly ? BattleAnimationCatalog.Find(keys) : FindEnemyEntry(keys);
        isPaused = pauseQuery;
        if (nextEntry == null || !nextEntry.HasAction(BattleAnimationAction.Idle) && !nextEntry.HasAction(BattleAnimationAction.NormalAttack)
            && !nextEntry.HasAction(BattleAnimationAction.SkillAttack) && !nextEntry.HasAction(BattleAnimationAction.Buff))
        {
            if (entry == null && identity == runtimeIdentity && body == target) return false;
            Cancel();
            body = target;
            actorRoot = visualRoot;
            identity = runtimeIdentity;
            facingRight = friendly;
            BindHitVisual(friendly);
            return false;
        }
        bool unchanged = entry == nextEntry && identity == runtimeIdentity && body == target;
        if (unchanged && sequence != null) return true;
        Cancel();
        body = target;
        actorRoot = visualRoot;
        entry = nextEntry;
        identity = runtimeIdentity;
        facingRight = friendly;
        homePosition = actorRoot.anchoredPosition;
        audioPlayer = GetComponent<BattleAnimationAudio>();
        if (audioPlayer == null) audioPlayer = gameObject.AddComponent<BattleAnimationAudio>();
        effects = GetComponent<BattleActionEffects>();
        if (effects == null) effects = gameObject.AddComponent<BattleActionEffects>();
        BindHitVisual(friendly);
        CharacterIdleImage.Stop(body);
        dying = false;
        Rest();
        if (sequence != null) return true;
        Cancel();
        return false;
    }

    public static BattleAnimationCatalog.Entry FindEnemyEntry(params string[] keys) => FindEnemyEntry(0L, keys);

    public static BattleAnimationCatalog.Entry FindEnemyEntry(long definitionId, params string[] keys)
    {
        uint identity = (uint)definitionId;
        foreach (string key in keys ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            BattleAnimationCatalog.Entry candidate = BattleAnimationCatalog.Find(key);
            if (candidate != null && candidate.key.StartsWith("Elite", StringComparison.Ordinal)) return candidate;
            if (identity == 0)
                foreach (char character in key.Trim()) identity = unchecked(identity * 31u + character);
        }
        if (definitionId > 0)
        {
            var byId = BattleAnimationCatalog.Find(definitionId.ToString());
            if (byId != null && byId.key.StartsWith("Elite", StringComparison.Ordinal)) return byId;
        }
        return BattleAnimationCatalog.Find("Elite0" + (1u + identity % 4u));
    }

    private void BindHitVisual(bool friendly)
    {
        Canvas owner = body != null ? body.canvas : null;
        presentationCanvas = owner != null ? owner.rootCanvas.transform as RectTransform : null;
        hitVisual = GetComponent<BattleActorHitVisual>();
        if (hitVisual == null) hitVisual = gameObject.AddComponent<BattleActorHitVisual>();
        hitVisual.Bind(body, actorRoot, friendly ? null : this);
    }

    public BattleAnimationSequence Resolve(BattleAnimationAction action, string skillKey, int variantSeed, bool? enemyTargets = null, int allyCount = 0)
    {
        if (entry == null) return null;
        BattleAnimationSequence selected = entry.Resolve(action, skillKey, variantSeed, action == BattleAnimationAction.Buff ? enemyTargets : null, allyCount);
        if (selected == null && action == BattleAnimationAction.SkillAttack)
            selected = entry.Resolve(BattleAnimationAction.Buff, skillKey, variantSeed, enemyTargets, allyCount);
        if (selected == null && (action == BattleAnimationAction.SkillAttack || action == BattleAnimationAction.Buff))
            selected = entry.Resolve(BattleAnimationAction.NormalAttack, skillKey, variantSeed);
        return selected != null && selected.IsValid ? selected : null;
    }

    public IEnumerator PlayAction(BattleAnimationSequence selected, BattleActorAnimation target, BattleActorAnimation[] allies, BattleActorAnimation[] opponents, int variantSeed, Action<int> impact, BattleActorAnimation[] affectedTargets = null, Action<float> prepareTargets = null, Action followUpTargetReaction = null)
    {
        if (!IsBound || selected == null || !selected.IsValid || dying) yield break;
        int operation = ++generation;
        Vector2 origin = actorRoot.anchoredPosition;
        crossThroughStride = 0f;
        bool approach = target != null && (selected.approachDistance > 0f || selected.approachContact != Vector2.zero)
            && (selected.action == BattleAnimationAction.NormalAttack || selected.action == BattleAnimationAction.SkillAttack || selected.targetsEnemy);
        if (approach)
        {
            Vector3 contact;
            if (selected.approachContact != Vector2.zero)
            {
                Vector2 contactOffset = selected.approachContact * UnitsToPixels;
                float attackMirror = SourceMirror;
                contactOffset.x *= attackMirror;
                Vector2 targetOffset = selected.attackTargetBodyOffset * target.BodyHeight;
                targetOffset.x *= attackMirror;
                contact = target.BodyCenter + target.transform.TransformVector(targetOffset) - transform.TransformVector(contactOffset);
            }
            else
                contact = target.FootPosition - transform.TransformVector(Vector3.right * FacingSign * selected.approachDistance * UnitsToPixels);
            Vector3 ground = target.GroundShadowCenter();
            Vector3 restingGround = GroundShadowCenter();
            if (selected.authoredCrossThrough && selected.impactFrames != null && selected.impactFrames.Length > 0 &&
                selected.TryGetFootContacts(0, out var startLeft, out var startRight) &&
                selected.TryGetFootContacts(selected.impactFrames[0], out var endLeft, out var endRight))
            {
                crossThroughSoleRange = new Vector2((startLeft.x + startRight.x) * .5f, (endLeft.x + endRight.x) * .5f);
                float midpoint = (crossThroughSoleRange.x + crossThroughSoleRange.y) * .5f;
                float bodyWidth = target.body.rectTransform.rect.width;
                float extraFront = Mathf.Clamp(bodyWidth * .29f, target.BodyHeight * .22f, target.BodyHeight * .40f);
                float extraRear = extraFront;
                if (presentationCanvas != null)
                {
                    float x = presentationCanvas.InverseTransformPoint(ground).x;
                    float room = FacingSign > 0f ? presentationCanvas.rect.xMax - x : x - presentationCanvas.rect.xMin;
                    float originalGap = Mathf.Abs(crossThroughSoleRange.y - midpoint) * BodyHeight;
                    extraRear = Mathf.Clamp(room - originalGap - BodyHeight * .29f, 0f, extraRear);
                }
                crossThroughStride = (extraFront + extraRear) * FacingSign;
                float offset = midpoint * BodyHeight * SourceMirror + extraFront * FacingSign;
                contact.x = ground.x - transform.TransformVector(Vector3.right * offset).x;
            }
            contact.y = FootPosition.y + ground.y - restingGround.y;
            Vector2 destination = origin + (Vector2)actorRoot.parent.InverseTransformVector(contact - FootPosition);
            if (selected.blinkApproach)
                actorRoot.anchoredPosition = destination;
            else
                yield return Travel(destination, variantSeed, operation);
        }
        if (operation != generation || !isActiveAndEnabled) yield break;
        if (prepareTargets != null)
        {
            int firstImpact = selected.impactFrames != null && selected.impactFrames.Length > 0
                ? Mathf.Clamp(selected.impactFrames[0], 0, selected.FrameCount - 1) : selected.FrameCount / 2;
            prepareTargets(SourceTime(selected, firstImpact));
        }
        meleeTarget = approach ? target : null;
        Begin(selected, false, impact);
        effects.Begin(this, selected, target, allies, opponents, variantSeed, affectedTargets);
        while (!completed && operation == generation && isActiveAndEnabled)
        {
            yield return null;
            if (followUpTargetReaction != null && operation == generation && isActiveAndEnabled
                && sequence == selected && CurrentFrame >= selected.followUpTargetReactionFrame)
            {
                followUpTargetReaction();
                followUpTargetReaction = null;
            }
        }
        if (operation != generation || !isActiveAndEnabled) yield break;
        effects.Clear();
        meleeTarget = null;
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
        bool matchRecovery = returning && sequence != null && sequence.recoveryFootX != 0f && movement != null;
        float landingX = matchRecovery ? sequence.recoveryFootX * SourceMirror : 0f;
        if (Mathf.Abs(destination.x - origin.x) > 0.01f) facingRight = destination.x > origin.x;
        float departureOffset = matchRecovery ? landingX - movement.departureFootX * SourceMirror : 0f;
        try
        {
            travelPoseOffsetX = departureOffset;
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
                        travelPoseOffsetX = departureOffset * (1f - Mathf.SmoothStep(0f, 1f, progress));
                        float frame = MovementFrame(movement, phase);
                        elapsed = SourceTime(movement, frame);
                        Sample(elapsed);
                    }
                    float along = Mathf.SmoothStep(0f, 1f, progress);
                    actorRoot.anchoredPosition = Vector2.Lerp(origin, destination, along);
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
                travelPoseOffsetX = 0f;
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
        int finalFrame = DisplayedPoseFrame;
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
                    bool showingAttack = progress < .4f;
                    Sprite pose = showingAttack ? finalPose : idlePose;
                    DisplayedPoseSequence = showingAttack ? sequence : idle;
                    DisplayedPoseFrame = showingAttack ? finalFrame : 0;
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
        if (body == null || dying) return;
        if (defeated)
        {
            meleeTarget = null;
            generation++;
            dying = true;
            if (effects != null) effects.Clear();
            externalSampling = false;
            BattleAnimationSequence death = Resolve(BattleAnimationAction.Death, null, variantSeed);
            if (death != null) Begin(death, false, null);
            else
            {
                fallbackDeath = true;
                fallbackDeathElapsed = 0f;
                fallbackDeathOrigin = feedback == null ? body.rectTransform.anchoredPosition : FeedbackRestPosition();
                fallbackDeathScale = body.rectTransform.localScale;
                looping = false;
                completed = false;
                onImpact = null;
                if (audioPlayer != null) audioPlayer.Clear();
                if (movementTrail != null) movementTrail.enabled = false;
            }
            return;
        }
        ReceiveImpact(variantSeed, profile, Vector2.right * direction, true, profile != null ? profile.hitStop : 0f);
    }

    public void PrepareFriendlyHit(float leadSeconds)
    {
        if (!friendlyActor || !IsBound || dying || leadSeconds <= 0f
            || (IsPlayingAction && sequence.action != BattleAnimationAction.Hit)) return;
        BattleAnimationSequence hit = Resolve(BattleAnimationAction.Hit, null, friendlyHitIndex);
        if (hit == null || hit.DefenseFrame == 0) return;
        Begin(hit, false, null);
        hitPreparationSpeed = SourceTime(hit, hit.DefenseFrame) / leadSeconds;
        hasPreparedHit = true;
    }

    public void ReceiveImpact(int variantSeed, BattleHitFeedbackProfile profile, Vector2 direction, bool restartHit, float stop, float staticDuration = 0f, float sparkScale = 1f)
    {
        if (body == null || dying) return;
        if (restartHit)
        {
            BattleAnimationSequence hit = hasPreparedHit ? sequence
                : Resolve(BattleAnimationAction.Hit, null, friendlyActor ? friendlyHitIndex : variantSeed);
            if (hit != null && (!IsPlayingAction || sequence.action == BattleAnimationAction.Hit))
            {
                if (!hasPreparedHit)
                {
                    float startTime = friendlyActor ? 0f : SourceTime(hit, hit.DefenseFrame);
                    Begin(hit, false, null, startTime);
                }
                if (friendlyActor)
                {
                    friendlyHitIndex++;
                    hasPreparedHit = false;
                    hitPreparationSpeed = 1f;
                    elapsed = SourceTime(hit, hit.DefenseFrame) + .000001f;
                    Sample(elapsed);
                }
            }
        }
        HoldFor(stop);
        if (profile == null) return;
        Vector2 incoming = ((Vector2)actorRoot.InverseTransformVector(direction)).normalized;
        if (incoming == Vector2.zero) incoming = Vector2.left * FacingSign;
        hitVisual?.PlayPaintedContact(profile, incoming);
        if (feedback != null && feedback.priority > profile.priority) return;
        contactEffectScale = sparkScale;
        if (!IsBound && feedback == null) staticBodyRest = body.rectTransform.anchoredPosition;
        feedback = profile;
        feedbackElapsed = 0f;
        feedbackSpeed = !IsBound && staticDuration > 0f ? Mathf.Max(profile.sparkDuration,
            Mathf.Max(profile.flashDuration + profile.afterglowDuration, profile.recoilAttack + profile.recoilReturn)) / staticDuration : 1f;
        contactFrame = Time.frameCount;
        contactDirection = incoming;
        feedbackDirection = profile.horizontalRecoil ? Vector2.right * Mathf.Sign(contactDirection.x) : contactDirection;
        ApplyFeedback(FeedbackRestPosition());
    }

    public void HoldFor(float seconds)
    {
        if (seconds <= 0f) return;
        hitStopRemaining = Mathf.Max(hitStopRemaining, seconds);
        holdStartFrame = Time.frameCount;
    }
    public BattleHitFeedbackProfile FeedbackAt(int index)
    {
        return sequence != null && sequence.impactFeedback != null && index >= 0 && index < sequence.impactFeedback.Length
            ? sequence.impactFeedback[index] : null;
    }

    public void Rest()
    {
        meleeTarget = null;
        if (!IsBound || dying) return;
        facingRight = friendlyActor;
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

    private void Begin(BattleAnimationSequence next, bool repeat, Action<int> impact, float startTime = 0f)
    {
        hasPreparedHit = false;
        hitPreparationSpeed = 1f;
        sequence = next;
        elapsed = startTime;
        duration = Mathf.Max(0.01f, next.Duration);
        looping = repeat;
        completed = false;
        impactCursor = 0;
        onImpact = impact;
        externalSampling = false;
        fallbackDeath = false;
        body.enabled = true;
        body.color = Color.white;
        audioPlayer.Begin(next, duration);
        Sample(elapsed);
    }

    private void Update()
    {
        if (body == null) return;
        bool pausedNow = Paused;
        if (pausedNow || hitStopRemaining > 0f)
        {
            if (!pausedNow)
            {
                if (Time.frameCount != holdStartFrame) hitStopRemaining = Mathf.Max(0f, hitStopRemaining - Time.deltaTime);
                if (feedback != null) ApplyFeedback(FeedbackRestPosition());
            }
            if (audioPlayer != null && sequence != null) audioPlayer.SetPaused(pausedNow);
            return;
        }
        if (fallbackDeath)
        {
            fallbackDeathElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(fallbackDeathElapsed / .32f);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector2 position = fallbackDeathOrigin + Vector2.down * (BodyHeight * .08f * eased);
            body.rectTransform.anchoredPosition = position;
            if (feedback != null) ApplyFeedback(position);
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
        if (!IsBound || sequence == null)
        {
            if (feedback != null) ApplyFeedback(staticBodyRest);
            return;
        }
        if (!completed && !externalSampling)
        {
            float nextTime = elapsed + Time.deltaTime * hitPreparationSpeed;
            if (hasPreparedHit && nextTime >= SourceTime(sequence, sequence.DefenseFrame))
                hitPreparationSpeed = 1f;
            if (onImpact != null && impactCursor < ImpactCount)
            {
                float contactTime = SourceTime(sequence, NextImpactFrame()) + .000001f;
                if (elapsed < contactTime) nextTime = Mathf.Min(nextTime, contactTime);
            }
            elapsed = nextTime;
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
        if (feedback != null) ApplyFeedback(FeedbackRestPosition());
    }

    private void Sample(float time)
    {
        int frame = FrameIndex(sequence, time);
        DisplayedPoseSequence = sequence;
        DisplayedPoseFrame = frame;
        Sprite[] presentation = sequence.presentationFrames;
        Sprite sprite = presentation != null && presentation.Length == sequence.FrameCount && presentation[frame] != null
            ? presentation[frame] : sequence.frames[frame];
        if (sequence.action == BattleAnimationAction.Move && sequence.movementBodyFrames != null && sequence.movementBodyFrames.Length == sequence.FrameCount && sequence.movementBodyFrames[frame] != null)
            sprite = sequence.movementBodyFrames[frame];
        SetSprite(body, sprite, UnitsToPixels, SourceMirror);
        body.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        body.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        body.rectTransform.anchoredPosition = BaseBodyPosition + TailFootCorrection(time);
        AlignMeleeGround();
        SampleMovementTrail(frame);
        renderedPresentationScale = PresentationScale;
        float sourceFrame = SourceFrame(sequence, time);
        audioPlayer.Sample(sourceFrame, false);
        effects.Sample(sourceFrame);
        if (onImpact == null) return;
        while (impactCursor < ImpactCount)
        {
            if (frame < NextImpactFrame()) break;
            int index = impactCursor++;
            onImpact(index);
        }
    }

    private int ImpactCount => Mathf.Max(1, sequence.impactFrames?.Length ?? 0);
    private int NextImpactFrame() => sequence.impactFrames != null && sequence.impactFrames.Length > 0
        ? Mathf.Clamp(sequence.impactFrames[impactCursor], 0, sequence.FrameCount - 1) : sequence.FrameCount / 2;

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

    internal Vector3 GroundShadowCenter()
    {
        RectTransform visual = body.rectTransform;
        if (!IsBound && TryGetComponent<IdleAdventureActorView>(out var view))
        {
            view.GetStaticSoles(out Vector2 left, out Vector2 right);
            return visual.TransformPoint((left + right) * .5f);
        }
        Vector2 center = entry != null ? entry.groundShadowOffset : Vector2.zero;
        var pose = DisplayedPoseSequence;
        if (pose != null && pose.TryGetFootContacts(DisplayedPoseFrame, out Vector2 leftSole, out Vector2 rightSole))
        {
            center = (leftSole + rightSole) * .5f;
            if (pose.action != BattleAnimationAction.Idle)
                center.y -= GroundLift(leftSole, rightSole);
        }
        return visual.TransformPoint(center * BodyHeight);
    }

    internal static float GroundLift(Vector2 left, Vector2 right)
    {
        float lower = Mathf.Min(left.y, right.y);
        return Mathf.Max(0f, lower - .02f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.055f, .16f, lower));
    }

    private void AlignMeleeGround()
    {
        if (meleeTarget == null || dying || meleeTarget.body == null) return;
        RectTransform visual = body.rectTransform;
        Vector3 own = GroundShadowCenter();
        Vector3 targetGround = meleeTarget.GroundShadowCenter();
        Vector3 separation = visual.parent.InverseTransformVector(own - targetGround);
        float minGap = (BodyHeight + meleeTarget.BodyHeight) * .16f;
        if (sequence.authoredCrossThrough)
        {
            // Extend only the distance already travelled by the feet in the authored sprite.
            float travel = 0f;
            if (crossThroughStride != 0f && sequence.TryGetFootContacts(DisplayedPoseFrame, out var left, out var right))
                travel = Mathf.InverseLerp(crossThroughSoleRange.x, crossThroughSoleRange.y, (left.x + right.x) * .5f);
            visual.anchoredPosition += new Vector2(travel * crossThroughStride, -separation.y);
            return;
        }
        float behind = Mathf.Max(0f, separation.x * FacingSign + minGap);
        visual.anchoredPosition += new Vector2(-FacingSign * behind, -separation.y);
    }

    private Vector2 FeedbackRestPosition() => IsBound ? BaseBodyPosition + TailFootCorrection(Mathf.Min(elapsed, duration)) : staticBodyRest;

    private void ApplyFeedback(Vector2 restPosition)
    {
        if (Time.frameCount != contactFrame) feedbackElapsed += Time.deltaTime * feedbackSpeed;
        float recoilDuration = Mathf.Max(0.01f, feedback.recoilAttack + feedback.recoilReturn);
        float recoilProgress = Mathf.Clamp01(feedbackElapsed / recoilDuration);
        float recoil = feedback.recoilMode == BattleHitRecoilMode.SoftArc
            ? Mathf.Sin(recoilProgress * Mathf.PI) * (1f - recoilProgress)
            : feedbackElapsed < feedback.recoilAttack
                ? Mathf.Lerp(.8f, 1f, Mathf.Sin(feedbackElapsed / feedback.recoilAttack * Mathf.PI * .5f))
                : Mathf.Pow(1f - Mathf.Clamp01((feedbackElapsed - feedback.recoilAttack) / Mathf.Max(.001f, feedback.recoilReturn)), 2f);
        float scale = PresentationScale;
        float pixels = presentationCanvas != null ? presentationCanvas.rect.height / 720f : 1f;
        body.rectTransform.anchoredPosition = restPosition + feedbackDirection * (recoil * feedback.recoilPixels * pixels);
        if (hitVisual != null) hitVisual.Show(feedback, feedbackElapsed, IsBound ? BodyHeight : body.rectTransform.rect.height,
            scale * contactEffectScale, contactDirection, !IsBound);
        float visualDuration = Mathf.Max(feedback.flashDuration + feedback.afterglowDuration, feedback.sparkDuration);
        if (feedbackElapsed >= Mathf.Max(recoilDuration, visualDuration))
        {
            body.rectTransform.anchoredPosition = restPosition;
            if (hitVisual != null) hitVisual.ClearFlashAndRays();
            feedback = null;
        }
    }

    private Vector2 TailFootCorrection(float time)
    {
        if (sequence == null) return Vector2.zero;
        if (sequence.action == BattleAnimationAction.Move) return Vector2.right * (travelPoseOffsetX * UnitsToPixels);
        if (externalSampling) return Vector2.zero;
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
        meleeTarget = null;
        facingRight = friendlyActor;
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
            if (!IsBound && feedback != null) body.rectTransform.anchoredPosition = staticBodyRest;
            body.enabled = true;
            body.color = Color.white;
            body.sprite = null;
        }
        sequence = null;
        entry = null;
        friendlyHitIndex = 0;
        hasPreparedHit = false;
        hitPreparationSpeed = 1f;
        DisplayedPoseSequence = null;
        DisplayedPoseFrame = 0;
        travelPoseOffsetX = 0f;
        identity = null;
        meleeTarget = null;
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
