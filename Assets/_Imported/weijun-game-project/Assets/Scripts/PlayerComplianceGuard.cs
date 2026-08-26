using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PlayerComplianceGuard : MonoBehaviour
{
    private const string LaunchSceneName = "Launch";
    private const float ResumeCheckCooldownSeconds = 1f;
    private static PlayerComplianceGuard instance;

    private bool statusRequestRunning;
    private bool forcedOffline;
    private float lastStatusRequestTime = -ResumeCheckCooldownSeconds;
    private GameObject noticeCanvasObject;
    private Image noticeBlocker;
    private TMP_Text noticeTitle;
    private TMP_Text noticeMessage;
    private Coroutine noticeHideCoroutine;

    public static void StartSession()
    {
        if (instance == null)
        {
            GameObject guardObject = new GameObject("PlayerComplianceGuard");
            instance = guardObject.AddComponent<PlayerComplianceGuard>();
        }

        instance.RequestStatusRefresh(true);
    }

    public static void ShowRealtimeWarning(int warningMinutes, int remainingSeconds, string message)
    {
        if (instance == null || instance.forcedOffline)
        {
            return;
        }

        string title = warningMinutes > 0 ? "防沉迷提醒" : "防沉迷状态提醒";
        string detail = string.IsNullOrEmpty(message)
            ? "剩余游戏时长：" + Mathf.Max(0, remainingSeconds) + " 秒"
            : message + "\n剩余游戏时长：" + Mathf.Max(0, remainingSeconds) + " 秒";
        instance.ShowNotice(title, detail, false, 6f);
    }

    public static void ForceOffline(string message)
    {
        if (instance == null)
        {
            GameObject guardObject = new GameObject("PlayerComplianceGuard");
            instance = guardObject.AddComponent<PlayerComplianceGuard>();
        }

        instance.BeginForcedOffline(message);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RequestStatusRefresh(true);
    }

    private void Update()
    {
        if (!forcedOffline && !GameSession.IsAuthenticated)
        {
            Destroy(gameObject);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            RequestStatusRefresh(false);
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused)
        {
            RequestStatusRefresh(false);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void RequestStatusRefresh(bool force)
    {
        if (forcedOffline || statusRequestRunning || !GameSession.IsAuthenticated)
        {
            return;
        }

        if (!force && Time.realtimeSinceStartup - lastStatusRequestTime < ResumeCheckCooldownSeconds)
        {
            return;
        }

        StartCoroutine(RefreshStatus());
    }

    private IEnumerator RefreshStatus()
    {
        statusRequestRunning = true;
        lastStatusRequestTime = Time.realtimeSinceStartup;
        string token = GameSession.Token;
        if (!ApiRequestUtility.TryCreateGetWithToken(
                "/anti-addiction/status",
                token,
                out UnityWebRequest request,
                out string error))
        {
            statusRequestRunning = false;
            Debug.LogWarning("无法创建防沉迷状态请求：" + error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            ComplianceStatusResponse response = ParseResponse(request);
            statusRequestRunning = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 401 || request.responseCode == 403)
                {
                    BeginForcedOffline(response == null ? "当前账号已无法继续游戏，请重新登录。" : response.message);
                }
                else
                {
                    Debug.LogWarning("防沉迷状态读取失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                }
                yield break;
            }

            if (response == null || response.code != 200 || response.data == null)
            {
                Debug.LogWarning("防沉迷状态响应无效。");
                yield break;
            }

            if (!response.data.gameAccessAllowed)
            {
                BeginForcedOffline(BuildBlockedMessage(response.data));
            }
        }
    }

    private void BeginForcedOffline(string message)
    {
        if (forcedOffline)
        {
            return;
        }

        forcedOffline = true;
        string detail = string.IsNullOrEmpty(message) ? "当前防沉迷规则不允许继续游戏。" : message;
        ShowNotice("无法继续游戏", detail, true, 0f);
        PlayerPrefs.SetString("ComplianceBlockedMessage", detail);
        PlayerPrefs.Save();
        RealtimePlatformBridge.Close();
        GameSession.Clear();
        StartCoroutine(ReturnToLaunchScene());
    }

    private IEnumerator ReturnToLaunchScene()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        SceneManager.LoadScene(LaunchSceneName);
        Destroy(gameObject);
    }

    private void ShowNotice(string title, string message, bool blocking, float hideAfterSeconds)
    {
        EnsureNoticeUi();
        if (noticeCanvasObject == null)
        {
            return;
        }

        noticeCanvasObject.SetActive(true);
        noticeBlocker.raycastTarget = blocking;
        noticeBlocker.color = blocking ? new Color(0f, 0f, 0f, 0.68f) : new Color(0f, 0f, 0f, 0f);
        noticeTitle.text = title;
        noticeMessage.text = message;
        if (noticeHideCoroutine != null)
        {
            StopCoroutine(noticeHideCoroutine);
            noticeHideCoroutine = null;
        }

        if (!blocking && hideAfterSeconds > 0f)
        {
            noticeHideCoroutine = StartCoroutine(HideNoticeAfter(hideAfterSeconds));
        }
    }

    private IEnumerator HideNoticeAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (!forcedOffline && noticeCanvasObject != null)
        {
            noticeCanvasObject.SetActive(false);
        }

        noticeHideCoroutine = null;
    }

    private void EnsureNoticeUi()
    {
        if (noticeCanvasObject != null)
        {
            return;
        }

        noticeCanvasObject = new GameObject(
            "ComplianceNoticeCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        noticeCanvasObject.transform.SetParent(transform, false);
        Canvas canvas = noticeCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = noticeCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        noticeBlocker = CreateImage("Blocker", noticeCanvasObject.transform, new Color(0f, 0f, 0f, 0f));
        Stretch(noticeBlocker.rectTransform);
        noticeBlocker.raycastTarget = false;

        Image card = CreateImage("Card", noticeCanvasObject.transform, new Color(0.12f, 0.16f, 0.22f, 0.97f));
        SetRect(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(720f, 128f));
        noticeTitle = CreateText("Title", card.transform, string.Empty, 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(noticeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-44f, 38f));
        noticeMessage = CreateText("Message", card.transform, string.Empty, 18, FontStyle.Normal, new Color(0.93f, 0.95f, 0.98f, 1f), TextAnchor.MiddleCenter);
        noticeMessage.enableWordWrapping = true;
        noticeMessage.overflowMode = TextOverflowModes.Overflow;
        SetRect(noticeMessage.rectTransform, Vector2.zero, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(-44f, 56f));
        noticeCanvasObject.SetActive(false);
    }

    private static string BuildBlockedMessage(ComplianceStatusData status)
    {
        if (status == null)
        {
            return "当前防沉迷规则不允许继续游戏。";
        }

        if (!string.IsNullOrEmpty(status.message))
        {
            return string.IsNullOrEmpty(status.nextAllowedTime)
                ? status.message
                : status.message + "\n下次可进入时间：" + status.nextAllowedTime;
        }

        return string.IsNullOrEmpty(status.nextAllowedTime)
            ? "当前防沉迷规则不允许继续游戏。"
            : "当前防沉迷规则不允许继续游戏。\n下次可进入时间：" + status.nextAllowedTime;
    }

    private static ComplianceStatusResponse ParseResponse(UnityWebRequest request)
    {
        if (request == null || request.downloadHandler == null || string.IsNullOrWhiteSpace(request.downloadHandler.text))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<ComplianceStatusResponse>(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("防沉迷状态响应解析失败：" + exception.Message);
            return null;
        }
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        TMP_Text text = gameObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle == FontStyle.Bold
            ? FontStyles.Bold
            : fontStyle == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
        text.color = color;
        text.alignment = ResolveTextAlignment(alignment);
        text.raycastTarget = false;
        return text;
    }

    private static TextAlignmentOptions ResolveTextAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    [Serializable]
    private class ComplianceStatusResponse
    {
        public int code;
        public string message;
        public ComplianceStatusData data;
    }

    [Serializable]
    private class ComplianceStatusData
    {
        public bool gameAccessAllowed;
        public string message;
        public string nextAllowedTime;
    }
}
