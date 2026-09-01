using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HomePageBookView : MonoBehaviour
{
    [Header("Page")]
    [SerializeField] private RectTransform mainPage;
    [SerializeField] private TMP_Text statusText;

    [Header("Player Summary")]
    [SerializeField] private Button avatarButton;
    [SerializeField] private TMP_Text avatarNameText;
    [SerializeField] private TMP_Text avatarLevelText;
    [SerializeField] private Image avatarExperienceBackground;
    [SerializeField] private Image avatarExperienceFill;
    [SerializeField] private TMP_Text avatarExperienceText;
    [SerializeField] private TMP_Text[] metricTexts = new TMP_Text[0];
    [SerializeField] private Button[] metricPlusButtons = new Button[0];

    [Header("Left Page Actions")]
    [SerializeField] private Button activityButton;
    [SerializeField] private Button roleButton;
    [SerializeField] private Button lotteryButton;
    [SerializeField] private Button mailButton;
    [SerializeField] private Button bagButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button friendsButton;
    [SerializeField] private Button noticeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button battleButton;

    [Header("Right Page Actions")]
    [SerializeField] private Button taskButton;
    [SerializeField] private Button dailySignButton;
    [SerializeField] private Button newbieSignButton;
    [SerializeField] private Button rechargeButton;

    [Header("Map")]
    [SerializeField] private Button enterMapButton;
    [SerializeField] private Button mapEnterStampButton;
    [SerializeField] private Button[] areaButtons = new Button[0];
    [SerializeField] private TMP_Text selectedAreaTitle;
    [SerializeField] private TMP_Text selectedAreaDescription;
    [SerializeField] private TMP_Text selectedAreaHint;

    [Header("Bottom Tabs")]
    [SerializeField] private Button bottomMailTab;
    [SerializeField] private Button bottomSignTab;
    [SerializeField] private Button bottomLotteryTab;
    [SerializeField] private Button bottomRechargeTab;
    [SerializeField] private Button bottomTaskTab;
    [SerializeField] private Button bottomFriendTab;
    [SerializeField] private Button bottomNoticeTab;

    public RectTransform MainPage => mainPage;
    public TMP_Text StatusText => statusText;
    public Button AvatarButton => avatarButton;
    public TMP_Text AvatarNameText => avatarNameText;
    public TMP_Text AvatarLevelText => avatarLevelText;
    public Image AvatarExperienceBackground => avatarExperienceBackground;
    public Image AvatarExperienceFill => avatarExperienceFill;
    public TMP_Text AvatarExperienceText => avatarExperienceText;
    public TMP_Text[] MetricTexts => metricTexts;
    public Button[] MetricPlusButtons => metricPlusButtons;
    public Button ActivityButton => activityButton;
    public Button RoleButton => roleButton;
    public Button BattleButton => battleButton;
    public Button LotteryButton => lotteryButton;
    public Button MailButton => mailButton;
    public Button BagButton => bagButton;
    public Button EquipmentButton => equipmentButton;
    public Button ShopButton => shopButton;
    public Button FriendsButton => friendsButton;
    public Button NoticeButton => noticeButton;
    public Button SettingsButton => settingsButton;
    public Button TaskButton => taskButton;
    public Button DailySignButton => dailySignButton;
    public Button NewbieSignButton => newbieSignButton;
    public Button RechargeButton => rechargeButton;
    public Button EnterMapButton => enterMapButton;
    public Button MapEnterStampButton => mapEnterStampButton;
    public Button[] AreaButtons => areaButtons;
    public TMP_Text SelectedAreaTitle => selectedAreaTitle;
    public TMP_Text SelectedAreaDescription => selectedAreaDescription;
    public TMP_Text SelectedAreaHint => selectedAreaHint;
    public Button BottomMailTab => bottomMailTab;
    public Button BottomSignTab => bottomSignTab;
    public Button BottomLotteryTab => bottomLotteryTab;
    public Button BottomRechargeTab => bottomRechargeTab;
    public Button BottomTaskTab => bottomTaskTab;
    public Button BottomFriendTab => bottomFriendTab;
    public Button BottomNoticeTab => bottomNoticeTab;

    public bool IsReady =>
        mainPage != null &&
        statusText != null &&
        avatarButton != null &&
        avatarNameText != null &&
        avatarLevelText != null &&
        avatarExperienceBackground != null &&
        avatarExperienceFill != null &&
        HasAll(metricTexts, 4) &&
        activityButton != null &&
        roleButton != null &&
        battleButton != null &&
        lotteryButton != null &&
        mailButton != null &&
        bagButton != null &&
        equipmentButton != null &&
        shopButton != null &&
        friendsButton != null &&
        noticeButton != null &&
        settingsButton != null &&
        taskButton != null &&
        dailySignButton != null &&
        enterMapButton != null;

    private static bool HasAll<T>(T[] values, int expectedCount) where T : Object
    {
        if (values == null || values.Length != expectedCount)
        {
            return false;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == null)
            {
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Book View References")]
    public void EditorCaptureReferences()
    {
        CaptureReferences();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    public void CaptureReferences()
    {
        mainPage = FindComponent<RectTransform>("MainBookPage");
        statusText = FindComponent<TMP_Text>("MainBookPage/Status");
        avatarButton = FindComponent<Button>("MainBookPage/BookAvatarFrame/AvatarButton");
        avatarNameText = FindComponent<TMP_Text>("MainBookPage/BookAvatarFrame/BookAvatarName");
        avatarLevelText = FindComponent<TMP_Text>("MainBookPage/BookAvatarFrame/BookAvatarLevel");
        avatarExperienceBackground = FindComponent<Image>("MainBookPage/BookAvatarFrame/BookAvatarExperienceBackground");
        avatarExperienceFill = FindComponent<Image>("MainBookPage/BookAvatarFrame/BookAvatarExperienceBackground/BookAvatarExperienceFill");
        avatarExperienceText = FindComponent<TMP_Text>("MainBookPage/BookAvatarFrame/BookAvatarExperienceText");

        CaptureMetrics();

        const string leftPage = "MainBookPage/LeftStoryPage/";
        activityButton = FindComponent<Button>(leftPage + "ActivityButton");
        roleButton = FindComponent<Button>(leftPage + "RoleButton");
        lotteryButton = FindComponent<Button>(leftPage + "LotteryButton");
        mailButton = FindComponent<Button>("MainBookPage/BottomMailButton");
        bagButton = FindComponent<Button>(leftPage + "SideBagButton");
        equipmentButton = FindComponent<Button>(leftPage + "SideEquipmentButton");
        shopButton = FindComponent<Button>(leftPage + "SideShopButton");
        friendsButton = FindComponent<Button>(leftPage + "FriendsButton");
        noticeButton = FindComponent<Button>("MainBookPage/BottomNoticeButton");
        settingsButton = FindComponent<Button>("MainBookPage/SideSettingsButton");

        const string rightPage = "MainBookPage/RightTownPage/";
        taskButton = FindComponent<Button>(leftPage + "BottomTaskButton");
        dailySignButton = FindComponent<Button>(leftPage + "BottomDailySignButton");
        battleButton = FindComponent<Button>(rightPage + "BattleModeEntry/BattleEnterButton");
        enterMapButton = FindComponent<Button>(rightPage + "ManagementModeEntry/ManagementEnterButton");
        mapEnterStampButton = null;
        areaButtons = new Button[0];
        selectedAreaTitle = null;
        selectedAreaDescription = null;
        selectedAreaHint = null;
    }

    private void CaptureMetrics()
    {
        if (mainPage == null)
        {
            metricTexts = new TMP_Text[0];
            metricPlusButtons = new Button[0];
            return;
        }

        List<TMP_Text> texts = new List<TMP_Text>();
        List<Button> plusButtons = new List<Button>();
        for (int i = 0; i < mainPage.childCount; i++)
        {
            Transform child = mainPage.GetChild(i);
            if (child.name != "BookMetric" || !child.gameObject.activeSelf)
            {
                continue;
            }

            Transform textTransform = child.Find("BookMetricText");
            if (textTransform != null)
            {
                texts.Add(textTransform.GetComponent<TMP_Text>());
            }

            Transform plusTransform = child.Find("MetricPlusButton");
            plusButtons.Add(plusTransform != null ? plusTransform.GetComponent<Button>() : null);
        }

        metricTexts = texts.ToArray();
        metricPlusButtons = plusButtons.ToArray();
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }
}
