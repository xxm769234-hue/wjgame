using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;

[DisallowMultipleComponent]
public sealed class LoginContentView : MonoBehaviour
{
    [SerializeField] private LoginCanvasStyle style;

    [Header("Login Controls")]
    [SerializeField] private InputField accountInput;
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Toggle rememberToggle;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button weChatLoginButton;
    [SerializeField] private Text statusText;

    [Header("Secondary Controls")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mailButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsCloseButton;

    public LoginCanvasStyle Style => style;
    public InputField AccountInput => accountInput;
    public InputField NicknameInput => nicknameInput;
    public InputField PasswordInput => passwordInput;
    public Toggle RememberToggle => rememberToggle;
    public Button LoginButton => loginButton;
    public Button RegisterButton => registerButton;
    public Button WeChatLoginButton => weChatLoginButton;
    public Text StatusText => statusText;
    public Button SettingsButton => settingsButton;
    public Button MailButton => mailButton;
    public GameObject SettingsPanel => settingsPanel;
    public Button SettingsCloseButton => settingsCloseButton;

    public bool HasCompleteLoginLayout =>
        accountInput != null &&
        nicknameInput != null &&
        passwordInput != null &&
        rememberToggle != null &&
        loginButton != null &&
        registerButton != null &&
        weChatLoginButton != null &&
        statusText != null &&
        settingsButton != null &&
        mailButton != null &&
        settingsPanel != null &&
        settingsCloseButton != null;

    private void Reset()
    {
        CaptureStyleReference();
    }

    private void OnValidate()
    {
        CaptureStyleReference();
    }

    private void CaptureStyleReference()
    {
        if (style == null)
        {
            style = GetComponent<LoginCanvasStyle>();
        }
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        LoginCanvasStyle newStyle,
        InputField newAccountInput,
        InputField newNicknameInput,
        InputField newPasswordInput,
        Toggle newRememberToggle,
        Button newLoginButton,
        Button newRegisterButton,
        Button newWeChatLoginButton,
        Text newStatusText,
        Button newSettingsButton,
        Button newMailButton,
        GameObject newSettingsPanel,
        Button newSettingsCloseButton)
    {
        style = newStyle;
        accountInput = newAccountInput;
        nicknameInput = newNicknameInput;
        passwordInput = newPasswordInput;
        rememberToggle = newRememberToggle;
        loginButton = newLoginButton;
        registerButton = newRegisterButton;
        weChatLoginButton = newWeChatLoginButton;
        statusText = newStatusText;
        settingsButton = newSettingsButton;
        mailButton = newMailButton;
        settingsPanel = newSettingsPanel;
        settingsCloseButton = newSettingsCloseButton;
    }

    [ContextMenu("Capture Login Content References")]
    private void EditorCaptureReferences()
    {
        CaptureStyleReference();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
