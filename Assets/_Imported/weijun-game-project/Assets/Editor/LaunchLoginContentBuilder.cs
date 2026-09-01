using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;
using Font = TMPro.TMP_FontAsset;
using InputField = TMPro.TMP_InputField;

public static class LaunchLoginContentBuilder
{
    private const string LaunchScenePath = "Assets/_Project/Scenes/Launch.unity";
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";

    private static readonly Color BackgroundColor = new Color(0.48f, 0.30f, 0.16f, 1f);
    private static readonly Color ShadowColor = new Color(0.18f, 0.10f, 0.05f, 0.28f);
    private static readonly Color ButtonShadowColor = new Color(0.18f, 0.10f, 0.05f, 0.24f);
    private static readonly Color PaperColor = new Color(0.98f, 0.95f, 0.86f, 1f);
    private static readonly Color PanelColor = new Color(0.42f, 0.25f, 0.12f, 0.42f);
    private static readonly Color DecorLineColor = new Color(0.77f, 0.45f, 0.22f, 0.72f);
    private static readonly Color PrimaryColor = new Color(0.09f, 0.28f, 0.58f, 1f);
    private static readonly Color WeChatButtonColor = new Color(0.16f, 0.70f, 0.32f, 1f);
    private static readonly Color InputColor = new Color(0.99f, 0.97f, 0.90f, 1f);
    private static readonly Color InputHoverColor = new Color(0.92f, 0.95f, 1f, 1f);
    private static readonly Color FieldTextColor = new Color(0.10f, 0.13f, 0.18f, 1f);
    private static readonly Color MutedTextColor = new Color(0.40f, 0.45f, 0.52f, 1f);
    private static readonly Color ErrorColor = new Color(0.85f, 0.18f, 0.18f, 1f);

