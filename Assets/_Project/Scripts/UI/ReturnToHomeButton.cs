using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ReturnToHomeButton : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private string buttonLabel = "";
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
        if (PlayerOnboardingInputLock.IsGameplayInputBlocked) return;
        if (BuildManager.Instance != null) BuildManager.Instance.ExitBuild();

        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    private void ConfigureAppearance()
    {
        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.color = Color.clear;
            background.type = Image.Type.Simple;
            background.raycastTarget = false;
        }

        SmallButtonIconUtility.ApplyIconButtonWithLabel(button, "\u8FD4\u56DE", "返回");
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -28f);
    }
}

public static class SmallButtonIconUtility
{
    private const string ResourcePath = "UI/SmallIcons/";

    public static void ApplyOutlinedLabel(TMP_Text label)
    {
        if (label == null) return;
        label.color = Color.white;
        Outline outline = label.GetComponent<Outline>();
        if (outline == null) outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = false;
    }

    public static void ApplyNavigationLabel(TMP_Text label)
    {
        if (label == null) return;
        label.color = new Color(0.22f, 0.12f, 0.07f, 1f);
        label.outlineColor = new Color32(255, 239, 207, 255);
        label.outlineWidth = 0.04f;
        label.extraPadding = true;
        Outline outline = label.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        label.UpdateMeshPadding();
    }

    public static bool ApplyToImage(Image image, string resourceName)
    {
        if (image == null) return false;

        Sprite sprite = LoadApprovedIcon(resourceName);
        if (sprite == null) sprite = Resources.Load<Sprite>(ResourcePath + resourceName);
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

    private static Sprite LoadApprovedIcon(string resourceName)
    {
        string approvedName;
        switch (resourceName)
        {
            case "返回": approvedName = "10_return"; break;
            case "建造副": approvedName = "06_build"; break;
            case "一键收取": approvedName = "18_resource_reward"; break;
            case "编队": approvedName = "13_formation"; break;
            case "对战副": approvedName = "19_battle"; break;
            default: return null;
        }
        return Resources.Load<Sprite>("UI/ApprovedUi/Icons/" + approvedName);
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

    public static Image ApplyIconOnlyToButton(Button button, string resourceName)
    {
        if (button == null) return null;

        button.transition = Selectable.Transition.ColorTint;

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.color = Color.clear;
            background.type = Image.Type.Simple;
            background.raycastTarget = false;
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

        icon.gameObject.SetActive(true);
        icon.color = Color.white;
        icon.raycastTarget = true;

        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        icon.transform.SetAsLastSibling();

        button.targetGraphic = icon;
        return icon;
    }

    public static Image ApplyIconButtonWithLabel(
        Button button,
        string resourceName,
        string labelText,
        float iconSize = 72f,
        float labelHeight = 32f,
        float labelGap = 4f)
    {
        if (button == null) return null;

        button.transition = Selectable.Transition.ColorTint;
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

        float buttonWidth = Mathf.Max(96f, iconSize, labelText.Length * 20f + 12f);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null) buttonRect.sizeDelta = new Vector2(buttonWidth, iconSize + labelGap + labelHeight);

        icon.gameObject.SetActive(true);
        icon.color = Color.white;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = new Vector2(0f, (labelHeight + labelGap) * 0.5f);
        icon.transform.SetAsFirstSibling();
        button.targetGraphic = icon;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            GameObject labelObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.layer = button.gameObject.layer;
            labelObject.transform.SetParent(button.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }
        if (label != null && label.transform != icon.transform)
        {
            label.gameObject.SetActive(true);
            label.font = RuntimeFontAssetProvider.Resolve();
            label.text = FrontendTextLocalization.Normalize(labelText);
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            ApplyNavigationLabel(label);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.margin = Vector4.zero;
            label.characterSpacing = 0f;
            label.lineSpacing = 0f;
            label.paragraphSpacing = 0f;
            label.maxVisibleCharacters = int.MaxValue;
            label.maxVisibleWords = int.MaxValue;
            label.maxVisibleLines = 1;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 20f;
            label.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(buttonWidth, labelHeight);
            labelRect.anchoredPosition = new Vector2(0f, -(iconSize + labelGap) * 0.5f);
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
