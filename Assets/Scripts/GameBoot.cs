using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameBoot : MonoBehaviour
{
    private const string BaseApiUrl = "http://localhost:8080/api";
    private const string HomeSceneName = "HomeScene";
    private const string RememberAccountKey = "LoginRememberAccount";
    private const string AccountKey = "LoginAccount";
    private const string LoginStateKey = "LoginState";
    private const string AuthTokenKey = "AuthToken";
    private const string TokenExpiresInKey = "TokenExpiresIn";
    private const string PlayerIdKey = "PlayerId";
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
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObj.GetComponent<RectTransform>();

        if (Application.isPlaying || !Application.isPlaying)
        {
            BuildFixedLoginLayout(root);
            return;
        }

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
        Color redTextColor = new Color(0.90f, 0.16f, 0.18f, 1f);
        Color shadowColor = new Color(0.18f, 0.10f, 0.05f, 0.28f);

        Image background = CreateImage("FixedLoginBackground", root, new Color(0.48f, 0.30f, 0.16f, 1f));
        Stretch(background.rectTransform);

        Image centerShadow = CreateImage("CenterPanelShadow", root, shadowColor);
        centerShadow.raycastTarget = false;
        SetRect(centerShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(8f, -65f), new Vector2(780f, 710f));

        Image centerPanel = CreateImage("CenterPanel", root, new Color(0.42f, 0.25f, 0.12f, 0.42f));
        centerPanel.raycastTarget = false;
        SetRect(centerPanel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, -55f), new Vector2(780f, 710f));

        CreateDecorLine(root, new Vector2(-600f, 350f), new Vector2(520f, 8f));
        CreateDecorLine(root, new Vector2(660f, 350f), new Vector2(420f, 8f));
        CreateDecorLine(root, new Vector2(-650f, -245f), new Vector2(420f, 8f));
        CreateDecorLine(root, new Vector2(650f, -245f), new Vector2(620f, 8f));

        Image logoShadow = CreateImage("LogoShadow", root, shadowColor);
        logoShadow.raycastTarget = false;
        SetRect(logoShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(8f, 202f), new Vector2(660f, 190f));

        Image logoPanel = CreateImage("LogoPanel", root, paperColor);
        SetRect(logoPanel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, 210f), new Vector2(650f, 180f));

        Text logoText = CreateText("LogoText", logoPanel.transform, "游戏logo", 34, FontStyle.Normal, new Color(0.95f, 0.18f, 0.25f, 1f), TextAnchor.MiddleCenter);
        Stretch(logoText.rectTransform);

        Text nicknameLabel = CreateText("NicknameLabel", root, "昵称:", 28, FontStyle.Normal, new Color(0.95f, 0.18f, 0.25f, 1f), TextAnchor.MiddleRight);
        SetRect(nicknameLabel.rectTransform, CenterAnchor(), CenterAnchor(), new Vector2(1f, 0.5f), new Vector2(-350f, -30f), new Vector2(120f, 48f));

        Image nicknameShadow = CreateImage("NicknameInputShadow", root, shadowColor);
        nicknameShadow.raycastTarget = false;
        SetRect(nicknameShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(8f, -38f), new Vector2(660f, 78f));

        accountInput = CreateInputField(root, "NicknameInput", string.Empty);
        SetRect(accountInput.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, -30f), new Vector2(650f, 70f));
        accountInput.GetComponent<Image>().color = new Color(0.99f, 0.97f, 0.90f, 1f);
        accountInput.characterLimit = 50;
        nicknameInput = accountInput;

        Text passwordLabel = CreateText("PasswordLabel", root, "密码:", 28, FontStyle.Normal, new Color(0.95f, 0.18f, 0.25f, 1f), TextAnchor.MiddleRight);
        SetRect(passwordLabel.rectTransform, CenterAnchor(), CenterAnchor(), new Vector2(1f, 0.5f), new Vector2(-350f, -175f), new Vector2(120f, 48f));

        Image passwordShadow = CreateImage("PasswordInputShadow", root, shadowColor);
        passwordShadow.raycastTarget = false;
        SetRect(passwordShadow.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(8f, -183f), new Vector2(660f, 78f));

        passwordInput = CreateInputField(root, "PasswordInput", string.Empty);
        passwordInput.contentType = InputField.ContentType.Password;
        passwordInput.characterLimit = 255;
        SetRect(passwordInput.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, -175f), new Vector2(650f, 70f));
        passwordInput.GetComponent<Image>().color = new Color(0.99f, 0.97f, 0.90f, 1f);

        loginButton = CreateFixedButton(root, "LoginButton", "登录", new Vector2(-205f, -335f), new Vector2(205f, 70f), paperColor, redTextColor, 26);
        loginButton.onClick.AddListener(TryLogin);

        registerButton = CreateFixedButton(root, "RegisterButton", "注册", new Vector2(35f, -335f), new Vector2(205f, 70f), paperColor, redTextColor, 26);
        registerButton.onClick.AddListener(TryRegister);

        weChatButton = CreateFixedButton(root, "OtherLoginButton", "其他登录", new Vector2(275f, -335f), new Vector2(205f, 70f), paperColor, redTextColor, 24);
        weChatButton.onClick.AddListener(TryWeChatLogin);

        Button settingsButton = CreateFixedButton(root, "SettingsButton", "设置", new Vector2(830f, 315f), new Vector2(110f, 90f), paperColor, redTextColor, 20);
        settingsButton.onClick.AddListener(ShowSettingsPanel);

        Button mailButton = CreateFixedButton(root, "MailButton", "邮件", new Vector2(830f, 205f), new Vector2(110f, 90f), paperColor, redTextColor, 20);
        mailButton.onClick.AddListener(() => ShowError("邮件功能待接入"));

        statusText = CreateText("StatusText", root, string.Empty, 18, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter);
        SetRect(statusText.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, -425f), new Vector2(650f, 34f));

        rememberToggle = CreateRememberToggle(root);
        rememberToggle.isOn = PlayerPrefs.GetInt(RememberAccountKey, 1) == 1;
        SetRect(rememberToggle.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(-5000f, -5000f), Vector2.zero);
        rememberToggle.gameObject.SetActive(false);

        guestButton = CreateButton(root, "HiddenGuestButton", string.Empty, Color.clear, Color.clear);
        guestButton.onClick.AddListener(LoginAsGuest);
        SetRect(guestButton.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(-5000f, -5000f), Vector2.zero);
        guestButton.gameObject.SetActive(false);

        bool rememberAccount = PlayerPrefs.GetInt(RememberAccountKey, 1) == 1;
        accountInput.text = rememberAccount ? PlayerPrefs.GetString(AccountKey, string.Empty) : string.Empty;
        passwordInput.text = string.Empty;

        BuildSettingsPanel(root);
        SetStatus(string.Empty, ErrorColor);
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