    [MenuItem("Tools/WeiJun UI/Rebuild Launch Login Content")]
    public static void RebuildActiveLaunchLoginContent()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LaunchScenePath)
        {
            scene = EditorSceneManager.OpenScene(LaunchScenePath, OpenSceneMode.Single);
        }

        BuildScene(scene, true);
    }

    public static void BuildLaunchScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LaunchScenePath, OpenSceneMode.Single);
        BuildScene(scene, true);
    }

    private static void BuildScene(Scene scene, bool saveScene)
    {
        GameBoot gameBoot = FindGameBoot(scene);
        if (gameBoot == null)
        {
            Debug.LogError("Launch scene does not contain GameBoot.");
            return;
        }

        LoginCanvasView canvasView = gameBoot.GetComponentInChildren<LoginCanvasView>(true);
        if (canvasView == null)
        {
            Debug.LogError("Launch scene does not contain LoginCanvasView.");
            return;
        }

        RectTransform contentRoot = canvasView.PrepareContentRoot();
        LoginContentView contentView = canvasView.ContentView;
        if (contentRoot == null || contentView == null)
        {
            Debug.LogError("Launch scene does not contain LoginContentView.");
            return;
        }

        LoginCanvasStyle style = contentView.Style;
        if (style == null)
        {
            style = contentRoot.GetComponent<LoginCanvasStyle>();
        }

        if (style == null)
        {
            style = contentRoot.gameObject.AddComponent<LoginCanvasStyle>();
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath)
            ?? RuntimeFontAssetProvider.Resolve();
        style.EditorSetFont(font);

        ClearChildren(contentRoot);
        BuildLoginContent(contentRoot, contentView, style);

        EditorUtility.SetDirty(contentView);
        EditorUtility.SetDirty(style);
        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene)
        {
            EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject = contentRoot.gameObject;
        Debug.Log("Launch LoginContent rebuilt as editable Unity UI components.");
    }

    private static void BuildLoginContent(RectTransform contentRoot, LoginContentView contentView, LoginCanvasStyle style)
    {
        Image background = CreateImage("Background", contentRoot, BackgroundColor);
        style.ApplyBackground(background);
        Stretch(background.rectTransform);

        RectTransform decoration = CreateRectObject("Decoration", contentRoot);
        Stretch(decoration);
        CreateDecorLine("TopLeftLine", decoration, new Vector2(-650f, 380f), style);
        CreateDecorLine("TopRightLine", decoration, new Vector2(650f, 380f), style);
        CreateDecorLine("BottomLeftLine", decoration, new Vector2(-650f, -380f), style);
        CreateDecorLine("BottomRightLine", decoration, new Vector2(650f, -380f), style);

        Image cardShadow = CreateImage("LoginCardShadow", contentRoot, style.ShadowColor);
        cardShadow.raycastTarget = false;
        SetCentered(cardShadow.rectTransform, new Vector2(10f, -10f), new Vector2(850f, 960f));

        Image card = CreateImage("LoginCard", contentRoot, PanelColor);
        style.ApplyPanel(card);
        card.raycastTarget = false;
        SetCentered(card.rectTransform, Vector2.zero, new Vector2(850f, 960f));

        BuildLogo(card.transform, style);
        InputField accountInput = BuildLoginInput(card.transform, "AccountField", "AccountInput", "账号", "请输入登录账号", 232f, 50, style);
        InputField nicknameInput = BuildLoginInput(card.transform, "NicknameField", "NicknameInput", "昵称", "注册或首次微信登录时填写", 124f, 50, style);
        InputField passwordInput = BuildLoginInput(card.transform, "PasswordField", "PasswordInput", "密码", "请输入密码", 16f, 255, style);
        passwordInput.contentType = InputField.ContentType.Password;

        Toggle rememberToggle = CreateRememberToggle(card.transform, style);
        SetCentered(rememberToggle.GetComponent<RectTransform>(), new Vector2(-214f, -56f), new Vector2(220f, 28f));

        RectTransform actions = CreateRectObject("LoginActions", card.transform);
        Stretch(actions);
        Button loginButton = CreateFixedButton(actions, "LoginButton", "登录", new Vector2(-158f, -132f), new Vector2(290f, 54f), PrimaryColor, Color.white, 22, style);
        Button registerButton = CreateFixedButton(actions, "RegisterButton", "注册并登录", new Vector2(158f, -132f), new Vector2(290f, 54f), new Color(0.76f, 0.83f, 0.92f, 1f), new Color(0.12f, 0.22f, 0.34f, 1f), 21, style);
        Button weChatButton = CreateFixedButton(actions, "WeChatLoginButton", "微信登录", new Vector2(-158f, -204f), new Vector2(290f, 54f), WeChatButtonColor, Color.white, 21, style);
        Text statusText = CreateText("StatusText", card.transform, string.Empty, 18, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter, style);
        SetCentered(statusText.rectTransform, new Vector2(0f, -276f), new Vector2(650f, 48f));

        RectTransform secondaryActions = CreateRectObject("SecondaryActions", contentRoot);
        Stretch(secondaryActions);
        Button settingsButton = CreateFixedButton(secondaryActions, "SettingsButton", "设置", new Vector2(852f, 390f), new Vector2(104f, 72f), PaperColor, style.PaperTextColor, 18, style);
        Button mailButton = CreateFixedButton(secondaryActions, "MailButton", "邮件", new Vector2(852f, 300f), new Vector2(104f, 72f), PaperColor, style.PaperTextColor, 18, style);

        GameObject settingsPanel = BuildSettingsPanel(contentRoot, style, out Button settingsCloseButton);
        contentView.EditorSetReferences(
            style,
            accountInput,
            nicknameInput,
            passwordInput,
            rememberToggle,
            loginButton,
            registerButton,
            weChatButton,
            statusText,
            settingsButton,
            mailButton,
            settingsPanel,
            settingsCloseButton);
    }

    private static void BuildLogo(Transform parent, LoginCanvasStyle style)
    {
        Image shadow = CreateImage("LogoShadow", parent, style.ShadowColor);
        shadow.raycastTarget = false;
        SetCentered(shadow.rectTransform, new Vector2(8f, 384f), new Vector2(650f, 126f));

        Image panel = CreateImage("LogoPanel", parent, style.PaperColor);
        style.ApplyPaper(panel);
        SetCentered(panel.rectTransform, new Vector2(0f, 392f), new Vector2(650f, 126f));

        Text title = CreateText("LogoText", panel.transform, "维钧游戏", 42, FontStyle.Bold, new Color(0.74f, 0.20f, 0.15f, 1f), TextAnchor.MiddleCenter, style);
        SetRect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(-24f, -44f));

        Text subtitle = CreateText("LogoSubtitle", panel.transform, "登录领地手册，继续你的旅程", 16, FontStyle.Normal, new Color(0.48f, 0.39f, 0.31f, 1f), TextAnchor.MiddleCenter, style);
        SetRect(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-24f, 30f));
    }

    private static InputField BuildLoginInput(
        Transform parent,
        string groupName,
        string inputName,
        string label,
        string placeholder,
        float yPosition,
        int characterLimit,
        LoginCanvasStyle style)
    {
        RectTransform group = CreateRectObject(groupName, parent);
        Stretch(group);

        Text fieldLabel = CreateText(inputName + "Label", group, label + "：", 22, FontStyle.Bold, style.LabelTextColor, TextAnchor.MiddleRight, style);
        SetRect(fieldLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-310f, yPosition), new Vector2(100f, 50f));

        Image shadow = CreateImage(inputName + "Shadow", group, style.ShadowColor);
        shadow.raycastTarget = false;
        SetCentered(shadow.rectTransform, new Vector2(58f, yPosition - 6f), new Vector2(570f, 58f));

        InputField input = CreateInputField(inputName, group, placeholder, style);
        input.characterLimit = characterLimit;
        SetCentered(input.GetComponent<RectTransform>(), new Vector2(50f, yPosition), new Vector2(570f, 58f));
        return input;
    }

    private static Toggle CreateRememberToggle(Transform parent, LoginCanvasStyle style)
    {
        RectTransform root = CreateRectObject("RememberToggle", parent);
        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;

        Image box = CreateImage("Box", root, Color.white);
        SetRect(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

        Image checkmark = CreateImage("Checkmark", box.transform, PrimaryColor);
        SetRect(checkmark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));

        toggle.targetGraphic = box;
        toggle.graphic = checkmark;

        Text label = CreateText("Label", root, "记住账号", 13, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft, style);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), Vector2.zero);
        return toggle;
    }

    private static GameObject BuildSettingsPanel(Transform parent, LoginCanvasStyle style, out Button closeButton)
    {
        Image panel = CreateImage("SettingsPanel", parent, PaperColor);
        style.ApplyPaper(panel);
        SetCentered(panel.rectTransform, Vector2.zero, new Vector2(460f, 320f));

        Text title = CreateText("Title", panel.transform, "设置", 30, FontStyle.Bold, FieldTextColor, TextAnchor.MiddleCenter, style);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 46f));

        Text body = CreateText("Body", panel.transform, "音乐：开启\n音效：开启\n画质：标准\n\n后续可在这里接入更多设置项。", 20, FontStyle.Normal, FieldTextColor, TextAnchor.UpperLeft, style);
        SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(-72f, 150f));

        closeButton = CreateFixedButton(panel.transform, "CloseButton", "关闭", new Vector2(0f, -118f), new Vector2(130f, 46f), new Color(0.55f, 0.34f, 0.18f, 1f), Color.white, 18, style);
        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    private static Button CreateFixedButton(
        Transform parent,
        string name,
        string text,
        Vector2 position,
        Vector2 size,
        Color color,
        Color textColor,
        int fontSize,
        LoginCanvasStyle style)
    {
        Image shadow = CreateImage(name + "Shadow", parent, style.ButtonShadowColor);
        shadow.raycastTarget = false;
        SetCentered(shadow.rectTransform, position + new Vector2(6f, -6f), size);

        Color buttonColor = style.ResolveButtonColor(name, color);
        Image image = CreateImage(name, parent, buttonColor);
        style.ApplyButton(image, name, buttonColor);
        image.raycastTarget = true;
        SetCentered(image.rectTransform, position, size);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateSelectableColors(buttonColor, Color.Lerp(buttonColor, Color.white, 0.12f), Color.Lerp(buttonColor, Color.black, 0.18f), new Color(0.70f, 0.75f, 0.82f, 1f));

        Text label = CreateText("Text", image.transform, text, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter, style);
        Stretch(label.rectTransform);
        return button;
    }

    private static InputField CreateInputField(string name, Transform parent, string placeholder, LoginCanvasStyle style)
    {
        Image frame = CreateImage(name, parent, InputColor);
        style.ApplyInput(frame);
        frame.raycastTarget = true;

        TMP_InputField input = frame.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = frame;
        input.transition = Selectable.Transition.ColorTint;
        input.colors = CreateSelectableColors(InputColor, InputHoverColor, Color.white, new Color(0.82f, 0.86f, 0.92f, 1f));
        input.lineType = TMP_InputField.LineType.SingleLine;

        Text text = CreateText("Text", frame.transform, string.Empty, 18, FontStyle.Normal, FieldTextColor, TextAnchor.MiddleLeft, style);
        Stretch(text.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
        input.textComponent = text;

        Text placeholderText = CreateText("Placeholder", frame.transform, placeholder, 16, FontStyle.Normal, new Color(0.56f, 0.61f, 0.68f, 1f), TextAnchor.MiddleLeft, style);
        Stretch(placeholderText.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
        input.placeholder = placeholderText;
        return input;
    }

    private static void CreateDecorLine(string name, Transform parent, Vector2 position, LoginCanvasStyle style)
    {
        Image line = CreateImage(name, parent, style.DecorLineColor);
        line.raycastTarget = false;
        SetCentered(line.rectTransform, position, new Vector2(520f, 8f));
    }

    private static GameBoot FindGameBoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameBoot gameBoot = roots[rootIndex].GetComponentInChildren<GameBoot>(true);
            if (gameBoot != null)
            {
                return gameBoot;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
        {
            Object.DestroyImmediate(parent.GetChild(childIndex).gameObject);
        }
    }

    private static RectTransform CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        gameObject.layer = uiLayer >= 0 ? uiLayer : 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform root = CreateRectObject(name, parent);
        root.gameObject.AddComponent<CanvasRenderer>();
        Image image = root.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment, LoginCanvasStyle style)
    {
        RectTransform root = CreateRectObject(name, parent);
        root.gameObject.AddComponent<CanvasRenderer>();
        TMP_Text text = root.gameObject.AddComponent<TextMeshProUGUI>();
        int resolvedFontSize = style.ResolveFontSize(fontSize);
        text.font = style.ResolveFont(RuntimeFontAssetProvider.Resolve());
        text.text = value;
        text.fontSize = resolvedFontSize;
        text.fontStyle = style.ResolveFontStyle(name, fontStyle);
        text.color = style.ResolveTextColor(name, color);
        text.alignment = ConvertTextAlignment(alignment);
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Clamp(resolvedFontSize - 4, 8, resolvedFontSize);
        text.fontSizeMax = resolvedFontSize;
        text.raycastTarget = false;
        return text;
    }

    private static TextAlignmentOptions ConvertTextAlignment(TextAnchor alignment)
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
            default: return TextAlignmentOptions.TopLeft;
        }
    }

    private static ColorBlock CreateSelectableColors(Color normal, Color highlighted, Color pressed, Color disabled)
    {
        return new ColorBlock
        {
            normalColor = normal,
            highlightedColor = highlighted,
            pressedColor = pressed,
            selectedColor = highlighted,
            disabledColor = disabled,
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
