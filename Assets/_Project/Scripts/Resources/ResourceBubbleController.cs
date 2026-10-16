using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ResourceBubbleController : MonoBehaviour
{
    private const int TextureSize = 96;

    private LandSlotController owner;
    private SpriteRenderer bubbleRenderer;
    private SpriteRenderer iconRenderer;
    private TextMeshPro amountText;
    private CircleCollider2D clickCollider;
    private Vector3 baseLocalScale = Vector3.one;
    private static Sprite fallbackBubbleSprite;
    private static Sprite fallbackIconSprite;

    public void Initialize(LandSlotController slot)
    {
        owner = slot;
        EnsureVisuals();
        HideBubble();
    }

    public void Show(string resourceName, int amount, Sprite icon, Color color, string sortingLayerName, int sortingOrder)
    {
        gameObject.SetActive(true);
        EnsureVisuals();

        bubbleRenderer.sprite = GetFallbackBubbleSprite();
        bubbleRenderer.color = Color.white;
        bubbleRenderer.sortingLayerName = sortingLayerName;
        bubbleRenderer.sortingOrder = sortingOrder;
        bubbleRenderer.enabled = true;

        iconRenderer.sprite = icon != null ? icon : GetFallbackIconSprite(color);
        iconRenderer.color = Color.white;
        iconRenderer.sortingLayerName = sortingLayerName;
        iconRenderer.sortingOrder = sortingOrder + 1;
        iconRenderer.enabled = true;

        amountText.text = $"+{amount}";
        MeshRenderer textRenderer = amountText.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingLayerName = sortingLayerName;
            textRenderer.sortingOrder = sortingOrder + 2;
        }
        amountText.gameObject.SetActive(true);

        clickCollider.enabled = true;
        name = string.IsNullOrEmpty(resourceName) ? "Resource Bubble" : $"Resource Bubble ({resourceName})";
    }

    public void HideBubble()
    {
        if (bubbleRenderer != null) bubbleRenderer.enabled = false;
        if (iconRenderer != null) iconRenderer.enabled = false;
        if (amountText != null) amountText.gameObject.SetActive(false);
        if (clickCollider != null) clickCollider.enabled = false;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.045f;
        transform.localScale = baseLocalScale * pulse;
    }

    private void OnMouseDown()
    {
        if (PlayerOnboardingInputLock.IsGameplayInputBlocked) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (owner != null) owner.CollectReadyResource();
    }

    private void EnsureVisuals()
    {
        if (bubbleRenderer == null)
        {
            GameObject bubbleObject = new GameObject("Bubble");
            bubbleObject.transform.SetParent(transform, false);
            bubbleObject.transform.localPosition = Vector3.zero;
            bubbleObject.transform.localScale = new Vector3(1.45f, 0.9f, 1f);
            bubbleRenderer = bubbleObject.AddComponent<SpriteRenderer>();
        }

        if (iconRenderer == null)
        {
            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new Vector3(-0.34f, 0f, -0.02f);
            iconObject.transform.localScale = Vector3.one * 0.38f;
            iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        }

        if (amountText == null)
        {
            Transform existing = transform.Find("Amount");
            if (existing != null) amountText = existing.GetComponent<TextMeshPro>();
        }
        if (amountText == null)
        {
            GameObject textObject = new GameObject("Amount", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0.32f, -0.03f, -0.03f);
            textObject.transform.localScale = Vector3.one * 0.08f;
            amountText = textObject.AddComponent<TextMeshPro>();
            amountText.font = RuntimeFontAssetProvider.Resolve();
            amountText.color = Color.black;
            amountText.fontSize = 3.2f;
            amountText.alignment = TextAlignmentOptions.Center;
            amountText.enableWordWrapping = false;
            amountText.rectTransform.sizeDelta = new Vector2(18f, 8f);
        }

        if (clickCollider == null)
        {
            clickCollider = gameObject.AddComponent<CircleCollider2D>();
            clickCollider.isTrigger = true;
            clickCollider.radius = 0.75f;
        }

        baseLocalScale = Vector3.one;
    }

    private static Sprite GetFallbackBubbleSprite()
    {
        if (fallbackBubbleSprite != null) return fallbackBubbleSprite;

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (TextureSize * 0.5f);
                Color pixel = Color.clear;
                if (distance <= 1f)
                {
                    float edge = Mathf.InverseLerp(1f, 0.82f, distance);
                    pixel = Color.Lerp(new Color(1f, 0.88f, 0.28f, 1f), Color.white, edge);
                }
                texture.SetPixel(x, y, pixel);
            }
        }
        texture.Apply();
        fallbackBubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        fallbackBubbleSprite.name = "RuntimeResourceBubble";
        return fallbackBubbleSprite;
    }

    private static Sprite GetFallbackIconSprite(Color color)
    {
        if (fallbackIconSprite != null) return fallbackIconSprite;

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (TextureSize * 0.5f);
                bool coin = distance <= 0.78f;
                bool shine = x > 26 && x < 42 && y > 52 && y < 74;
                Color pixel = Color.clear;
                if (coin) pixel = color;
                if (coin && distance > 0.63f) pixel = new Color(0.82f, 0.48f, 0.08f, 1f);
                if (coin && shine) pixel = Color.Lerp(pixel, Color.white, 0.45f);
                texture.SetPixel(x, y, pixel);
            }
        }
        texture.Apply();
        fallbackIconSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        fallbackIconSprite.name = "RuntimeResourceIcon";
        return fallbackIconSprite;
    }
}
