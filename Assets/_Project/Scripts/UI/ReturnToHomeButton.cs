using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ReturnToHomeButton : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private string buttonLabel = "返回主页";
    [SerializeField] private Color buttonColor = new Color(0.18f, 0.26f, 0.34f, 0.96f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.RemoveListener(ReturnToHome);
        button.onClick.AddListener(ReturnToHome);
        ConfigureAppearance();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(ReturnToHome);
    }

    public void ReturnToHome()
    {
        if (BuildManager.Instance != null) BuildManager.Instance.ExitBuild();

        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    private void ConfigureAppearance()
    {
        Image background = GetComponent<Image>();
        if (background != null) background.color = buttonColor;

        TMP_Text label = GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        label.font = RuntimeFontAssetProvider.Resolve();
        label.text = FrontendTextLocalization.Normalize(buttonLabel);
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        SmallButtonIconUtility.ApplyToButton(button, "返回", 42f, 7f, 7f);
    }
}

public static class SmallButtonIconUtility
{
    private const string ResourcePath = "UI/SmallIcons/";

    public static bool ApplyToImage(Image image, string resourceName)
    {
        if (image == null) return false;

        Sprite sprite = Resources.Load<Sprite>(ResourcePath + resourceName);
        if (sprite == null)
        {
            Debug.LogWarning($"Small button icon was not found: {ResourcePath}{resourceName}", image);
            return false;
        }

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        return true;
    }

    public static Image ApplyToButton(
        Button button,
        string resourceName,
        float iconWidth = 44f,
        float horizontalPadding = 8f,
        float verticalPadding = 6f)
    {
        if (button == null) return null;

        Transform iconTransform = button.transform.Find("SmallIcon");
        Image icon;
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("SmallIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = button.gameObject.layer;
            iconObject.transform.SetParent(button.transform, false);
            icon = iconObject.GetComponent<Image>();
        }
        else
        {
            icon = iconTransform.GetComponent<Image>();
            if (icon == null) icon = iconTransform.gameObject.AddComponent<Image>();
        }

        if (!ApplyToImage(icon, resourceName)) return null;

        icon.color = Color.white;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        iconRect.offsetMax = new Vector2(horizontalPadding + iconWidth, -verticalPadding);
        icon.transform.SetAsFirstSibling();

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.transform != icon.transform)
        {
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(horizontalPadding + iconWidth + 4f, labelRect.offsetMin.y);
            labelRect.offsetMax = new Vector2(-horizontalPadding, labelRect.offsetMax.y);
        }

        return icon;
    }

    public static Image ApplyIconButtonWithLabel(
        Button button,
        string resourceName,
        string labelText,
        float iconSize = 72f,
        float labelHeight = 24f,
        float labelGap = 4f)
    {
        if (button == null) return null;

        button.transition = Selectable.Transition.None;
        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.color = Color.clear;
            background.type = Image.Type.Simple;
            background.raycastTarget = true;
            if (button.targetGraphic == null) button.targetGraphic = background;
        }

        Transform iconTransform = button.transform.Find("SmallIcon");
        Image icon;
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("SmallIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = button.gameObject.layer;
            iconObject.transform.SetParent(button.transform, false);
            icon = iconObject.GetComponent<Image>();
        }
        else
        {
            icon = iconTransform.GetComponent<Image>();
            if (icon == null) icon = iconTransform.gameObject.AddComponent<Image>();
        }

        if (!ApplyToImage(icon, resourceName)) return null;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null) buttonRect.sizeDelta = new Vector2(iconSize, iconSize);

        icon.color = Color.white;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = Vector2.zero;
        icon.transform.SetAsFirstSibling();

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.transform != icon.transform)
        {
            label.font = RuntimeFontAssetProvider.Resolve();
            label.text = FrontendTextLocalization.Normalize(labelText);
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(iconSize, labelHeight);
            labelRect.anchoredPosition = new Vector2(0f, -(iconSize * 0.5f + labelGap + labelHeight * 0.5f));
        }

        return icon;
    }

    public static Image ApplyVerticalIconButton(
        Button button,
        string resourceName,
        string labelText,
        float iconSize = 112f,
        float labelHeight = 26f,
        float labelGap = 6f)
    {
        if (button == null) return null;

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.color = Color.clear;
            background.type = Image.Type.Simple;
            background.raycastTarget = true;
        }

        Transform iconTransform = button.transform.Find("SmallIcon");
        Image icon;
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("SmallIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = button.gameObject.layer;
            iconObject.transform.SetParent(button.transform, false);
            icon = iconObject.GetComponent<Image>();
        }
        else
        {
            icon = iconTransform.GetComponent<Image>();
            if (icon == null) icon = iconTransform.gameObject.AddComponent<Image>();
        }

        if (!ApplyToImage(icon, resourceName)) return null;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            Vector2 centerAnchor = (buttonRect.anchorMin + buttonRect.anchorMax) * 0.5f;
            buttonRect.anchorMin = centerAnchor;
            buttonRect.anchorMax = centerAnchor;
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(iconSize, iconSize + labelGap + labelHeight);
        }

        icon.color = Color.white;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = new Vector2(0f, (labelHeight + labelGap) * 0.5f);
        icon.transform.SetAsFirstSibling();

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.transform != icon.transform)
        {
            label.font = RuntimeFontAssetProvider.Resolve();
            label.text = FrontendTextLocalization.Normalize(labelText);
            label.fontSize = 22f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(iconSize, labelHeight);
            labelRect.anchoredPosition = new Vector2(0f, -(iconSize + labelGap) * 0.5f);
        }

        return icon;
    }
}
