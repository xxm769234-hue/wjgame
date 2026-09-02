using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LaunchLoginContentBuilder
{
    private const string LaunchScenePath = "Assets/_Project/Scenes/Launch.unity";
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal.otf";
    private const string TmpFontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const string BackgroundSpritePath = "Assets/_Imported/weijun-game-project/Assets/UI/背景/c9a90ac012534a68af69a6c031d102ca.png";
    private const string LogoSpritePath = "Assets/_Project/Art/UI/Login/书屿梦行.png";
    private const string StartGameButtonSpritePath = "Assets/_Project/Art/UI/Login/按键xuanli.png";

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

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        style.EditorSetFont(font);
        style.EditorSetLoginSprites(
            AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath),
            AssetDatabase.LoadAssetAtPath<Sprite>(LogoSpritePath),
            AssetDatabase.LoadAssetAtPath<Sprite>(StartGameButtonSpritePath));

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
        background.raycastTarget = false;
        Stretch(background.rectTransform);

        RectTransform landingContent = CreateRectObject("LandingContent", contentRoot);
        Stretch(landingContent);
        Image logo = CreateImage("GameLogo", landingContent, Color.white);
        style.ApplyLogo(logo);
        logo.preserveAspect = true;
        logo.raycastTarget = false;
        SetCentered(logo.rectTransform, new Vector2(0f, 210f), new Vector2(620f, 465f));

        Button startGameButton = CreateStartGameButton(landingContent, style);
        Toggle privacyConsentToggle = CreatePrivacyConsentToggle(contentRoot, style);
        SetCentered(privacyConsentToggle.GetComponent<RectTransform>(), new Vector2(-95f, -355f), new Vector2(490f, 38f));
        CreateFixedButton(contentRoot, "PrivacyPolicyButton", "查看隐私协议", new Vector2(270f, -355f), new Vector2(180f, 42f), PaperColor, style.PaperTextColor, 15, style);

        Text healthAdvice = CreateText(
            "HealthyGamingAdvice",
            contentRoot,
            "抵制不良游戏，拒绝盗版游戏。注意自我保护，谨防受骗上当。\n适度游戏益脑，沉迷游戏伤身。合理安排时间，享受健康生活。",
            21,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            style);
        SetRect(healthAdvice.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1280f, 68f));
        Outline healthOutline = healthAdvice.gameObject.AddComponent<Outline>();
        healthOutline.effectColor = new Color(0.24f, 0.13f, 0.07f, 0.92f);
        healthOutline.effectDistance = new Vector2(2f, -2f);

        Image loginDialogOverlay = CreateImage("LoginDialog", contentRoot, new Color(0.05f, 0.03f, 0.02f, 0.68f));
        loginDialogOverlay.raycastTarget = true;
        Stretch(loginDialogOverlay.rectTransform);

        Image cardShadow = CreateImage("DialogShadow", loginDialogOverlay.transform, style.ShadowColor);
        cardShadow.raycastTarget = false;
        SetCentered(cardShadow.rectTransform, new Vector2(12f, -12f), new Vector2(860f, 740f));

        Image card = CreateImage("DialogPanel", loginDialogOverlay.transform, PaperColor);
        style.ApplyPaper(card);
        card.raycastTarget = true;
        SetCentered(card.rectTransform, Vector2.zero, new Vector2(860f, 740f));
        Outline cardOutline = card.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.22f, 0.12f, 0.06f, 0.72f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        Text dialogTitle = CreateText("DialogTitle", card.transform, "账号登录", 36, FontStyle.Bold, style.PaperTextColor, TextAnchor.MiddleCenter, style);
        SetCentered(dialogTitle.rectTransform, new Vector2(0f, 318f), new Vector2(600f, 54f));
        Text dialogSubtitle = CreateText("DialogSubtitle", card.transform, "登录后继续你的书屿旅程", 17, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleCenter, style);
        SetCentered(dialogSubtitle.rectTransform, new Vector2(0f, 278f), new Vector2(600f, 36f));
        Button loginDialogCloseButton = CreateFixedButton(card.transform, "LoginDialogCloseButton", "×", new Vector2(382f, 320f), new Vector2(54f, 48f), new Color(0.55f, 0.34f, 0.18f, 1f), Color.white, 28, style);

        InputField accountInput = BuildLoginInput(card.transform, "AccountField", "AccountInput", "账号", "请输入登录账号", 186f, 50, style);
        InputField nicknameInput = BuildLoginInput(card.transform, "NicknameField", "NicknameInput", "昵称", "注册或首次微信登录时填写", 81f, 50, style);
        InputField passwordInput = BuildLoginInput(card.transform, "PasswordField", "PasswordInput", "密码", "请输入密码", -24f, 255, style);
        passwordInput.contentType = InputField.ContentType.Password;

        Toggle rememberToggle = CreateRememberToggle(card.transform, style);
        SetCentered(rememberToggle.GetComponent<RectTransform>(), new Vector2(-214f, -82f), new Vector2(220f, 28f));

        RectTransform actions = CreateRectObject("LoginActions", card.transform);
        Stretch(actions);
        Button loginButton = CreateFixedButton(actions, "LoginButton", "登录", new Vector2(-158f, -154f), new Vector2(290f, 54f), PrimaryColor, Color.white, 22, style);
        Button registerButton = CreateFixedButton(actions, "RegisterButton", "注册并登录", new Vector2(158f, -154f), new Vector2(290f, 54f), new Color(0.76f, 0.83f, 0.92f, 1f), new Color(0.12f, 0.22f, 0.34f, 1f), 21, style);
        Button weChatButton = CreateFixedButton(actions, "WeChatLoginButton", "微信登录", new Vector2(-158f, -224f), new Vector2(290f, 54f), WeChatButtonColor, Color.white, 21, style);
        Button settingsButton = CreateFixedButton(actions, "SettingsButton", "设置", new Vector2(78f, -224f), new Vector2(150f, 54f), PaperColor, style.PaperTextColor, 18, style);
        Button mailButton = CreateFixedButton(actions, "MailButton", "邮件", new Vector2(256f, -224f), new Vector2(150f, 54f), PaperColor, style.PaperTextColor, 18, style);
        Text statusText = CreateText("StatusText", card.transform, string.Empty, 18, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter, style);
        SetCentered(statusText.rectTransform, new Vector2(0f, -304f), new Vector2(690f, 52f));

        GameObject settingsPanel = BuildSettingsPanel(contentRoot, style, out Button settingsCloseButton);
        loginDialogOverlay.gameObject.SetActive(false);
        contentView.EditorSetReferences(
            style,
            startGameButton,
            loginDialogOverlay.gameObject,
            loginDialogCloseButton,
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

    private static Button CreateStartGameButton(Transform parent, LoginCanvasStyle style)
    {
        Image image = CreateImage("StartGameButton", parent, Color.white);
        style.ApplyStartGameButton(image);
        image.preserveAspect = true;
        image.raycastTarget = true;
        SetCentered(image.rectTransform, new Vector2(0f, -220f), new Vector2(430f, 164f));

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateSelectableColors(Color.white, new Color(1f, 0.96f, 0.90f, 1f), new Color(0.82f, 0.82f, 0.82f, 1f), new Color(0.62f, 0.62f, 0.62f, 0.82f));

        Text label = CreateText("Text", image.transform, "开始游戏", 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, style);
        Stretch(label.rectTransform, new Vector2(24f, 16f), new Vector2(-24f, -16f));
        Outline outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.10f, 0.05f, 0.82f);
        outline.effectDistance = new Vector2(2f, -2f);
        return button;
    }

    private static Toggle CreatePrivacyConsentToggle(Transform parent, LoginCanvasStyle style)
    {
        RectTransform root = CreateRectObject("PrivacyConsentToggle", parent);
        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;

        Image box = CreateImage("Box", root, Color.white);
        SetRect(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
        Image checkmark = CreateImage("Checkmark", box.transform, PrimaryColor);
        SetRect(checkmark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        toggle.targetGraphic = box;
        toggle.graphic = checkmark;
        toggle.isOn = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.layer = root.gameObject.layer;
        labelObject.transform.SetParent(root, false);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        label.text = "我已阅读并同意当前隐私协议";
        label.fontSize = style.ResolveFontSize(15);
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(34f, 0f), Vector2.zero);
        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.24f, 0.13f, 0.07f, 0.88f);
        outline.effectDistance = new Vector2(1f, -1f);
        return toggle;
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

        InputField input = frame.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame;
        input.transition = Selectable.Transition.ColorTint;
        input.colors = CreateSelectableColors(InputColor, InputHoverColor, Color.white, new Color(0.82f, 0.86f, 0.92f, 1f));
        input.lineType = InputField.LineType.SingleLine;

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
        Text text = root.gameObject.AddComponent<Text>();
        int resolvedFontSize = style.ResolveFontSize(fontSize);
        text.font = style.ResolveFont(AssetDatabase.LoadAssetAtPath<Font>(FontPath));
        text.text = value;
        text.fontSize = resolvedFontSize;
        text.fontStyle = style.ResolveFontStyle(name, fontStyle);
        text.color = style.ResolveTextColor(name, color);
        text.alignment = alignment;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Clamp(resolvedFontSize - 4, 8, resolvedFontSize);
        text.resizeTextMaxSize = resolvedFontSize;
        text.raycastTarget = false;
        return text;
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