#if UNITY_WEBGL && !UNITY_EDITOR
        ShowError("请在微信小游戏环境中调用 wx.login，并回调 OnWeChatLoginCodeReceived");
#else
        ShowError("微信登录需要在微信小游戏环境中使用");
#endif
    }

    public void OnWeChatLoginCodeReceived(string code)
    {
        if (requestRunning)
        {
            return;
        }

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
        ShowError(string.IsNullOrEmpty(message) ? "微信登录失败" : message);
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

        using (UnityWebRequest request = new UnityWebRequest(BaseApiUrl + path, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetBusyState(false, $"登录服务连接失败：{request.error}");
                yield break;
            }

            ApiResponse response;
            try
            {
                response = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                SetBusyState(false, "登录服务返回数据格式错误");
                yield break;
            }

            if (response == null)
            {
                SetBusyState(false, "登录服务无响应");
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
        PlayerPrefs.SetInt(LoginStateKey, 1);
        PlayerPrefs.SetInt(RememberAccountKey, rememberToggle.isOn ? 1 : 0);
        PlayerPrefs.SetString(AuthTokenKey, data.token);
        PlayerPrefs.SetInt(TokenExpiresInKey, data.expiresIn);
        PlayerPrefs.SetInt(PlayerIdKey, user.id);
        PlayerPrefs.SetString(PlayerNicknameKey, user.nickname ?? string.Empty);
        PlayerPrefs.SetInt("PlayerLv", user.level);
        PlayerPrefs.SetString("LoginUsername", user.username ?? string.Empty);
        PlayerPrefs.SetInt("Gold", user.gold);
        PlayerPrefs.SetInt("Wood", user.wood);
        PlayerPrefs.SetInt("Stone", user.stone);
        PlayerPrefs.SetInt("Iron", user.iron);

        if (rememberToggle.isOn)
        {
            PlayerPrefs.SetString(AccountKey, user.username ?? accountInput.text.Trim());
        }
        else
        {
            PlayerPrefs.DeleteKey(AccountKey);
        }

        PlayerPrefs.Save();
    }

    private void SaveGuestLoginState()
    {
        PlayerPrefs.SetInt(LoginStateKey, 1);
        PlayerPrefs.SetInt(RememberAccountKey, rememberToggle.isOn ? 1 : 0);
        PlayerPrefs.DeleteKey(AuthTokenKey);
        PlayerPrefs.SetInt(TokenExpiresInKey, 0);
        PlayerPrefs.SetInt(PlayerIdKey, 0);
        PlayerPrefs.SetString(PlayerNicknameKey, "游客");
        PlayerPrefs.SetInt("PlayerLv", 1);
        PlayerPrefs.SetString("LoginUsername", "Guest");
        PlayerPrefs.SetInt("Gold", 1000);
        PlayerPrefs.SetInt("Wood", 500);
        PlayerPrefs.SetInt("Stone", 500);
        PlayerPrefs.SetInt("Iron", 300);
        PlayerPrefs.Save();
    }

    private void SetBusyState(bool busy, string message)
    {
        requestRunning = busy;

        accountInput.interactable = !busy;
        nicknameInput.interactable = !busy;
        passwordInput.interactable = !busy;
        rememberToggle.interactable = !busy;
        loginButton.interactable = !busy;
        registerButton.interactable = !busy;
        weChatButton.interactable = !busy;
        guestButton.interactable = !busy;

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
