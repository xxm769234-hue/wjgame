using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Small contact flash and rays on the existing actor image, with no duplicate body.</summary>
[DisallowMultipleComponent]
public sealed class BattleActorHitVisual : MonoBehaviour
{
    private static readonly int HitColor = Shader.PropertyToID("_HitColor");
    private static readonly int HitAmount = Shader.PropertyToID("_HitAmount");
    private static readonly int HitCenter = Shader.PropertyToID("_HitCenter");
    private static readonly int HitRadius = Shader.PropertyToID("_HitRadius");
    private static readonly int HitUVRect = Shader.PropertyToID("_HitUVRect");
    private static readonly int HitLocalRect = Shader.PropertyToID("_HitLocalRect");
    private Image body;
    private RectTransform actorRoot;
    private Material originalMaterial;
    private Material flashMaterial;
    private Texture2D rayTexture;
    private Sprite raySprite;
    private RectTransform sparkRoot;
    private Image[] rays;

    public void Bind(Image image, RectTransform root)
    {
        if (body != image)
        {
            Clear();
            body = image;
            originalMaterial = body != null ? body.material : null;
        }
        actorRoot = root;
        if (sparkRoot != null && sparkRoot.parent != actorRoot) sparkRoot.SetParent(actorRoot, false);
    }

    public void Show(BattleHitFeedbackProfile profile, float elapsed, float bodyHeight, float scale, float mirror)
    {
        if (body == null || profile == null)
        {
            Clear();
            return;
        }
        float amount = elapsed < profile.flashDuration ? profile.flashAmount
            : profile.afterglowAmount * (1f - Mathf.Clamp01((elapsed - profile.flashDuration) / Mathf.Max(.001f, profile.afterglowDuration)));
        Color tint = elapsed < profile.flashDuration ? profile.flashColor : profile.afterglowColor;
        if (amount > 0f && EnsureMaterial())
        {
            if (body.material != flashMaterial) body.material = flashMaterial;
            Vector2 center = Vector2.up * (bodyHeight * .52f);
            float radius = bodyHeight * Mathf.Clamp(profile.flashBodyFraction, .04f, 1f);
            ConfigureMaterial(flashMaterial, tint, amount, center, radius);
            // MaskableGraphic may supply a stencil material copy beneath a Mask.
            Material rendered = body.materialForRendering;
            if (rendered != null && rendered != flashMaterial) ConfigureMaterial(rendered, tint, amount, center, radius);
        }
        else RestoreMaterial();

        if (profile.sparkDuration <= 0f || profile.sparkPixels <= 0f || elapsed >= profile.sparkDuration)
        {
            if (sparkRoot != null) sparkRoot.gameObject.SetActive(false);
            return;
        }
        EnsureSpark();
        sparkRoot.gameObject.SetActive(true);
        sparkRoot.anchorMin = body.rectTransform.anchorMin;
        sparkRoot.anchorMax = body.rectTransform.anchorMax;
        sparkRoot.anchoredPosition = body.rectTransform.anchoredPosition + Vector2.up * (bodyHeight * .52f);
        float progress = Mathf.Clamp01(elapsed / profile.sparkDuration);
        float opacity = (1f - progress) * (1f - progress);
        Color contact = profile.contactColor;
        contact.a *= opacity;
        for (int i = 0; i < rays.Length; i++)
        {
            Image ray = rays[i];
            RectTransform rect = ray.rectTransform;
            ray.color = contact;
            if (i == 0)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.one * (Mathf.Max(1f, profile.sparkWidthPixels) * (2.5f - progress) * scale);
                continue;
            }
            float angle = (i - 1) * 60f + 12f;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians) * mirror, Mathf.Sin(radians));
            float reach = profile.sparkPixels * scale * (i % 2 == 0 ? .8f : 1f);
            rect.anchoredPosition = direction * reach * (.22f + progress * .58f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            rect.sizeDelta = new Vector2(Mathf.Max(.5f, profile.sparkRayWidthPixels) * scale,
                reach * (.65f - progress * .4f));
        }
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
        sparkRoot.SetParent(actorRoot, false);
        sparkRoot.SetSiblingIndex(body.rectTransform.GetSiblingIndex() + 1);
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

    private void RestoreMaterial()
    {
        if (body != null && flashMaterial != null && body.material == flashMaterial) body.material = originalMaterial;
    }

    public void Clear()
    {
        RestoreMaterial();
        if (sparkRoot != null) sparkRoot.gameObject.SetActive(false);
    }

    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        Clear();
        if (sparkRoot != null) Destroy(sparkRoot.gameObject);
        if (flashMaterial != null) Destroy(flashMaterial);
        if (raySprite != null) Destroy(raySprite);
        if (rayTexture != null) Destroy(rayTexture);
    }
}
