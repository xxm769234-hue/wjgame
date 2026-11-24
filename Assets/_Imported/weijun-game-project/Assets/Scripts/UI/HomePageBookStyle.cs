using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HomePageBookStyle : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite bookCoverSprite;
    [SerializeField] private Sprite leftPageSprite;
    [SerializeField] private Sprite rightPageSprite;
    [SerializeField] private Sprite metricSprite;
    [SerializeField] private Sprite featureButtonSprite;
    [SerializeField] private Sprite bottomTabSprite;
    [SerializeField] private Sprite mapCardSprite;

    [Header("Colors")]
    [SerializeField] private Color deskColor = new Color(0.54f, 0.61f, 0.60f, 1f);
    [SerializeField] private Color bookCoverColor = new Color(0.47f, 0.30f, 0.18f, 1f);
    [SerializeField] private Color pageColor = new Color(0.97f, 0.93f, 0.82f, 1f);
    [SerializeField] private Color metricColor = new Color(0.98f, 0.91f, 0.74f, 0.98f);
    [SerializeField] private Color featureButtonColor = new Color(0.99f, 0.94f, 0.81f, 1f);
    [SerializeField] private Color bottomTabColor = new Color(0.98f, 0.88f, 0.62f, 1f);
    [SerializeField] private Color mapCardColor = new Color(0.79f, 0.86f, 0.73f, 1f);

    public Color DeskColor => deskColor;
    public Color BookCoverColor => bookCoverColor;
    public Color PageColor => pageColor;
    public Color MetricColor => metricColor;
    public Color FeatureButtonColor => featureButtonColor;
    public Color BottomTabColor => bottomTabColor;
    public Color MapCardColor => mapCardColor;
    public bool HasMapCardSprite => mapCardSprite != null;

    public void ApplyBackground(Image image)
    {
        Apply(image, backgroundSprite, deskColor, false);
    }

    public void ApplyBookCover(Image image)
    {
        Apply(image, bookCoverSprite, bookCoverColor, true);
    }

    public void ApplyLeftPage(Image image)
    {
        Apply(image, leftPageSprite, pageColor, true);
    }

    public void ApplyRightPage(Image image)
    {
        Apply(image, rightPageSprite != null ? rightPageSprite : leftPageSprite, pageColor, true);
    }

    public void ApplyMetric(Image image)
    {
        Apply(image, metricSprite, metricColor, true);
    }

    public void ApplyFeatureButton(Image image)
    {
        if (featureButtonSprite == null)
        {
            return;
        }

        Apply(image, featureButtonSprite, featureButtonColor, true);
    }

    public void ApplyBottomTab(Image image)
    {
        Apply(image, bottomTabSprite != null ? bottomTabSprite : featureButtonSprite, bottomTabColor, true);
    }

    public void ApplyMapCard(Image image)
    {
        Apply(image, mapCardSprite, mapCardColor, false);
    }

    [ContextMenu("Apply Style To Book Preview")]
    public void ApplyToHierarchy()
    {
        Transform mainPage = transform.Find("MainBookPage");
        if (mainPage == null)
        {
            return;
        }

        ApplyBackground(mainPage.GetComponent<Image>());
        ApplyAtPath(mainPage, "OpenBookCover", ApplyBookCover);
        ApplyAtPath(mainPage, "LeftStoryPage", ApplyLeftPage);
        ApplyAtPath(mainPage, "RightTownPage", ApplyRightPage);

        Image[] images = mainPage.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (IsFeatureButton(image.name))
            {
                ApplyFeatureButton(image);
            }
            else if (IsBottomTab(image.name))
            {
                ApplyBottomTab(image);
            }
            else if (image.name == "EnterMapButton" && HasMapCardSprite)
            {
                ApplyMapCard(image);
            }
        }
    }

    private static void ApplyAtPath(Transform root, string path, System.Action<Image> applyStyle)
    {
        Transform target = root.Find(path);
        if (target != null)
        {
            applyStyle(target.GetComponent<Image>());
        }
    }

    private static bool IsFeatureButton(string objectName)
    {
        switch (objectName)
        {
            case "RightBattleButton":
            case "BattleEnterButton":
            case "ManagementEnterButton":
            case "ActivityButton":
            case "RoleButton":
            case "LotteryButton":
            case "BottomMailButton":
            case "SideBagButton":
            case "SideEquipmentButton":
            case "SideShopButton":
            case "FriendsButton":
            case "BottomNoticeButton":
            case "SideSettingsButton":
            case "BottomTaskButton":
            case "BottomDailySignButton":
            case "BottomNewbieSignButton":
            case "BottomRechargeButton":
                return true;
            default:
                return false;
        }
    }

    private static bool IsBottomTab(string objectName)
    {
        switch (objectName)
        {
            case "BottomMailTab":
            case "BottomSignTab":
            case "BottomLotteryTab":
            case "BottomRechargeTab":
            case "BottomTaskTab":
            case "BottomFriendTab":
            case "BottomNoticeTab":
                return true;
            default:
                return false;
        }
    }

    private static void Apply(Image image, Sprite sprite, Color fallbackColor, bool slicedWhenPossible)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = null;
        image.color = fallbackColor;
        image.preserveAspect = false;
        image.type = Image.Type.Simple;
        if (sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = false;
        image.type = slicedWhenPossible && HasBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
    }

    private static bool HasBorder(Sprite sprite)
    {
        Vector4 border = sprite.border;
        return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ApplyToHierarchy();
        }
    }

    public void EditorSetDefaultSprites(Sprite background)
    {
        if (backgroundSprite == null)
        {
            backgroundSprite = background;
        }
    }
#endif
}
