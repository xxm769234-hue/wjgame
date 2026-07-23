using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventorySlotView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button slotButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private Image fallbackBackgroundImage;
    [SerializeField] private Text fallbackText;
    [SerializeField] private Text quantityText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private GameObject lockRoot;

    [Header("Slot Style")]
    [SerializeField] private Color emptyColor = new Color(0.92f, 0.68f, 0.34f, 1f);
    [SerializeField] private Color occupiedColor = new Color(0.98f, 0.78f, 0.40f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.62f, 0.62f, 0.58f, 1f);
    [SerializeField] private Color innerColor = new Color(1f, 0.86f, 0.54f, 0.68f);
    [SerializeField] private Color lockedInnerColor = new Color(0.70f, 0.70f, 0.67f, 1f);

    private Sprite prefabItemIcon;

    private void Awake()
    {
        if (itemIconImage != null)
        {
            prefabItemIcon = itemIconImage.sprite;
        }
    }

    public void ShowEmpty()
    {
        ResetState();
        SetColor(backgroundImage, emptyColor);
        SetColor(innerImage, innerColor);
    }

    public void ShowLocked(UnityAction onClick)
    {
        ResetState();
        SetColor(backgroundImage, lockedColor);
        SetColor(innerImage, lockedInnerColor);
        SetActive(lockRoot, true);
        BindClick(onClick);
    }

    public void ShowItem(
        Sprite itemSprite,
        string fallbackLabel,
        int quantity,
        bool selected,
        Color fallbackColor,
        UnityAction onClick)
    {
        ResetState();
        SetColor(backgroundImage, occupiedColor);
        SetColor(innerImage, innerColor);

        Sprite displaySprite = itemSprite != null ? itemSprite : prefabItemIcon;
        if (itemIconImage != null)
        {
            itemIconImage.sprite = displaySprite;
            itemIconImage.enabled = displaySprite != null;
            itemIconImage.preserveAspect = true;
        }

        bool useFallback = displaySprite == null;
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

        SetActive(selectedFrame, selected);
        BindClick(onClick);
    }

    private void ResetState()
    {
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
        SetActive(selectedFrame, false);
        SetActive(lockRoot, false);
    }

    private void BindClick(UnityAction onClick)
    {
        if (slotButton != null && onClick != null)
        {
            slotButton.onClick.AddListener(onClick);
        }
    }

    private static void SetColor(Graphic graphic, Color color)
    {
        if (graphic != null)
        {
            graphic.color = color;
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
        Text itemFallbackText,
        Text itemQuantityText,
        GameObject selection,
        GameObject lockedRoot)
    {
        slotButton = button;
        backgroundImage = background;
        innerImage = inner;
        itemIconImage = itemIcon;
        fallbackBackgroundImage = fallbackBackground;
        fallbackText = itemFallbackText;
        quantityText = itemQuantityText;
        selectedFrame = selection;
        lockRoot = lockedRoot;
    }
#endif
}
