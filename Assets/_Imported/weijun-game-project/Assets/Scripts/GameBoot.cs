using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;
using Font = TMPro.TMP_FontAsset;

public partial class GameBoot : MonoBehaviour
{
    private const string HomeSceneName = "HomeScene";
    private const string RememberAccountKey = "LoginRememberAccount";
    private const string AccountKey = "LoginAccount";
    private const string PlayerNicknameKey = "PlayerNickname";

    private static readonly Color BackgroundColor = new Color(0.93f, 0.96f, 0.99f, 1f);
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.14f);
    private static readonly Color CardColor = new Color(1f, 1f, 1f, 0.98f);
    private static readonly Color BrandColor = new Color(0.10f, 0.26f, 0.52f, 1f);
    private static readonly Color BrandAccentColor = new Color(0.19f, 0.54f, 0.95f, 1f);
    private static readonly Color PrimaryColor = new Color(0.09f, 0.28f, 0.58f, 1f);
    private static readonly Color PrimaryPressedColor = new Color(0.06f, 0.20f, 0.42f, 1f);
    private static readonly Color SecondaryButtonColor = new Color(0.87f, 0.90f, 0.95f, 1f);
    private static readonly Color WeChatButtonColor = new Color(0.16f, 0.70f, 0.32f, 1f);
    private static readonly Color FieldColor = new Color(0.96f, 0.98f, 1f, 1f);
    private static readonly Color FieldHoverColor = new Color(0.92f, 0.95f, 1f, 1f);
    private static readonly Color FieldTextColor = new Color(0.10f, 0.13f, 0.18f, 1f);
    private static readonly Color MutedTextColor = new Color(0.40f, 0.45f, 0.52f, 1f);
    private static readonly Color LightTextColor = new Color(1f, 1f, 1f, 0.92f);
    private static readonly Color ErrorColor = new Color(0.85f, 0.18f, 0.18f, 1f);

    private InputField accountInput;
    private InputField nicknameInput;
    private InputField passwordInput;
    private Toggle rememberToggle;
    private Text statusText;
    private Button loginButton;
    private Button registerButton;
    private Button weChatButton;
    private GameObject settingsPanel;
    private bool requestRunning;
    private bool runtimePolicyBlocked;
    private GameObject runtimePolicyOverlay;

    [SerializeField] private LoginCanvasView loginCanvasView;
    private Font defaultFont;

    private LoginCanvasStyle LoginStyle => loginCanvasView != null ? loginCanvasView.Style : null;

    private void Start()
    {
        defaultFont = RuntimeFontAssetProvider.Resolve();
        BuildLoginUI();
        string complianceBlockedMessage = PlayerPrefs.GetString("ComplianceBlockedMessage", string.Empty);
        if (!string.IsNullOrEmpty(complianceBlockedMessage))
        {
            PlayerPrefs.DeleteKey("ComplianceBlockedMessage");
            PlayerPrefs.Save();
            ShowError(complianceBlockedMessage);
        }
        StartCoroutine(LoadClientRuntimePolicy());
    }

    private void Update()
    {
        if ((privacyPolicyOverlay != null && privacyPolicyOverlay.activeSelf)
            || (accountManagementPanel != null && accountManagementPanel.activeSelf)
            || (realNamePanel != null && realNamePanel.activeSelf))
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TryLogin();
        }
    }

    private void BuildLoginUI()
    {
        EnsureEventSystem();

        if (loginCanvasView == null)
        {
            loginCanvasView = GetComponentInChildren<LoginCanvasView>(true);
        }

        if (loginCanvasView == null)
        {
            GameObject canvasObject = new GameObject("LoginCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LoginCanvasStyle), typeof(LoginCanvasView));
            canvasObject.transform.SetParent(transform, false);
            loginCanvasView = canvasObject.GetComponent<LoginCanvasView>();
        }

        RectTransform root = loginCanvasView.PrepareContentRoot();
        if (TryBindSceneLoginLayout())
        {
            EnsurePrivacyControls(root);
            return;
        }

        loginCanvasView.ClearContentForRuntimeFallback();
        BuildFixedLoginLayout(root);
        EnsurePrivacyControls(root);
    }

    private bool TryBindSceneLoginLayout()
    {
        LoginContentView contentView = loginCanvasView != null ? loginCanvasView.ContentView : null;
        if (contentView == null || !contentView.HasCompleteLoginLayout)
        {
            return false;
        }

        accountInput = contentView.AccountInput;
        nicknameInput = contentView.NicknameInput;
        passwordInput = contentView.PasswordInput;
        rememberToggle = contentView.RememberToggle;
        statusText = contentView.StatusText;
        loginButton = contentView.LoginButton;
        registerButton = contentView.RegisterButton;
        weChatButton = contentView.WeChatLoginButton;
        settingsPanel = contentView.SettingsPanel;

        loginButton.onClick.AddListener(TryLogin);
        registerButton.onClick.AddListener(TryRegister);
        weChatButton.onClick.AddListener(TryWeChatLogin);
        contentView.SettingsButton.onClick.AddListener(ShowSettingsPanel);
        contentView.MailButton.onClick.AddListener(() => ShowError("登录后可在首页查看系统邮件"));
        contentView.SettingsCloseButton.onClick.AddListener(HideSettingsPanel);

        settingsPanel.SetActive(false);
        InitializeLoginFields();
        SetStatus(string.Empty, ErrorColor);
        return true;
    }

    private void InitializeLoginFields()
    {
        bool rememberAccount = PlayerPrefs.GetInt(RememberAccountKey, 1) == 1;
        rememberToggle.isOn = rememberAccount;
        accountInput.text = rememberAccount ? PlayerPrefs.GetString(AccountKey, string.Empty) : string.Empty;
        nicknameInput.text = PlayerPrefs.GetString(PlayerNicknameKey, string.Empty);
        passwordInput.text = string.Empty;
    }

    private void BuildModernLoginLayout(RectTransform root)
    {
        Image background = CreateImage("Background", root, BackgroundColor);
        Stretch(background.rectTransform);

        Image shadow = CreateImage("CardShadow", root, ShadowColor);
        SetRect(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, -10f), new Vector2(1048f, 628f));

        Image card = CreateImage("LoginCard", root, CardColor);
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 620f));

        HorizontalLayoutGroup cardLayout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        cardLayout.padding = new RectOffset(0, 0, 0, 0);
        cardLayout.spacing = 0f;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = false;
        cardLayout.childForceExpandHeight = true;

        Image brandPanel = CreateImage("BrandPanel", card.transform, BrandColor);
        SetLayout(brandPanel.gameObject, 360f, 620f);

        Image formPanel = CreateImage("FormPanel", card.transform, Color.white);
        SetLayout(formPanel.gameObject, 680f, 620f);

        VerticalLayoutGroup brandLayout = brandPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        brandLayout.padding = new RectOffset(10, 34, 34, 34);
        brandLayout.spacing = 10f;
        brandLayout.childAlignment = TextAnchor.UpperLeft;
        brandLayout.childControlWidth = true;
        brandLayout.childControlHeight = false;
        brandLayout.childForceExpandWidth = true;
        brandLayout.childForceExpandHeight = false;

        Image accent = CreateImage("AccentLine", brandPanel.transform, BrandAccentColor);
        SetLayout(accent.gameObject, 56f, 4f);

        Text brandTitle = CreateText("BrandTitle", brandPanel.transform, "维钧游戏", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetLayout(brandTitle.gameObject, 0f, 48f);

        Text brandSubtitle = CreateText("BrandSubtitle", brandPanel.transform, "登录后继续你的旅程", 16, FontStyle.Normal, LightTextColor, TextAnchor.MiddleLeft);
        SetLayout(brandSubtitle.gameObject, 0f, 26f);

        CreateSpacer(brandPanel.transform, 16f);

        Text brandHint = CreateText("BrandHint", brandPanel.transform, "账号同步 · 微信登录", 15, FontStyle.Normal, LightTextColor, TextAnchor.MiddleLeft);
        SetLayout(brandHint.gameObject, 0f, 24f);

        CreateSpacer(brandPanel.transform, 14f);

        Text brandFoot = CreateText("BrandFoot", brandPanel.transform, "WJ Game Project", 13, FontStyle.Normal, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleLeft);
        SetLayout(brandFoot.gameObject, 0f, 20f);

        VerticalLayoutGroup formLayout = formPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(34, 34, 30, 28);
        formLayout.spacing = 6f;
        formLayout.childAlignment = TextAnchor.UpperCenter;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = false;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;

        Text formTitle = CreateText("FormTitle", formPanel.transform, "账号登录", 34, FontStyle.Bold, FieldTextColor, TextAnchor.MiddleLeft);
        SetLayout(formTitle.gameObject, 0f, 42f);

        Text formSubtitle = CreateText("FormSubtitle", formPanel.transform, "使用账号继续进入游戏", 16, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);
        SetLayout(formSubtitle.gameObject, 0f, 22f);

        CreateSpacer(formPanel.transform, 4f);

        Text accountLabel = CreateText("AccountLabel", formPanel.transform, "账号", 12, FontStyle.Bold, MutedTextColor, TextAnchor.MiddleLeft);
        SetLayout(accountLabel.gameObject, 0f, 16f);
        accountInput = CreateInputField(formPanel.transform, "AccountInput", "请输入账号");
        SetLayout(accountInput.gameObject, 0f, 28f);

        Text nicknameLabel = CreateText("NicknameLabel", formPanel.transform, "昵称", 12, FontStyle.Bold, MutedTextColor, TextAnchor.MiddleLeft);
        SetLayout(nicknameLabel.gameObject, 0f, 16f);
        nicknameInput = CreateInputField(formPanel.transform, "NicknameInput", "注册时填写昵称");
        SetLayout(nicknameInput.gameObject, 0f, 28f);

        Text passwordLabel = CreateText("PasswordLabel", formPanel.transform, "密码", 12, FontStyle.Bold, MutedTextColor, TextAnchor.MiddleLeft);
        SetLayout(passwordLabel.gameObject, 0f, 16f);
        passwordInput = CreateInputField(formPanel.transform, "PasswordInput", "请输入密码");
        passwordInput.contentType = InputField.ContentType.Password;
        SetLayout(passwordInput.gameObject, 0f, 28f);

        rememberToggle = CreateRememberToggle(formPanel.transform);
        SetLayout(rememberToggle.gameObject, 0f, 20f);

        loginButton = CreateButton(formPanel.transform, "LoginButton", "登录", PrimaryColor, Color.white);
        loginButton.onClick.AddListener(TryLogin);
        SetLayout(loginButton.gameObject, 0f, 20f);

        registerButton = CreateButton(formPanel.transform, "RegisterButton", "注册并登录", SecondaryButtonColor, new Color(0.12f, 0.17f, 0.24f, 1f));
        registerButton.onClick.AddListener(TryRegister);
        SetLayout(registerButton.gameObject, 0f, 20f);

        weChatButton = CreateButton(formPanel.transform, "WeChatButton", "微信登录", WeChatButtonColor, Color.white);
        weChatButton.onClick.AddListener(TryWeChatLogin);
        SetLayout(weChatButton.gameObject, 0f, 20f);

        statusText = CreateText("StatusText", formPanel.transform, string.Empty, 14, FontStyle.Normal, ErrorColor, TextAnchor.MiddleLeft);
        SetLayout(statusText.gameObject, 0f, 18f);

        bool rememberAccount = PlayerPrefs.GetInt(RememberAccountKey, 1) == 1;
        rememberToggle.isOn = rememberAccount;
        accountInput.text = rememberAccount ? PlayerPrefs.GetString(AccountKey, string.Empty) : string.Empty;
        nicknameInput.text = PlayerPrefs.GetString(PlayerNicknameKey, string.Empty);
        passwordInput.text = string.Empty;

        SetStatus(string.Empty, ErrorColor);
    }

    private void BuildFixedLoginLayout(RectTransform root)
    {
        LoginCanvasStyle style = LoginStyle;
        Color paperColor = style != null ? style.PaperColor : new Color(0.98f, 0.95f, 0.86f, 1f);
        Color inkColor = style != null ? style.PaperTextColor : new Color(0.28f, 0.16f, 0.09f, 1f);
        Color shadowColor = style != null ? style.ShadowColor : new Color(0.18f, 0.10f, 0.05f, 0.28f);

        Image background = CreateImage("FixedLoginBackground", root, new Color(0.48f, 0.30f, 0.16f, 1f));
        if (style != null)
        {
            style.ApplyBackground(background);
        }
        Stretch(background.rectTransform);

        Image centerShadow = CreateImage("CenterPanelShadow", root, shadowColor);
        centerShadow.raycastTarget = false;
        SetRect(centerShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(10f, -10f), new Vector2(850f, 960f));

        Image centerPanel = CreateImage("CenterPanel", root, new Color(0.42f, 0.25f, 0.12f, 0.42f));
        if (style != null)
        {
            style.ApplyPanel(centerPanel);
        }
        centerPanel.raycastTarget = false;
        SetRect(centerPanel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(850f, 960f));

        CreateDecorLine(root, new Vector2(-650f, 380f), new Vector2(520f, 8f));
        CreateDecorLine(root, new Vector2(650f, 380f), new Vector2(520f, 8f));
        CreateDecorLine(root, new Vector2(-650f, -380f), new Vector2(520f, 8f));
        CreateDecorLine(root, new Vector2(650f, -380f), new Vector2(520f, 8f));

        Image logoShadow = CreateImage("LogoShadow", root, shadowColor);
        logoShadow.raycastTarget = false;
        SetRect(logoShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(8f, 384f), new Vector2(650f, 126f));

        Image logoPanel = CreateImage("LogoPanel", root, paperColor);
        if (style != null)
        {
            style.ApplyPaper(logoPanel);
        }
        SetRect(logoPanel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, 392f), new Vector2(650f, 126f));

        Text logoText = CreateText("LogoText", logoPanel.transform, "维钧游戏", 42, FontStyle.Bold, new Color(0.74f, 0.20f, 0.15f, 1f), TextAnchor.MiddleCenter);
        SetRect(logoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(-24f, -44f));
        Text logoSubtitle = CreateText("LogoSubtitle", logoPanel.transform, "登录领地手册，继续你的旅程", 16, FontStyle.Normal, new Color(0.48f, 0.39f, 0.31f, 1f), TextAnchor.MiddleCenter);
        SetRect(logoSubtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-24f, 30f));

        accountInput = CreateBookInput(root, "AccountInput", "请输入登录账号", "账号", 232f, shadowColor);
        accountInput.characterLimit = 50;

        nicknameInput = CreateBookInput(root, "NicknameInput", "注册或首次微信登录时填写", "昵称", 124f, shadowColor);
        nicknameInput.characterLimit = 50;

        passwordInput = CreateBookInput(root, "PasswordInput", "请输入密码", "密码", 16f, shadowColor);
        passwordInput.contentType = InputField.ContentType.Password;
        passwordInput.characterLimit = 255;

        rememberToggle = CreateRememberToggle(root);
        SetRect(rememberToggle.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(-214f, -56f), new Vector2(220f, 28f));

        loginButton = CreateFixedButton(root, "LoginButton", "登录", new Vector2(-158f, -132f), new Vector2(290f, 54f), PrimaryColor, Color.white, 22);
        loginButton.onClick.AddListener(TryLogin);

        registerButton = CreateFixedButton(root, "RegisterButton", "注册并登录", new Vector2(158f, -132f), new Vector2(290f, 54f), new Color(0.76f, 0.83f, 0.92f, 1f), new Color(0.12f, 0.22f, 0.34f, 1f), 21);
        registerButton.onClick.AddListener(TryRegister);

        weChatButton = CreateFixedButton(root, "WeChatLoginButton", "微信登录", new Vector2(-158f, -204f), new Vector2(290f, 54f), WeChatButtonColor, Color.white, 21);
        weChatButton.onClick.AddListener(TryWeChatLogin);

        Button settingsButton = CreateFixedButton(root, "SettingsButton", "设置", new Vector2(852f, 390f), new Vector2(104f, 72f), paperColor, inkColor, 18);
        settingsButton.onClick.AddListener(ShowSettingsPanel);

        Button mailButton = CreateFixedButton(root, "MailButton", "邮件", new Vector2(852f, 300f), new Vector2(104f, 72f), paperColor, inkColor, 18);
        mailButton.onClick.AddListener(() => ShowError("登录后可在首页查看系统邮件"));

        statusText = CreateText("StatusText", root, string.Empty, 18, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter);
        SetRect(statusText.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, -276f), new Vector2(650f, 48f));

        bool rememberAccount = PlayerPrefs.GetInt(RememberAccountKey, 1) == 1;
        rememberToggle.isOn = rememberAccount;
        accountInput.text = rememberAccount ? PlayerPrefs.GetString(AccountKey, string.Empty) : string.Empty;
        nicknameInput.text = PlayerPrefs.GetString(PlayerNicknameKey, string.Empty);
        passwordInput.text = string.Empty;

        BuildSettingsPanel(root);
        SetStatus(string.Empty, ErrorColor);
    }

    private InputField CreateBookInput(Transform root, string name, string placeholder, string label, float y, Color shadowColor)
    {
        Color labelColor = LoginStyle != null ? LoginStyle.LabelTextColor : new Color(0.82f, 0.24f, 0.18f, 1f);
        Text fieldLabel = CreateText(name + "Label", root, label + "：", 22, FontStyle.Bold, labelColor, TextAnchor.MiddleRight);
        SetRect(fieldLabel.rectTransform, CenterAnchor(), CenterAnchor(), new Vector2(1f, 0.5f), new Vector2(-310f, y), new Vector2(100f, 50f));

        Image shadow = CreateImage(name + "Shadow", root, shadowColor);
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(58f, y - 6f), new Vector2(570f, 58f));

        InputField input = CreateInputField(root, name, placeholder);
        SetRect(input.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(50f, y), new Vector2(570f, 58f));
        if (LoginStyle != null)
        {
            LoginStyle.ApplyInput(input.GetComponent<Image>());
        }
        input.textComponent.fontSize = ResolveFontSize(18);
        Text placeholderText = input.placeholder as Text;
        if (placeholderText != null)
        {
            placeholderText.fontSize = ResolveFontSize(16);
        }

        return input;
    }

    private void BuildSettingsPanel(RectTransform root)
    {
        Image panel = CreateImage("SettingsPanel", root, new Color(0.98f, 0.95f, 0.86f, 0.98f));
        settingsPanel = panel.gameObject;
        SetRect(panel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(460f, 320f));

        Text title = CreateText("Title", panel.transform, "设置", 30, FontStyle.Bold, FieldTextColor, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 46f));

        Text body = CreateText("Body", panel.transform, "音乐：开启\n音效：开启\n画质：标准\n\n后续可在这里接入更多设置项。", 20, FontStyle.Normal, FieldTextColor, TextAnchor.UpperLeft);
        SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(-72f, 150f));

        Button closeButton = CreateFixedButton(panel.transform, "CloseButton", "关闭", new Vector2(0f, -118f), new Vector2(130f, 46f), new Color(0.55f, 0.34f, 0.18f, 1f), Color.white, 18);
        closeButton.onClick.AddListener(HideSettingsPanel);

        settingsPanel.SetActive(false);
    }

    private void ShowSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
        }
    }

    private void HideSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private Button CreateFixedButton(Transform parent, string name, string text, Vector2 position, Vector2 size, Color color, Color textColor, int fontSize)
    {
        Color shadowColor = LoginStyle != null ? LoginStyle.ButtonShadowColor : new Color(0.18f, 0.10f, 0.05f, 0.24f);
        Image shadow = CreateImage(name + "Shadow", parent, shadowColor);
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), position + new Vector2(6f, -6f), size);

        Button button = CreateButton(parent, name, text, color, textColor);
        SetRect(button.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), position, size);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            int resolvedFontSize = ResolveFontSize(fontSize);
            label.fontSize = resolvedFontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(8, resolvedFontSize - 8);
            label.fontSizeMax = resolvedFontSize;
        }

        return button;
    }

    private void CreateDecorLine(Transform parent, Vector2 position, Vector2 size)
    {
        Color lineColor = LoginStyle != null ? LoginStyle.DecorLineColor : new Color(0.77f, 0.45f, 0.22f, 0.72f);
        Image line = CreateImage("DecorLine", parent, lineColor);
        SetRect(line.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), position, size);
    }

    private Vector2 CenterAnchor()
    {
        return new Vector2(0.5f, 0.5f);
    }

    private void TryLogin()
    {
        lastLoginWasWeChat = false;
        if (runtimePolicyBlocked || requestRunning || !ValidatePrivacyConsentForAuthentication() || !ValidateAccountAndPassword())
        {
            return;
        }

        LoginRequest request = new LoginRequest
        {
            username = accountInput.text.Trim(),
            password = passwordInput.text
        };

        StartCoroutine(SendAuthRequest("/user/login", JsonUtility.ToJson(request)));
    }

    private void TryRegister()
    {
        lastLoginWasWeChat = false;
        if (runtimePolicyBlocked || requestRunning || !ValidatePrivacyConsentForAuthentication() || !ValidateAccountAndPassword())
        {
            return;
        }

        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            ShowError("请输入昵称");
            nicknameInput.Select();
            return;
        }

        RegisterRequest request = new RegisterRequest
        {
            username = accountInput.text.Trim(),
            password = passwordInput.text,
            nickname = nickname
        };

        StartCoroutine(SendAuthRequest("/user/register", JsonUtility.ToJson(request)));
    }

    private void TryWeChatLogin()
    {
        if (runtimePolicyBlocked || requestRunning || !ValidatePrivacyConsentForAuthentication())
        {
            return;
        }

        lastLoginWasWeChat = true;
        if (string.IsNullOrEmpty(privacyConsentRequestId))
        {
            privacyConsentRequestId = NewRequestId();
        }

        waitingForManagementConsent = false;
        SetBusyState(true, "正在调用微信隐私授权...");
        if (WeChatPlatformBridge.IsAvailable)
        {
            WeChatPlatformBridge.RequestPrivacyAuthorization(this);
            return;
        }

        BeginWeChatLogin();
    }

    private void BeginWeChatLogin()
    {
        SetBusyState(true, "正在调用微信登录...");
        WeChatPlatformBridge.RequestLogin(this);
    }

    public void OnWeChatPrivacyAuthorized()
    {
        if (waitingForManagementConsent)
        {
            StartCoroutine(SendPrivacyConsent());
            return;
        }

        BeginWeChatLogin();
    }

    public void OnWeChatPrivacyAuthorizationFailed(string message)
    {
        waitingForManagementConsent = false;
        SetBusyState(false, string.IsNullOrEmpty(message) ? "隐私授权未完成，请重试" : message);
    }

    public void OnWeChatLoginCodeReceived(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            SetBusyState(false, string.Empty);
            ShowError("微信登录 code 为空");
            return;
        }

        WeChatLoginRequest request = new WeChatLoginRequest
        {
            code = code,
            nickname = nicknameInput.text.Trim(),
            privacyAuthorized = true,
            privacyPolicyVersion = currentPolicyVersion,
            privacyConsentRequestId = privacyConsentRequestId,
            clientVersion = Application.version,
            recoverCancellation = pendingCancellationRecovery,
            cancellationRecoveryRequestId = pendingCancellationRecovery
                ? cancellationRecoveryRequestId
                : null
        };

        StartCoroutine(SendAuthRequest("/user/wx-login", JsonUtility.ToJson(request)));
    }

    public void OnWeChatLoginFailed(string message)
    {
        SetBusyState(false, string.IsNullOrEmpty(message) ? "微信登录失败" : message);
    }

    private bool ValidateAccountAndPassword()
    {
        string account = accountInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(account))
        {
            ShowError("请输入账号");
            accountInput.Select();
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("请输入密码");
            passwordInput.Select();
            return false;
        }

        return true;
    }

    private bool ValidatePrivacyConsentForAuthentication()
    {
        if (!privacyPolicyLoaded || string.IsNullOrEmpty(currentPolicyVersion))
        {
            ShowError("隐私政策尚未加载完成，请稍后重试。");
            return false;
        }

        if (privacyConsentToggle == null || !privacyConsentToggle.isOn)
        {
            ShowError("请阅读并勾选同意当前隐私政策后再登录。");
            ShowPrivacyPolicyOverlay();
            return false;
        }

        return true;
    }

    private IEnumerator SendAuthRequest(string path, string json)
    {
        if (requestRunning)
        {
            yield break;
        }

        SetBusyState(true, "正在连接服务器...");

        if (!ApiRequestUtility.TryCreateJsonRequest(
                path,
                UnityWebRequest.kHttpVerbPOST,
                json,
                false,
                out UnityWebRequest request,
                out string configError))
        {
            SetBusyState(false, configError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            ApiResponse response = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
                {
                    response = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                }
            }
            catch (Exception)
            {
                response = null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (response != null && response.code == 7708)
                {
                    ClearClosedAccountLocalState();
                    SetBusyState(false, "账号已注销，请重新注册或登录其他账号。");
                    yield break;
                }

                if (response != null && response.code == 7703)
                {
                    privacyConsentRequestId = NewRequestId();
                    SetBusyState(false, "隐私政策已更新，请重新阅读并确认。");
                    yield return LoadPrivacyPolicies();
                    if (!runtimePolicyBlocked)
                    {
                        ShowPrivacyPolicyOverlay();
                    }
                    yield break;
                }

                if (response != null && response.code == 7702)
                {
                    SetBusyState(false, "请先阅读并同意当前隐私政策");
                    ShowPrivacyPolicyOverlay();
                    yield break;
                }

                string failure = ApiRequestUtility.GetErrorMessage(request, "登录服务连接失败");
                SetBusyState(false, failure);
                if (request.responseCode == 426 || request.responseCode == 503)
                {
                    yield return LoadClientRuntimePolicy();
                }
                yield break;
            }

            if (response == null)
            {
                SetBusyState(false, "登录服务返回数据格式错误");
                yield break;
            }

            if (response.code == 7702)
            {
                SetBusyState(false, "请先阅读并同意当前隐私政策");
                ShowPrivacyPolicyOverlay();
                yield break;
            }

            if (response.code == 7708)
            {
                ClearClosedAccountLocalState();
                SetBusyState(false, "账号已注销，请重新注册或登录其他账号。");
                yield break;
            }

            if (response.code == 7703)
            {
                privacyConsentRequestId = NewRequestId();
                SetBusyState(false, "隐私政策已更新，请重新阅读并确认。");
                yield return LoadPrivacyPolicies();
                if (!runtimePolicyBlocked)
                {
                    ShowPrivacyPolicyOverlay();
                }
                yield break;
            }

            if (response.code != 200)
            {
                SetBusyState(false, string.IsNullOrEmpty(response.message) ? "登录失败" : response.message);
                yield break;
            }

            if (response.data != null
                && !string.IsNullOrEmpty(response.data.token)
                && (response.data.accountManagementOnly
                    || response.data.privacyConsentRequired
                    || response.data.user == null
                    || (response.data.compliance != null
                        && (response.data.compliance.restrictedToken
                            || !response.data.compliance.gameAccessAllowed))))
            {
                OpenAccountManagementScreen(response.data, path == "/user/wx-login");
                yield break;
            }

            if (response.data != null
                && !response.data.accountManagementOnly
                && !response.data.privacyConsentRequired
                && response.data.compliance == null)
            {
                SetBusyState(false, "服务器未返回实名和防沉迷准入状态，暂不能进入游戏。");
                yield break;
            }

            if (response.data == null || string.IsNullOrEmpty(response.data.token) || response.data.user == null)
            {
                SetBusyState(false, "登录成功但缺少 token 或玩家信息");
                yield break;
            }

            SaveLoginState(response.data);
            PlayerComplianceGuard.StartSession();
            SceneManager.LoadScene(HomeSceneName);
        }
    }

    private IEnumerator LoadClientRuntimePolicy()
    {
        if (runtimePolicyBlocked)
        {
            yield break;
        }

        SetBusyState(true, "正在检查客户端版本和服务器状态...");
        string path = string.Format(
            CultureInfo.InvariantCulture,
            "/client/runtime-policy?platform=UNITY&channel={0}&versionCode={1}",
            UnityWebRequest.EscapeURL(ApiRuntimeConfig.GetClientChannel()),
            ApiRuntimeConfig.GetClientVersionCode());
        if (!ApiRequestUtility.TryCreateGet(path, false, out UnityWebRequest request, out string error))
        {
            SetBusyState(false, "启动策略读取失败，可继续登录：" + error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            ClientRuntimePolicyApiResponse response = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
                {
                    response = JsonUtility.FromJson<ClientRuntimePolicyApiResponse>(request.downloadHandler.text);
                }
            }
            catch (Exception)
            {
                response = null;
            }

            if (request.result != UnityWebRequest.Result.Success
                || response == null
                || response.code != 200
                || response.data == null)
            {
                runtimePolicyBlocked = true;
                SetBusyState(false, "启动策略读取失败，暂时无法登录：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                ShowRuntimePolicyOverlay("启动策略不可用", "客户端启动策略或隐私政策暂时不可用，请稍后重试。", string.Empty);
                yield break;
            }

            ClientRuntimePolicyData policy = response.data;
            if (policy.forceUpdate)
            {
                runtimePolicyBlocked = true;
                SetBusyState(false, string.Empty);
                ShowRuntimePolicyOverlay(
                    "需要更新客户端",
                    string.IsNullOrWhiteSpace(policy.message)
                        ? $"当前版本需要更新到 {policy.minimumVersionName ?? policy.latestVersionName} 后才能继续。"
                        : policy.message,
                    policy.updateUrl);
                yield break;
            }

            if (policy.maintenanceActive && policy.loginBlocked)
            {
                runtimePolicyBlocked = true;
                SetBusyState(false, string.Empty);
                ShowRuntimePolicyOverlay(
                    "服务器维护中",
                    string.IsNullOrWhiteSpace(policy.message) ? "服务器正在维护，请稍后再试。" : policy.message,
                    string.Empty);
                yield break;
            }

            yield return LoadPrivacyPolicies();
            if (runtimePolicyBlocked)
            {
                yield break;
            }

            string status = policy.updateAvailable
                ? $"发现新版本 {policy.latestVersionName}，当前版本仍可继续登录。"
                : string.Empty;
            SetBusyState(false, status);
        }
    }

    private void ShowRuntimePolicyOverlay(string title, string message, string updateUrl)
    {
        if (runtimePolicyOverlay != null)
        {
            Destroy(runtimePolicyOverlay);
        }

        RectTransform root = loginCanvasView != null ? loginCanvasView.PrepareContentRoot() : null;
        if (root == null)
        {
            ShowError(message);
            return;
        }

        Image overlay = CreateImage("ClientRuntimePolicyOverlay", root, new Color(0.04f, 0.07f, 0.12f, 0.96f));
        runtimePolicyOverlay = overlay.gameObject;
        Stretch(overlay.rectTransform);

        Image card = CreateImage("PolicyCard", overlay.transform, new Color(1f, 1f, 1f, 0.98f));
        SetRect(card.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(720f, 360f));

        Text titleText = CreateText("PolicyTitle", card.transform, title, 34, FontStyle.Bold, BrandColor, TextAnchor.MiddleCenter);
        SetRect(titleText.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, 92f), new Vector2(620f, 64f));

        Text messageText = CreateText("PolicyMessage", card.transform, message, 20, FontStyle.Normal, FieldTextColor, TextAnchor.MiddleCenter);
        messageText.enableWordWrapping = true;
        messageText.overflowMode = TextOverflowModes.Overflow;
        SetRect(messageText.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, 5f), new Vector2(620f, 120f));

        if (!string.IsNullOrWhiteSpace(updateUrl))
        {
            Button updateButton = CreateFixedButton(
                card.transform,
                "PolicyUpdateButton",
                "前往更新",
                new Vector2(0f, -105f),
                new Vector2(260f, 58f),
                PrimaryColor,
                Color.white,
                22);
            updateButton.onClick.AddListener(() => Application.OpenURL(updateUrl));
        }
    }

    private void SaveLoginState(LoginData data)
    {
        UserData user = data.user;
        PlayerPrefs.SetInt(RememberAccountKey, rememberToggle.isOn ? 1 : 0);

        if (rememberToggle.isOn)
        {
            PlayerPrefs.SetString(AccountKey, user.username ?? accountInput.text.Trim());
        }
        else
        {
            PlayerPrefs.DeleteKey(AccountKey);
        }

        GameSession.SaveAuthenticated(
            data.token,
            data.expiresIn,
            ClampLongToInt(user.id, 0),
            user.username,
            user.nickname,
            Mathf.Max(1, user.level),
            ClampLongToInt(user.gold, 0),
            ClampLongToInt(user.wood, 0),
            ClampLongToInt(user.stone, 0),
            ClampLongToInt(user.iron, 0),
            ClampLongToInt(user.diamond, 0),
            user.totalExperience,
            ClampLongToInt(user.levelCurveRevision, 0),
            ClampLongToInt(user.currentLevelExperience, 0),
            ClampLongToInt(user.requiredExperienceForNextLevel, 0),
            Mathf.Clamp(user.experienceProgressBps, 0, 10000),
            user.maxLevel,
            user.economyFrozen);
        PlayerPrefs.SetString("PlayerAvatarUrl", user.avatarUrl ?? string.Empty);
        PlayerPrefs.SetString("PlayerAvatarSource", user.avatarSource ?? "DEFAULT");
        PlayerPrefs.SetString("PlayerAvatarCode", user.avatarCode ?? string.Empty);
        PlayerPrefs.SetString("PlayerAvatarVersion", Math.Max(0L, user.avatarVersion).ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString("PlayerAvatarUpdatedTime", user.avatarUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt("PlayerAvatarOwnerId", ClampLongToInt(user.id, 0));
        PlayerPrefs.DeleteKey("PlayerAvatarResourcePath");
        PlayerPrefs.SetString("PlayerNicknameVersion", Math.Max(0L, user.nicknameVersion).ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString("PlayerNicknameUpdatedTime", user.nicknameUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt("PlayerNicknameChangeCount", Math.Max(0, user.nicknameChangeCount));
        PlayerPrefs.SetInt("PlayerNicknameChangeRequired", user.nicknameChangeRequired ? 1 : 0);
        PlayerPrefs.SetString("AccountStatus", string.IsNullOrEmpty(data.accountStatus) ? "ACTIVE" : data.accountStatus);
        PlayerPrefs.SetInt("AccountManagementOnly", data.accountManagementOnly ? 1 : 0);
        PlayerPrefs.SetInt("PrivacyConsentRequired", data.privacyConsentRequired ? 1 : 0);
        PlayerPrefs.SetString("PrivacyPolicyVersion", data.privacyPolicyVersion ?? string.Empty);
        PlayerPrefs.Save();
    }

    private void SetBusyState(bool busy, string message)
    {
        requestRunning = busy;

        if (accountInput != null) accountInput.interactable = !busy;
        if (nicknameInput != null) nicknameInput.interactable = !busy;
        if (passwordInput != null) passwordInput.interactable = !busy;
        if (rememberToggle != null) rememberToggle.interactable = !busy;
        if (loginButton != null) loginButton.interactable = !busy;
        if (registerButton != null) registerButton.interactable = !busy;
        if (weChatButton != null) weChatButton.interactable = !busy;
        if (privacyConsentToggle != null) privacyConsentToggle.interactable = !busy && !runtimePolicyBlocked;
        if (privacyPolicyButton != null) privacyPolicyButton.interactable = !busy;

        SetStatus(message, busy ? MutedTextColor : ErrorColor);
    }

    private static int ClampLongToInt(long value, int minimum)
    {
        long clamped = Math.Max((long)minimum, value);
        return clamped > int.MaxValue ? int.MaxValue : (int)clamped;
    }

    private void ShowError(string message)
    {
        SetStatus(message, ErrorColor);
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.color = color;
            statusText.text = message ?? string.Empty;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string name, Transform parent, string text, int size, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        Text label = obj.GetComponent<Text>();
        LoginCanvasStyle style = LoginStyle;
        int resolvedFontSize = ResolveFontSize(size);
        label.font = style != null ? style.ResolveFont(defaultFont) : defaultFont;
        label.text = text;
        label.fontSize = resolvedFontSize;
        label.fontStyle = style != null ? style.ResolveFontStyle(name, fontStyle) : ConvertFontStyle(fontStyle);
        label.color = style != null ? style.ResolveTextColor(name, color) : color;
        label.alignment = ResolveComplianceAlignment(alignment);
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Clamp(resolvedFontSize - 4, 8, resolvedFontSize);
        label.fontSizeMax = resolvedFontSize;
        label.raycastTarget = false;
        return label;
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private InputField CreateInputField(Transform parent, string name, string placeholder)
    {
        Color inputColor = LoginStyle != null ? LoginStyle.InputColor : FieldColor;
        Color inputHoverColor = LoginStyle != null ? LoginStyle.InputHoverColor : FieldHoverColor;
        Image frame = CreateImage(name, parent, inputColor);
        frame.raycastTarget = true;

        InputField input = frame.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame;
        input.transition = Selectable.Transition.ColorTint;
        input.colors = CreateSelectableColors(inputColor, inputHoverColor, Color.white, new Color(0.82f, 0.86f, 0.92f, 1f));
        input.lineType = InputField.LineType.SingleLine;

        Text text = CreateText("Text", frame.transform, string.Empty, 14, FontStyle.Normal, FieldTextColor, TextAnchor.MiddleLeft);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);
        input.textComponent = text;

        Text placeholderText = CreateText("Placeholder", frame.transform, placeholder, 12, FontStyle.Normal, new Color(0.56f, 0.61f, 0.68f, 1f), TextAnchor.MiddleLeft);
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(16f, 0f);
        placeholderRect.offsetMax = new Vector2(-16f, 0f);
        input.placeholder = placeholderText;

        return input;
    }

    private Toggle CreateRememberToggle(Transform parent)
    {
        GameObject obj = new GameObject("RememberToggle", typeof(RectTransform), typeof(Toggle));
        obj.transform.SetParent(parent, false);
        Toggle toggle = obj.GetComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;

        Image box = CreateImage("Box", obj.transform, Color.white);
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = new Vector2(0f, 0.5f);
        boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(18f, 18f);

        Image checkmark = CreateImage("Checkmark", box.transform, PrimaryColor);
        RectTransform checkRect = checkmark.rectTransform;
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.anchoredPosition = Vector2.zero;
        checkRect.sizeDelta = new Vector2(10f, 10f);

        toggle.targetGraphic = box;
        toggle.graphic = checkmark;

        Text label = CreateText("Label", obj.transform, "记住账号", 13, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(26f, 0f);
        labelRect.offsetMax = Vector2.zero;

        return toggle;
    }

    private Button CreateButton(Transform parent, string name, string text, Color color, Color textColor)
    {
        LoginCanvasStyle style = LoginStyle;
        Color buttonColor = style != null ? style.ResolveButtonColor(name, color) : color;
        Image image = CreateImage(name, parent, buttonColor);
        if (style != null)
        {
            style.ApplyButton(image, name, buttonColor);
        }
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateSelectableColors(buttonColor, Color.Lerp(buttonColor, Color.white, 0.12f), Color.Lerp(buttonColor, Color.black, 0.18f), new Color(0.70f, 0.75f, 0.82f, 1f));

        Text label = CreateText("Text", image.transform, text, 13, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        return button;
    }

    private ColorBlock CreateSelectableColors(Color normal, Color highlighted, Color pressed, Color disabled)
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

    private int ResolveFontSize(int defaultSize)
    {
        return LoginStyle != null ? LoginStyle.ResolveFontSize(defaultSize) : defaultSize;
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject obj = new GameObject("Spacer", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        SetLayout(obj, 0f, height);
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void SetLayout(GameObject obj, float preferredWidth, float preferredHeight)
    {
        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
    }

#pragma warning disable 0649
    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class RegisterRequest
    {
        public string username;
        public string password;
        public string nickname;
    }

    [Serializable]
    private class WeChatLoginRequest
    {
        public string code;
        public string nickname;
        public bool privacyAuthorized;
        public string privacyPolicyVersion;
        public string privacyConsentRequestId;
        public string clientVersion;
        public bool recoverCancellation;
        public string cancellationRecoveryRequestId;
    }

    [Serializable]
    private class ApiResponse
    {
        public int code;
        public string message;
        public LoginData data;
    }

    [Serializable]
    private class LoginData
    {
        public string token;
        public int expiresIn;
        public UserData user;
        public string accountStatus;
        public bool accountManagementOnly;
        public bool privacyConsentRequired;
        public string privacyPolicyVersion;
        public ComplianceData compliance;
    }

    [Serializable]
    private class UserData
    {
        public long id;
        public string username;
        public string nickname;
        public long nicknameVersion;
        public string nicknameUpdatedTime;
        public int nicknameChangeCount;
        public bool nicknameChangeRequired;
        public int level;
        public long totalExperience;
        public long levelCurveRevision;
        public long currentLevelExperience;
        public long requiredExperienceForNextLevel;
        public int experienceProgressBps;
        public bool maxLevel;
        public bool economyFrozen;
        public long gold;
        public long wood;
        public long stone;
        public long iron;
        public long diamond;
        public string avatarUrl;
        public string avatarSource;
        public string avatarCode;
        public long avatarVersion;
        public string avatarUpdatedTime;
        public ItemData[] items;
    }

    [Serializable]
    private class ClientRuntimePolicyApiResponse
    {
        public int code;
        public string message;
        public ClientRuntimePolicyData data;
    }

    [Serializable]
    private class ClientRuntimePolicyData
    {
        public bool configured;
        public string platform;
        public string channelCode;
        public long currentVersionCode;
        public long minimumVersionCode;
        public string minimumVersionName;
        public long latestVersionCode;
        public string latestVersionName;
        public bool updateAvailable;
        public bool forceUpdate;
        public string updateUrl;
        public bool maintenanceActive;
        public bool loginBlocked;
        public string maintenanceStartTime;
        public string maintenanceEndTime;
        public string message;
        public string serverTime;
    }

    [Serializable]
    private class ItemData
    {
        public long itemId;
        public string itemKey;
        public string name;
        public string itemType;
        public string icon;
        public long quantity;
    }

    [Serializable]
    private class ComplianceData
    {
        public bool enforcementEnabled;
        public string verificationStatus;
        public string ageGroup;
        public bool gameAccessAllowed;
        public bool restrictedToken;
        public string blockReason;
        public string message;
        public int usedSeconds;
        public int remainingSeconds;
        public string nextAllowedTime;
        public string profileValidUntil;
        public string serverTime;
    }
#pragma warning restore 0649
}
