using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HomePageBookView : MonoBehaviour
{
    [Header("Page")]
    [SerializeField] private RectTransform mainPage;
    [SerializeField] private Text statusText;

    [Header("Player Summary")]
    [SerializeField] private Button avatarButton;
    [SerializeField] private Text avatarNameText;
    [SerializeField] private Text[] metricTexts = new Text[0];
    [SerializeField] private Button[] metricPlusButtons = new Button[0];

    [Header("Left Page Actions")]
    [SerializeField] private Button battleButton;
    [SerializeField] private Button lotteryButton;
    [SerializeField] private Button mailButton;
    [SerializeField] private Button bagButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button friendsButton;
    [SerializeField] private Button noticeButton;
    [SerializeField] private Button settingsButton;

    [Header("Right Page Actions")]
    [SerializeField] private Button taskButton;
    [SerializeField] private Button dailySignButton;
    [SerializeField] private Button newbieSignButton;
    [SerializeField] private Button rechargeButton;

    [Header("Map")]
    [SerializeField] private Button enterMapButton;
    [SerializeField] private Button mapEnterStampButton;
    [SerializeField] private Button[] areaButtons = new Button[0];
    [SerializeField] private Text selectedAreaTitle;
    [SerializeField] private Text selectedAreaDescription;
    [SerializeField] private Text selectedAreaHint;

    [Header("Bottom Tabs")]
    [SerializeField] private Button bottomMailTab;
    [SerializeField] private Button bottomSignTab;
    [SerializeField] private Button bottomLotteryTab;
    [SerializeField] private Button bottomRechargeTab;
    [SerializeField] private Button bottomTaskTab;
    [SerializeField] private Button bottomFriendTab;
    [SerializeField] private Button bottomNoticeTab;

    public RectTransform MainPage => mainPage;
    public Text StatusText => statusText;
    public Button AvatarButton => avatarButton;
    public Text AvatarNameText => avatarNameText;
    public Text[] MetricTexts => metricTexts;
    public Button[] MetricPlusButtons => metricPlusButtons;
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
    public Text SelectedAreaTitle => selectedAreaTitle;
    public Text SelectedAreaDescription => selectedAreaDescription;
    public Text SelectedAreaHint => selectedAreaHint;
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
        HasAll(metricTexts, 6) &&
        HasAll(metricPlusButtons, 2) &&
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
        newbieSignButton != null &&
        rechargeButton != null &&
        enterMapButton != null &&
        mapEnterStampButton != null &&
        HasAll(areaButtons, 5) &&
        selectedAreaTitle != null &&
        selectedAreaDescription != null &&
        selectedAreaHint != null &&
        bottomMailTab != null &&
        bottomSignTab != null &&
        bottomLotteryTab != null &&
        bottomRechargeTab != null &&
        bottomTaskTab != null &&
        bottomFriendTab != null &&
        bottomNoticeTab != null;

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
        mainPage = FindComponent<RectTransform>("MainBookPage");
        statusText = FindComponent<Text>("MainBookPage/Status");
        avatarButton = FindComponent<Button>("MainBookPage/BookAvatarFrame/AvatarButton");
        avatarNameText = FindComponent<Text>("MainBookPage/BookAvatarFrame/BookAvatarName");

        CaptureMetrics();

        const string leftPage = "MainBookPage/LeftStoryPage/";
        battleButton = FindComponent<Button>(leftPage + "RightBattleButton");
        lotteryButton = FindComponent<Button>(leftPage + "ActivityButton");
        mailButton = FindComponent<Button>(leftPage + "BottomMailButton");
        bagButton = FindComponent<Button>(leftPage + "SideBagButton");
        equipmentButton = FindComponent<Button>(leftPage + "SideEquipmentButton");
        shopButton = FindComponent<Button>(leftPage + "SideShopButton");
        friendsButton = FindComponent<Button>(leftPage + "FriendsButton");
        noticeButton = FindComponent<Button>(leftPage + "BottomNoticeButton");
        settingsButton = FindComponent<Button>(leftPage + "SideSettingsButton");

        const string rightPage = "MainBookPage/RightTownPage/";
        taskButton = FindComponent<Button>(rightPage + "BottomTaskButton");
        dailySignButton = FindComponent<Button>(rightPage + "BottomDailySignButton");
        newbieSignButton = FindComponent<Button>(rightPage + "BottomNewbieSignButton");
        rechargeButton = FindComponent<Button>(rightPage + "BottomRechargeButton");
        enterMapButton = FindComponent<Button>(rightPage + "EnterMapButton");
        mapEnterStampButton = FindComponent<Button>(rightPage + "EnterMapButton/MapEnterStamp");
        areaButtons = new[]
        {
            FindComponent<Button>(rightPage + "grassThumb"),
            FindComponent<Button>(rightPage + "forestThumb"),
            FindComponent<Button>(rightPage + "seaThumb"),
            FindComponent<Button>(rightPage + "volcanoThumb"),
            FindComponent<Button>(rightPage + "snowThumb")
        };
        selectedAreaTitle = FindComponent<Text>(rightPage + "EnterMapButton/MapInfoPaper/SelectedTitle");
        selectedAreaDescription = FindComponent<Text>(rightPage + "EnterMapButton/MapInfoPaper/SelectedDescription");
        selectedAreaHint = FindComponent<Text>(rightPage + "EnterMapButton/MapInfoPaper/SelectedHint");

        bottomMailTab = FindComponent<Button>("MainBookPage/BottomMailTab");
        bottomSignTab = FindComponent<Button>("MainBookPage/BottomSignTab");
        bottomLotteryTab = FindComponent<Button>("MainBookPage/BottomLotteryTab");
        bottomRechargeTab = FindComponent<Button>("MainBookPage/BottomRechargeTab");
        bottomTaskTab = FindComponent<Button>("MainBookPage/BottomTaskTab");
        bottomFriendTab = FindComponent<Button>("MainBookPage/BottomFriendTab");
        bottomNoticeTab = FindComponent<Button>("MainBookPage/BottomNoticeTab");

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void CaptureMetrics()
    {
        if (mainPage == null)
        {
            metricTexts = new Text[0];
            metricPlusButtons = new Button[0];
            return;
        }

        List<Text> texts = new List<Text>();
        List<Button> plusButtons = new List<Button>();
        for (int i = 0; i < mainPage.childCount; i++)
        {
            Transform child = mainPage.GetChild(i);
            if (child.name != "BookMetric")
            {
                continue;
            }

            Transform textTransform = child.Find("BookMetricText");
            if (textTransform != null)
            {
                texts.Add(textTransform.GetComponent<Text>());
            }

            Transform plusTransform = child.Find("MetricPlusButton");
            if (plusTransform != null)
            {
                plusButtons.Add(plusTransform.GetComponent<Button>());
            }
        }

        metricTexts = texts.ToArray();
        metricPlusButtons = plusButtons.ToArray();
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }
#endif
}
