using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using LegacyText = UnityEngine.UI.Text;

[DisallowMultipleComponent]
public sealed class InventorySlotView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button slotButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private Image fallbackBackgroundImage;
    [SerializeField] private TMP_Text fallbackText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Image selectedImage;
    [SerializeField] private GameObject lockRoot;

    [Header("Slot Style")]
    [SerializeField] private Color emptyColor = new Color(0.92f, 0.68f, 0.34f, 1f);
    [SerializeField] private Color occupiedColor = new Color(0.98f, 0.78f, 0.40f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.62f, 0.62f, 0.58f, 1f);
    [SerializeField] private Color innerColor = new Color(1f, 0.86f, 0.54f, 0.68f);
    [SerializeField] private Color lockedInnerColor = new Color(0.70f, 0.70f, 0.67f, 1f);

    private Sprite _emptyBackgroundSprite;
    private Color _emptyBackgroundColor;
    private bool _backgroundInitialized;

    private void Awake()
    {
        UpgradeFontAsset();
        UpgradeLegacyTextComponents();
        ResolveTextReferences();
    }

    public void ShowEmpty()
    {
        ResetState();
    }

    public void SetSlotImages(Sprite normalSprite, Sprite selectedSprite)
    {
    }

    public void ShowLocked(UnityAction onClick)
    {
        ResetState();
        SetActive(lockRoot, true);
        BindClick(onClick);
    }

    public void ShowItem(
        Sprite itemSprite,
        string fallbackLabel,
        int quantity,
        bool selected,
        Color fallbackColor,
        Sprite qualityBackground,
        UnityAction onClick)
    {
        ResetState();

        if (backgroundImage != null && qualityBackground != null)
        {
            backgroundImage.sprite = qualityBackground;
            backgroundImage.overrideSprite = null;
            backgroundImage.color = Color.white;
        }

        if (itemIconImage != null)
        {
            itemIconImage.overrideSprite = null;
            itemIconImage.sprite = itemSprite;
            itemIconImage.enabled = itemSprite != null;
        }

        bool useFallback = itemSprite == null;
        SetActive(fallbackBackgroundImage, useFallback);
        if (fallbackBackgroundImage != null)
        {
            fallbackBackgroundImage.color = fallbackColor;
        }

        SetActive(fallbackText, useFallback);
        if (fallbackText != null)
        {
            fallbackText.text = string.IsNullOrEmpty(fallbackLabel) ? "物" : fallbackLabel;
        }

        SetActive(quantityText, true);
        if (quantityText != null)
        {
            quantityText.text = Mathf.Max(0, quantity).ToString();
        }

        SetActive(selectedImage, selected);
        BindClick(onClick);
    }

    private void ResetState()
    {
        RestoreEmptyBackground();

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.interactable = true;
        }

        if (itemIconImage != null)
        {
            itemIconImage.enabled = false;
        }

        SetActive(fallbackBackgroundImage, false);
        SetActive(fallbackText, false);
        SetActive(quantityText, false);
        SetActive(selectedImage, false);
        SetActive(lockRoot, false);
    }

    /// <summary>Restores the authored empty slot style before binding another item.</summary>
    private void RestoreEmptyBackground()
    {
        if (!_backgroundInitialized)
        {
            _backgroundInitialized = true;
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }
            if (backgroundImage == null)
            {
                Debug.LogWarning("Inventory slot has no background Image: " + name, this);
                return;
            }
            _emptyBackgroundSprite = backgroundImage.sprite;
            _emptyBackgroundColor = backgroundImage.color;
        }

        if (backgroundImage != null)
        {
            backgroundImage.sprite = _emptyBackgroundSprite;
            backgroundImage.overrideSprite = null;
            backgroundImage.color = _emptyBackgroundColor;
        }
    }

    private void BindClick(UnityAction onClick)
    {
        if (slotButton != null && onClick != null)
        {
            slotButton.onClick.AddListener(onClick);
        }
    }

    private void UpgradeFontAsset()
    {
        if (fallbackText != null && fallbackText.font == null)
        {
            fallbackText.font = RuntimeFontAssetProvider.Resolve();
        }

        if (quantityText != null && quantityText.font == null)
        {
            quantityText.font = RuntimeFontAssetProvider.Resolve();
        }
    }

    private void UpgradeLegacyTextComponents()
    {
        LegacyText[] legacyTexts = GetComponentsInChildren<LegacyText>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            LegacyText legacy = legacyTexts[i];
            if (legacy == null || legacy.GetComponent<TMP_Text>() != null)
            {
                continue;
            }

            TextMeshProUGUI replacement = legacy.gameObject.AddComponent<TextMeshProUGUI>();
            replacement.text = legacy.text;
            replacement.fontSize = legacy.fontSize;
            replacement.color = legacy.color;
            replacement.raycastTarget = legacy.raycastTarget;
            replacement.alignment = ConvertAlignment(legacy.alignment);
            legacy.enabled = false;
        }
    }

    private void ResolveTextReferences()
    {
        if (fallbackText == null)
        {
            fallbackText = FindText("FallbackText");
        }

        if (quantityText == null)
        {
            quantityText = FindText("QuantityText");
            if (quantityText == null)
            {
                quantityText = FindText("Quantity");
            }
        }
    }

    private TMP_Text FindText(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != objectName)
            {
                continue;
            }

            TMP_Text text = children[i].GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void SetActive(Component component, bool active)
    {
        if (component != null)
        {
            component.gameObject.SetActive(active);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        Button button,
        Image background,
        Image inner,
        Image itemIcon,
        Image fallbackBackground,
        TMP_Text itemFallbackText,
        TMP_Text itemQuantityText,
        Image selectedStateImage,
        GameObject lockedRoot)
    {
        slotButton = button;
        backgroundImage = background;
        innerImage = inner;
        itemIconImage = itemIcon;
        fallbackBackgroundImage = fallbackBackground;
        fallbackText = itemFallbackText;
        quantityText = itemQuantityText;
        selectedImage = selectedStateImage;
        lockRoot = lockedRoot;
    }
#endif
}
