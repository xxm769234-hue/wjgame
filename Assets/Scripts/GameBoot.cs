using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameBoot : MonoBehaviour
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
    private static readonly Color GuestButtonColor = new Color(0.94f, 0.95f, 0.97f, 1f);
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
    private Button guestButton;
    private GameObject settingsPanel;
    private bool requestRunning;

    private Font defaultFont;

    private void Start()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildLoginUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TryLogin();
        }
    }

    private void BuildLoginUI()
    {
        EnsureEventSystem();

        GameObject canvasObj = new GameObject("LoginCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f;

        RectTransform root = canvasObj.GetComponent<RectTransform>();
        BuildFixedLoginLayout(root);
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

        Text brandHint = CreateText("BrandHint", brandPanel.transform, "账号同步 · 微信登录 · 游客进入", 15, FontStyle.Normal, LightTextColor, TextAnchor.MiddleLeft);
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

        guestButton = CreateButton(formPanel.transform, "GuestButton", "游客进入", GuestButtonColor, new Color(0.12f, 0.17f, 0.24f, 1f));
        guestButton.onClick.AddListener(LoginAsGuest);
        SetLayout(guestButton.gameObject, 0f, 20f);

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
        Color paperColor = new Color(0.98f, 0.95f, 0.86f, 1f);
        Color inkColor = new Color(0.28f, 0.16f, 0.09f, 1f);
        Color shadowColor = new Color(0.18f, 0.10f, 0.05f, 0.28f);

        Image background = CreateImage("FixedLoginBackground", root, new Color(0.48f, 0.30f, 0.16f, 1f));
        Stretch(background.rectTransform);

        Image centerShadow = CreateImage("CenterPanelShadow", root, shadowColor);
        centerShadow.raycastTarget = false;
        SetRect(centerShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(10f, -10f), new Vector2(850f, 960f));

        Image centerPanel = CreateImage("CenterPanel", root, new Color(0.42f, 0.25f, 0.12f, 0.42f));
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
        SetRect(logoPanel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, 392f), new Vector2(650f, 126f));

        Text logoText = CreateText("LogoText", logoPanel.transform, "微郡", 42, FontStyle.Bold, new Color(0.74f, 0.20f, 0.15f, 1f), TextAnchor.MiddleCenter);
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

        guestButton = CreateFixedButton(root, "GuestButton", "游客进入", new Vector2(158f, -204f), new Vector2(290f, 54f), GuestButtonColor, inkColor, 21);
        guestButton.onClick.AddListener(LoginAsGuest);

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
        Text fieldLabel = CreateText(name + "Label", root, label + "：", 22, FontStyle.Bold, new Color(0.82f, 0.24f, 0.18f, 1f), TextAnchor.MiddleRight);
        SetRect(fieldLabel.rectTransform, CenterAnchor(), CenterAnchor(), new Vector2(1f, 0.5f), new Vector2(-310f, y), new Vector2(100f, 50f));

        Image shadow = CreateImage(name + "Shadow", root, shadowColor);
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(58f, y - 6f), new Vector2(570f, 58f));

        InputField input = CreateInputField(root, name, placeholder);
        SetRect(input.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(50f, y), new Vector2(570f, 58f));
        input.GetComponent<Image>().color = new Color(0.99f, 0.97f, 0.90f, 1f);
        input.textComponent.fontSize = 18;
        Text placeholderText = input.placeholder as Text;
        if (placeholderText != null)
        {
            placeholderText.fontSize = 16;
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
        Image shadow = CreateImage(name + "Shadow", parent, new Color(0.18f, 0.10f, 0.05f, 0.24f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), position + new Vector2(6f, -6f), size);

        Button button = CreateButton(parent, name, text, color, textColor);
        SetRect(button.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), position, size);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.fontSize = fontSize;
            label.resizeTextMinSize = Mathf.Max(8, fontSize - 8);
            label.resizeTextMaxSize = fontSize;
        }

        return button;
    }

    private void CreateDecorLine(Transform parent, Vector2 position, Vector2 size)
    {
        Image line = CreateImage("DecorLine", parent, new Color(0.77f, 0.45f, 0.22f, 0.72f));
        SetRect(line.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), position, size);
    }

    private Vector2 CenterAnchor()
    {
        return new Vector2(0.5f, 0.5f);
    }

    private void TryLogin()
    {
        if (requestRunning || !ValidateAccountAndPassword())
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
        if (requestRunning || !ValidateAccountAndPassword())
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
        if (requestRunning)
        {
            return;
        }

        SetBusyState(true, "正在调用微信登录...");
        WeChatPlatformBridge.RequestLogin(this);
    }

    public void OnWeChatLoginCodeReceived(string code)
    {
        SetBusyState(false, string.Empty);

        if (string.IsNullOrEmpty(code))
        {
            ShowError("微信登录 code 为空");
            return;
        }

        WeChatLoginRequest request = new WeChatLoginRequest
        {
            code = code,
            nickname = nicknameInput.text.Trim()
        };

        StartCoroutine(SendAuthRequest("/user/wx-login", JsonUtility.ToJson(request)));
    }

    public void OnWeChatLoginFailed(string message)
    {
        SetBusyState(false, string.IsNullOrEmpty(message) ? "微信登录失败" : message);
    }

    private void LoginAsGuest()
    {
        if (requestRunning)
        {
            return;
        }

        SaveGuestLoginState();
        SceneManager.LoadScene(HomeSceneName);
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
                SetBusyState(false, ApiRequestUtility.GetErrorMessage(request, "登录服务连接失败"));
                yield break;
            }

            if (response == null)
            {
                SetBusyState(false, "登录服务返回数据格式错误");
                yield break;
            }

            if (response.code != 200)
            {
                SetBusyState(false, string.IsNullOrEmpty(response.message) ? "登录失败" : response.message);
                yield break;
            }

            if (response.data == null || string.IsNullOrEmpty(response.data.token) || response.data.user == null)
            {
                SetBusyState(false, "登录成功但缺少 token 或玩家信息");
                yield break;
            }

            SaveLoginState(response.data);
            SceneManager.LoadScene(HomeSceneName);
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
            user.id,
            user.username,
            user.nickname,
            user.level,
            user.gold,
            user.wood,
            user.stone,
            user.iron);
    }

    private void SaveGuestLoginState()
    {
        PlayerPrefs.SetInt(RememberAccountKey, rememberToggle.isOn ? 1 : 0);
        GameSession.SaveGuest(PlayerPrefs.GetString("GuestNickname", "游客"));
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
        if (guestButton != null) guestButton.interactable = !busy;

        SetStatus(message, busy ? MutedTextColor : ErrorColor);
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

    private Text CreateText(string name, Transform parent, string text, int size, FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.transform.SetParent(parent, false);

        Text label = obj.GetComponent<Text>();
        label.font = defaultFont;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Clamp(size - 4, 8, size);
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;
        return label;
    }

    private InputField CreateInputField(Transform parent, string name, string placeholder)
    {
        Image frame = CreateImage(name, parent, FieldColor);
        frame.raycastTarget = true;

        InputField input = frame.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame;
        input.transition = Selectable.Transition.ColorTint;
        input.colors = CreateSelectableColors(FieldColor, FieldHoverColor, Color.white, new Color(0.82f, 0.86f, 0.92f, 1f));
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
        Image image = CreateImage(name, parent, color);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateSelectableColors(color, Color.Lerp(color, Color.white, 0.12f), Color.Lerp(color, Color.black, 0.18f), new Color(0.70f, 0.75f, 0.82f, 1f));

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
    }

    [Serializable]
    private class UserData
    {
        public int id;
        public string username;
        public string nickname;
        public int level;
        public int gold;
        public int wood;
        public int stone;
        public int iron;
        public ItemData[] items;
    }

    [Serializable]
    private class ItemData
    {
        public int itemId;
        public string itemKey;
        public string name;
        public string itemType;
        public string icon;
        public int quantity;
    }
#pragma warning restore 0649
}
