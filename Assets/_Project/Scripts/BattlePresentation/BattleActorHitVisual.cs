using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleActorHitVisual : MonoBehaviour
{
    private static readonly int HitColor = Shader.PropertyToID("_HitColor");
    private static readonly int HitAmount = Shader.PropertyToID("_HitAmount");
    private static readonly int HitCenter = Shader.PropertyToID("_HitCenter");
    private static readonly int HitRadius = Shader.PropertyToID("_HitRadius");
    private static readonly int HitUVRect = Shader.PropertyToID("_HitUVRect");
    private static readonly int HitLocalRect = Shader.PropertyToID("_HitLocalRect");
    private static readonly float[] RayAngles = { -46f, -13f, 19f, 57f, 151f, 213f };
    private static readonly float[] RayLengths = { .62f, 1f, .84f, .54f, .49f, .36f };
    private Image body;
    private RectTransform actorRoot;
    private Material originalMaterial;
    private Material flashMaterial;
    private Texture2D rayTexture;
    private Sprite raySprite;
    private RectTransform sparkRoot;
    private Image[] rays;

    private static Texture2D contactSheet;
    private const float PaintedSizeMultiplier = 1.30f;
    private const float PaintedPeakHoldSeconds = .033f;
    private BattleActorAnimation paintedTarget;
    private RawImage paintedContact;
    private BattleHitFeedbackProfile paintedProfile;
    private Vector2 paintedDirection;
    private float paintedElapsed;
    private int incomingAttacks;
    private Image reactionPose;
    private BattleActorAnimation recipient;
    private int paintedStartFrame;

    public void Bind(Image image, RectTransform root, BattleActorAnimation enemy = null)
    {
        if (body != image)
        {
            Clear();
            body = image;
            originalMaterial = body != null ? body.material : null;
        }
        paintedTarget = enemy;
        actorRoot = root;
        recipient = GetComponent<BattleActorAnimation>();
    }

    public void Show(BattleHitFeedbackProfile profile, float elapsed, float bodyHeight, float scale, float hitDirection, bool staticSprite = false)
    {
        Show(profile, elapsed, bodyHeight, scale, Vector2.right * Mathf.Sign(hitDirection), staticSprite);
    }

    public void Show(BattleHitFeedbackProfile profile, float elapsed, float bodyHeight, float scale, Vector2 direction, bool staticSprite = false)
    {
        if (body == null || profile == null)
        {
            Clear();
            return;
        }
        bool primary = elapsed < profile.flashDuration;
        float glow = Mathf.Clamp01((elapsed - profile.flashDuration) / Mathf.Max(.001f, profile.afterglowDuration));
        float amount = primary ? profile.flashAmount : profile.afterglowAmount * (1f - glow);
        Color tint = primary ? profile.flashColor : profile.afterglowColor;
        RectTransform spriteRect = body.rectTransform;
        Vector3 contactPoint = ContactPoint(bodyHeight, direction, staticSprite);
        Vector2 center = spriteRect.InverseTransformPoint(contactPoint);
        if (amount > 0f && EnsureMaterial())
        {
            if (body.material != flashMaterial) body.material = flashMaterial;
            float radius = bodyHeight * Mathf.Clamp(profile.flashBodyFraction, .04f, 1f);
            ConfigureMaterial(flashMaterial, tint, amount, center, radius);
            // MaskableGraphic may supply a stencil material copy beneath a Mask.
            Material rendered = body.materialForRendering;
            if (rendered != null && rendered != flashMaterial) ConfigureMaterial(rendered, tint, amount, center, radius);
        }
        else RestoreMaterial();

        if (paintedTarget != null || profile.sparkDuration <= 0f || profile.sparkPixels <= 0f || elapsed >= profile.sparkDuration)
        {
            if (sparkRoot != null) sparkRoot.gameObject.SetActive(false);
            return;
        }
        EnsureSpark();
        sparkRoot.gameObject.SetActive(true);
        sparkRoot.position = contactPoint;
        sparkRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        float progress = Mathf.Clamp01(elapsed / profile.sparkDuration);
        float travel = 1f - (1f - progress) * (1f - progress);
        float opacity = 1f - Mathf.SmoothStep(0f, 1f, progress);
        Color contact = profile.contactColor;
        contact.a *= opacity;
        for (int i = 0; i < rays.Length; i++)
        {
            Image ray = rays[i];
            RectTransform rect = ray.rectTransform;
            ray.color = contact;
            if (i == 0)
            {
                ray.color = Color.Lerp(new Color(1f, 1f, 1f, contact.a), contact, travel);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.one * (Mathf.Max(1f, profile.sparkWidthPixels) * (2.5f - progress) * scale);
                continue;
            }
            float angle = RayAngles[i - 1];
            float radians = angle * Mathf.Deg2Rad;
            Vector2 rayDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float reach = Mathf.Min(profile.sparkPixels * scale, bodyHeight * .24f) * RayLengths[i - 1];
            rect.anchoredPosition = rayDirection * reach * (.22f + travel * .75f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            rect.sizeDelta = new Vector2(Mathf.Max(.5f, profile.sparkRayWidthPixels * (1f - .65f * travel)) * scale,
                reach * Mathf.Lerp(.74f, .15f, progress * progress));
        }
    }

    private Vector3 ContactPoint(float height, Vector2 direction, bool staticSprite)
    {
        RectTransform sprite = body.rectTransform;
        Vector3 torso = staticSprite ? sprite.TransformPoint(sprite.rect.center)
            : sprite.position + actorRoot.TransformVector(Vector2.up * (height * .52f));
        return torso - actorRoot.TransformVector(direction * (height * .12f));
    }

    public void PlayPaintedContact(BattleHitFeedbackProfile profile, Vector2 direction)
    {
        if (paintedTarget == null || profile.sparkDuration <= 0f || profile.sparkPixels <= 0f) return;
        if (paintedContact == null)
        {
            if (contactSheet == null) contactSheet = Resources.Load<Texture2D>("BattleAnimations/InkContact");
            if (contactSheet == null) return;
            var visual = new GameObject("Battle Painted Contact", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            paintedContact = visual.GetComponent<RawImage>();
            PlaceContact(paintedContact.rectTransform);
            paintedContact.raycastTarget = false;
            paintedContact.texture = contactSheet;
        }
        paintedProfile = profile;
        paintedElapsed = 0f;
        paintedStartFrame = Time.frameCount;
        paintedDirection = direction;
        paintedContact.color = Color.Lerp(Color.white, profile.contactColor, .18f);
        paintedContact.enabled = true;
        SamplePaintedContact();
    }

    private void LateUpdate()
    {
        UpdateReactionPose();
        if (paintedProfile == null || paintedTarget == null) return;
        if (!paintedTarget.Paused && Time.frameCount != paintedStartFrame) paintedElapsed += Time.deltaTime;
        if (paintedElapsed >= paintedProfile.sparkDuration + PaintedPeakHoldSeconds || !body.isActiveAndEnabled)
        {
            paintedProfile = null;
            paintedContact.enabled = false;
            return;
        }
        SamplePaintedContact();
    }

    private void SamplePaintedContact()
    {
        const int columns = 4;
        float peakTime = paintedProfile.sparkDuration * .22f;
        float sheetTime = Mathf.Min(paintedElapsed, peakTime)
            + Mathf.Max(0f, paintedElapsed - (peakTime + PaintedPeakHoldSeconds));
        int frame = Mathf.Min(columns * columns - 1, (int)(sheetTime / paintedProfile.sparkDuration * columns * columns));
        Rect cell = new Rect((frame % columns) * .25f, (columns - 1 - frame / columns) * .25f, .25f, .25f);
        if (paintedContact.uvRect != cell) paintedContact.uvRect = cell;
        float height = paintedTarget.IsBound ? paintedTarget.BodyHeight : body.rectTransform.rect.height;
        RectTransform rect = paintedContact.rectTransform;
        rect.position = ContactPoint(height, paintedDirection, !paintedTarget.IsBound);
        rect.rotation = actorRoot.rotation * Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.right, paintedDirection));
        float size = (40f + 2.7f * paintedProfile.sparkPixels) * PaintedSizeMultiplier;
        rect.sizeDelta = Vector2.one * Mathf.Min(size * paintedTarget.PresentationScale, height * .66f);
    }

    internal void BeginIncomingAttack()
    {
        incomingAttacks++;
        if (reactionPose != null || body == null) return;
        var layers = BattleFieldLayers.For(actorRoot);
        if (layers == null || recipient == null) return;
        reactionPose = new GameObject("Battle Reaction Pose", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        layers.PlaceEffect(reactionPose.rectTransform, recipient, 3);
        reactionPose.raycastTarget = false;
        reactionPose.enabled = false;
    }

    internal void EndIncomingAttack()
    {
        incomingAttacks = Mathf.Max(0, incomingAttacks - 1);
        if (incomingAttacks == 0 && reactionPose != null) reactionPose.enabled = false;
    }

    private void UpdateReactionPose()
    {
        if (reactionPose == null) return;
        reactionPose.enabled = incomingAttacks > 0 && body != null && body.isActiveAndEnabled && body.sprite != null;
        if (!reactionPose.enabled) return;
        RectTransform source = body.rectTransform;
        RectTransform pose = reactionPose.rectTransform;
        pose.pivot = source.pivot;
        pose.sizeDelta = source.rect.size;
        pose.SetPositionAndRotation(source.position, source.rotation);
        Vector3 parentScale = pose.parent.lossyScale;
        Vector3 scale = source.lossyScale;
        pose.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, 1f);
        reactionPose.sprite = body.overrideSprite;
        reactionPose.preserveAspect = body.preserveAspect;
        reactionPose.material = body.material;
        Color color = body.color;
        color.a *= .67f;
        reactionPose.color = color;
    }

    private bool EnsureMaterial()
    {
        if (flashMaterial != null) return true;
        Shader shader = Resources.Load<Shader>("BattleAnimations/BattleHitFlash");
        if (shader == null || !shader.isSupported) return false;
        flashMaterial = new Material(shader) { name = "Battle local hit flash", hideFlags = HideFlags.DontSave };
        return true;
    }

    private void ConfigureMaterial(Material material, Color tint, float amount, Vector2 center, float radius)
    {
        material.SetColor(HitColor, tint);
        material.SetFloat(HitAmount, Mathf.Clamp01(amount));
        material.SetVector(HitCenter, new Vector4(center.x, center.y, 0f, 0f));
        material.SetFloat(HitRadius, Mathf.Max(1f, radius));
        Sprite sprite = body.sprite;
        Vector4 uv = sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
        Vector4 padding = sprite != null ? DataUtility.GetPadding(sprite) : Vector4.zero;
        Vector2 sourceSize = sprite != null ? sprite.rect.size : Vector2.one;
        Rect rect = body.rectTransform.rect;
        float left = padding.x / Mathf.Max(1f, sourceSize.x);
        float bottom = padding.y / Mathf.Max(1f, sourceSize.y);
        float width = 1f - (padding.x + padding.z) / Mathf.Max(1f, sourceSize.x);
        float height = 1f - (padding.y + padding.w) / Mathf.Max(1f, sourceSize.y);
        // Reconstruct position from sprite UVs: UGUI can bake the vertex position
        // into canvas space when it batches, but these UVs retain their meaning.
        material.SetVector(HitUVRect, uv);
        material.SetVector(HitLocalRect, new Vector4(rect.x + rect.width * left, rect.y + rect.height * bottom,
            rect.width * width, rect.height * height));
    }

    private void EnsureSpark()
    {
        if (sparkRoot != null) return;
        rayTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Battle contact ray", hideFlags = HideFlags.DontSave };
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float diamond = Mathf.Abs((x + .5f - 16f) / 16f) + Mathf.Abs((y + .5f - 16f) / 16f);
            pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1f - diamond) * 8f) * 255f));
        }
        rayTexture.SetPixels32(pixels);
        rayTexture.Apply(false, true);
        raySprite = Sprite.Create(rayTexture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32f);
        raySprite.hideFlags = HideFlags.DontSave;
        sparkRoot = new GameObject("Battle Contact Spark", typeof(RectTransform)).GetComponent<RectTransform>();
        PlaceContact(sparkRoot);
        rays = new Image[7];
        for (int i = 0; i < rays.Length; i++)
        {
            Image ray = new GameObject("Contact Ray " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            ray.transform.SetParent(sparkRoot, false);
            ray.sprite = raySprite;
            ray.raycastTarget = false;
            rays[i] = ray;
        }
    }

    private void PlaceContact(RectTransform graphic)
    {
        var layers = BattleFieldLayers.For(actorRoot);
        if (layers != null && recipient != null) layers.PlaceEffect(graphic, recipient, 4);
        else
        {
            graphic.SetParent(actorRoot, false);
            graphic.SetSiblingIndex(body.rectTransform.GetSiblingIndex() + 1);
        }
    }

    private void RestoreMaterial()
    {
        if (body != null && flashMaterial != null && body.material == flashMaterial) body.material = originalMaterial;
    }

    public void ClearFlashAndRays()
    {
        RestoreMaterial();
        if (sparkRoot != null) sparkRoot.gameObject.SetActive(false);
    }

    public void Clear()
    {
        ClearFlashAndRays();
        paintedProfile = null;
        if (paintedContact != null) paintedContact.enabled = false;
    }

    private void OnDisable()
    {
        incomingAttacks = 0;
        if (reactionPose != null) reactionPose.enabled = false;
        Clear();
    }
    private void OnDestroy()
    {
        Clear();
        if (reactionPose != null) Destroy(reactionPose.gameObject);
        if (sparkRoot != null) Destroy(sparkRoot.gameObject);
        if (paintedContact != null) Destroy(paintedContact.gameObject);
        if (flashMaterial != null) Destroy(flashMaterial);
        if (raySprite != null) Destroy(raySprite);
        if (rayTexture != null) Destroy(rayTexture);
    }
}
