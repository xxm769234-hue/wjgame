using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleActionEffects : MonoBehaviour
{
    private sealed class Instance
    {
        public BattleEffectBinding binding;
        public Image image;
        public BattleActorAnimation recipient;
        public bool launched;
        public Vector2 launchPosition;
        public Vector2 flightDirection;
        public Vector2? formationPosition;
        public float formationScale;
        public float arrivalDelay;
        public float depthScale = 1f;
        public BattleFieldLayers.DepthItem depthItem;
    }

    private readonly List<Instance> instances = new List<Instance>();
    private BattleActorAnimation actor;
    private BattleActorAnimation target;
    private BattleActorAnimation[] allies;
    private BattleActorAnimation[] opponents;
    private RectTransform space;
    private RectTransform underlay;
    private RectTransform overlay;
    private BattleFieldLayers fieldLayers;

    public void Begin(BattleActorAnimation source, BattleAnimationSequence sequence, BattleActorAnimation destination, BattleActorAnimation[] teammates, BattleActorAnimation[] enemies, int variantSeed, BattleActorAnimation[] affectedTargets = null)
    {
        Clear();
        actor = source;
        target = destination;
        BattleActorAnimation[] recipients = affectedTargets ?? (destination != null ? new[] { destination } : new BattleActorAnimation[0]);
        allies = FilterRecipients(recipients, teammates, source);
        opponents = FilterRecipients(recipients, enemies, null);
        if (sequence.effects == null || sequence.effects.Length == 0) return;
        Transform ancestor = actor.transform;
        while (ancestor.parent != null && ancestor.name != "Battlefield" && ancestor.GetComponent<Canvas>() == null) ancestor = ancestor.parent;
        space = ancestor as RectTransform;
        if (space == null) return;
        fieldLayers = BattleFieldLayers.For(space);
        if (fieldLayers == null)
        {
            underlay = MakeLayer("Battle Ground Effects", space, true);
            overlay = MakeLayer("Battle Action Effects", space, false);
        }
        foreach (BattleEffectBinding binding in sequence.effects)
        {
            if (binding == null || binding.effect == null || !binding.effect.IsValid) continue;
            if (binding.placement == BattleEffectPlacement.Ally || binding.placement == BattleEffectPlacement.AllyAbove)
                foreach (BattleActorAnimation recipient in allies) AddInstance(binding, recipient);
            else if (binding.placement == BattleEffectPlacement.RowProjectile && opponents.Length > 0)
            {
                BattleActorAnimation farthest = opponents[0];
                foreach (var opponent in opponents)
                    if ((opponent.GroundShadowCenter().x - farthest.GroundShadowCenter().x) * actor.FacingSign > 0f)
                        farthest = opponent;
                AddInstance(binding, farthest);
            }
            else if (binding.placement == BattleEffectPlacement.RowProjectile) continue;
            else if (binding.placement == BattleEffectPlacement.Enemy || binding.placement == BattleEffectPlacement.EnemyGround || binding.placement == BattleEffectPlacement.Projectile)
                foreach (BattleActorAnimation recipient in opponents) AddInstance(binding, recipient);
            else if (binding.placement == BattleEffectPlacement.PartyGround)
            {
                if (allies.Length > 0) AddPartyGround(binding);
            }
            else if (binding.IsFullParty)
            {
                if (allies.Length == 0) continue;
                foreach (BattleActorAnimation member in allies)
                {
                    if (member == null || member.IsDead || member.IsPlayingDeath || !member.isActiveAndEnabled) continue;
                    if (binding.placement == BattleEffectPlacement.FullPartyTravel && member == actor) continue;
                    Instance bloom = AddInstance(binding, member);
                    Vector2 separation = Local(member.FootPosition) - Local(actor.FootPosition);
                    bloom.arrivalDelay = Mathf.Min(4f, separation.magnitude / Mathf.Max(1f, actor.BodyHeight) * .8f);
                    bloom.depthScale = Mathf.Clamp(1f - separation.y / Mathf.Max(1f, space.rect.height) * .33f, .87f, 1.13f);
                }
            }
            else if (binding.placement == BattleEffectPlacement.EnemyFormationGround)
            {
                if (opponents.Length > 0)
                {
                    var field = AddInstance(binding, opponents[0]);
                    Bounds occupied = new Bounds(Local(opponents[0].GroundShadowCenter()), Vector3.zero);
                    foreach (var member in opponents)
                    {
                        Vector2 feet = Local(member.GroundShadowCenter());
                        float height = member.BodyHeight;
                        occupied.Encapsulate(feet + new Vector2(-height * .30f, 0f));
                        occupied.Encapsulate(feet + new Vector2(height * .30f, height * .70f));
                    }
                    field.formationPosition = occupied.center;
                    float reach = Mathf.Max(occupied.size.x * 1.60f, occupied.size.y * 1.45f);
                    field.formationScale = Mathf.Clamp(reach, actor.BodyHeight * 4f, binding.width * actor.UnitsToPixels)
                        / Mathf.Max(.01f, binding.effect.referenceWidth);
                }
            }
            else AddInstance(binding, source);
        }
    }

    private static BattleActorAnimation[] FilterRecipients(BattleActorAnimation[] candidates, BattleActorAnimation[] party, BattleActorAnimation self)
    {
        List<BattleActorAnimation> matches = new List<BattleActorAnimation>();
        foreach (BattleActorAnimation candidate in candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDead || matches.Contains(candidate)) continue;
            bool matchesSide = candidate == self;
            if (party != null)
                foreach (BattleActorAnimation member in party) if (member == candidate) { matchesSide = true; break; }
            if (matchesSide) matches.Add(candidate);
        }
        return matches.ToArray();
    }

    private Instance AddInstance(BattleEffectBinding binding, BattleActorAnimation recipient)
    {
        bool ground = binding.placement == BattleEffectPlacement.PartyGround || binding.placement == BattleEffectPlacement.FullPartyGround
            || binding.placement == BattleEffectPlacement.CasterGround || binding.placement == BattleEffectPlacement.EnemyGround || binding.placement == BattleEffectPlacement.EnemyFormationGround;
        GameObject visual = new GameObject(binding.effect.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Image image = visual.GetComponent<Image>();
        BattleFieldLayers.DepthItem depth = null;
        if (fieldLayers != null && (!ground || binding.sortWithCombatants))
        {
            depth = fieldLayers.PlaceEffect(image.rectTransform, recipient, ground ? -1 : 2);
            if (binding.placement == BattleEffectPlacement.EnemyFormationGround)
            {
                float far = float.MinValue, near = float.MaxValue;
                foreach (var enemy in opponents)
                {
                    float groundY = Local(enemy.GroundShadowCenter()).y;
                    near = Mathf.Min(near, groundY);
                    far = Mathf.Max(far, groundY);
                }
                depth.formationGroundY = (near + far) * .5f;
            }
            if ((binding.placement == BattleEffectPlacement.Projectile && binding.projectileDistance <= 0f)
                || binding.placement == BattleEffectPlacement.FullPartyTravel || binding.placement == BattleEffectPlacement.RowProjectile)
            {
                depth.origin = actor;
                depth.travel = 0f;
            }
        }
        else visual.transform.SetParent(fieldLayers != null ? fieldLayers.Ground : ground ? underlay : overlay, false);
        image.raycastTarget = false;
        image.enabled = false;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(binding.opacity));
        ConfigureImageSpace(image);
        var instance = new Instance { binding = binding, image = image, recipient = recipient, depthItem = depth };
        instances.Add(instance);
        return instance;
    }

    private void AddPartyGround(BattleEffectBinding binding)
    {
        Instance field = AddInstance(binding, allies[0]);
        Bounds party = PartyBounds(allies, actor);
        FitPartyGround(field, party);
        if (allies.Length == 1) return;
        Vector2 center = field.formationPosition.Value;
        if (party.size.y > actor.BodyHeight) center.y += binding.effect.referenceHeight * field.formationScale * .025f;
        Vector2 radius = new Vector2(binding.effect.referenceWidth, binding.effect.referenceHeight)
            * (field.formationScale * .5f);
        foreach (BattleActorAnimation member in allies)
        {
            Vector2 delta = Local(member.FootPosition) - center;
            if (new Vector2(delta.x / Mathf.Max(.01f, radius.x), delta.y / Mathf.Max(.01f, radius.y)).sqrMagnitude <= 1f) continue;
            var ordered = (BattleActorAnimation[])allies.Clone();
            System.Array.Sort(ordered, (left, right) => Local(left.FootPosition).y.CompareTo(Local(right.FootPosition).y));
            for (int start = 0; start < ordered.Length;)
            {
                Bounds row = new Bounds(Local(ordered[start].FootPosition), Vector3.zero);
                int end = start + 1;
                while (end < ordered.Length && Local(ordered[end].FootPosition).y - row.min.y <= actor.BodyHeight * .55f)
                    row.Encapsulate(Local(ordered[end++].FootPosition));
                FitPartyGround(start == 0 ? field : AddInstance(binding, ordered[start]), row);
                start = end;
            }
            break;
        }
    }

    private void FitPartyGround(Instance instance, Bounds party)
    {
        BattleEffectBinding binding = instance.binding;
        BattleEffectDefinition effect = binding.effect;
        float padding = actor.BodyHeight * 1.6f;
        float scale = Mathf.Max((party.size.x + padding) / Mathf.Max(.01f, effect.referenceWidth),
            (party.size.y + padding) / Mathf.Max(.01f, effect.referenceHeight));
        float mirror = (binding.flipX ? -1f : 1f) * actor.SourceMirror;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        for (int frame = 0; frame < effect.sequence.FrameCount; frame++)
        {
            Sprite sprite = effect.sequence.frames[frame];
            if (sprite == null) continue;
            Vector2 first = -sprite.pivot / sprite.pixelsPerUnit - effect.frameAnchors[frame];
            Vector2 last = first + sprite.rect.size / sprite.pixelsPerUnit;
            first.x *= mirror;
            last.x *= mirror;
            min = Vector2.Min(min, Vector2.Min(first, last));
            max = Vector2.Max(max, Vector2.Max(first, last));
        }
        Rect visible = space.rect;
        Canvas canvas = instance.image.canvas;
        if (canvas != null)
        {
            var corners = new Vector3[4];
            ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(corners);
            Vector2 bottom = space.InverseTransformPoint(corners[0]);
            Vector2 top = space.InverseTransformPoint(corners[2]);
            visible = Rect.MinMaxRect(Mathf.Max(visible.xMin, bottom.x), Mathf.Max(visible.yMin, bottom.y),
                Mathf.Min(visible.xMax, top.x), Mathf.Min(visible.yMax, top.y));
        }
        Vector2 offset = binding.targetOffset * actor.UnitsToPixels;
        offset.x *= actor.SourceMirror;
        Vector2 center = (Vector2)party.center + offset;
        if (party.size.y > actor.BodyHeight)
        {
            scale *= 1.25f;
            center.y -= effect.referenceHeight * scale * .025f;
        }
        if (visible.width > 0f && visible.height > 0f)
        {
            scale = Mathf.Min(scale, visible.width / Mathf.Max(.01f, max.x - min.x),
                visible.height / Mathf.Max(.01f, max.y - min.y));
            center.x = Mathf.Clamp(center.x, visible.xMin - min.x * scale, visible.xMax - max.x * scale);
            center.y = Mathf.Clamp(center.y, visible.yMin - min.y * scale, visible.yMax - max.y * scale);
        }
        instance.formationPosition = center;
        instance.formationScale = scale;
    }

    public void Sample(float actionFrame)
    {
        if (actor == null || space == null) return;
        foreach (Instance instance in instances)
        {
            BattleEffectBinding binding = instance.binding;
            float sourceFrame = actionFrame - instance.arrivalDelay;
            bool hasTarget = binding.placement == BattleEffectPlacement.EnemyFormationGround || (instance.recipient != null && instance.recipient.isActiveAndEnabled);
            bool visible = sourceFrame >= binding.startFrame && sourceFrame < binding.endFrame && hasTarget;
            instance.image.enabled = visible;
            if (!visible) continue;
            BattleEffectDefinition effect = binding.effect;
            float effectFrame;
            if (binding.placement == BattleEffectPlacement.Projectile && sourceFrame < binding.launchFrame)
                effectFrame = Interpolate(sourceFrame, binding.startFrame, binding.launchFrame, binding.firstEffectFrame, binding.launchEffectFrame);
            else if (sourceFrame < binding.contactFrame)
                effectFrame = Interpolate(sourceFrame, binding.placement == BattleEffectPlacement.Projectile ? binding.launchFrame : binding.startFrame, binding.contactFrame, binding.placement == BattleEffectPlacement.Projectile ? binding.launchEffectFrame : binding.firstEffectFrame, binding.contactEffectFrame);
            else
                effectFrame = Interpolate(sourceFrame, binding.contactFrame, binding.endFrame, binding.contactEffectFrame, effect.sequence.FrameCount - 1);
            bool frameAligned = binding.preserveSourceScale && actor.CurrentSequence != null
                && effect.sequence.FrameCount == actor.CurrentSequence.FrameCount
                && Mathf.Approximately(binding.startFrame, 0f)
                && Mathf.Approximately(binding.contactFrame, 0f)
                && binding.firstEffectFrame == 0
                && binding.contactEffectFrame == 0
                && Mathf.Approximately(binding.endFrame, effect.sequence.FrameCount);
            int frame = frameAligned
                ? Mathf.Clamp(Mathf.FloorToInt(sourceFrame), 0, effect.sequence.FrameCount - 1)
                : Mathf.Clamp(Mathf.FloorToInt(effectFrame), 0, effect.sequence.FrameCount - 1);
            if (!frameAligned)
            {
                float tailFrames = Mathf.Min(3f, (binding.endFrame - binding.contactFrame) * .3f);
                float remaining = binding.endFrame - sourceFrame;
                float opacity = tailFrames > 0f ? Mathf.SmoothStep(0f, 1f, remaining / tailFrames) : 1f;
                instance.image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(binding.opacity) * opacity);
            }
            Vector2 destination = Destination(instance);
            Vector2 anchor = destination;
            float mirror = (binding.flipX ? -1f : 1f) * actor.SourceMirror;
            Quaternion rotation = Quaternion.identity;
            if (binding.placement == BattleEffectPlacement.RowProjectile)
            {
                if (!instance.launched)
                {
                    instance.launchPosition = ProjectileOrigin(binding, destination, binding.startFrame);
                    Vector2 end = Local(instance.recipient.BodyCenter);
                    end.x += actor.FacingSign * instance.recipient.BodyHeight * .30f;
                    float forward = (end.x - instance.launchPosition.x) * actor.FacingSign;
                    end.x = instance.launchPosition.x + actor.FacingSign * Mathf.Max(actor.BodyHeight, forward);
                    instance.formationPosition = end;
                    instance.launched = true;
                }
                float progress = Mathf.InverseLerp(binding.launchFrame, binding.endFrame - 1f, sourceFrame);
                anchor = Vector2.Lerp(instance.launchPosition, instance.formationPosition.Value, progress);
                if (instance.depthItem != null) instance.depthItem.travel = progress;
            }
            if (binding.placement == BattleEffectPlacement.Projectile)
            {
                if (!instance.launched && sourceFrame >= binding.launchFrame)
                {
                    instance.launchPosition = ProjectileOrigin(binding, destination, binding.launchFrame);
                    instance.launched = true;
                }
                Vector2 origin = instance.launched ? instance.launchPosition : ProjectileOrigin(binding, destination, sourceFrame);
                float progress = Mathf.InverseLerp(binding.launchFrame, binding.contactFrame, sourceFrame);
                progress *= .68f + .32f * progress;
                if (instance.depthItem != null) instance.depthItem.travel = progress;
                float arc = binding.arcHeight * actor.UnitsToPixels;
                anchor = Vector2.Lerp(origin, destination, progress) + Vector2.up * (Mathf.Sin(progress * Mathf.PI) * arc);
                Vector2 tangent = destination - origin + Vector2.up * (Mathf.PI * arc * Mathf.Cos(progress * Mathf.PI));
                instance.flightDirection = tangent;
                if (binding.contactFrame > binding.launchFrame && tangent.sqrMagnitude > .000001f)
                {
                    Vector2 forward = effect.ProjectileForwardAt(frame, binding.launchEffectFrame, binding.contactEffectFrame);
                    forward.x *= mirror;
                    float aim = sourceFrame < binding.launchFrame
                        ? Mathf.InverseLerp(binding.startFrame, binding.launchFrame, sourceFrame) : 1f;
                    rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(forward, tangent) * aim);
                }
            }
            float scale = binding.preserveSourceScale ? actor.UnitsToPixels : binding.width * actor.UnitsToPixels / Mathf.Max(0.01f, effect.referenceWidth);
            if (binding.placement == BattleEffectPlacement.PartyGround || binding.placement == BattleEffectPlacement.EnemyFormationGround)
                scale = instance.formationScale;
            BattleActorAnimation.SetSprite(instance.image, effect.sequence.frames[frame], scale, mirror);
            ConfigureImageSpace(instance.image);
            if (binding.IsFullParty)
            {
                RectTransform bloom = instance.image.rectTransform;
                bloom.sizeDelta *= instance.depthScale;
                Vector2 foot = Local(instance.recipient.FootPosition);
                if (binding.placement == BattleEffectPlacement.FullPartyTravel)
                {
                    Vector2 hand = Local(actor.BodyCenter) + new Vector2(actor.FacingSign * .14f, .18f) * actor.BodyHeight;
                    float travel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(binding.startFrame, binding.endFrame, sourceFrame));
                    if (instance.depthItem != null) instance.depthItem.travel = travel;
                    float height = actor.BodyHeight * .36f;
                    Vector2 tangent = foot - hand + Vector2.up * (height * Mathf.PI * Mathf.Cos(travel * Mathf.PI));
                    bloom.localScale = Vector3.one;
                    bloom.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.right, tangent));
                    bloom.anchoredPosition = Vector2.Lerp(hand, foot, travel) + Vector2.up * (height * Mathf.Sin(travel * Mathf.PI));
                }
                else bloom.anchoredPosition = foot;
                continue;
            }
            Vector2 offset = effect.frameAnchors[frame] * scale;
            offset.x *= mirror;
            instance.image.rectTransform.localRotation = rotation;
            instance.image.rectTransform.anchoredPosition = anchor - (Vector2)(rotation * offset);
        }
    }

    public bool TryGetProjectileContactDirection(BattleActorAnimation recipient, float sourceFrame, out Vector2 direction)
    {
        foreach (Instance instance in instances)
        {
            BattleEffectBinding binding = instance.binding;
            if (instance.recipient == recipient && instance.launched && binding.placement == BattleEffectPlacement.Projectile
                && Mathf.Abs(sourceFrame - binding.contactFrame) <= 1f && instance.flightDirection.sqrMagnitude > .000001f)
            {
                direction = ((Vector2)space.TransformVector(instance.flightDirection)).normalized;
                return true;
            }
        }
        direction = Vector2.zero;
        return false;
    }

    private Vector2 Destination(Instance instance)
    {
        BattleEffectBinding binding = instance.binding;
        Vector2 position;
        switch (binding.placement)
        {
            case BattleEffectPlacement.CasterGround:
                position = Local(actor.GroundShadowCenter());
                break;
            case BattleEffectPlacement.CasterAbove:
                position = Local(actor.FootPosition) + Vector2.up * actor.BodyHeight * 1.08f;
                break;
            case BattleEffectPlacement.AllyAbove:
                position = Local(instance.recipient.FootPosition) + Vector2.up * instance.recipient.BodyHeight * 1.08f;
                break;
            case BattleEffectPlacement.Ally:
                position = Local(instance.recipient.GroundShadowCenter());
                break;
            case BattleEffectPlacement.PartyGround:
                return instance.formationPosition.Value;
            case BattleEffectPlacement.EnemyGround:
                position = Local(instance.recipient.GroundShadowCenter());
                break;
            case BattleEffectPlacement.EnemyFormationGround:
                if (!instance.formationPosition.HasValue)
                {
                    Bounds bounds = PartyBounds(opponents, instance.recipient);
                    instance.formationPosition = (Vector2)bounds.center + Vector2.Scale(binding.formationOffset, bounds.extents);
                }
                position = instance.formationPosition.Value;
                break;
            default:
                position = Local(instance.recipient.BodyCenter);
                break;
        }
        Vector2 offset = binding.targetOffset * actor.UnitsToPixels;
        if (binding.placement == BattleEffectPlacement.Enemy || binding.placement == BattleEffectPlacement.Projectile) offset += binding.targetBodyOffset * instance.recipient.BodyHeight;
        offset.x *= actor.SourceMirror;
        return position + offset;
    }

    private Vector2 ProjectileOrigin(BattleEffectBinding binding, Vector2 destination, float sourceFrame)
    {
        if (binding.projectileDirection.sqrMagnitude > 0.0001f && binding.projectileDistance > 0f)
        {
            Vector2 direction = binding.projectileDirection.normalized;
            direction.x *= (binding.flipX ? -1f : 1f) * actor.SourceMirror;
            return destination - direction * binding.projectileDistance * actor.UnitsToPixels;
        }
        Vector2 offset = binding.sourceOffset * actor.BodyHeight;
        if (binding.sourceKeys != null && binding.sourceKeys.Length > 0)
        {
            offset = binding.sourceKeys[0].localPosition;
            for (int index = 1; index < binding.sourceKeys.Length; index++)
            {
                BattleEffectSourceKey previous = binding.sourceKeys[index - 1];
                BattleEffectSourceKey current = binding.sourceKeys[index];
                offset = Vector2.Lerp(previous.localPosition, current.localPosition, Mathf.InverseLerp(previous.frame, current.frame, sourceFrame));
                if (sourceFrame <= current.frame) break;
            }
            offset *= actor.UnitsToPixels;
        }
        offset.x *= actor.SourceMirror;
        return Local(actor.FootPosition) + offset;
    }

    private Bounds PartyBounds(BattleActorAnimation[] party, BattleActorAnimation fallback)
    {
        Bounds bounds = new Bounds(fallback != null ? (Vector3)Local(fallback.FootPosition) : Vector3.zero, Vector3.zero);
        bool found = false;
        if (party != null)
            foreach (BattleActorAnimation member in party)
            {
                if (member == null || !member.isActiveAndEnabled || member.IsDead) continue;
                Vector2 position = Local(member.FootPosition);
                if (!found) bounds = new Bounds(position, Vector3.zero);
                else bounds.Encapsulate(position);
                found = true;
            }
        return bounds;
    }

    private Vector2 Local(Vector3 worldPosition) { return space.InverseTransformPoint(worldPosition); }

    private static RectTransform MakeLayer(string layerName, RectTransform parent, bool behind)
    {
        RectTransform layer = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.pivot = parent.pivot;
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.anchoredPosition = Vector2.zero;
        layer.localRotation = Quaternion.identity;
        layer.localScale = Vector3.one;
        if (behind) layer.SetAsFirstSibling();
        else layer.SetAsLastSibling();
        return layer;
    }

    private void ConfigureImageSpace(Image image)
    {
        if (image == null || space == null) return;
        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = space.pivot;
        imageRect.anchorMax = space.pivot;
        imageRect.localRotation = Quaternion.identity;
    }

    private static float Interpolate(float value, float start, float end, float first, float last)
    {
        return Mathf.Lerp(first, last, end > start ? Mathf.InverseLerp(start, end, value) : 1f);
    }

    public void Clear()
    {
        foreach (Instance instance in instances)
        {
            if (instance.image == null) continue;
            instance.image.enabled = false;
            instance.image.sprite = null;
            if (fieldLayers != null) Destroy(instance.image.gameObject);
        }
        if (underlay != null) { underlay.gameObject.SetActive(false); Destroy(underlay.gameObject); }
        if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
        instances.Clear();
        underlay = null;
        overlay = null;
        fieldLayers = null;
        space = null;
        actor = null;
        target = null;
    }

    private void OnDisable() { Clear(); }
}
