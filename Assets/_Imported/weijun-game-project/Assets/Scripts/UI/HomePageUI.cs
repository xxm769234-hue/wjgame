using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using Font = TMPro.TMP_FontAsset;

public partial class HomePageUI : MonoBehaviour
{
    private const string HomeSceneName = "HomeScene";
    private const string SimulationSceneName = "Simulation management mode";
    private const string IdleAdventureSceneName = "Idle Adventure mode";
    private const string LaunchSceneName = "Launch";
    private const string AuthTokenKey = GameSession.AuthTokenKey;
    private const string PlayerIdKey = GameSession.PlayerIdKey;
    private const string LotteryChanceKey = "LotteryChanceCount";
    private const int DefaultLotteryChanceCount = 100;
    private const int InventoryCapacity = 40;
    private const int InventoryColumns = InventoryPanelView.GridColumns;
    private const int InventoryRows = 5;
    private const int DefaultInventoryUnlockedSlots = 32;
    private const float HomeFeatureButtonAlphaHitTestThreshold = 0.1f;

    private static readonly Color DeskColor = new Color(0.78f, 0.84f, 0.87f, 1f);
    private static readonly Color PageColor = new Color(0.97f, 0.95f, 0.90f, 1f);
    private static readonly Color PageShadeColor = new Color(0.88f, 0.84f, 0.76f, 1f);
    private static readonly Color InkColor = new Color(0.11f, 0.12f, 0.14f, 1f);
    private static readonly Color MutedColor = new Color(0.42f, 0.41f, 0.38f, 1f);
    private static readonly Color PrimaryColor = new Color(0.23f, 0.39f, 0.63f, 1f);
    private static readonly Color DisabledTextColor = new Color(0.63f, 0.58f, 0.49f, 1f);

    [SerializeField] private Font defaultFont;
    private Font fallbackFont;
    private Transform mainPageTransform;
    private HomePageBookView boundBookView;
    private HomePageBookStyle bookStyle;
    private Button avatarButton;
    private TMP_Text bookAvatarLevelText;
    private Image bookAvatarExperienceBackground;
    private Image bookAvatarExperienceFill;
    private TMP_Text bookAvatarExperienceText;
    private GameObject personalInfoPopup;
    private GameObject mailPanelObject;
    private GameObject signPanelObject;
    private GameObject lotteryPanelObject;
    private GameObject rechargePanelObject;
    private bool rechargeSceneAuthoredStateCaptured;
    private readonly Dictionary<string, bool> rechargeSceneAuthoredActiveStates = new Dictionary<string, bool>();
    private GameObject taskPanelObject;
    private GameObject inventoryPanelObject;
    private GameObject inventoryInputBlockerObject;
    private GameObject settingsPanelObject;
    private InventoryPanelView inventoryPanelView;
    private AnnouncementPanelView announcementPanelView;
    private HomePageScenePanels scenePanels;
    private string activeTaskCategory = "daily";
    private InventoryItemResponse[] currentInventoryItems = new InventoryItemResponse[0];
    private readonly Dictionary<string, Sprite> inventorySpriteCache = new Dictionary<string, Sprite>();
    private string activeInventoryCategory = "all";
    private string selectedInventoryItemKey = string.Empty;
    private string inventoryStatusMessage = string.Empty;
    private bool inventoryServerBacked;
    private bool lotteryRolling;
    private Text popupNameText;
    private Text popupAccountText;
    private Text popupLevelText;
    private Text popupResourceText;
    private Text popupModeText;
    private GameObject contentPanelObject;
    private Text contentTitleText;
    private Text contentBodyText;
    private Text statusText;
    private Text selectedAreaTitle;
    private Text selectedAreaDescription;
    private Text selectedAreaHint;
    private AreaInfo selectedArea;

    private readonly AreaInfo[] areas =
    {
        new AreaInfo("草原", "grass", "基础生产区域，适合农场和早期资源布局。", "入门区域，可直接进入", new Color(0.42f, 0.64f, 0.32f, 1f)),
        new AreaInfo("森林", "forest", "木材更充足，适合扩建和中期经营。", "消耗金币解锁", new Color(0.24f, 0.48f, 0.32f, 1f)),
        new AreaInfo("海洋", "sea", "水域资源与特殊建筑区域。", "消耗金币解锁", new Color(0.20f, 0.53f, 0.72f, 1f)),
        new AreaInfo("火山", "volcano", "高等级挑战区域，资源收益更激进。", "等级解锁", new Color(0.72f, 0.27f, 0.19f, 1f)),
        new AreaInfo("雪地", "snow", "稳定产出区域，适合后期布局。", "开放区域，可直接进入", new Color(0.52f, 0.62f, 0.74f, 1f))
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForActiveScene();
    }

    private static void TryCreateForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != HomeSceneName)
        {
            return;
        }

        if (FindObjectOfType<HomePageUI>() != null)
        {
            return;
        }

        new GameObject("BookHomePageUI").AddComponent<HomePageUI>();
    }

    private void Awake()
    {
        if (!GameSession.HasValidSession)
        {
            SceneManager.LoadScene(LaunchSceneName);
            return;
        }

        fallbackFont = RuntimeFontAssetProvider.Resolve();
        if (defaultFont == null)
        {
            defaultFont = fallbackFont;
        }
        scenePanels = FindObjectOfType<HomePageScenePanels>(true);
        if (scenePanels != null)
        {
            scenePanels.HideAll();
        }

        AnnouncementPanelView initialAnnouncementPanel = FindSceneAnnouncementPanel();
        if (initialAnnouncementPanel != null)
        {
            initialAnnouncementPanel.gameObject.SetActive(false);
        }

        EnsureEventSystem();
        Build();
        RefreshCurrentAvatarVisuals();
        if (HasAuthSession())
        {
            StartCoroutine(RefreshAccountSnapshot(string.Empty));
            RefreshServerSignInStatus();
            LoadServerOnboarding();
        }
        BeginRealtimeConnection();
    }

    private void Build()
    {
        RectTransform root = GetOrCreateBookHomeRoot();
        HomePageBookView view = root.GetComponent<HomePageBookView>();
        if (view != null && view.MainPage != null)
        {
            view.CaptureReferences();
            BindBookView(view);
        }
        else
        {
            ClearGeneratedBookChildren(root);
            BuildOpenBookMainPage(root);
            view.CaptureReferences();
            AttachGeneratedBookView(view);
        }

        SelectArea(GetLastArea(), PlayerPrefs.HasKey("OpenAreaLandType"));
    }

    private void BindBookView(HomePageBookView view)
    {
        boundBookView = view;
        mainPageTransform = view.MainPage;
        ConfigureHomeFeatureButtonHitAreas(view);
        avatarButton = view.AvatarButton;
        statusText = view.StatusText;
        selectedAreaTitle = view.SelectedAreaTitle;
        selectedAreaDescription = view.SelectedAreaDescription;
        selectedAreaHint = view.SelectedAreaHint;

        if (view.AvatarNameText != null)
        {
            string fallbackName = string.IsNullOrEmpty(view.AvatarNameText.text) ? "Player" : view.AvatarNameText.text;
            view.AvatarNameText.text = PlayerPrefs.GetString("PlayerNickname", fallbackName);
        }

        bookAvatarLevelText = view.AvatarLevelText;
        bookAvatarExperienceBackground = view.AvatarExperienceBackground;
        bookAvatarExperienceFill = view.AvatarExperienceFill;
        bookAvatarExperienceText = view.AvatarExperienceText;
        EnsureBookAvatarSummaryControls();
        RefreshBookAvatarSummary();

        SetBookMetricValue(view.MetricTexts, 0, GetHomePurificationAgent().ToString());
        SetBookMetricValue(view.MetricTexts, 1, GetHomeStaminaDisplay());
        SetBookMetricValue(view.MetricTexts, 2, PlayerPrefs.GetInt("Gold", 1000).ToString());
        SetBookMetricValue(view.MetricTexts, 3, PlayerPrefs.GetInt("Crystal", 427).ToString());

        Button[] metricPlusButtons = view.MetricPlusButtons;
        if (metricPlusButtons != null && metricPlusButtons.Length > 3)
        {
            BindButton(metricPlusButtons[3], ShowRechargeContent);
        }

        BindButton(view.AvatarButton, ShowPersonalInfo);
        BindButton(view.ActivityButton, ShowNewbieSignContent);
        BindButton(view.RoleButton, ShowCharacterContent);
        BindButton(view.BattleButton, ShowBattleContent);
        BindButton(view.LotteryButton, ShowLotteryContent);
        BindButton(view.MailButton, ShowMailContent);
        BindButton(view.BagButton, ShowBagContent);
        BindButton(view.EquipmentButton, ShowEquipmentContent);
        BindButton(view.ShopButton, ShowShopContent);
        BindButton(view.FriendsButton, ShowFriendsContent);
        BindButton(view.NoticeButton, ShowAnnouncement);
        BindButton(view.SettingsButton, ShowSettingsContent);
        BindButton(view.TaskButton, ShowTaskContent);
        BindButton(view.DailySignButton, ShowDailySignContent);
        BindButton(view.EnterMapButton, EnterSelectedArea);
        BindButton(view.MapEnterStampButton, EnterSelectedArea);

        for (int i = 0; i < view.AreaButtons.Length && i < areas.Length; i++)
        {
            AreaInfo area = areas[i];
            BindButton(view.AreaButtons[i], () => SelectAreaAndEnter(area));
        }

    }

    private void AttachGeneratedBookView(HomePageBookView view)
    {
        boundBookView = view;
        mainPageTransform = view.MainPage;
        ConfigureHomeFeatureButtonHitAreas(view);
        avatarButton = view.AvatarButton;
        statusText = view.StatusText;
        selectedAreaTitle = view.SelectedAreaTitle;
        selectedAreaDescription = view.SelectedAreaDescription;
        selectedAreaHint = view.SelectedAreaHint;
        bookAvatarLevelText = view.AvatarLevelText;
        bookAvatarExperienceBackground = view.AvatarExperienceBackground;
        bookAvatarExperienceFill = view.AvatarExperienceFill;
        bookAvatarExperienceText = view.AvatarExperienceText;
        RefreshBookAvatarSummary();
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button != null && action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void ConfigureHomeFeatureButtonHitAreas(HomePageBookView view)
    {
        if (view == null)
        {
            Debug.LogWarning("HomePageBookView is missing; home feature button hit areas cannot be configured.");
            return;
        }

        ConfigureIrregularButtonHitArea(view.RoleButton);
        ConfigureIrregularButtonHitArea(view.EquipmentButton);
        ConfigureIrregularButtonHitArea(view.TaskButton);
        ConfigureIrregularButtonHitArea(view.LotteryButton);
        ConfigureIrregularButtonHitArea(view.FriendsButton);
        ConfigureIrregularButtonHitArea(view.ShopButton);
        ConfigureIrregularButtonHitArea(view.BagButton);
    }

    private static void ConfigureIrregularButtonHitArea(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogWarning("Home feature button " + button.name + " is missing its Image component.", button);
            return;
        }

        image.alphaHitTestMinimumThreshold = image.sprite == null
            ? 0f
            : HomeFeatureButtonAlphaHitTestThreshold;
    }

    private static void SetBookMetricValue(TMP_Text[] metricTexts, int index, string value)
    {
        if (metricTexts == null || index < 0 || index >= metricTexts.Length || metricTexts[index] == null)
        {
            return;
        }

        metricTexts[index].text = value;
    }

    private static int GetHomePurificationAgent()
    {
        return PlayerPrefs.GetInt("PurificationAgent", 0);
    }

    private int GetHomeStamina()
    {
        if (serverStaminaLoaded)
        {
            return GetAuthoritativeHomeStamina();
        }

        return 0;
    }

    private void EnsureBookAvatarSummaryControls()
    {
        if (mainPageTransform == null)
        {
            return;
        }

        Transform frame = mainPageTransform.Find("BookAvatarFrame");
        if (frame == null)
        {
            Debug.LogWarning("BookAvatarFrame was not found; avatar summary controls cannot be refreshed.", this);
            return;
        }

        if (bookAvatarLevelText == null)
        {
            bookAvatarLevelText = FindSceneComponent<TMP_Text>(frame, "BookAvatarLevel");
        }

        if (bookAvatarExperienceBackground == null)
        {
            bookAvatarExperienceBackground = FindSceneComponent<Image>(frame, "BookAvatarExperienceBackground");
        }

        if (bookAvatarExperienceFill == null && bookAvatarExperienceBackground != null)
        {
            bookAvatarExperienceFill = FindSceneComponent<Image>(
                bookAvatarExperienceBackground.transform,
                "BookAvatarExperienceFill");
        }

        if (bookAvatarExperienceText == null)
        {
            bookAvatarExperienceText = FindSceneComponent<TMP_Text>(frame, "BookAvatarExperienceText");
        }

        if (bookAvatarLevelText == null
            || bookAvatarExperienceBackground == null
            || bookAvatarExperienceFill == null)
        {
            Debug.LogWarning("BookAvatarFrame is missing one or more scene-bound avatar summary components.", this);
        }
    }

    private void RefreshBookAvatarSummary()
    {
        EnsureBookAvatarSummaryControls();

        int level = Mathf.Max(1, PlayerPrefs.GetInt("PlayerLv", 1));
        if (bookAvatarLevelText != null)
        {
            bookAvatarLevelText.text = $"Lv.{level}";
        }

        int currentExperience = Mathf.Max(0, PlayerPrefs.GetInt("PlayerCurrentLevelExperience", 0));
        int requiredExperience = Mathf.Max(0, PlayerPrefs.GetInt("PlayerRequiredExperience", 0));
        float progress = Mathf.Clamp01(PlayerPrefs.GetInt("PlayerExperienceProgressBps", 0) / 10000f);
        if (progress <= 0f && PlayerPrefs.GetInt("PlayerMaxLevel", 0) == 0)
        {
            if (requiredExperience > 0)
            {
                progress = Mathf.Clamp01(currentExperience / (float)requiredExperience);
            }
        }

        if (PlayerPrefs.GetInt("PlayerMaxLevel", 0) == 1)
        {
            progress = 1f;
        }

        if (bookAvatarExperienceFill != null)
        {
            bookAvatarExperienceFill.fillAmount = progress;
        }

        if (bookAvatarExperienceText != null)
        {
            bookAvatarExperienceText.text = PlayerPrefs.GetInt("PlayerMaxLevel", 0) == 1
                ? "MAX"
                : requiredExperience > 0
                    ? $"{currentExperience}/{requiredExperience}"
                    : currentExperience.ToString();
        }
    }

#if UNITY_EDITOR
    public void EditorSetFont(Font value)
    {
        defaultFont = value;
    }

    public void EditorRebuildBookPreview()
    {
        RectTransform root = transform as RectTransform;
        if (root == null)
        {
            return;
        }

        fallbackFont = RuntimeFontAssetProvider.Resolve();
        if (defaultFont == null)
        {
            defaultFont = fallbackFont;
        }
        bookStyle = GetComponent<HomePageBookStyle>();
        if (bookStyle == null)
        {
            bookStyle = gameObject.AddComponent<HomePageBookStyle>();
        }

        HomePageBookView view = GetComponent<HomePageBookView>();
        if (view == null)
        {
            view = gameObject.AddComponent<HomePageBookView>();
        }

        ClearGeneratedBookChildren(root);
        BuildOpenBookMainPage(root);

        AreaInfo previewArea = GetLastArea();
        selectedArea = previewArea;
        if (selectedAreaTitle != null)
        {
            selectedAreaTitle.text = previewArea.name;
            selectedAreaDescription.text = previewArea.description;
            selectedAreaHint.text = previewArea.unlockHint;
            selectedAreaHint.color = previewArea.color;
        }

        Button[] previewButtons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < previewButtons.Length; i++)
        {
            previewButtons[i].onClick.RemoveAllListeners();
        }

        view.EditorCaptureReferences();
        bookStyle.ApplyToHierarchy();
        UnityEditor.EditorUtility.SetDirty(view);
        UnityEditor.EditorUtility.SetDirty(bookStyle);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private RectTransform GetOrCreateBookHomeRoot()
    {
        Transform panelsRoot = scenePanels != null ? scenePanels.transform : null;
        if (panelsRoot == null)
        {
            GameObject existingPanels = GameObject.Find("HomePanelsCanvas");
            panelsRoot = existingPanels != null ? existingPanels.transform : null;
        }

        if (panelsRoot == null)
        {
            GameObject canvasObj = new GameObject("HomePanelsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HomePageScenePanels));
            canvasObj.layer = LayerMask.NameToLayer("UI");
            panelsRoot = canvasObj.transform;

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panelsRect = canvasObj.GetComponent<RectTransform>();
            panelsRect.localScale = Vector3.one;
            Stretch(panelsRect);

            scenePanels = canvasObj.GetComponent<HomePageScenePanels>();
        }

        RectTransform bookRoot = null;
        for (int i = 0; i < panelsRoot.childCount; i++)
        {
            Transform child = panelsRoot.GetChild(i);
            if (child.name == "BookHomeCanvas")
            {
                bookRoot = child as RectTransform;
                break;
            }
        }

        if (bookRoot == null)
        {
            GameObject bookObj = new GameObject("BookHomeCanvas", typeof(RectTransform), typeof(HomePageBookStyle), typeof(HomePageBookView));
            bookObj.layer = LayerMask.NameToLayer("UI");
            bookObj.transform.SetParent(panelsRoot, false);
            bookRoot = bookObj.GetComponent<RectTransform>();
        }

        panelsRoot.localScale = Vector3.one;
        bookRoot.SetAsFirstSibling();
        Stretch(bookRoot);

        bookStyle = bookRoot.GetComponent<HomePageBookStyle>();
        if (bookStyle == null)
        {
            bookStyle = bookRoot.gameObject.AddComponent<HomePageBookStyle>();
        }

        if (bookRoot.GetComponent<HomePageBookView>() == null)
        {
            bookRoot.gameObject.AddComponent<HomePageBookView>();
        }

        return bookRoot;
    }

    private void ClearGeneratedBookChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void BuildOpenBookMainPage(Transform root)
    {
        BuildRequestedBookMainPage(root);
    }

    private void BuildRequestedBookMainPage(Transform root)
    {
        Image page = CreateImage("MainBookPage", root, bookStyle != null ? bookStyle.DeskColor : new Color(0.54f, 0.61f, 0.60f, 1f));
        if (bookStyle != null)
        {
            bookStyle.ApplyBackground(page);
        }

        mainPageTransform = page.transform;
        Stretch(page.rectTransform);
        AddBookAvatarDisplay(page.transform);

        AddBookMetric(page.transform, "\u51c0\u5316\u5242", GetHomePurificationAgent().ToString(), new Vector2(174f, -24f), new Vector2(166f, 48f), new Color(0.62f, 0.39f, 0.20f, 1f), false, null);
        AddBookMetric(page.transform, "\u4f53\u529b", GetHomeStaminaDisplay(), new Vector2(360f, -24f), new Vector2(188f, 48f), new Color(0.88f, 0.61f, 0.18f, 1f), false, null);
        AddBookMetric(page.transform, "\u91d1\u5e01", PlayerPrefs.GetInt("Gold", 1000).ToString(), new Vector2(568f, -24f), new Vector2(188f, 48f), new Color(0.28f, 0.64f, 0.86f, 1f), false, null);
        AddBookMetric(page.transform, "\u94bb\u77f3", PlayerPrefs.GetInt("Crystal", 427).ToString(), new Vector2(776f, -24f), new Vector2(188f, 48f), new Color(0.32f, 0.55f, 0.31f, 1f), true, ShowRechargeContent);

        Transform leftPage = CreateBookSheet(page.transform, "LeftStoryPage", new Vector2(96f, -128f), new Vector2(846f, 814f), false).transform;
        Transform rightPage = CreateBookSheet(page.transform, "RightTownPage", new Vector2(978f, -128f), new Vector2(846f, 814f), true).transform;

        AddBookPageTitle(leftPage, "\u57ce\u9547\u624b\u8bb0", "\u89d2\u8272 / \u88c5\u5907 / \u529f\u80fd", new Vector2(56f, -30f));
        AddBookPageTitle(rightPage, "\u9886\u5730\u5730\u56fe", "\u63a2\u7d22 / \u6a21\u62df\u7ecf\u8425 / \u5bf9\u6218", new Vector2(56f, -30f));

        CreateBookFeatureButton(leftPage, "BottomDailySignButton", "\u6bcf\u65e5\u7b7e\u5230", "\u6bcf\u65e5\u5956\u52b1", new Vector2(56f, -104f), new Vector2(340f, 100f), new Color(0.78f, 0.46f, 0.22f, 1f), ShowDailySignContent);
        CreateBookFeatureButton(leftPage, "ActivityButton", "\u6d3b\u52a8", "\u65b0\u624b\u7b7e\u5230", new Vector2(434f, -104f), new Vector2(340f, 100f), new Color(0.57f, 0.48f, 0.76f, 1f), ShowNewbieSignContent);
        CreateBookFeatureButton(leftPage, "RoleButton", "\u89d2\u8272", "\u89d2\u8272\u4e0e\u6210\u957f", new Vector2(56f, -244f), new Vector2(218f, 124f), new Color(0.72f, 0.38f, 0.22f, 1f), ShowCharacterContent);
        CreateBookFeatureButton(leftPage, "SideEquipmentButton", "\u88c5\u5907", "\u89d2\u8272\u88c5\u5907", new Vector2(306f, -244f), new Vector2(218f, 124f), new Color(0.36f, 0.46f, 0.70f, 1f), ShowEquipmentContent);
        CreateBookFeatureButton(leftPage, "LotteryButton", "\u62bd\u5956", "\u5956\u6c60\u4e0e\u62bd\u53d6", new Vector2(556f, -244f), new Vector2(218f, 124f), new Color(0.78f, 0.52f, 0.18f, 1f), ShowLotteryContent);
        CreateBookFeatureButton(leftPage, "BottomTaskButton", "\u4efb\u52a1", "\u4e3b\u7ebf\u4e0e\u65e5\u5e38", new Vector2(56f, -402f), new Vector2(166f, 112f), new Color(0.32f, 0.58f, 0.64f, 1f), ShowTaskContent);
        CreateBookFeatureButton(leftPage, "FriendsButton", "\u597d\u53cb", "\u597d\u53cb\u5217\u8868", new Vector2(240f, -402f), new Vector2(166f, 112f), new Color(0.46f, 0.38f, 0.70f, 1f), ShowFriendsContent);
        CreateBookFeatureButton(leftPage, "SideBagButton", "\u4ed3\u5e93", "\u7269\u54c1\u4e0e\u8d44\u6e90", new Vector2(424f, -402f), new Vector2(166f, 112f), new Color(0.50f, 0.34f, 0.20f, 1f), ShowBagContent);
        CreateBookFeatureButton(leftPage, "SideShopButton", "\u5546\u57ce", "\u9053\u5177\u4e0e\u793c\u5305", new Vector2(608f, -402f), new Vector2(166f, 112f), new Color(0.37f, 0.58f, 0.38f, 1f), ShowShopContent);

        CreateBookTopActionButton(page.transform, "BottomMailButton", "\u90ae\u4ef6", new Vector2(1450f, -24f), ShowMailContent);
        CreateBookTopActionButton(page.transform, "BottomNoticeButton", "\u516c\u544a", new Vector2(1590f, -24f), ShowAnnouncement);
        CreateBookTopActionButton(page.transform, "SideSettingsButton", "\u8bbe\u7f6e", new Vector2(1730f, -24f), ShowSettingsContent);

        CreateModeEntry(
            rightPage,
            "BattleModeEntry",
            "BattleEnterButton",
            "\u5bf9\u6218",
            "Sprites/Arts/png/volcano.png",
            new Vector2(56f, -104f),
            new Vector2(711.2f, 300f),
            new Color(0.48f, 0.18f, 0.13f, 1f),
            ShowBattleContent);
        CreateModeEntry(
            rightPage,
            "ManagementModeEntry",
            "ManagementEnterButton",
            "\u6a21\u62df\u7ecf\u8425",
            "Sprites/Arts/png/grass.jpeg",
            new Vector2(56f, -430f),
            new Vector2(711.2f, 300f),
            new Color(0.30f, 0.48f, 0.28f, 1f),
            EnterSelectedArea);

        statusText = CreateText("Status", page.transform, string.Empty, 18, FontStyle.Bold, new Color(0.98f, 0.89f, 0.66f, 1f), TextAnchor.MiddleRight);
        SetRect(statusText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1348f, -28f), new Vector2(480f, 36f));
    }

    private Button CreateBookTopActionButton(Transform parent, string name, string title, Vector2 position, Action onClick)
    {
        Button button = CreateButton(parent, name, string.Empty, bookStyle != null ? bookStyle.FeatureButtonColor : new Color(0.99f, 0.94f, 0.81f, 1f), InkColor);
        button.onClick.AddListener(() => onClick());
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(124f, 52f));
        if (bookStyle != null)
        {
            bookStyle.ApplyFeatureButton(button.GetComponent<Image>());
        }

        Text titleText = CreateText("Title", button.transform, title, 18, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);
        SetRect(titleText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(124f, 52f));
        return button;
    }

    private void AddOpenBookSpread(Transform parent)
    {
        Image shadow = CreateImage("OpenBookShadow", parent, new Color(0.12f, 0.08f, 0.05f, 0.30f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(128f, -156f), new Vector2(1668f, 826f));

        Image cover = CreateImage("OpenBookCover", parent, bookStyle != null ? bookStyle.BookCoverColor : new Color(0.47f, 0.30f, 0.18f, 1f));
        if (bookStyle != null)
        {
            bookStyle.ApplyBookCover(cover);
        }
        cover.raycastTarget = false;
        SetRect(cover.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(84f, -112f), new Vector2(1752f, 862f));

        Image spineShadow = CreateImage("BookSpineShadow", parent, new Color(0.15f, 0.08f, 0.04f, 0.42f));
        spineShadow.raycastTarget = false;
        SetRect(spineShadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(930f, -128f), new Vector2(64f, 814f));

        Image spineLight = CreateImage("BookSpineLight", parent, new Color(0.88f, 0.75f, 0.54f, 0.55f));
        spineLight.raycastTarget = false;
        SetRect(spineLight.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(956f, -132f), new Vector2(9f, 804f));
    }

    private Image CreateBookSheet(Transform parent, string name, Vector2 position, Vector2 size, bool rightPage)
    {
        if (name != "LeftStoryPage" && name != "RightTownPage")
        {
            Image shadow = CreateImage(name + "SoftShadow", parent, new Color(0.16f, 0.10f, 0.05f, 0.20f));
            shadow.raycastTarget = false;
            SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(rightPage ? 10f : 5f, -10f), size);
        }

        Image sheet = CreateImage(name, parent, bookStyle != null ? bookStyle.PageColor : new Color(0.97f, 0.93f, 0.82f, 1f));
        if (bookStyle != null)
        {
            if (rightPage)
            {
                bookStyle.ApplyRightPage(sheet);
            }
            else
            {
                bookStyle.ApplyLeftPage(sheet);
            }
        }
        SetRect(sheet.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        if (name != "RightTownPage" && name != "LeftStoryPage")
        {
            Image edge = CreateImage(name + "InnerEdge", sheet.transform, rightPage ? new Color(0.76f, 0.62f, 0.42f, 0.20f) : new Color(1f, 0.98f, 0.90f, 0.28f));
            edge.raycastTarget = false;
            SetRect(edge.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), rightPage ? Vector2.zero : new Vector2(size.x - 42f, 0f), new Vector2(42f, size.y));
        }

        if (name != "LeftStoryPage" && name != "RightTownPage")
        {
            for (int i = 0; i < 18; i++)
            {
                float x = 34f + ((i * 107) % Mathf.RoundToInt(size.x - 72f));
                float y = 68f + ((i * 83) % Mathf.RoundToInt(size.y - 118f));
                Image dot = CreateImage(name + "PaperDot", sheet.transform, new Color(0.44f, 0.30f, 0.16f, 0.08f));
                dot.raycastTarget = false;
                SetRect(dot.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(x, -y), new Vector2(5f + i % 3, 5f + i % 3));
            }
        }

        return sheet;
    }

    private void AddBookPageTitle(Transform parent, string title, string subtitle, Vector2 position)
    {
        if (parent != null && (parent.name == "LeftStoryPage" || parent.name == "RightTownPage"))
        {
            return;
        }

        Image mark = CreateImage("PageTitleMark", parent, new Color(0.33f, 0.49f, 0.53f, 1f));
        mark.raycastTarget = false;
        SetRect(mark.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(0f, -10f), new Vector2(12f, 52f));

        Text titleText = CreateText("PageTitle", parent, title, 34, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(titleText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(26f, 0f), new Vector2(260f, 42f));

        Text subText = CreateText("PageSubtitle", parent, subtitle, 17, FontStyle.Bold, MutedColor, TextAnchor.MiddleLeft);
        SetRect(subText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(288f, -8f), new Vector2(300f, 32f));

        Image line = CreateImage("PageTitleLine", parent, new Color(0.41f, 0.26f, 0.13f, 0.38f));
        line.raycastTarget = false;
        SetRect(line.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(0f, -60f), new Vector2(718f, 3f));
    }

    private void AddBookAvatarDisplay(Transform parent)
    {
        Image shadow = CreateImage("BookAvatarShadow", parent, new Color(0.12f, 0.07f, 0.04f, 0.28f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(52f, -24f), new Vector2(96f, 76f));

        Image frame = CreateImage("BookAvatarFrame", parent, new Color(0.96f, 0.83f, 0.55f, 1f));
        SetRect(frame.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(44f, -18f), new Vector2(96f, 76f));

        Image portrait = CreateImage("BookAvatarPortrait", frame.transform, new Color(0.80f, 0.88f, 0.86f, 1f));
        SetRect(portrait.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(10f, -8f), new Vector2(76f, 46f));

        Image head = CreateImage("BookAvatarHead", portrait.transform, new Color(0.95f, 0.72f, 0.52f, 1f));
        SetRect(head.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(27f, -6f), new Vector2(22f, 22f));

        Text name = CreateText("BookAvatarName", frame.transform, PlayerPrefs.GetString("PlayerNickname", "玩家"), 15, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);
        SetRect(name.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(8f, -54f), new Vector2(80f, 18f));

        Text level = CreateText("BookAvatarLevel", frame.transform, $"Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}", 11, FontStyle.Normal, InkColor, TextAnchor.MiddleCenter);
        SetRect(level.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(56f, -8f), new Vector2(30f, 16f));

        Image experienceBackground = CreateImage("BookAvatarExperienceBackground", frame.transform, new Color(0.22f, 0.16f, 0.10f, 0.55f));
        experienceBackground.raycastTarget = false;
        SetRect(experienceBackground.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(8f, -69f), new Vector2(80f, 5f));

        Image experienceFill = CreateImage("BookAvatarExperienceFill", experienceBackground.transform, new Color(0.95f, 0.79f, 0.38f, 1f));
        experienceFill.raycastTarget = false;
        experienceFill.type = Image.Type.Filled;
        experienceFill.fillMethod = Image.FillMethod.Horizontal;
        experienceFill.fillOrigin = 0;
        SetRect(experienceFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

        Button button = CreateButton(frame.transform, "AvatarButton", string.Empty, new Color(1f, 1f, 1f, 0f), Color.white);
        button.onClick.AddListener(ShowPersonalInfo);
        Stretch(button.GetComponent<RectTransform>());
        avatarButton = button;
    }

    private void AddBookMetric(Transform parent, string label, string value, Vector2 position, Vector2 size, Color accent, bool hasPlus, Action onPlus)
    {
        Image box = CreateImage("BookMetric", parent, bookStyle != null ? bookStyle.MetricColor : new Color(0.98f, 0.91f, 0.74f, 0.98f));
        SetRect(box.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        Image icon = CreateImage("BookMetricIcon", box.transform, accent);
        icon.raycastTarget = false;
        SetRect(icon.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(10f, -10f), new Vector2(28f, 28f));

        Text text = CreateText("BookMetricText", box.transform, value, 18, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(text.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(46f, -7f), new Vector2(hasPlus ? size.x - 84f : size.x - 56f, 34f));

        if (!hasPlus || onPlus == null)
        {
            return;
        }

        Button plus = CreateButton(box.transform, "MetricPlusButton", "+", accent, Color.white);
        plus.onClick.AddListener(() => onPlus());
        SetRect(plus.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x - 34f, -8f), new Vector2(28f, 28f));
        ConfigureClearButtonLabel(plus, 22);
    }

    private Button CreateBookFeatureButton(Transform parent, string name, string title, string caption, Vector2 position, Vector2 size, Color accent, Action onClick)
    {
        if (name == "BottomDailySignButton" || name == "ActivityButton")
        {
            Button editableButton = CreateButton(parent, name, string.Empty, bookStyle != null ? bookStyle.FeatureButtonColor : new Color(0.99f, 0.94f, 0.81f, 1f), InkColor);
            editableButton.onClick.AddListener(() => onClick());
            if (bookStyle != null)
            {
                bookStyle.ApplyFeatureButton(editableButton.GetComponent<Image>());
            }

            GameObject iconObject = new GameObject("IconPatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(editableButton.transform, false);
            iconObject.GetComponent<Image>().raycastTarget = false;

            GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(editableButton.transform, false);
            TextMeshProUGUI editableTitle = titleObject.GetComponent<TextMeshProUGUI>();
            editableTitle.text = title;
            editableTitle.raycastTarget = false;
            return editableButton;
        }

        if (ShouldCreateBookFeatureShadow(name))
        {
            Image shadow = CreateImage(name + "Shadow", parent, new Color(0.17f, 0.10f, 0.05f, 0.18f));
            shadow.raycastTarget = false;
            SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(6f, -7f), size);
        }

        Button button = CreateButton(parent, name, string.Empty, bookStyle != null ? bookStyle.FeatureButtonColor : new Color(0.99f, 0.94f, 0.81f, 1f), InkColor);
        button.onClick.AddListener(() => onClick());
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        if (bookStyle != null)
        {
            bookStyle.ApplyFeatureButton(button.GetComponent<Image>());
        }

        Image accentBar = CreateImage("Accent", button.transform, accent);
        accentBar.raycastTarget = false;
        SetRect(accentBar.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(12f, size.y));

        Image icon = CreateImage("IconPatch", button.transform, new Color(accent.r, accent.g, accent.b, 0.18f));
        icon.raycastTarget = false;
        SetRect(icon.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -22f), new Vector2(Mathf.Min(76f, size.x * 0.28f), Mathf.Min(76f, size.y - 38f)));

        Text titleText = CreateText("Title", button.transform, title, size.y > 150f ? 34 : 25, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(titleText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.y > 150f ? 120f : 88f, -24f), new Vector2(size.x - (size.y > 150f ? 146f : 108f), 42f));

        Text captionText = CreateText("Caption", button.transform, caption, size.y > 150f ? 19 : 15, FontStyle.Bold, MutedColor, TextAnchor.UpperLeft);
        SetRect(captionText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.y > 150f ? 120f : 88f, -72f), new Vector2(size.x - (size.y > 150f ? 146f : 108f), size.y - 84f));

        Image bottomLine = CreateImage("BottomInkLine", button.transform, new Color(0.34f, 0.21f, 0.10f, 0.22f));
        bottomLine.raycastTarget = false;
        SetRect(bottomLine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -size.y + 18f), new Vector2(size.x - 56f, 3f));
        return button;
    }

    private static bool ShouldCreateBookFeatureShadow(string name)
    {
        switch (name)
        {
            case "SideBagButton":
            case "SideEquipmentButton":
            case "SideShopButton":
            case "FriendsButton":
            case "BottomTaskButton":
            case "RoleButton":
            case "LotteryButton":
                return false;
            default:
                return true;
        }
    }

    private void CreateMapShowcase(Transform parent, Vector2 position, Vector2 size)
    {
        Button map = CreateButton(parent, "EnterMapButton", string.Empty, bookStyle != null ? bookStyle.MapCardColor : new Color(0.79f, 0.86f, 0.73f, 1f), InkColor);
        map.onClick.AddListener(EnterSelectedArea);
        SetRect(map.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        Image image = map.GetComponent<Image>();
        if (bookStyle != null && bookStyle.HasMapCardSprite)
        {
            bookStyle.ApplyMapCard(image);
        }
        else
        {
            Sprite sprite = LoadSpriteFromAsset("Sprites/Arts/png/grass.jpeg");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
            }
        }

        Image wash = CreateImage("MapPaperWash", map.transform, new Color(0.98f, 0.93f, 0.76f, 0.32f));
        wash.raycastTarget = false;
        Stretch(wash.rectTransform);

        Image info = CreateImage("MapInfoPaper", map.transform, new Color(0.99f, 0.94f, 0.81f, 0.94f));
        info.raycastTarget = false;
        SetRect(info.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -218f), new Vector2(438f, 96f));

        selectedAreaTitle = CreateText("SelectedTitle", info.transform, string.Empty, 28, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(selectedAreaTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f, -10f), new Vector2(160f, 34f));

        selectedAreaDescription = CreateText("SelectedDescription", info.transform, string.Empty, 16, FontStyle.Bold, MutedColor, TextAnchor.UpperLeft);
        SetRect(selectedAreaDescription.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f, -45f), new Vector2(290f, 42f));

        selectedAreaHint = CreateText("SelectedHint", info.transform, string.Empty, 16, FontStyle.Bold, PrimaryColor, TextAnchor.MiddleRight);
        SetRect(selectedAreaHint.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(270f, -12f), new Vector2(150f, 30f));

        Button enter = CreateButton(map.transform, "MapEnterStamp", "\u6a21\u62df\u7ecf\u8425", new Color(0.72f, 0.28f, 0.18f, 0.96f), Color.white);
        enter.onClick.AddListener(EnterSelectedArea);
        SetRect(enter.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x - 170f, -244f), new Vector2(136f, 66f));
        ConfigureClearButtonLabel(enter, 22);
    }

    private void CreateModeEntry(
        Transform parent,
        string entryName,
        string buttonName,
        string buttonTitle,
        string spritePath,
        Vector2 position,
        Vector2 size,
        Color buttonColor,
        Action onClick)
    {
        Image entry = CreateImage(entryName, parent, new Color(0.82f, 0.78f, 0.66f, 1f));
        entry.raycastTarget = false;
        SetRect(entry.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        Sprite sprite = LoadSpriteFromAsset(spritePath);
        if (sprite != null)
        {
            entry.sprite = sprite;
            entry.color = Color.white;
            entry.preserveAspect = false;
        }

        Button button = CreateButton(entry.transform, buttonName, buttonTitle, buttonColor, Color.white);
        button.onClick.AddListener(() => onClick());
        SetRect(
            button.GetComponent<RectTransform>(),
            TopLeftAnchor(),
            TopLeftAnchor(),
            TopLeftAnchor(),
            new Vector2((size.x - 178f) * 0.5f, -size.y + 30f),
            new Vector2(178f, 62f));
        ConfigureClearButtonLabel(button, 22);
    }

    private void CreateAreaThumb(Transform parent, AreaInfo area, string spritePath, Vector2 position)
    {
        Button thumb = CreateButton(parent, area.landType + "Thumb", string.Empty, area.color, InkColor);
        thumb.onClick.AddListener(() => SelectAreaAndEnter(area));
        SetRect(thumb.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(124f, 86f));

        Image image = thumb.GetComponent<Image>();
        Sprite sprite = LoadSpriteFromAsset(spritePath);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
        }

        Image labelBg = CreateImage("ThumbLabelBg", thumb.transform, new Color(0.18f, 0.12f, 0.06f, 0.54f));
        labelBg.raycastTarget = false;
        SetRect(labelBg.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -58f), new Vector2(124f, 28f));

        Text label = CreateText("ThumbLabel", thumb.transform, area.name, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(label.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(4f, -60f), new Vector2(116f, 24f));
    }

    private Image CreatePage(string name, Transform parent)
    {
        Image page = CreateImage(name, parent, PageColor);
        VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 38, 34);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return page;
    }

    private void AddBackgroundGrid(Transform parent)
    {
        for (int x = 100; x < 1900; x += 100)
        {
            Image line = CreateImage("GridVertical", parent, new Color(0.94f, 0.75f, 0.48f, 0.10f));
            line.raycastTarget = false;
            SetRect(line.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(x, 0f), new Vector2(2f, 1080f));
        }

        for (int y = 90; y < 1040; y += 90)
        {
            Image line = CreateImage("GridHorizontal", parent, new Color(0.94f, 0.75f, 0.48f, 0.09f));
            line.raycastTarget = false;
            SetRect(line.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -y), new Vector2(1920f, 2f));
        }
    }

    private void AddParchmentSpeckles(Transform parent)
    {
        for (int i = 0; i < 42; i++)
        {
            float x = 95f + ((i * 173) % 1720);
            float y = 72f + ((i * 97) % 890);
            float size = 4f + (i % 4) * 2f;
            Image speckle = CreateImage("ParchmentSpeckle", parent, new Color(0.38f, 0.22f, 0.10f, 0.10f));
            speckle.raycastTarget = false;
            SetRect(speckle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(x, -y), new Vector2(size, size));
        }
    }

    private void AddPageEdgeShading(Transform parent)
    {
        Image top = CreateImage("PageEdgeTop", parent, new Color(0.36f, 0.19f, 0.09f, 0.20f));
        top.raycastTarget = false;
        SetRect(top.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(1920f, 70f));

        Image bottom = CreateImage("PageEdgeBottom", parent, new Color(0.27f, 0.14f, 0.07f, 0.24f));
        bottom.raycastTarget = false;
        SetRect(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(1920f, 80f));

        Image left = CreateImage("PageEdgeLeft", parent, new Color(0.30f, 0.15f, 0.07f, 0.22f));
        left.raycastTarget = false;
        SetRect(left.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(58f, 1080f));

        Image right = CreateImage("PageEdgeRight", parent, new Color(0.30f, 0.15f, 0.07f, 0.22f));
        right.raycastTarget = false;
        SetRect(right.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(58f, 1080f));
    }

    private void AddBookCorners(Transform parent)
    {
        AddCornerLines(parent, new Vector2(0f, -18f), 45f);
        AddCornerLines(parent, new Vector2(1920f, -18f), 135f);
        AddCornerLines(parent, new Vector2(0f, -1062f), -45f);
        AddCornerLines(parent, new Vector2(1920f, -1062f), -135f);
    }

    private void AddCornerLines(Transform parent, Vector2 position, float rotation)
    {
        for (int i = 0; i < 3; i++)
        {
            Image line = CreateImage("BookCornerLine", parent, new Color(0.86f, 0.62f, 0.25f, 0.95f));
            line.raycastTarget = false;
            SetRect(line.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(i * 12f, -i * 12f), new Vector2(120f, 7f));
            line.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }
    }

    private void AddTopMetric(Transform parent, string text, Vector2 position, Vector2 size)
    {
        Image shadow = CreateImage("TopMetricShadow", parent, new Color(0.16f, 0.08f, 0.04f, 0.22f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(5f, -5f), size);

        Image border = CreateImage("TopMetricBorder", parent, new Color(0.42f, 0.22f, 0.11f, 0.95f));
        border.raycastTarget = false;
        SetRect(border.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(-3f, 3f), size + new Vector2(6f, 6f));

        Image box = CreateImage("TopMetric", parent, new Color(0.64f, 0.37f, 0.17f, 0.82f));
        SetRect(box.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        Image shine = CreateImage("TopMetricShine", box.transform, new Color(1f, 0.82f, 0.38f, 0.18f));
        shine.raycastTarget = false;
        SetRect(shine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 15f));

        Text label = CreateText("Text", box.transform, text, 27, FontStyle.Bold, new Color(0.90f, 1f, 0.05f, 1f), TextAnchor.MiddleLeft);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f), new Vector2(-12f, 0f));
    }

    private void AddSmallPlusButton(Transform parent, Vector2 position, Action onClick)
    {
        Image shadow = CreateImage("TopPlusButtonShadow", parent, new Color(0.16f, 0.08f, 0.04f, 0.22f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(3f, -3f), new Vector2(34f, 28f));

        Button button = CreateButton(parent, "TopPlusButton", "+", new Color(1f, 0.95f, 0.78f, 1f), new Color(0.58f, 0.28f, 0.12f, 1f));
        button.onClick.AddListener(() => onClick());
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(34f, 28f));
        ShrinkButtonLabel(button, 20);
    }

    private void AddAvatarDisplay(Transform parent)
    {
        Image shadow = CreateImage("AvatarFrameShadow", parent, new Color(0.16f, 0.08f, 0.04f, 0.28f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(10f, -8f), new Vector2(150f, 130f));

        Image border = CreateImage("AvatarFrameBorder", parent, new Color(0.42f, 0.21f, 0.10f, 1f));
        border.raycastTarget = false;
        SetRect(border.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, 4f), new Vector2(158f, 138f));

        Image frame = CreateImage("AvatarFrame", parent, new Color(0.98f, 0.76f, 0.45f, 1f));
        SetRect(frame.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(4f, 0f), new Vector2(150f, 130f));

        Image portrait = CreateImage("AvatarPortrait", frame.transform, new Color(0.96f, 0.90f, 0.78f, 1f));
        SetRect(portrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(-22f, -38f));

        Image head = CreateImage("AvatarHead", portrait.transform, new Color(0.95f, 0.73f, 0.54f, 1f));
        SetRect(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(42f, 42f));

        Image body = CreateImage("AvatarBody", portrait.transform, new Color(0.28f, 0.61f, 0.82f, 1f));
        SetRect(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(82f, 44f));

        Text name = CreateText("AvatarName", frame.transform, PlayerPrefs.GetString("PlayerNickname", "玩家"), 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-12f, 24f));

        Button button = CreateButton(frame.transform, "AvatarButton", string.Empty, new Color(1f, 1f, 1f, 0f), Color.white);
        button.onClick.AddListener(ShowPersonalInfo);
        Stretch(button.GetComponent<RectTransform>());
        avatarButton = button;
    }

    private void BuildLeftPage(Transform page)
    {
        VerticalLayoutGroup pageLayout = page.GetComponent<VerticalLayoutGroup>();
        if (pageLayout != null)
        {
            pageLayout.enabled = false;
        }

        if (Application.isPlaying || !Application.isPlaying)
        {
            BuildPanelStyleLeftPage(page);
            return;
        }

        Image pageImage = page.GetComponent<Image>();
        if (pageImage != null)
        {
            pageImage.color = new Color(0.49f, 0.30f, 0.15f, 1f);
        }

        AddWoodLine(page, new Vector2(200f, -48f), new Vector2(310f, 5f));
        AddWoodLine(page, new Vector2(520f, -48f), new Vector2(300f, 5f));
        AddWoodLine(page, new Vector2(0f, -508f), new Vector2(165f, 5f));
        AddWoodLine(page, new Vector2(0f, -818f), new Vector2(590f, 5f));

        avatarButton = CreateButton(page, "AvatarButton", "头像", Color.white, new Color(0.95f, 0.18f, 0.25f, 1f));
        avatarButton.onClick.AddListener(ShowPersonalInfo);
        SetRect(avatarButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(80f, 80f));
        ShrinkButtonLabel(avatarButton, 22);

        CreateTopStatBox(page, "Gold:1000", new Vector2(290f, -20f));
        CreateTopStatBox(page, $"Level:{PlayerPrefs.GetInt("PlayerLv", 1)}", new Vector2(530f, -20f));

        Button mailButton = CreateLeftShortcutButton(page, "MailButton", "邮件", new Vector2(20f, -140f), new Color(0.23f, 0.67f, 0.22f, 1f));
        mailButton.onClick.AddListener(ShowMailContent);

        Button bagButton = CreateLeftShortcutButton(page, "BagButton", "背包", new Vector2(20f, -235f), new Color(0.23f, 0.67f, 0.22f, 1f));
        bagButton.onClick.AddListener(ShowBagContent);

        Button settingsButton = CreateLeftShortcutButton(page, "SettingsButton", "设置", new Vector2(20f, -330f), new Color(0.23f, 0.67f, 0.22f, 1f));
        settingsButton.onClick.AddListener(ShowSettingsContent);

        Button noticeButton = CreateLeftShortcutButton(page, "NoticeButton", "公告", new Vector2(20f, -455f), new Color(0.90f, 0.88f, 0.10f, 1f));
        noticeButton.onClick.AddListener(ShowAnnouncement);

        Image contentPanel = CreateImage("LeftContentPanel", page, new Color(0.90f, 0.88f, 0.10f, 1f));
        contentPanelObject = contentPanel.gameObject;
        SetRect(contentPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -198f), new Vector2(420f, 475f));

        contentTitleText = CreateText("ContentTitle", contentPanel.transform, string.Empty, 24, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(contentTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(-70f, 32f));

        Button closeButton = CreateButton(contentPanel.transform, "ContentCloseButton", "X", new Color(0.55f, 0.34f, 0.18f, 0.92f), Color.white);
        closeButton.onClick.AddListener(HideContentPanel);
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(24f, 24f));
        ShrinkButtonLabel(closeButton, 14);

        contentBodyText = CreateText("ContentBody", contentPanel.transform, string.Empty, 16, FontStyle.Normal, InkColor, TextAnchor.UpperLeft);
        SetRect(contentBodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -62f), new Vector2(-40f, 385f));

        statusText = CreateText("Status", page, string.Empty, 16, FontStyle.Normal, DisabledTextColor, TextAnchor.MiddleLeft);
        SetRect(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -690f), new Vector2(420f, 28f));
        ShowAnnouncement();
    }

    private void BuildPanelStyleLeftPage(Transform page)
    {
        Image pageImage = page.GetComponent<Image>();
        if (pageImage != null)
        {
            pageImage.color = new Color(0.49f, 0.30f, 0.15f, 1f);
        }

        avatarButton = CreateButton(page, "AvatarButton", "头像", Color.white, new Color(0.95f, 0.18f, 0.25f, 1f));
        avatarButton.onClick.AddListener(ShowPersonalInfo);
        SetRect(avatarButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, 0f), new Vector2(88f, 70f));
        ShrinkButtonLabel(avatarButton, 22);

        AddWoodLine(page, new Vector2(200f, -48f), new Vector2(310f, 5f));
        AddWoodLine(page, new Vector2(520f, -48f), new Vector2(220f, 5f));
        AddWoodLine(page, new Vector2(0f, -526f), new Vector2(150f, 5f));
        AddWoodLine(page, new Vector2(0f, -818f), new Vector2(590f, 5f));

        CreateTopStatBox(page, "Gold:1000", new Vector2(310f, -20f));
        CreateTopStatBox(page, $"Level:{PlayerPrefs.GetInt("PlayerLv", 1)}", new Vector2(560f, -20f));

        Button mailButton = CreatePanelSideButton(page, "MailButton", "邮件", new Vector2(0f, -88f), new Color(1f, 0.74f, 0.42f, 1f));
        mailButton.onClick.AddListener(ShowMailContent);

        Button bagButton = CreatePanelSideButton(page, "BagButton", "背包", new Vector2(0f, -131f), Color.white);
        bagButton.onClick.AddListener(ShowBagContent);

        Button equipmentButton = CreatePanelSideButton(page, "EquipmentButton", "装备", new Vector2(0f, -174f), new Color(1f, 0.78f, 0.56f, 1f));
        equipmentButton.onClick.AddListener(ShowEquipmentContent);

        Button noticeButton = CreatePanelSideButton(page, "NoticeButton", "公告", new Vector2(0f, -217f), Color.white);
        noticeButton.onClick.AddListener(ShowAnnouncement);

        Button settingsButton = CreatePanelSideButton(page, "SettingsButton", "设置", new Vector2(0f, -260f), Color.white);
        settingsButton.onClick.AddListener(ShowSettingsContent);

        Button rechargeButton = CreatePanelSideButton(page, "RechargeButton", "充值", new Vector2(0f, -303f), new Color(0.50f, 0.30f, 0.16f, 0.92f));
        rechargeButton.onClick.AddListener(ShowRechargeContent);

        Button lotteryButton = CreatePanelSideButton(page, "LotteryButton", "抽奖", new Vector2(0f, -346f), new Color(0.50f, 0.30f, 0.16f, 0.92f));
        lotteryButton.onClick.AddListener(ShowLotteryContent);

        Button dailySignButton = CreatePanelSideButton(page, "DailySignButton", "每日签到", new Vector2(0f, -389f), new Color(0.98f, 0.88f, 0.65f, 1f));
        dailySignButton.onClick.AddListener(ShowDailySignContent);

        Button newbieSignButton = CreatePanelSideButton(page, "NewbieSignButton", "新手签到", new Vector2(0f, -432f), new Color(0.96f, 0.70f, 0.42f, 1f));
        newbieSignButton.onClick.AddListener(ShowNewbieSignContent);

        Button shopButton = CreatePanelSideButton(page, "ShopButton", "商城", new Vector2(0f, -475f), new Color(0.48f, 0.72f, 0.52f, 1f));
        shopButton.onClick.AddListener(ShowShopContent);

        Image panelShadow = CreateImage("LeftContentPanelShadow", page, new Color(0.12f, 0.07f, 0.04f, 0.28f));
        panelShadow.raycastTarget = false;
        SetRect(panelShadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(173f, -186f), new Vector2(585f, 580f));

        Image contentPanel = CreateImage("LeftContentPanel", page, new Color(0.93f, 0.69f, 0.43f, 0.97f));
        contentPanelObject = contentPanel.gameObject;
        SetRect(contentPanel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(165f, -178f), new Vector2(585f, 580f));

        Image panelHeader = CreateImage("LeftContentPanelHeader", contentPanel.transform, new Color(0.60f, 0.32f, 0.16f, 0.20f));
        panelHeader.raycastTarget = false;
        SetRect(panelHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 54f));

        contentTitleText = CreateText("ContentTitle", contentPanel.transform, string.Empty, 24, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(contentTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(-70f, 32f));

        Button closeButton = CreateButton(contentPanel.transform, "ContentCloseButton", "X", new Color(0.55f, 0.34f, 0.18f, 0.92f), Color.white);
        closeButton.onClick.AddListener(HideContentPanel);
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(24f, 24f));
        ShrinkButtonLabel(closeButton, 14);

        contentBodyText = CreateText("ContentBody", contentPanel.transform, string.Empty, 16, FontStyle.Normal, InkColor, TextAnchor.UpperLeft);
        SetRect(contentBodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -62f), new Vector2(-40f, 490f));

        Button battleButton = CreatePanelBottomButton(page, "BattleButton", "对战", new Vector2(0f, -828f));
        battleButton.onClick.AddListener(ShowBattleContent);
        statusText = CreateText("Status", page, string.Empty, 16, FontStyle.Normal, DisabledTextColor, TextAnchor.MiddleLeft);
        SetRect(statusText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(220f, -828f), new Vector2(520f, 28f));
        ShowAnnouncement();
    }

    private Button CreatePanelSideButton(Transform parent, string name, string text, Vector2 position, Color color)
    {
        Color normalColor = color;
        if (Mathf.Approximately(color.r, 1f) && Mathf.Approximately(color.g, 1f) && Mathf.Approximately(color.b, 1f))
        {
            normalColor = new Color(0.98f, 0.88f, 0.65f, 1f);
        }

        Color textColor = normalColor.r < 0.65f ? new Color(1f, 0.86f, 0.58f, 1f) : new Color(0.56f, 0.13f, 0.08f, 1f);

        Image shadow = CreateImage(name + "Shadow", parent, new Color(0.18f, 0.10f, 0.05f, 0.20f));
        shadow.raycastTarget = false;
        SetRect(shadow.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(4f, -4f), new Vector2(76f, 40f));

        Button button = CreateButton(parent, name, text, normalColor, textColor);
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(76f, 40f));
        ShrinkButtonLabel(button, 18);
        return button;
    }

    private Button CreatePanelBottomButton(Transform parent, string name, string text, Vector2 position)
    {
        Image shadow = CreateImage(name + "Shadow", parent, new Color(0.18f, 0.10f, 0.05f, 0.20f));
        shadow.raycastTarget = false;
        SetRect(shadow.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(3f, -3f), new Vector2(58f, 40f));

        Button button = CreateButton(parent, name, text, new Color(0.96f, 0.70f, 0.42f, 1f), new Color(0.50f, 0.12f, 0.08f, 1f));
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(58f, 40f));
        ShrinkButtonLabel(button, 18);
        return button;
    }

    private Vector2 TopLeftAnchor()
    {
        return new Vector2(0f, 1f);
    }

    private void AddWoodLine(Transform parent, Vector2 position, Vector2 size)
    {
        Image shadow = CreateImage("WoodLineShadow", parent, new Color(0.18f, 0.10f, 0.05f, 0.16f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(2f, -2f), size);

        Image line = CreateImage("WoodLine", parent, new Color(0.76f, 0.43f, 0.21f, 0.72f));
        line.raycastTarget = false;
        SetRect(line.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
    }

    private void CreateTopStatBox(Transform parent, string text, Vector2 position)
    {
        Image shadow = CreateImage("TopStatBoxShadow", parent, new Color(0.18f, 0.10f, 0.05f, 0.22f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position + new Vector2(4f, -4f), new Vector2(162f, 42f));

        Image box = CreateImage("TopStatBox", parent, new Color(0.52f, 0.32f, 0.17f, 0.86f));
        SetRect(box.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(162f, 42f));

        Text label = CreateText("Text", box.transform, text, 28, FontStyle.Bold, new Color(0.93f, 0.96f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, 0f));
    }

    private Button CreateLeftShortcutButton(Transform parent, string name, string text, Vector2 position, Color color)
    {
        Button button = CreateButton(parent, name, text, color, new Color(0.95f, 0.18f, 0.25f, 1f));
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(80f, 80f));
        ShrinkButtonLabel(button, 24);
        return button;
    }

    private void BuildTopInfoBar(Transform page)
    {
        GameObject row = CreateLayoutObject("TopInfoBar", page);
        SetLayout(row, 0f, 96f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        Image avatarBlock = CreateSoftBlock("AvatarBlock", row.transform);
        SetLayout(avatarBlock.gameObject, 88f, 96f);

        VerticalLayoutGroup avatarLayout = avatarBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        avatarLayout.padding = new RectOffset(8, 8, 8, 6);
        avatarLayout.spacing = 3f;
        avatarLayout.childControlHeight = false;
        avatarLayout.childControlWidth = true;
        avatarLayout.childForceExpandHeight = false;

        avatarButton = CreateButton(avatarBlock.transform, "AvatarButton", "头像", new Color(1f, 1f, 1f, 0.92f), new Color(0.90f, 0.16f, 0.22f, 1f));
        avatarButton.onClick.AddListener(ShowPersonalInfo);
        SetLayout(avatarButton.gameObject, 0f, 54f);
        ShrinkButtonLabel(avatarButton, 16);

        Text avatarName = CreateText("AvatarName", avatarBlock.transform, PlayerPrefs.GetString("PlayerNickname", "玩家"), 12, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);
        SetLayout(avatarName.gameObject, 0f, 16f);

        Image resourceBlock = CreateSoftBlock("ResourceInfo", row.transform);
        SetFlexibleLayout(resourceBlock.gameObject, 96f);

        HorizontalLayoutGroup resourceLayout = resourceBlock.gameObject.AddComponent<HorizontalLayoutGroup>();
        resourceLayout.padding = new RectOffset(20, 20, 18, 18);
        resourceLayout.spacing = 22f;
        resourceLayout.childAlignment = TextAnchor.MiddleLeft;
        resourceLayout.childControlHeight = false;
        resourceLayout.childControlWidth = false;
        resourceLayout.childForceExpandHeight = false;
        resourceLayout.childForceExpandWidth = false;

        AddTopStat(resourceBlock.transform, $"Gold:{PlayerPrefs.GetInt("Gold", 1000)}");
        AddTopStat(resourceBlock.transform, $"Level:{PlayerPrefs.GetInt("PlayerLv", 1)}");
    }

    private void AddTopStat(Transform parent, string value)
    {
        Image stat = CreateImage("TopStat", parent, new Color(0.62f, 0.39f, 0.20f, 0.82f));
        SetLayout(stat.gameObject, 168f, 42f);

        Text text = CreateText("Text", stat.transform, value, 22, FontStyle.Bold, new Color(0.93f, 0.96f, 0.08f, 1f), TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
    }

    private void BuildMainContentArea(Transform page)
    {
        GameObject row = CreateLayoutObject("MainContentRow", page);
        SetLayout(row, 0f, 582f);

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        BuildShortcutColumn(row.transform);
        BuildContentPanel(row.transform);
    }

    private void BuildShortcutColumn(Transform parent)
    {
        Image buttonBlock = CreateSoftBlock("ShortcutColumn", parent);
        SetLayout(buttonBlock.gameObject, 96f, 582f);

        VerticalLayoutGroup buttonLayout = buttonBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        buttonLayout.padding = new RectOffset(10, 10, 14, 14);
        buttonLayout.spacing = 10f;
        buttonLayout.childControlHeight = false;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandHeight = false;

        Button mailButton = CreateShortcutButton(buttonBlock.transform, "MailButton", "邮件", new Color(0.25f, 0.68f, 0.28f, 1f));
        mailButton.onClick.AddListener(ShowMailContent);

        Button bagButton = CreateShortcutButton(buttonBlock.transform, "BagButton", "背包", new Color(0.26f, 0.64f, 0.24f, 1f));
        bagButton.onClick.AddListener(ShowBagContent);

        Button settingsButton = CreateShortcutButton(buttonBlock.transform, "SettingsButton", "设置", new Color(0.24f, 0.62f, 0.24f, 1f));
        settingsButton.onClick.AddListener(ShowSettingsContent);

        Button noticeButton = CreateShortcutButton(buttonBlock.transform, "NoticeButton", "公告", new Color(0.90f, 0.86f, 0.12f, 1f), new Color(0.95f, 0.18f, 0.25f, 1f));
        noticeButton.onClick.AddListener(ShowAnnouncement);
    }

    private Button CreateShortcutButton(Transform parent, string name, string text, Color color)
    {
        return CreateShortcutButton(parent, name, text, color, new Color(0.95f, 0.18f, 0.25f, 1f));
    }

    private Button CreateShortcutButton(Transform parent, string name, string text, Color color, Color textColor)
    {
        Button button = CreateButton(parent, name, text, color, textColor);
        SetLayout(button.gameObject, 0f, 62f);
        ShrinkButtonLabel(button, 18);
        return button;
    }

    private void BuildContentPanel(Transform parent)
    {
        Image contentPanel = CreateImage("LeftContentPanel", parent, new Color(0.90f, 0.88f, 0.10f, 0.92f));
        SetFlexibleLayout(contentPanel.gameObject, 582f);

        VerticalLayoutGroup contentLayout = contentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(28, 28, 24, 24);
        contentLayout.spacing = 14f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        contentTitleText = CreateText("ContentTitle", contentPanel.transform, string.Empty, 26, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(contentTitleText.gameObject, 0f, 34f);

        contentBodyText = CreateText("ContentBody", contentPanel.transform, string.Empty, 18, FontStyle.Normal, InkColor, TextAnchor.UpperLeft);
        SetLayout(contentBodyText.gameObject, 0f, 470f);
    }

    private void ShowPersonalInfo()
    {
        ShowAccountProfilePanel();
    }

    private void ShowFriendsContent()
    {
        ShowFriendsPanel();
    }

    private void ShowMailContent()
    {
        ShowMailContent(0L);
    }

    private void ShowMailContent(long targetMailId)
    {
        MarkTaskProgress("TaskMailOpened", "TaskMailDate");
        if (targetMailId > 0L)
        {
            selectedMailId = targetMailId;
            pendingMailRouteId = targetMailId;
        }
        mailShowingNotifications = false;

        SetLeftContent("邮件", "正在读取服务器邮件...", "正在请求邮件接口");
        StartCoroutine(GetMailList());
    }

    private void ShowBagContent()
    {
        MarkTaskProgress("TaskBagOpened");
        activeInventoryCategory = "all";
        selectedInventoryItemKey = string.Empty;
        ResetResourceSaleForInventory();

        ShowInventoryPanel(new InventoryItemResponse[0], true, "正在读取服务器背包...");
        StartCoroutine(GetInventoryList(false));
    }

    private void ShowSettingsContent()
    {
        HideEquipmentPanel();
        HideShopPanel();
        if (TryShowSettingsScenePanel("设置修改后会即时生效"))
        {
            return;
        }

        SetLeftContent(
            "设置",
            $"音乐：{(GameSettingsRuntime.MusicEnabled ? "开启" : "关闭")}  {GameSettingsRuntime.MusicVolume}%\n" +
            $"音效：{(GameSettingsRuntime.SoundEnabled ? "开启" : "关闭")}  {GameSettingsRuntime.SoundVolume}%",
            "声音设置修改后会即时生效"
        );
    }

    private void ShowEquipmentContent()
    {
        ShowEquipmentPanel();
    }

    private void ShowCharacterContent()
    {
        ShowRolePanel();
    }

    private void ShowRechargeContent()
    {
        MarkTaskProgress("TaskRechargeOpened");

        SetLeftContent("充值", "正在读取充值商品...", "正在请求充值商品接口");
        StartCoroutine(GetPaymentProducts());
    }

    private void ShowLotteryContent()
    {
        ShowLotteryContent(string.Empty, 0);
    }

    private void ShowLotteryContent(string poolCode, int highlightNpcId)
    {
        MarkTaskProgress("TaskLotteryOpened");
        if (HasAuthSession())
        {
            OpenServerLottery(poolCode, highlightNpcId);
            return;
        }

        EnsureLotteryChanceCount();
        ShowLotteryPanel("点击开始消耗 1 次机会", string.Empty, 0, false);
    }

    private void PerformLotteryDraw()
    {
        if (HasAuthSession())
        {
            BeginServerCardDraw("SINGLE");
            return;
        }

        if (lotteryRolling)
        {
            return;
        }

        int chances = EnsureLotteryChanceCount();
        if (chances <= 0)
        {
            ShowLotteryPanel("机会不足：抽奖次数已用完", string.Empty, 0, false);
            return;
        }

        PlayerPrefs.SetInt(LotteryChanceKey, chances - 1);
        PlayerPrefs.Save();

        string rewardName = "金币";
        string rewardKey = "Gold";
        int rewardValue = 80;
        int roll = UnityEngine.Random.Range(0, 100);
        if (roll >= 80)
        {
            rewardName = "铁矿";
            rewardKey = "Iron";
            rewardValue = 120;
        }
        else if (roll >= 55)
        {
            rewardName = "石材";
            rewardKey = "Stone";
            rewardValue = 150;
        }
        else if (roll >= 30)
        {
            rewardName = "木材";
            rewardKey = "Wood";
            rewardValue = 150;
        }

        StartCoroutine(PlayLotteryFlipEffect(rewardName, rewardKey, rewardValue));
    }

    private IEnumerator PlayLotteryFlipEffect(string finalRewardName, string finalRewardKey, int finalRewardValue)
    {
        lotteryRolling = true;
        string[] previewNames = { "金币", "木材", "石材", "铁矿", "抽奖券", "英雄箱" };
        int[] previewValues = { 80, 150, 150, 120, 1, 1 };

        for (int i = 0; i < 18; i++)
        {
            int index = (i + UnityEngine.Random.Range(0, previewNames.Length)) % previewNames.Length;
            ShowLotteryPanel("奖品翻动中...", previewNames[index], previewValues[index], true, true);
            yield return new WaitForSeconds(0.045f + i * 0.006f);
        }

        ShowLotteryPanel("即将开奖...", finalRewardName, finalRewardValue, true, true);
        yield return new WaitForSeconds(0.22f);

        lotteryRolling = false;
        AddLocalResource(finalRewardKey, finalRewardValue);
        PlayerPrefs.SetString("TaskLotteryDate", GetTaskDate());
        PlayerPrefs.Save();
        ShowLotteryPanel($"获得：{finalRewardName} x{finalRewardValue}", finalRewardName, finalRewardValue, true);
    }

    private void ShowDailySignContent()
    {
        if (!HasAuthSession())
        {
            SetStatus("请先登录账号");
            return;
        }

        OpenServerSignIn("MONTHLY");
    }

    private void ShowNewbieSignContent()
    {
        if (!HasAuthSession())
        {
            SetStatus("请先登录账号");
            return;
        }

        OpenServerSignIn("NEWBIE");
    }

    private void ShowTaskContent()
    {
        if (HasAuthSession())
        {
            OpenServerTasks("daily");
            return;
        }

        ShowTaskPanel("daily", "每日任务会在次日刷新，完成后记得领取奖励");
    }

    private void ShowTaskPanel(string category, string message)
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("任务列表", "任务面板暂时无法显示。", message);
            return;
        }

        activeTaskCategory = category;
        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();
        HideAccountProfilePanel();
        HideFriendsPanel();

        TaskDisplayItem[] tasks = BuildTaskItems(category) ?? new TaskDisplayItem[0];
        int completedCount = 0;
        for (int i = 0; i < tasks.Length; i++)
        {
            if (tasks[i].IsComplete)
            {
                completedCount++;
            }
        }

        if (TryShowTaskScenePanel(category, message, tasks, completedCount))
        {
            return;
        }

        Image shadow = CreateImage("TaskPanelShadow", mainPageTransform, new Color(0.10f, 0.05f, 0.02f, 0.48f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(256f, -110f), new Vector2(1418f, 808f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("TaskPanel", mainPageTransform, new Color(0.42f, 0.22f, 0.10f, 1f));
        taskPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(240f, -94f), new Vector2(1410f, 800f));

        Image paper = CreateImage("TaskPaper", panel.transform, new Color(1f, 0.88f, 0.66f, 1f));
        SetRect(paper.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(14f, -14f), new Vector2(1382f, 772f));
        paper.raycastTarget = false;

        Image header = CreateImage("TaskHeader", panel.transform, new Color(0.48f, 0.17f, 0.10f, 1f));
        SetRect(header.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -22f), new Vector2(1366f, 68f));
        header.raycastTarget = false;

        Image headerHighlight = CreateImage("TaskHeaderHighlight", header.transform, new Color(1f, 0.74f, 0.32f, 0.24f));
        SetRect(headerHighlight.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(1366f, 10f));
        headerHighlight.raycastTarget = false;

        Text title = CreateMailText("TaskTitle", panel.transform, "任务列表", 36, FontStyle.Bold, new Color(1f, 0.86f, 0.46f, 1f), TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(58f, -30f), new Vector2(230f, 48f));

        Text subtitle = CreateMailText("TaskSubtitle", panel.transform, "完成委托，领取领地成长物资", 18, FontStyle.Bold, new Color(1f, 0.90f, 0.70f, 0.88f), TextAnchor.MiddleLeft);
        SetRect(subtitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(286f, -38f), new Vector2(360f, 34f));

        Text totalProgress = CreateMailText("TaskTotalProgress", panel.transform, $"本页进度  {completedCount}/{tasks.Length}", 20, FontStyle.Bold, new Color(1f, 0.88f, 0.58f, 1f), TextAnchor.MiddleRight);
        SetRect(totalProgress.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(970f, -38f), new Vector2(300f, 34f));

        Button close = CreateButton(panel.transform, "TaskCloseButton", "X", new Color(0.78f, 0.27f, 0.16f, 1f), Color.white);
        close.onClick.AddListener(HideTaskPanel);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1322f, -30f), new Vector2(50f, 50f));
        ConfigureClearButtonLabel(close, 24);

        Image sidebar = CreateImage("TaskSidebar", panel.transform, new Color(0.72f, 0.43f, 0.20f, 0.92f));
        SetRect(sidebar.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -106f), new Vector2(210f, 604f));

        AddTaskCategoryButton(sidebar.transform, "daily", "每日任务", new Vector2(16f, -24f), category == "daily");
        AddTaskCategoryButton(sidebar.transform, "main", "主线任务", new Vector2(16f, -128f), category == "main");
        AddTaskCategoryButton(sidebar.transform, "growth", "成长任务", new Vector2(16f, -232f), category == "growth");
        AddTaskCategoryButton(sidebar.transform, "activity", "活动任务", new Vector2(16f, -336f), category == "activity");

        for (int i = 0; i < tasks.Length; i++)
        {
            AddTaskRow(panel.transform, tasks[i], i, new Vector2(258f, -184f - i * 131f));
        }

        Image footer = CreateImage("TaskFooter", panel.transform, new Color(0.54f, 0.28f, 0.12f, 0.90f));
        SetRect(footer.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(258f, -718f), new Vector2(1092f, 42f));
        Text footerText = CreateMailText("TaskFooterText", footer.transform, tasks.Length == 0 ? "\u6682\u65e0\u4efb\u52a1" : message, 17, FontStyle.Bold, new Color(1f, 0.88f, 0.64f, 1f), TextAnchor.MiddleCenter);
        SetRect(footerText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f, -4f), new Vector2(1056f, 32f));

        SetStatus(message);
    }

    private void AddTaskCategoryButton(Transform parent, string category, string label, Vector2 position, bool selected)
    {
        Color buttonColor = selected ? new Color(0.88f, 0.35f, 0.18f, 1f) : new Color(0.98f, 0.77f, 0.43f, 1f);
        Color labelColor = selected ? Color.white : new Color(0.38f, 0.18f, 0.08f, 1f);
        Button button = CreateButton(parent, "TaskCategory_" + category, label, buttonColor, labelColor);
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(178f, 84f));
        ConfigureClearButtonLabel(button, 22);
        button.onClick.AddListener(() => ShowTaskPanel(category, $"已切换到{label}"));

        if (selected)
        {
            Image marker = CreateImage("SelectedMarker", button.transform, new Color(1f, 0.89f, 0.42f, 1f));
            SetRect(marker.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-8f, 0f), new Vector2(14f, 38f));
            marker.raycastTarget = false;
        }
    }

    private void AddTaskRow(Transform parent, TaskDisplayItem task, int index, Vector2 position)
    {
        bool claimed = IsTaskClaimed(task);
        Color rowColor = claimed
            ? new Color(0.83f, 0.78f, 0.65f, 1f)
            : task.IsComplete
                ? new Color(0.84f, 0.90f, 0.62f, 1f)
                : index % 2 == 0
                    ? new Color(1f, 0.93f, 0.76f, 1f)
                    : new Color(0.96f, 0.86f, 0.66f, 1f);

        Image rowShadow = CreateImage("TaskRowShadow_" + task.id, parent, new Color(0.26f, 0.12f, 0.05f, 0.24f));
        SetRect(rowShadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(5f, -6f), new Vector2(1092f, 118f));
        rowShadow.raycastTarget = false;

        Image row = CreateImage("TaskRow_" + task.id, parent, rowColor);
        SetRect(row.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(1092f, 118f));

        Image accent = CreateImage("TaskRowAccent", row.transform, task.IsComplete ? new Color(0.35f, 0.58f, 0.23f, 1f) : new Color(0.82f, 0.35f, 0.16f, 1f));
        SetRect(accent.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(9f, 118f));
        accent.raycastTarget = false;

        Text taskTitle = CreateMailText("TaskItemTitle", row.transform, task.title, 22, FontStyle.Bold, new Color(0.30f, 0.13f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetRect(taskTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -20f), new Vector2(585f, 34f));

        Text description = CreateMailText("TaskItemDescription", row.transform, task.description, 16, FontStyle.Normal, new Color(0.43f, 0.31f, 0.21f, 1f), TextAnchor.UpperLeft);
        SetRect(description.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -58f), new Vector2(585f, 34f));

        Text progressTitle = CreateMailText("TaskProgressTitle", row.transform, task.IsComplete ? "任务完成" : "任务进度", 16, FontStyle.Bold, new Color(0.40f, 0.22f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetRect(progressTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(494f, -15f), new Vector2(190f, 24f));

        Image progressBg = CreateImage("TaskProgressBg", row.transform, new Color(0.34f, 0.20f, 0.11f, 0.65f));
        SetRect(progressBg.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(494f, -49f), new Vector2(190f, 18f));
        float progressRatio = Mathf.Clamp01(task.current / (float)Mathf.Max(1, task.target));
        Image progressFill = CreateImage("TaskProgressFill", row.transform, task.IsComplete ? new Color(0.42f, 0.70f, 0.23f, 1f) : new Color(0.95f, 0.58f, 0.18f, 1f));
        SetRect(progressFill.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(494f, -49f), new Vector2(190f * progressRatio, 18f));
        progressFill.raycastTarget = false;

        Text progressValue = CreateMailText("TaskProgressValue", row.transform, $"{Mathf.Min(task.current, task.target)}/{task.target}", 16, FontStyle.Bold, new Color(0.39f, 0.22f, 0.11f, 1f), TextAnchor.MiddleCenter);
        SetRect(progressValue.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(494f, -72f), new Vector2(190f, 24f));

        Image reward = CreateImage("TaskReward", row.transform, new Color(0.99f, 0.79f, 0.37f, 0.88f));
        SetRect(reward.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -101f), new Vector2(590f, 30f));
        AddTaskRewardIcon(reward.transform, task.rewardKey, new Vector2(0f, -1f), new Vector2(30f, 30f));
        Text rewardText = CreateMailText("TaskRewardText", reward.transform, string.IsNullOrEmpty(task.rewardSummary) ? $"{task.rewardName} x{task.rewardAmount}" : task.rewardSummary, 16, FontStyle.Bold, new Color(0.38f, 0.17f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetRect(rewardText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(40f, -1f), new Vector2(540f, 30f));

        string buttonLabel = claimed ? "已领取" : task.IsComplete ? "领取" : "未完成";
        Color buttonColor = claimed || !task.IsComplete
            ? new Color(0.55f, 0.53f, 0.47f, 1f)
            : task.IsComplete
                ? new Color(0.38f, 0.66f, 0.24f, 1f)
                : new Color(0.83f, 0.34f, 0.16f, 1f);
        Button actionButton = CreateButton(row.transform, "TaskActionButton", buttonLabel, buttonColor, Color.white);
        SetRect(actionButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(922f, -20f), new Vector2(150f, 78f));
        ConfigureClearButtonLabel(actionButton, 25);

        if (claimed || !task.IsComplete)
        {
            actionButton.interactable = false;
        }
        else if (task.IsComplete)
        {
            actionButton.onClick.AddListener(() => ClaimTaskReward(task));
        }
        else
        {
            actionButton.interactable = false;
        }
    }

    private void AddTaskRewardIcon(Transform parent, string rewardKey, Vector2 position, Vector2 size)
    {
        if (rewardKey == "EquipmentChest")
        {
            AddMiniChest(parent, position, size);
            return;
        }

        if (rewardKey == "LotteryTicket")
        {
            AddSealToken(parent, position, size);
            return;
        }

        Color color = new Color(1f, 0.71f, 0.12f, 1f);
        if (rewardKey == "Wood")
        {
            color = new Color(0.30f, 0.64f, 0.28f, 1f);
        }
        else if (rewardKey == "Stone")
        {
            color = new Color(0.55f, 0.58f, 0.62f, 1f);
        }
        else if (rewardKey == "Crystal" || rewardKey == "SoulCrystal")
        {
            color = new Color(0.28f, 0.67f, 0.95f, 1f);
        }

        AddMiniGem(parent, position, size, color);
    }

    private TaskDisplayItem[] BuildTaskItems(string category)
    {
        if (HasAuthSession())
        {
            return BuildServerTaskItems(category);
        }

        string today = GetTaskDate();
        int newbieDays = PlayerPrefs.GetInt("NewbieSignDays", 0);

        if (category == "main")
        {
            return new[]
            {
                new TaskDisplayItem("main_bag", "整理随身背包", "打开背包，确认领地物资与道具。", PlayerPrefs.GetInt("TaskBagOpened", 0), 1, "Gold", "金币", 200, false, ShowBagContent),
                new TaskDisplayItem("main_mail", "查看系统邮件", "查看一次系统邮件或活动通知。", PlayerPrefs.GetInt("TaskMailOpened", 0), 1, "Wood", "木材", 150, false, ShowMailContent),
                new TaskDisplayItem("main_map", "踏入第一块领地", "选择任意区域并进入地图。", PlayerPrefs.GetInt("TaskMapEntered", 0), 1, "EquipmentChest", "装备宝箱", 1, false, EnterSelectedArea)
            };
        }

        if (category == "growth")
        {
            return new[]
            {
                new TaskDisplayItem("growth_level", "领主等级达到 Lv.2", "通过经营与对战积累成长经验。", PlayerPrefs.GetInt("PlayerLv", 1), 2, "Gold", "金币", 300, false, ShowBattleContent),
                new TaskDisplayItem("growth_chest", "获得 1 个装备宝箱", "完成签到或主线任务获得宝箱。", PlayerPrefs.GetInt("EquipmentChest", 0), 1, "LotteryTicket", "抽奖券", 1, false, ShowBagContent)
            };
        }

        if (category == "activity")
        {
            return new[]
            {
                new TaskDisplayItem("activity_newbie_one", "领取新手签到奖励", "完成新手签到第 1 天。", newbieDays, 1, "Gold", "金币", 100, false, ShowNewbieSignContent),
                new TaskDisplayItem("activity_newbie_three", "完成三日新手签到", "累计领取全部三天新手奖励。", newbieDays, 3, "LotteryTicket", "抽奖券", 1, false, ShowNewbieSignContent),
                new TaskDisplayItem("activity_lottery", "浏览幸运抽奖机", "打开抽奖中心并查看当前机会。", PlayerPrefs.GetInt("TaskLotteryOpened", 0), 1, "Gold", "金币", 100, false, ShowLotteryContent),
                new TaskDisplayItem("activity_recharge", "浏览充值商店", "打开充值页面查看钻石档位。", PlayerPrefs.GetInt("TaskRechargeOpened", 0), 1, "Crystal", "钻石", 10, false, ShowRechargeContent)
            };
        }

        return new[]
        {
            new TaskDisplayItem("daily_sign", "完成每日签到", "领取今日签到奖励，连续签到可得稀有物资。", PlayerPrefs.GetString("DailySignDate", string.Empty) == today ? 1 : 0, 1, "Gold", "金币", 100, true, ShowDailySignContent),
            new TaskDisplayItem("daily_mail", "查看今日邮件", "查看系统邮件和最新领地消息。", PlayerPrefs.GetString("TaskMailDate", string.Empty) == today ? 1 : 0, 1, "Wood", "木材", 80, true, ShowMailContent),
            new TaskDisplayItem("daily_lottery", "完成一次抽奖", "使用一次机会并等待奖品翻出。", PlayerPrefs.GetString("TaskLotteryDate", string.Empty) == today ? 1 : 0, 1, "LotteryTicket", "抽奖券", 1, true, ShowLotteryContent),
            new TaskDisplayItem("daily_map", "进入任意地图", "选择领地区域并进入一次地图。", PlayerPrefs.GetString("TaskMapDate", string.Empty) == today ? 1 : 0, 1, "Stone", "石材", 100, true, EnterSelectedArea)
        };
    }

    private string GetTaskCategoryTitle(string category)
    {
        if (category == "main") return HasAuthSession() ? "每周任务" : "主线任务";
        if (category == "growth") return "成长任务";
        if (category == "activity") return "活动任务";
        return "每日任务";
    }

    private string GetTaskCategoryNote(string category)
    {
        if (category == "main") return HasAuthSession() ? "每周任务按服务端北京时间周期刷新" : "推进领地基础功能，奖励仅可领取一次";
        if (category == "growth") return "达到资源与等级目标后即可领取";
        if (category == "activity") return "参与签到、抽奖与商店活动";
        return HasAuthSession() ? "每日任务按服务端北京时间周期刷新" : "每日 00:00 刷新完成状态与奖励";
    }

    private bool IsTaskClaimed(TaskDisplayItem task)
    {
        if (task != null && task.serverBacked)
        {
            return string.Equals(task.serverStatus, "CLAIMED", StringComparison.OrdinalIgnoreCase);
        }

        return PlayerPrefs.GetInt(GetTaskClaimKey(task), 0) == 1;
    }

    private string GetTaskClaimKey(TaskDisplayItem task)
    {
        return "TaskClaim_" + task.id + (task.daily ? "_" + DateTime.Now.ToString("yyyyMMdd") : string.Empty);
    }

    private void ClaimTaskReward(TaskDisplayItem task)
    {
        if (!task.IsComplete || IsTaskClaimed(task))
        {
            return;
        }

        if (HasAuthSession())
        {
            BeginClaimServerTask(task);
            return;
        }

        AddLocalResource(task.rewardKey, task.rewardAmount);
        PlayerPrefs.SetInt(GetTaskClaimKey(task), 1);
        PlayerPrefs.Save();
        ShowTaskPanel(activeTaskCategory, $"领取成功：{task.rewardName} x{task.rewardAmount}");
    }

    private void MarkTaskProgress(string permanentKey, string dailyKey = null)
    {
        PlayerPrefs.SetInt(permanentKey, 1);
        if (!string.IsNullOrEmpty(dailyKey))
        {
            PlayerPrefs.SetString(dailyKey, GetTaskDate());
        }

        PlayerPrefs.Save();
    }

    private string GetTaskDate()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    private void ShowInventoryPanel(InventoryItemResponse[] items, bool serverBacked, string status)
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("背包", status, status);
            return;
        }

        currentInventoryItems = items ?? new InventoryItemResponse[0];
        inventoryServerBacked = serverBacked;
        inventoryStatusMessage = status ?? string.Empty;
        selectedInventoryItemKey = string.Empty;
        if (serverBacked)
        {
            SyncInventoryResourceValues(currentInventoryItems);
        }
        RenderInventoryPanel();
    }

    private void SyncInventoryResourceValues(InventoryItemResponse[] items)
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            InventoryItemResponse item = items[i];
            string key = NormalizeInventoryKey(GetInventoryItemKey(item));
            if (key == "gold" || key == "wood" || key == "stone" || key == "iron" || key == "diamond" || key == "crystal" || key == "soulcrystal" || key == "soul_crystal" || key == "purification_agent" || key == "purificationagent")
            {
                string localKey = key.Replace("_", string.Empty);
                if (localKey == "diamond" || localKey == "crystal")
                {
                    localKey = "Crystal";
                }
                else if (localKey == "soulcrystal")
                {
                    localKey = "SoulCrystal";
                }
                else if (localKey == "purificationagent")
                {
                    localKey = "PurificationAgent";
                }
                else
                {
                    localKey = char.ToUpper(localKey[0]) + localKey.Substring(1);
                }

                PlayerPrefs.SetInt(localKey, Mathf.Max(0, item.quantity));
            }
        }

        PlayerPrefs.Save();
        RefreshHomeResourceMetrics();
        QueueBackendInventoryRefresh();
    }

    private void RenderInventoryPanel()
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("背包", inventoryStatusMessage, inventoryStatusMessage);
            return;
        }

        List<InventoryItemResponse> visibleItems = GetFilteredInventoryItems();
        int selectedIndex = FindFilteredInventoryIndex(visibleItems, selectedInventoryItemKey);
        if (selectedIndex < 0)
        {
            selectedInventoryItemKey = string.Empty;
        }

        int ownedCount = GetOwnedInventoryCount();
        int unlockedSlots = Mathf.Max(GetInventoryUnlockedSlots(), Mathf.Min(InventoryCapacity, visibleItems.Count));

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        if (TryShowInventoryScenePanel(visibleItems, selectedIndex, ownedCount, unlockedSlots))
        {
            SetStatus(inventoryStatusMessage);
            return;
        }

        InventoryPanelView inventoryPrefab = Resources.Load<InventoryPanelView>("UI/InventoryPanel");
        if (inventoryPrefab != null)
        {
            EnsureInventoryInputBlocker(mainPageTransform);
            inventoryPanelView = Instantiate(inventoryPrefab, mainPageTransform, false);
            inventoryPanelObject = inventoryPanelView.gameObject;
            ConfigureInventoryPrefab(inventoryPanelView, visibleItems, selectedIndex, ownedCount, unlockedSlots);
            SetStatus(inventoryStatusMessage);
            return;
        }

        EnsureInventoryInputBlocker(mainPageTransform);

        Image panel = CreateImage("InventoryPanel", mainPageTransform, new Color(0.39f, 0.19f, 0.07f, 1f));
        inventoryPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(100f, -78f), new Vector2(1720f, 900f));

        Image header = CreateImage("InventoryHeader", panel.transform, new Color(0.48f, 0.23f, 0.08f, 1f));
        SetRect(header.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(16f, -14f), new Vector2(1688f, 74f));
        header.raycastTarget = false;

        Image headerLight = CreateImage("InventoryHeaderLight", header.transform, new Color(1f, 0.71f, 0.22f, 0.22f));
        SetRect(headerLight.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(1688f, 9f));
        headerLight.raycastTarget = false;

        Text title = CreateMailText("InventoryTitle", panel.transform, "仓库", 36, FontStyle.Bold, new Color(1f, 0.88f, 0.47f, 1f), TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(120f, -28f), new Vector2(170f, 52f));

        Text subtitle = CreateMailText("InventorySubtitle", panel.transform, "领地物资与道具管理", 17, FontStyle.Bold, new Color(1f, 0.89f, 0.67f, 0.88f), TextAnchor.MiddleLeft);
        SetRect(subtitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(298f, -30f), new Vector2(280f, 42f));

        Button close = CreateButton(panel.transform, "InventoryCloseButton", string.Empty, Color.white, Color.white);
        close.onClick.AddListener(HideInventoryPanel);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1537f, -48f), new Vector2(75f, 77f));

        Image sidebar = CreateImage("InventorySidebar", panel.transform, new Color(0.69f, 0.38f, 0.13f, 0.96f));
        SetRect(sidebar.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(24f, -108f), new Vector2(218f, 672f));

        AddInventoryCategoryButton(sidebar.transform, "all", "全部", new Vector2(16f, -18f), activeInventoryCategory == "all");
        AddInventoryCategoryButton(sidebar.transform, "equipment", "装备", new Vector2(16f, -106f), activeInventoryCategory == "equipment");
        AddInventoryCategoryButton(sidebar.transform, "medicine", "药品", new Vector2(16f, -194f), activeInventoryCategory == "medicine");
        AddInventoryCategoryButton(sidebar.transform, "other", "其它", new Vector2(16f, -282f), activeInventoryCategory == "other");

        Button expand = CreateButton(sidebar.transform, "InventoryExpandButton", "扩充格子", new Color(0.88f, 0.58f, 0.20f, 1f), new Color(0.35f, 0.15f, 0.04f, 1f));
        expand.onClick.AddListener(ExpandInventorySlots);
        SetRect(expand.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(16f, -458f), new Vector2(178f, 54f));
        ConfigureClearButtonLabel(expand, 17);

        Text sidebarHint = CreateMailText("InventorySidebarHint", sidebar.transform, GetInventorySidebarHint(), 16, FontStyle.Bold, new Color(1f, 0.87f, 0.60f, 0.94f), TextAnchor.MiddleCenter);
        SetRect(sidebarHint.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f, -540f), new Vector2(174f, 48f));
        sidebar.gameObject.SetActive(false);

        Image gridFrame = CreateImage("InventoryGridFrame", panel.transform, new Color(0.48f, 0.23f, 0.08f, 1f));
        SetRect(gridFrame.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(258f, -108f), new Vector2(1060f, 678f));
        gridFrame.raycastTarget = false;

        Image gridPaper = CreateImage("InventoryGridPaper", gridFrame.transform, new Color(0.98f, 0.79f, 0.43f, 1f));
        SetRect(gridPaper.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(12f, -12f), new Vector2(1036f, 654f));
        gridPaper.raycastTarget = false;

        GameObject itemParent = new GameObject("ItemParent", typeof(RectTransform));
        itemParent.transform.SetParent(gridPaper.transform, false);
        Stretch(itemParent.GetComponent<RectTransform>());

        Text gridTitle = CreateMailText("InventoryGridTitle", panel.transform, "物品栏", 22, FontStyle.Bold, new Color(0.42f, 0.19f, 0.05f, 1f), TextAnchor.MiddleLeft);
        SetRect(gridTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(286f, -120f), new Vector2(260f, 34f));

        Vector2 slotStart = new Vector2(16f, -38f);
        Vector2 slotSize = new Vector2(106f, 106f);
        int generatedSlotCount = InventoryPanelView.GetRequiredSlotCount(visibleItems.Count);
        float viewportHeight = -slotStart.y * 2f + slotSize.y + (InventoryPanelView.GridVisibleRows - 1) * 118f;
        float contentHeight = -slotStart.y * 2f + slotSize.y + (generatedSlotCount / InventoryColumns - 1) * 118f;
        gridPaper.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
        gridPaper.raycastTarget = true;
        gridPaper.gameObject.AddComponent<RectMask2D>();
        RectTransform itemContent = itemParent.GetComponent<RectTransform>();
        itemContent.anchorMin = new Vector2(0f, 1f);
        itemContent.anchorMax = new Vector2(1f, 1f);
        itemContent.pivot = new Vector2(0f, 1f);
        itemContent.anchoredPosition = Vector2.zero;
        itemContent.sizeDelta = new Vector2(0f, contentHeight);
        ScrollRect itemScroll = gridPaper.gameObject.AddComponent<ScrollRect>();
        itemScroll.viewport = gridPaper.rectTransform;
        itemScroll.content = itemContent;
        itemScroll.horizontal = false;
        itemScroll.vertical = visibleItems.Count > InventoryColumns * InventoryPanelView.GridVisibleRows;
        itemScroll.enabled = itemScroll.vertical;
        itemScroll.movementType = ScrollRect.MovementType.Clamped;
        itemScroll.scrollSensitivity = 118f;
        for (int slotIndex = 0; slotIndex < generatedSlotCount; slotIndex++)
        {
            int row = slotIndex / InventoryColumns;
            int column = slotIndex % InventoryColumns;
            Vector2 position = slotStart + new Vector2(column * 118f, -row * 118f);
            InventoryItemResponse item = slotIndex < visibleItems.Count ? visibleItems[slotIndex] : null;
            AddInventorySlot(itemParent.transform, item, slotIndex, position, slotSize, false);
        }

        AddInventoryFooter(panel.transform, ownedCount, visibleItems.Count, unlockedSlots);
        AddInventorySideAction(panel.transform, new Vector2(50f, -234f), "仓库", "仓", true, null);
        AddInventorySideAction(panel.transform, new Vector2(50f, -414f), "商店", "店", false, ShowShopContent);

        selectedIndex = FindFilteredInventoryIndex(visibleItems, selectedInventoryItemKey);
        if (selectedIndex >= 0)
        {
            int row = selectedIndex / InventoryColumns;
            int column = selectedIndex % InventoryColumns;
            Vector2 selectedPosition = slotStart + new Vector2(column * 118f, -row * 118f);
            AddInventoryDetail(panel.transform, visibleItems[selectedIndex]);
        }

        SetStatus(inventoryStatusMessage);
    }

    private void ConfigureInventoryPrefab(
        InventoryPanelView view,
        List<InventoryItemResponse> visibleItems,
        int selectedIndex,
        int ownedCount,
        int unlockedSlots)
    {
        view.SetMode(inventoryServerBacked);
        view.SetSidebarHint(GetInventorySidebarHint());
        view.SetResourceValue("Gold", GetInventoryResourceValue("Gold"));
        view.SetResourceValue("Diamond", GetInventoryResourceValue("Crystal"));
        view.SetResourceIcon("Gold", view.ResolveIcon("gold", "金币", "gold"));
        view.SetResourceIcon("Diamond", view.ResolveIcon("diamond", "钻石", "diamond"));
        int slotCount = view.PrepareSlotGrid(visibleItems.Count);
        view.SetCounts(ownedCount, visibleItems.Count, slotCount, slotCount);
        view.SetStatus(inventoryStatusMessage);
        view.SetEmptyVisible(false);
        view.BindChromeActions(HideInventoryPanel, ExpandInventorySlots, SortInventoryItems, ShowRechargeContent);
        view.SetCategoriesVisible(false);
        view.BindSideActions(null, ShowShopContent);

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            InventorySlotView slot = view.GetSlot(slotIndex);
            if (slot == null)
            {
                continue;
            }

            InventoryItemResponse item = slotIndex < visibleItems.Count ? visibleItems[slotIndex] : null;
            bool hasItem = item != null && item.quantity > 0;
            view.SetSlotVisible(slotIndex, true);
            if (!hasItem)
            {
                slot.ShowEmpty();
                continue;
            }

            string itemKey = GetInventoryItemKey(item);
            Sprite sprite = view.ResolveIcon(item.itemKey, item.name, item.icon);

            string fallbackLabel = string.IsNullOrEmpty(item.name) ? "物" : item.name.Substring(0, 1);
            slot.ShowItem(
                sprite,
                fallbackLabel,
                item.quantity,
                itemKey == selectedInventoryItemKey,
                GetInventoryFallbackColor(item),
                view.ResolveQualityBackground(item.quality),
                () => SelectInventoryItem(itemKey));
        }

        for (int slotIndex = slotCount; slotIndex < view.SlotCount; slotIndex++)
        {
            InventorySlotView slot = view.GetSlot(slotIndex);
            if (slot != null)
            {
                slot.ShowEmpty();
                view.SetSlotVisible(slotIndex, false);
            }
        }

        if (selectedIndex < 0 || selectedIndex >= slotCount)
        {
            view.HideContextMenu();
            view.ShowResourceDetail("资源详情", "请选择物品", 0, "点击物品后，在这里查看名称、类型、数量和说明");
            return;
        }

        InventoryItemResponse selectedItem = visibleItems[selectedIndex];
        view.ShowResourceDetail(
            selectedItem.name,
            GetInventoryTypeLabel(selectedItem),
            selectedItem.quantity,
            selectedItem.description);

        view.BindSellAction(SellSelectedInventoryItem);
    }

    private Color GetInventoryFallbackColor(InventoryItemResponse item)
    {
        string category = GetInventoryCategory(item);
        if (category == "equipment") return new Color(0.35f, 0.47f, 0.78f, 1f);
        if (category == "medicine") return new Color(0.25f, 0.68f, 0.38f, 1f);
        return new Color(0.80f, 0.38f, 0.14f, 1f);
    }

    private InventoryItemResponse[] BuildLocalInventoryItems()
    {
        return new[]
        {
            CreateLocalInventoryItem(1, "Wood", "木材", "MATERIAL", "wood", GetLocalQuantity("Wood", 500)),
            CreateLocalInventoryItem(2, "Stone", "石材", "MATERIAL", "stone", GetLocalQuantity("Stone", 500)),
            CreateLocalInventoryItem(3, "Iron", "铁矿", "MATERIAL", "iron", GetLocalQuantity("Iron", 300)),
            CreateLocalInventoryItem(4, "Food", "食物", "CONSUMABLE", "food", GetLocalQuantity("Food", 8)),
            CreateLocalInventoryItem(5, "HealingPotion", "恢复药水", "MEDICINE", "potion", GetLocalQuantity("HealingPotion", 3)),
            CreateLocalInventoryItem(6, "SpeedItem", "加速卷轴", "CONSUMABLE", "speed", GetLocalQuantity("SpeedItem", 2)),
            CreateLocalInventoryItem(7, "BeginnerSword", "新手短剑", "EQUIPMENT", "sword", GetLocalQuantity("BeginnerSword", 1)),
            CreateLocalInventoryItem(8, "ClothArmor", "布甲", "EQUIPMENT", "armor", GetLocalQuantity("ClothArmor", 1)),
            CreateLocalInventoryItem(10, "EquipmentChest", "装备宝箱", "CHEST", "chest", GetLocalQuantity("EquipmentChest", 0)),
            CreateLocalInventoryItem(11, "HeroChest", "英雄宝箱", "CHEST", "hero_chest", GetLocalQuantity("HeroChest", 0)),
            CreateLocalInventoryItem(12, "AdvancedHeroChest", "高级英雄箱", "CHEST", "advanced_chest", GetLocalQuantity("AdvancedHeroChest", 0)),
            CreateLocalInventoryItem(13, "LotteryTicket", "高级抽奖券", "TICKET", "ticket", GetLocalQuantity("LotteryTicket", 0))
        };
    }

    private InventoryItemResponse CreateLocalInventoryItem(int itemId, string key, string name, string type, string icon, int quantity)
    {
        return new InventoryItemResponse
        {
            itemId = itemId,
            itemKey = key,
            name = name,
            itemType = type,
            icon = icon,
            quantity = Mathf.Max(0, quantity)
        };
    }

    private List<InventoryItemResponse> GetFilteredInventoryItems()
    {
        List<InventoryItemResponse> result = new List<InventoryItemResponse>();
        if (currentInventoryItems == null)
        {
            return result;
        }

        for (int i = 0; i < currentInventoryItems.Length; i++)
        {
            InventoryItemResponse item = currentInventoryItems[i];
            if (item == null || item.quantity <= 0 || IsTopCurrencyItem(item))
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private int GetOwnedInventoryCount()
    {
        int count = 0;
        if (currentInventoryItems == null)
        {
            return count;
        }

        for (int i = 0; i < currentInventoryItems.Length; i++)
        {
            if (currentInventoryItems[i] != null && currentInventoryItems[i].quantity > 0 && !IsTopCurrencyItem(currentInventoryItems[i]))
            {
                count++;
            }
        }

        return count;
    }

    private int FindFilteredInventoryIndex(List<InventoryItemResponse> items, string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey) || items == null)
        {
            return -1;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (GetInventoryItemKey(items[i]) == itemKey)
            {
                return i;
            }
        }

        return -1;
    }

    private InventoryItemResponse FindCurrentInventoryItem(string itemKey)
    {
        if (currentInventoryItems == null || string.IsNullOrEmpty(itemKey))
        {
            return null;
        }

        for (int i = 0; i < currentInventoryItems.Length; i++)
        {
            if (GetInventoryItemKey(currentInventoryItems[i]) == itemKey)
            {
                return currentInventoryItems[i];
            }
        }

        return null;
    }

    private string GetInventoryItemKey(InventoryItemResponse item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string key = string.IsNullOrEmpty(item.itemKey) ? item.name : item.itemKey;
        return key ?? string.Empty;
    }

    private string GetInventoryCategory(InventoryItemResponse item)
    {
        if (IsEquipmentItem(item))
        {
            return "equipment";
        }

        if (IsMedicineItem(item))
        {
            return "medicine";
        }

        return "other";
    }

    private bool IsTopCurrencyItem(InventoryItemResponse item)
    {
        if (item == null)
        {
            return false;
        }

        string key = CompactInventoryIdentifier(item.itemKey);
        string name = CompactInventoryIdentifier(item.name);
        string icon = CompactInventoryIdentifier(item.icon);
        string type = CompactInventoryIdentifier(item.itemType);
        return type == "currency"
            || IsInventoryCurrencyIdentifier(key)
            || IsInventoryCurrencyIdentifier(name)
            || IsInventoryCurrencyIdentifier(icon);
    }

    private bool IsInventoryCurrencyIdentifier(string value)
    {
        return value == "gold"
            || value == "coin"
            || value == "coins"
            || value == "goldcoin"
            || value == "currencygold"
            || value == "金币"
            || value == "金幣"
            || value == "crystal"
            || value == "diamond"
            || value == "钻石"
            || value == "鑽石"
            || value == "bluecrystal"
            || value == "蓝晶"
            || value == "藍晶"
            || value == "soulcrystal"
            || value == "魂晶"
            || value == "purplecrystal"
            || value == "紫晶";
    }

    private string CompactInventoryIdentifier(string value)
    {
        return NormalizeInventoryKey(value)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }

    private bool IsPetItem(InventoryItemResponse item)
    {
        string type = (item == null ? string.Empty : item.itemType) ?? string.Empty;
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        string name = (item == null ? string.Empty : item.name) ?? string.Empty;
        return type.IndexOf("PET", StringComparison.OrdinalIgnoreCase) >= 0
            || key.Contains("pet")
            || name.Contains("灵宠")
            || name.Contains("宠物");
    }

    private bool IsMedicineItem(InventoryItemResponse item)
    {
        string type = (item == null ? string.Empty : item.itemType) ?? string.Empty;
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        string name = (item == null ? string.Empty : item.name) ?? string.Empty;
        return type.IndexOf("MEDICINE", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("CONSUMABLE", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("POTION", StringComparison.OrdinalIgnoreCase) >= 0
            || key.Contains("potion")
            || key.Contains("food")
            || key.Contains("speed")
            || name.Contains("药")
            || name.Contains("食物")
            || name.Contains("加速");
    }

    private string NormalizeInventoryKey(string key)
    {
        return (key ?? string.Empty).Trim().ToLowerInvariant();
    }

    private int GetInventoryUnlockedSlots()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt("InventoryUnlockedSlots", DefaultInventoryUnlockedSlots), 1, InventoryCapacity);
    }

    private int GetLocalQuantity(string key, int defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    private void SetInventoryCategory(string category)
    {
        activeInventoryCategory = "all";
        selectedInventoryItemKey = string.Empty;
        inventoryStatusMessage = "仓库不区分物品分类";
        RenderInventoryPanel();
    }

    private string GetInventoryCategoryLabel(string category)
    {
        if (category == "equipment") return "装备分类";
        if (category == "medicine") return "药品分类";
        if (category == "other") return "其它分类";
        return "全部物品";
    }

    private void SortInventoryItems()
    {
        if (currentInventoryItems == null || currentInventoryItems.Length == 0)
        {
            inventoryStatusMessage = "当前没有可整理的物品";
            RenderInventoryPanel();
            return;
        }

        int mergedGroupCount;
        int releasedSlotCount;
        currentInventoryItems = MergeAndSortInventoryItems(currentInventoryItems, out mergedGroupCount, out releasedSlotCount);
        selectedInventoryItemKey = string.Empty;
        if (inventoryServerBacked)
        {
            SyncInventoryResourceValues(currentInventoryItems);
        }

        if (GetOwnedInventoryCount() == 0)
        {
            inventoryStatusMessage = "当前没有可整理的物品";
            RenderInventoryPanel();
            return;
        }

        string serverSuffix = inventoryServerBacked ? "（仅改变当前显示，服务器数据未修改）" : string.Empty;
        inventoryStatusMessage = mergedGroupCount > 0
            ? $"整理完成：合并 {mergedGroupCount} 组物品，释放 {releasedSlotCount} 个背包格{serverSuffix}"
            : "整理完成：未发现可合并物品，已按数量从多到少排列" + serverSuffix;
        RenderInventoryPanel();
    }

    private InventoryItemResponse[] MergeAndSortInventoryItems(
        InventoryItemResponse[] sourceItems,
        out int mergedGroupCount,
        out int releasedSlotCount)
    {
        List<InventoryMergeStack> stacks = new List<InventoryMergeStack>();
        Dictionary<string, InventoryMergeStack> stacksByKey = new Dictionary<string, InventoryMergeStack>(StringComparer.Ordinal);

        for (int sourceIndex = 0; sourceIndex < sourceItems.Length; sourceIndex++)
        {
            InventoryItemResponse sourceItem = sourceItems[sourceIndex];
            if (sourceItem == null || sourceItem.quantity <= 0)
            {
                continue;
            }

            string stackKey = GetInventoryStackKey(sourceItem, sourceIndex);
            InventoryMergeStack stack;
            if (!stacksByKey.TryGetValue(stackKey, out stack))
            {
                stack = new InventoryMergeStack
                {
                    stackKey = stackKey,
                    item = CloneInventoryItem(sourceItem),
                    sourceCount = 1
                };
                stacksByKey.Add(stackKey, stack);
                stacks.Add(stack);
                continue;
            }

            long mergedQuantity = (long)stack.item.quantity + sourceItem.quantity;
            stack.item.quantity = (int)Math.Min(mergedQuantity, int.MaxValue);
            stack.sourceCount++;
            FillMissingInventoryMetadata(stack.item, sourceItem);
        }

        mergedGroupCount = 0;
        releasedSlotCount = 0;
        for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
        {
            InventoryMergeStack stack = stacks[stackIndex];
            if (stack.sourceCount > 1 && !IsTopCurrencyItem(stack.item))
            {
                mergedGroupCount++;
                releasedSlotCount += stack.sourceCount - 1;
            }
        }

        stacks.Sort(CompareMergedInventoryStacks);
        InventoryItemResponse[] mergedItems = new InventoryItemResponse[stacks.Count];
        for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
        {
            mergedItems[stackIndex] = stacks[stackIndex].item;
        }

        return mergedItems;
    }

    private string GetInventoryStackKey(InventoryItemResponse item, int sourceIndex)
    {
        string itemKey = NormalizeInventoryKey(item.itemKey);
        if (!string.IsNullOrEmpty(itemKey))
        {
            return "key:" + itemKey;
        }

        if (item.itemId != 0)
        {
            return "id:" + item.itemId;
        }

        string name = NormalizeInventoryKey(item.name);
        string itemType = NormalizeInventoryKey(item.itemType);
        string icon = NormalizeInventoryKey(item.icon);
        if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(itemType) || !string.IsNullOrEmpty(icon))
        {
            return $"metadata:{name.Length}:{name}{itemType.Length}:{itemType}{icon.Length}:{icon}";
        }

        return "anonymous:" + sourceIndex;
    }

    private InventoryItemResponse CloneInventoryItem(InventoryItemResponse sourceItem)
    {
        return new InventoryItemResponse
        {
            itemId = sourceItem.itemId,
            itemKey = sourceItem.itemKey,
            name = sourceItem.name,
            itemType = sourceItem.itemType,
            quality = sourceItem.quality,
            icon = sourceItem.icon,
            description = sourceItem.description,
            quantity = sourceItem.quantity
        };
    }

    private void FillMissingInventoryMetadata(InventoryItemResponse targetItem, InventoryItemResponse sourceItem)
    {
        if (targetItem.itemId == 0) targetItem.itemId = sourceItem.itemId;
        if (string.IsNullOrWhiteSpace(targetItem.itemKey)) targetItem.itemKey = sourceItem.itemKey;
        if (string.IsNullOrWhiteSpace(targetItem.name)) targetItem.name = sourceItem.name;
        if (string.IsNullOrWhiteSpace(targetItem.itemType)) targetItem.itemType = sourceItem.itemType;
        if (string.IsNullOrWhiteSpace(targetItem.quality)) targetItem.quality = sourceItem.quality;
        if (string.IsNullOrWhiteSpace(targetItem.icon)) targetItem.icon = sourceItem.icon;
        if (string.IsNullOrWhiteSpace(targetItem.description)) targetItem.description = sourceItem.description;
    }

    private int CompareMergedInventoryStacks(InventoryMergeStack left, InventoryMergeStack right)
    {
        if (left == null) return 1;
        if (right == null) return -1;

        int quantityCompare = right.item.quantity.CompareTo(left.item.quantity);
        if (quantityCompare != 0)
        {
            return quantityCompare;
        }

        int nameCompare = string.Compare(left.item.name, right.item.name, StringComparison.Ordinal);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        return string.Compare(left.stackKey, right.stackKey, StringComparison.Ordinal);
    }

    private int GetInventoryResourceValue(string key)
    {
        if (key == "Gold") return PlayerPrefs.GetInt("Gold", 1000);
        if (key == "Crystal") return PlayerPrefs.GetInt("Crystal", 427);
        if (key == "SoulCrystal") return PlayerPrefs.GetInt("SoulCrystal", 1200);
        return PlayerPrefs.GetInt(key, 0);
    }

    private string GetResourceIconAssetPath(string key)
    {
        if (string.Equals(key, "Gold", StringComparison.OrdinalIgnoreCase)) return "UI/资源图标/金币.png";
        if (string.Equals(key, "Crystal", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "Diamond", StringComparison.OrdinalIgnoreCase)) return "UI/资源图标/万能彩墨.png";
        return "UI/资源图标/" + key + ".png";
    }

    private void AddInventoryCategoryButton(Transform parent, string category, string label, Vector2 position, bool selected)
    {
        Color buttonColor = selected ? new Color(0.92f, 0.46f, 0.16f, 1f) : new Color(0.91f, 0.67f, 0.29f, 1f);
        Color textColor = selected ? Color.white : new Color(0.44f, 0.21f, 0.07f, 1f);
        Button button = CreateButton(parent, "InventoryCategory_" + category, label, buttonColor, textColor);
        button.onClick.AddListener(() => SetInventoryCategory(category));
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(178f, 72f));
        ConfigureClearButtonLabel(button, 22);

        if (selected)
        {
            Image marker = CreateImage("InventoryCategoryMarker", button.transform, new Color(1f, 0.88f, 0.42f, 1f));
            SetRect(marker.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-8f, 0f), new Vector2(12f, 40f));
            marker.raycastTarget = false;
        }
    }

    private void AddInventorySlot(Transform parent, InventoryItemResponse item, int slotIndex, Vector2 position, Vector2 size, bool locked)
    {
        Color slotColor = locked
            ? new Color(0.62f, 0.62f, 0.58f, 1f)
            : Color.white;
        Button slot = CreateButton(parent, "InventorySlot_" + slotIndex, string.Empty, slotColor, Color.white);
        SetRect(slot.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        Image background = slot.GetComponent<Image>();

        Image selectedImage = CreateImage("InventorySlotSelectedImage", slot.transform, Color.white);
        SetRect(selectedImage.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, size);
        selectedImage.raycastTarget = false;
        selectedImage.gameObject.SetActive(false);

        if (locked)
        {
            slot.onClick.AddListener(ExpandInventorySlots);
            AddInventoryLockMark(slot.transform, new Vector2(34f, -26f), new Vector2(38f, 46f));
            return;
        }

        if (item == null)
        {
            return;
        }

        string itemKey = GetInventoryItemKey(item);
        slot.onClick.AddListener(() => SelectInventoryItem(itemKey));
        AddInventoryItemIcon(slot.transform, item, new Vector2(19f, -12f), new Vector2(68f, 68f), slotIndex);

        Text quantity = CreateMailText("InventoryQuantity", slot.transform, item.quantity.ToString(), 16, FontStyle.Bold, new Color(0.35f, 0.15f, 0.04f, 1f), TextAnchor.MiddleRight);
        SetRect(quantity.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(54f, -84f), new Vector2(42f, 22f));

        if (itemKey == selectedInventoryItemKey)
        {
            selectedImage.gameObject.SetActive(true);
        }
    }

    private void AddInventoryLockMark(Transform parent, Vector2 position, Vector2 size)
    {
        Image body = CreateImage("InventoryLockBody", parent, new Color(0.42f, 0.43f, 0.42f, 0.95f));
        SetRect(body.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(2f, -15f), new Vector2(size.x - 4f, size.y * 0.58f));
        body.raycastTarget = false;

        Image shackle = CreateImage("InventoryLockShackle", parent, new Color(0.52f, 0.53f, 0.52f, 0.95f));
        SetRect(shackle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(size.x * 0.24f, -2f), new Vector2(size.x * 0.52f, size.y * 0.46f));
        shackle.raycastTarget = false;

        Text lockText = CreateMailText("InventoryLockText", parent, "锁", 15, FontStyle.Bold, new Color(0.86f, 0.87f, 0.84f, 1f), TextAnchor.MiddleCenter);
        SetRect(lockText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(2f, -22f), new Vector2(size.x - 4f, 28f));
    }

    private void AddInventoryItemIcon(Transform parent, InventoryItemResponse item, Vector2 position, Vector2 size, int slotIndex)
    {
        Image icon = CreateImage("InventoryItemIcon", parent, Color.white);
        SetRect(icon.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        icon.raycastTarget = false;

        string path = GetInventoryIconAssetPath(item, slotIndex);
        Sprite sprite = LoadInventorySprite(path);
        if (sprite != null)
        {
            icon.sprite = sprite;
            icon.preserveAspect = true;
            if (sprite.texture != null)
            {
                sprite.texture.filterMode = FilterMode.Point;
            }
            return;
        }

        Color fallbackColor = new Color(0.80f, 0.38f, 0.14f, 1f);
        string category = GetInventoryCategory(item);
        if (category == "equipment") fallbackColor = new Color(0.35f, 0.47f, 0.78f, 1f);
        if (category == "medicine") fallbackColor = new Color(0.25f, 0.68f, 0.38f, 1f);
        AddMiniGem(parent, position + new Vector2(10f, -10f), size - new Vector2(20f, 20f), fallbackColor);

        string firstLetter = string.IsNullOrEmpty(item.name) ? "物" : item.name.Substring(0, 1);
        Text fallbackText = CreateMailText("InventoryItemFallback", parent, firstLetter, 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(fallbackText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
    }

    private string GetInventoryIconAssetPath(InventoryItemResponse item, int slotIndex)
    {
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        string name = item == null ? string.Empty : item.name;
        string icon = item == null ? string.Empty : NormalizeInventoryKey(item.icon);
        if (key == "gold" || icon == "gold" || name == "金币") return "UI/资源图标/金币.png";
        if (key == "diamond" || key == "crystal" || icon == "diamond" || icon == "crystal" || name == "钻石") return "UI/资源图标/万能彩墨.png";
        if (key.Contains("wood") || icon.Contains("wood") || name.Contains("木材")) return "UI/资源图标/木材.png";
        if (key.Contains("stone") || icon.Contains("stone") || name.Contains("石材")) return "UI/资源图标/石材.png";
        if (key.Contains("iron") || key.Contains("ore") || icon.Contains("iron") || icon.Contains("ore") || name.Contains("矿")) return "UI/资源图标/矿石.png";
        if (key.Contains("wheat") || icon.Contains("wheat") || name.Contains("小麦")) return "UI/资源图标/小麦.png";
        if (key.Contains("fish") || icon.Contains("fish") || name.Contains("鱼")) return "UI/资源图标/鱼.png";
        if (key.Contains("meat") || icon.Contains("meat") || name.Contains("肉")) return "UI/资源图标/肉.png";
        if (key.Contains("bread") || key.Contains("food") || icon.Contains("bread") || icon.Contains("food") || name.Contains("面包") || name.Contains("食物")) return "UI/资源图标/面包.png";
        if (key.Contains("metal") || icon.Contains("metal") || name.Contains("金属")) return "UI/资源图标/金属.png";
        if (key.Contains("purification") || icon.Contains("purification") || name.Contains("净化剂")) return "UI/资源图标/净化剂.png";
        if (key.Contains("stamina") || icon.Contains("stamina") || name.Contains("体力")) return "UI/资源图标/体力.png";
        if (key.Contains("chest") || icon.Contains("chest") || name.Contains("宝箱")) return "UI/资源图标/宝箱（未打开）.png";
        return string.Empty;
    }

    private int StableInventoryHash(string value)
    {
        int hash = 17;
        string text = value ?? string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            hash = hash * 31 + text[i];
        }

        return Mathf.Abs(hash);
    }

    private Sprite LoadInventorySprite(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        if (inventorySpriteCache.TryGetValue(relativePath, out Sprite cached))
        {
            return cached;
        }

        Sprite sprite = LoadSpriteFromAsset(relativePath);
        inventorySpriteCache[relativePath] = sprite;
        return sprite;
    }

    private void AddInventoryFooter(Transform parent, int ownedCount, int visibleCount, int unlockedSlots)
    {
        Image footer = CreateImage("InventoryFooter", parent, new Color(0.50f, 0.24f, 0.08f, 1f));
        SetRect(footer.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(258f, -800f), new Vector2(1060f, 64f));

        Text capacity = CreateMailText("InventoryCapacity", footer.transform, $"背包空间：{ownedCount:00}/{InventoryCapacity}", 18, FontStyle.Bold, new Color(1f, 0.86f, 0.48f, 1f), TextAnchor.MiddleLeft);
        SetRect(capacity.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(20f, -10f), new Vector2(250f, 34f));

        Text visible = CreateMailText("InventoryVisibleCount", footer.transform, $"当前分类 {visibleCount} 件  已开放 {unlockedSlots} 格", 15, FontStyle.Normal, new Color(1f, 0.78f, 0.48f, 0.90f), TextAnchor.MiddleLeft);
        SetRect(visible.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(20f, -36f), new Vector2(320f, 24f));

        Image sliderLine = CreateImage("InventorySortSliderLine", footer.transform, new Color(0.22f, 0.10f, 0.03f, 0.90f));
        SetRect(sliderLine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(510f, -29f), new Vector2(126f, 8f));
        sliderLine.raycastTarget = false;
        for (int i = 0; i < 3; i++)
        {
            Image knob = CreateImage("InventorySortKnob", footer.transform, i == 1 ? new Color(1f, 0.66f, 0.22f, 1f) : new Color(0.28f, 0.14f, 0.05f, 1f));
            SetRect(knob.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(500f + i * 55f, -24f), new Vector2(18f, 18f));
            knob.raycastTarget = false;
        }

        Button organize = CreateButton(parent, "InventoryOrganizeButton", "一键整理", new Color(0.89f, 0.58f, 0.18f, 1f), new Color(0.36f, 0.16f, 0.04f, 1f));
        organize.onClick.AddListener(SortInventoryItems);
        SetRect(organize.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(874f, -11f), new Vector2(164f, 44f));
        ConfigureClearButtonLabel(organize, 18);
    }

    private void AddInventorySideAction(Transform parent, Vector2 position, string label, string iconText, bool selected, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, "InventorySideAction_" + label, string.Empty, selected ? new Color(0.91f, 0.49f, 0.16f, 1f) : new Color(0.69f, 0.38f, 0.14f, 1f), Color.white);
        if (action != null)
        {
            button.onClick.AddListener(action);
        }
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(154f, 126f));

        Text icon = CreateMailText("InventorySideIcon", button.transform, iconText, 34, FontStyle.Bold, new Color(1f, 0.91f, 0.66f, 1f), TextAnchor.MiddleCenter);
        SetRect(icon.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(40f, -12f), new Vector2(74f, 54f));

        Text sideLabel = CreateMailText("InventorySideLabel", button.transform, label, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(sideLabel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(12f, -82f), new Vector2(130f, 30f));
    }

    private void AddInventoryDetail(Transform parent, InventoryItemResponse item)
    {
        Image detail = CreateImage("InventoryResourceDetail", parent, new Color(0.30f, 0.15f, 0.05f, 0.96f));
        SetRect(detail.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1225f, -108f), new Vector2(300f, 672f));

        Text name = CreateMailText("InventoryDetailName", detail.transform, item.name ?? "物品", 25, FontStyle.Bold, new Color(1f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft);
        SetRect(name.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -34f), new Vector2(244f, 40f));

        Text type = CreateMailText("InventoryDetailType", detail.transform, GetInventoryTypeLabel(item), 16, FontStyle.Normal, new Color(1f, 0.78f, 0.45f, 1f), TextAnchor.MiddleLeft);
        SetRect(type.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -148f), new Vector2(244f, 28f));

        Text quantity = CreateMailText("InventoryDetailQuantity", detail.transform, $"数量  {item.quantity}", 19, FontStyle.Bold, new Color(1f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft);
        SetRect(quantity.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(36f, -222f), new Vector2(244f, 32f));

        Text description = CreateMailText("InventoryDetailDescription", detail.transform, item.description ?? "服务器物品配置未提供额外说明", 15, FontStyle.Normal, new Color(1f, 0.84f, 0.60f, 0.94f), TextAnchor.UpperLeft);
        SetRect(description.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -304f), new Vector2(244f, 210f));

        Button sell = CreateButton(detail.transform, "InventorySellAction", "出售", new Color(0.91f, 0.37f, 0.13f, 1f), Color.white);
        sell.onClick.AddListener(SellSelectedInventoryItem);
        SetRect(sell.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -540f), new Vector2(236f, 52f));
        ConfigureClearButtonLabel(sell, 16);
    }

    private string GetInventoryActionLabel(InventoryItemResponse item)
    {
        if (IsEquipmentItem(item)) return "装备";
        if (IsChestItem(item)) return "打开";
        if (IsInventoryUsable(item)) return "使用";
        return "查看";
    }

    private string GetInventoryTypeLabel(InventoryItemResponse item)
    {
        string category = GetInventoryCategory(item);
        if (category == "equipment") return "装备";
        if (category == "medicine") return "药品";
        if (IsChestItem(item)) return "宝箱";
        if ((item.itemType ?? string.Empty).IndexOf("TICKET", StringComparison.OrdinalIgnoreCase) >= 0) return "道具";
        return "材料";
    }

    private void SelectInventoryItem(string itemKey)
    {
        InventoryItemResponse item = FindCurrentInventoryItem(itemKey);
        if (item == null)
        {
            return;
        }

        selectedInventoryItemKey = itemKey;
        inventoryStatusMessage = $"已选择：{item.name}  x{item.quantity}";
        RenderInventoryPanel();
    }

    private void HandlePrimaryInventoryAction()
    {
        UseSelectedInventoryItem();
    }

    private void UseSelectedInventoryItem()
    {
        InventoryItemResponse item = FindCurrentInventoryItem(selectedInventoryItemKey);
        if (item == null)
        {
            return;
        }

        if (inventoryServerBacked)
        {
            inventoryStatusMessage = "当前接口只提供背包查询，使用物品接口尚未开放，未修改服务器库存";
            RenderInventoryPanel();
            return;
        }

        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        if (IsEquipmentItem(item))
        {
            PlayerPrefs.SetString("EquippedItemKey", GetInventoryItemKey(item));
            PlayerPrefs.Save();
            inventoryStatusMessage = $"已装备：{item.name}";
            RenderInventoryPanel();
            return;
        }

        if (!IsInventoryUsable(item))
        {
            inventoryStatusMessage = $"{item.name}是材料类物品，当前没有可执行的使用动作";
            RenderInventoryPanel();
            return;
        }

        ChangeLocalInventoryQuantity(item, -1);
        if (IsChestItem(item))
        {
            if (key.Contains("equipment"))
            {
                ChangeLocalQuantity("BeginnerSword", 1, 1);
                inventoryStatusMessage = "打开装备宝箱，获得新手短剑 x1";
            }
            else if (key.Contains("advanced"))
            {
                AddLocalResource("Gold", 500);
                inventoryStatusMessage = "打开高级英雄箱，获得金币 x500";
            }
            else
            {
                AddLocalResource("Gold", 300);
                inventoryStatusMessage = "打开英雄宝箱，获得金币 x300";
            }
        }
        else if (key.Contains("lottery") || key.Contains("ticket"))
        {
            PlayerPrefs.SetInt(LotteryChanceKey, EnsureLotteryChanceCount() + 1);
            inventoryStatusMessage = "抽奖券已使用，抽奖机会 +1";
        }
        else if (key.Contains("healing") || key.Contains("potion"))
        {
            int hp = Mathf.Min(1000, PlayerPrefs.GetInt("PlayerHp", 1000) + 120);
            PlayerPrefs.SetInt("PlayerHp", hp);
            inventoryStatusMessage = "恢复药水已使用，生命值恢复 120";
        }
        else if (key.Contains("food"))
        {
            int energy = Mathf.Min(100, PlayerPrefs.GetInt("PlayerEnergy", 100) + 20);
            PlayerPrefs.SetInt("PlayerEnergy", energy);
            inventoryStatusMessage = "食物已使用，体力恢复 20";
        }
        else if (key.Contains("speed"))
        {
            PlayerPrefs.SetString("SpeedBuffUntil", DateTime.Now.AddMinutes(10).ToString("O"));
            inventoryStatusMessage = "加速卷轴已使用，建造速度提升 10 分钟";
        }
        else
        {
            inventoryStatusMessage = $"已使用：{item.name}";
        }

        PlayerPrefs.Save();
        currentInventoryItems = BuildLocalInventoryItems();
        if (FindCurrentInventoryItem(selectedInventoryItemKey) == null || FindCurrentInventoryItem(selectedInventoryItemKey).quantity <= 0)
        {
            selectedInventoryItemKey = string.Empty;
        }
        RenderInventoryPanel();
    }

    private void SellSelectedInventoryItem()
    {
        BeginSelectedInventorySale();
    }

    private bool IsChestItem(InventoryItemResponse item)
    {
        string type = (item == null ? string.Empty : item.itemType) ?? string.Empty;
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        string name = (item == null ? string.Empty : item.name) ?? string.Empty;
        return type.IndexOf("CHEST", StringComparison.OrdinalIgnoreCase) >= 0
            || key.Contains("chest")
            || name.Contains("宝箱")
            || name.Contains("英雄箱");
    }

    private bool IsInventoryUsable(InventoryItemResponse item)
    {
        if (item == null)
        {
            return false;
        }

        string type = item.itemType ?? string.Empty;
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        return IsChestItem(item)
            || IsMedicineItem(item)
            || type.IndexOf("TICKET", StringComparison.OrdinalIgnoreCase) >= 0
            || key.Contains("ticket");
    }

    private void ChangeLocalInventoryQuantity(InventoryItemResponse item, int delta)
    {
        string key = GetInventoryItemKey(item);
        int defaultValue = item == null ? 0 : item.quantity;
        int next = Mathf.Max(0, GetLocalQuantity(key, defaultValue) + delta);
        SetLocalQuantity(key, next);
    }

    private void ChangeLocalQuantity(string key, int delta, int defaultValue)
    {
        SetLocalQuantity(key, Mathf.Max(0, GetLocalQuantity(key, defaultValue) + delta));
    }

    private void SetLocalQuantity(string key, int quantity)
    {
        if (!string.IsNullOrEmpty(key))
        {
            PlayerPrefs.SetInt(key, Mathf.Max(0, quantity));
        }
    }

    private void ExpandInventorySlots()
    {
        if (inventoryServerBacked)
        {
            inventoryStatusMessage = "服务器背包扩容接口尚未开放，当前只能查看锁定格子";
            RenderInventoryPanel();
            return;
        }

        int unlocked = GetInventoryUnlockedSlots();
        if (unlocked >= InventoryCapacity)
        {
            inventoryStatusMessage = "背包已扩充到最大容量";
            RenderInventoryPanel();
            return;
        }

        int cost = 500;
        int gold = GetInventoryResourceValue("Gold");
        if (gold < cost)
        {
            inventoryStatusMessage = $"扩充需要金币 x{cost}，当前金币不足";
            RenderInventoryPanel();
            return;
        }

        AddLocalResource("Gold", -cost);
        PlayerPrefs.SetInt("InventoryUnlockedSlots", Mathf.Min(InventoryCapacity, unlocked + 8));
        PlayerPrefs.Save();
        currentInventoryItems = BuildLocalInventoryItems();
        inventoryStatusMessage = "背包已扩充 8 格";
        RenderInventoryPanel();
    }

    private void ShowBattleContent()
    {
        SelectHomeMode(
            "battle",
            "对战模式",
            "已选择对战模式。\n\n当前状态：准备进入对战玩法\n\n功能内容\n1. 竞技场入口\n2. 匹配入口\n3. 防守阵容\n4. 赛季奖励\n\n说明：当前项目还没有独立对战场景和对战接口，这里先完成首页模式选择，后续可根据 HomeSelectedMode=battle 跳转到对战场景。"
        );
    }


    private void SelectHomeMode(string mode, string title, string body)
    {
        PlayerPrefs.SetString("HomeSelectedMode", mode);
        PlayerPrefs.Save();
        if (string.Equals(mode, "battle", StringComparison.OrdinalIgnoreCase))
        {
            if (!HasAuthSession())
            {
                SetStatus("Please sign in before entering battle mode.");
                return;
            }

            SceneManager.LoadScene(IdleAdventureSceneName);
            return;
        }
        SetLeftContent(title, body, $"已选择{title}");
    }

    private void ShowAnnouncement()
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("公告", BuildAnnouncementBody(), "已显示游戏公告");
            return;
        }

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        announcementPanelView = FindSceneAnnouncementPanel();
        if (announcementPanelView == null)
        {
            SetLeftContent("公告", BuildAnnouncementBody(), "场景公告组件未找到，已使用文本模式显示");
            return;
        }

        if (!EnsureAnnouncementPanelHierarchyActive())
        {
            SetLeftContent("游戏公告", BuildAnnouncementBody(), "公告面板无法显示，请检查场景层级");
            return;
        }

        announcementPanelView.transform.SetAsLastSibling();
        if (HasAuthSession())
        {
            announcementPanelView.SetStatus("正在同步服务器公告", "正在读取当前有效公告...");
            announcementPanelView.BindClose(HideAnnouncementPanel);
            StartCoroutine(GetServerAnnouncements());
            SetStatus("正在读取服务器公告");
            return;
        }

        announcementPanelView.SetStatus("请先登录", "登录后查看服务器公告");
        announcementPanelView.BindClose(HideAnnouncementPanel);
        SetStatus("已打开游戏公告");
    }

    private string BuildAnnouncementBody()
    {
        return string.Join("\n", new[]
        {
            "《微郡》7月版本更新公告",
            string.Empty,
            "亲爱的领主：",
            "本次首页与领地系统更新已经完成。重新进入首页后，即可体验以下新增内容。",
            string.Empty,
            "---------- 版本更新 ----------",
            "一、全新书页主页",
            "1. 首页采用领地手册与地图书页布局，主要入口重新分类排列。",
            "2. 玩家头像、资源概览、设置、邮件等常用信息集中展示。",
            "3. 建造、对战、经营三个模式已加入右侧功能区。",
            string.Empty,
            "二、系统功能补充",
            "1. 背包支持装备、药品和其它分类，可进行使用、出售、整理与扩容。",
            "2. 任务列表包含每日任务、主线任务、成长任务和活动任务。",
            "3. 系统邮件、充值商店和幸运抽奖界面已补充完整。",
            string.Empty,
            "---------- 活动公告 ----------",
            "活动一：每日签到",
            "活动时间：长期开放",
            "活动说明：连续签到以两周为一个周期，可领取金币、资源和稀有道具。",
            string.Empty,
            "活动二：新手三日签到",
            "活动时间：新角色创建后开放",
            "活动说明：累计签到三天，可领取资源补给与装备宝箱。",
            string.Empty,
            "活动三：幸运抽奖",
            "活动时间：长期开放",
            "活动说明：消耗抽奖机会参与翻牌，奖励以当前抽奖结算结果为准。",
            string.Empty,
            "---------- 优化与修复 ----------",
            "1. 优化书页背景、按钮尺寸和文字清晰度。",
            "2. 修复部分面板重叠、内容超出边框和关闭按钮不可见的问题。",
            "3. 背包界面已改为 Unity Prefab 组件，后续可直接替换图片和样式。",
            string.Empty,
            "温馨提示：",
            "账号模式会读取服务器数据；本地任务、活动任务和抽奖记录仅用于对应功能的本地流程。",
            string.Empty,
            "感谢各位领主的支持，祝您游戏愉快！",
            string.Empty,
            "微郡运营组",
            "2026年7月21日"
        });
    }

    private void ShowRechargePanel(PaymentProductResponse[] products, string status, bool disabled)
    {
        if (GetRegisteredScenePanel(HomePageScenePanelType.Recharge) == null)
        {
            Debug.LogWarning("RechargeScenePanel is missing. Restore the authored scene panel to open recharge.", this);
            SetStatus("充值面板未配置，请检查场景中的 RechargeScenePanel。");
            return;
        }

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        PaymentProductResponse[] displayProducts = GetAvailableRechargeProducts(products);

        TryShowRechargeScenePanel(displayProducts, status, disabled);
    }

    private void AddRechargeProductCard(Transform parent, PaymentProductResponse product, int index, Vector2 position, bool disabled)
    {
        string name = GetRechargeProductName(product);
        string rewardName = GetRechargeRewardName(product);
        int apiRewardAmount = GetShopRewardAmount(product);
        int rewardQuantity = Mathf.Max(0, apiRewardAmount);
        int bonus = 0;

        Image shadow = CreateImage("RechargeProductShadow", parent, new Color(0.12f, 0.05f, 0.02f, 0.24f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(6f, -6f), new Vector2(250f, 248f));
        shadow.raycastTarget = false;

        Image card = CreateImage("RechargeProductCard", parent, new Color(0.48f, 0.31f, 0.17f, 0.94f));
        SetRect(card.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(250f, 248f));
        card.raycastTarget = false;

        Image header = CreateImage("ProductHeader", card.transform, new Color(0.72f, 0.56f, 0.31f, 1f));
        SetRect(header.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(250f, 38f));
        Text headerText = CreateMailText("HeaderText", header.transform, $"{rewardName}x{rewardQuantity}", 20, FontStyle.Bold, new Color(0.30f, 0.17f, 0.08f, 1f), TextAnchor.MiddleCenter);
        Stretch(headerText.rectTransform);

        Image shine = CreateImage("ProductShine", card.transform, new Color(1f, 0.86f, 0.42f, 0.18f));
        SetRect(shine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(16f, -50f), new Vector2(218f, 92f));
        shine.raycastTarget = false;

        AddSoulCrystalPack(card.transform, index, new Vector2(54f, -56f), new Vector2(132f, 104f));

        Image tag = CreateImage("FirstRechargeTag", card.transform, new Color(0.72f, 0.16f, 0.10f, 1f));
        SetRect(tag.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(164f, -92f), new Vector2(70f, 70f));
        Text tagText = CreateMailText("TagText", tag.transform, "钻石\n充值", 17, FontStyle.Bold, new Color(1f, 0.88f, 0.48f, 1f), TextAnchor.MiddleCenter);
        Stretch(tagText.rectTransform);

        Image crystalLine = CreateImage("CrystalLine", card.transform, new Color(0.36f, 0.24f, 0.14f, 0.55f));
        SetRect(crystalLine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -164f), new Vector2(250f, 30f));
        AddMiniGem(card.transform, new Vector2(62f, -161f), new Vector2(28f, 24f), new Color(0.32f, 0.72f, 1f, 1f));
        Text amount = CreateMailText("RewardAmount", card.transform, $"{rewardQuantity}", 21, FontStyle.Bold, new Color(0.82f, 0.72f, 1f, 1f), TextAnchor.MiddleLeft);
        SetRect(amount.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(96f, -159f), new Vector2(100f, 26f));

        Button price = CreateButton(card.transform, "RechargePriceButton", FormatRechargePrice(product), new Color(0.58f, 0.20f, 0.16f, 1f), new Color(1f, 0.86f, 0.48f, 1f));
        price.onClick.AddListener(() => HandleRechargeProductSelected(product, name, rewardName, rewardQuantity, bonus, disabled));
        price.interactable = !disabled;
        SetRect(price.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -196f), new Vector2(250f, 52f));
        ConfigureClearButtonLabel(price, 27);
    }

    private void HandleRechargeProductSelected(PaymentProductResponse product, string productName, string rewardName, int rewardQuantity, int bonus, bool disabled)
    {
        if (disabled)
        {
            SetStatus("当前不能充值：请使用账号或微信登录，并确认后端支付服务可用。");
            return;
        }

        SetStatus($"已选择 {productName}：{rewardName} x{rewardQuantity}，正在创建支付订单。");
        BeginPaymentOrder(product, productName);
    }

    private void AddRechargeBackground(Transform parent, Vector2 size)
    {
        Image glow = CreateImage("RechargeGlow", parent, new Color(0.98f, 0.64f, 0.22f, 0.16f));
        SetRect(glow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -90f), new Vector2(size.x, 560f));
        glow.raycastTarget = false;

        for (int i = 0; i < 8; i++)
        {
            Image beam = CreateImage("RechargeBeam", parent, i % 2 == 0 ? new Color(0.32f, 0.17f, 0.08f, 0.34f) : new Color(0.78f, 0.46f, 0.18f, 0.12f));
            SetRect(beam.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(i * 178f, 0f), new Vector2(112f, size.y));
            beam.raycastTarget = false;
        }

        Image topShade = CreateImage("RechargeTopShade", parent, new Color(0.18f, 0.09f, 0.04f, 0.38f));
        SetRect(topShade.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(size.x, 94f));
        topShade.raycastTarget = false;

        Image bottom = CreateImage("RechargeBottomFade", parent, new Color(0.22f, 0.10f, 0.04f, 0.44f));
        SetRect(bottom.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -672f), new Vector2(size.x, 128f));
        bottom.raycastTarget = false;
    }

    private void AddRechargeResourceBadge(Transform parent, Vector2 position, string label, int value)
    {
        Image badge = CreateImage("RechargeResourceBadge", parent, new Color(0.22f, 0.12f, 0.07f, 0.46f));
        SetRect(badge.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(204f, 36f));
        AddMiniGem(badge.transform, new Vector2(12f, -7f), new Vector2(28f, 24f), new Color(0.32f, 0.72f, 1f, 1f));

        Text text = CreateMailText("Text", badge.transform, $"{label} {value}    +", 18, FontStyle.Bold, new Color(0.92f, 0.82f, 0.62f, 1f), TextAnchor.MiddleLeft);
        SetRect(text.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(46f, -4f), new Vector2(150f, 30f));
    }

    private void AddRechargeVipBadge(Transform parent, Vector2 position)
    {
        Image badge = CreateImage("RechargeVipBadge", parent, new Color(0.28f, 0.37f, 0.34f, 1f));
        SetRect(badge.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(100f, 96f));

        Image core = CreateImage("VipCore", badge.transform, new Color(0.20f, 0.68f, 0.46f, 1f));
        SetRect(core.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -14f), new Vector2(44f, 54f));

        Text text = CreateMailText("VipText", badge.transform, "尊贵0", 18, FontStyle.Bold, new Color(0.82f, 0.96f, 1f, 1f), TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(8f, -64f), new Vector2(84f, 24f));
    }

    private void AddSoulCrystalPack(Transform parent, int index, Vector2 position, Vector2 size)
    {
        if (index == 3 || index == 4)
        {
            AddRechargeMoneyBag(parent, position + new Vector2(-10f, -4f), size * 1.04f);
        }
        else if (index >= 6)
        {
            AddMiniChest(parent, position + new Vector2(-8f, -4f), size * 1.05f);
        }
        else
        {
            AddSoulCrystalCluster(parent, position, size, index + 1);
        }
    }

    private void AddSoulCrystalCluster(Transform parent, Vector2 position, Vector2 size, int count)
    {
        int gemCount = Mathf.Clamp(count + 1, 2, 5);
        for (int i = 0; i < gemCount; i++)
        {
            Vector2 offset = new Vector2((i % 3) * size.x * 0.22f, -(i / 3) * size.y * 0.18f);
            Vector2 gemSize = size * (0.42f + (i % 2) * 0.08f);
            AddMiniGem(parent, position + offset + new Vector2(18f, -10f - i * 5f), gemSize, new Color(0.32f, 0.72f, 1f, 1f));
        }
    }

    private void AddEnvelopeStack(Transform parent, Vector2 position, Vector2 size, int count)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            AddEnvelope(parent, position + new Vector2(i * 26f, -i * 10f), size, i * -4f);
        }
    }

    private void AddEnvelopeTray(Transform parent, Vector2 position, Vector2 size)
    {
        Image tray = CreateImage("EnvelopeTray", parent, new Color(0.54f, 0.27f, 0.11f, 1f));
        SetRect(tray.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(0f, -size.y + 24f), new Vector2(size.x, 30f));
        tray.raycastTarget = false;
        AddEnvelopeStack(parent, position + new Vector2(24f, 0f), size * 0.78f, 3);
    }

    private void AddEnvelopeSpill(Transform parent, Vector2 position, Vector2 size)
    {
        AddEnvelope(parent, position + new Vector2(18f, -10f), size * 0.62f, -8f);
        AddEnvelope(parent, position + new Vector2(78f, -44f), size * 0.66f, 8f);
        AddEnvelope(parent, position + new Vector2(132f, -24f), size * 0.58f, -3f);
        AddMiniChest(parent, position + new Vector2(176f, -70f), size * 0.38f);
    }

    private void AddEnvelope(Transform parent, Vector2 position, Vector2 size, float rotation)
    {
        Image envelope = CreateImage("RechargeEnvelope", parent, new Color(1f, 0.94f, 0.74f, 1f));
        SetRect(envelope.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        envelope.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        envelope.raycastTarget = false;

        Image flap = CreateImage("EnvelopeFlap", envelope.transform, new Color(0.86f, 0.76f, 0.54f, 0.55f));
        SetRect(flap.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x * 0.18f, -size.y * 0.18f), new Vector2(size.x * 0.64f, size.y * 0.36f));
        flap.raycastTarget = false;

        AddSealToken(envelope.transform, new Vector2(size.x * 0.48f, -size.y * 0.42f), new Vector2(size.x * 0.18f, size.x * 0.18f));
    }

    private void AddRechargeMoneyBag(Transform parent, Vector2 position, Vector2 size)
    {
        Image bag = CreateImage("RechargeMoneyBag", parent, new Color(0.68f, 0.42f, 0.22f, 1f));
        SetRect(bag.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(18f, -42f), size);
        bag.raycastTarget = false;

        Image tie = CreateImage("MoneyBagTie", parent, new Color(0.54f, 0.18f, 0.12f, 1f));
        SetRect(tie.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(size.x * 0.42f, -size.y * 0.18f), new Vector2(size.x * 0.30f, size.y * 0.16f));
        tie.raycastTarget = false;

        for (int i = 0; i < 5; i++)
        {
            AddMiniGem(parent, position + new Vector2(12f + i * 24f, -6f - (i % 2) * 18f), size * 0.26f, new Color(0.32f, 0.72f, 1f, 1f));
        }
    }

    private void AddSealToken(Transform parent, Vector2 position, Vector2 size)
    {
        Image seal = CreateImage("SealToken", parent, new Color(0.78f, 0.06f, 0.08f, 1f));
        SetRect(seal.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        seal.raycastTarget = false;

        Image shine = CreateImage("SealShine", seal.transform, new Color(1f, 0.48f, 0.42f, 0.36f));
        SetRect(shine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x * 0.18f, -size.y * 0.12f), size * 0.35f);
        shine.raycastTarget = false;
    }

    private string FormatRechargePrice(PaymentProductResponse product)
    {
        if (product == null || product.priceFen <= 0)
        {
            return "价格异常";
        }

        if (string.IsNullOrEmpty(product.currency) || string.Equals(product.currency, "CNY", StringComparison.OrdinalIgnoreCase))
        {
            return $"￥{product.priceFen / 100f:0.##}";
        }

        return $"{product.priceFen / 100f:0.##} {product.currency}";
    }

    private string GetRechargeProductName(PaymentProductResponse product)
    {
        return product == null || string.IsNullOrWhiteSpace(product.name) ? "钻石商品" : product.name;
    }

    private string GetRechargeRewardName(PaymentProductResponse product)
    {
        return product == null || string.IsNullOrWhiteSpace(product.rewardItemName) ? "钻石" : product.rewardItemName;
    }

    private PaymentProductResponse[] GetAvailableRechargeProducts(PaymentProductResponse[] products)
    {
        if (products == null || products.Length == 0)
        {
            return Array.Empty<PaymentProductResponse>();
        }

        List<PaymentProductResponse> available = new List<PaymentProductResponse>();
        for (int i = 0; i < products.Length && available.Count < 8; i++)
        {
            PaymentProductResponse product = products[i];
            if (product != null
                && !string.IsNullOrWhiteSpace(product.productCode)
                && product.priceFen > 0
                && GetShopRewardAmount(product) > 0
                && string.Equals(product.rewardItemKey, "diamond", StringComparison.OrdinalIgnoreCase))
            {
                available.Add(product);
            }
        }

        return available.ToArray();
    }

    private void SetRechargeEntryVisible(bool visible)
    {
        if (boundBookView != null)
        {
            Button[] metricPlusButtons = boundBookView.MetricPlusButtons;
            if (metricPlusButtons != null && metricPlusButtons.Length > 3 && metricPlusButtons[3] != null)
            {
                metricPlusButtons[3].gameObject.SetActive(visible);
            }

            if (boundBookView.RechargeButton != null)
            {
                boundBookView.RechargeButton.gameObject.SetActive(visible);
            }

            if (boundBookView.BottomRechargeTab != null)
            {
                boundBookView.BottomRechargeTab.gameObject.SetActive(visible);
            }
        }

        if (mainPageTransform == null)
        {
            return;
        }

        Button[] buttons = mainPageTransform.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            string buttonName = button.name;
            if (buttonName == "RechargeButton"
                || buttonName == "BottomRechargeButton"
                || buttonName == "BottomRechargeTab")
            {
                button.gameObject.SetActive(visible);
            }
        }
    }

    private void ShowLotteryPanel(string status, string rewardName, int rewardValue, bool hasResult, bool isRolling = false)
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("抽奖", status, status);
            return;
        }

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        if (TryShowLotteryScenePanel(status, rewardName, rewardValue, hasResult, isRolling))
        {
            return;
        }

        Image shadow = CreateImage("LotteryPanelShadow", mainPageTransform, new Color(0.08f, 0.05f, 0.03f, 0.44f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(576f, -92f), new Vector2(788f, 900f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("LotteryPanel", mainPageTransform, new Color(1f, 0.78f, 0.08f, 1f));
        lotteryPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(560f, -72f), new Vector2(780f, 895f));

        AddLotteryBackground(panel.transform, new Vector2(780f, 895f));

        Text title = CreateMailText("LotteryTitle", panel.transform, "幸运抽奖机", 56, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(190f, -36f), new Vector2(400f, 76f));
        Outline titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.10f, 0.10f, 0.10f, 0.96f);
        titleOutline.effectDistance = new Vector2(2.4f, -2.4f);

        AddLotteryDoodle(panel.transform, new Vector2(118f, -70f), "?", new Color(0.93f, 0.24f, 0.28f, 1f));
        AddLotteryDoodle(panel.transform, new Vector2(606f, -94f), "!", new Color(0.16f, 0.42f, 0.76f, 1f));

        Button close = CreateButton(panel.transform, "LotteryCloseButton", "X", new Color(0.91f, 0.39f, 0.16f, 1f), Color.white);
        if (isRolling)
        {
            close.interactable = false;
        }
        else
        {
            close.onClick.AddListener(HideLotteryPanel);
        }
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(702f, -22f), new Vector2(54f, 54f));
        ConfigureClearButtonLabel(close, 26);

        Image machineShadow = CreateImage("LotteryMachineShadow", panel.transform, new Color(0.15f, 0.08f, 0.03f, 0.28f));
        SetRect(machineShadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(104f, -206f), new Vector2(590f, 456f));
        machineShadow.raycastTarget = false;

        Image machine = CreateImage("LotteryMachine", panel.transform, new Color(0.96f, 0.30f, 0.34f, 1f));
        SetRect(machine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(92f, -188f), new Vector2(590f, 456f));
        AddLotteryBulbs(machine.transform, new Vector2(590f, 456f));

        Image screen = CreateImage("LotteryScreen", machine.transform, new Color(0.18f, 0.18f, 0.18f, 1f));
        SetRect(screen.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(54f, -74f), new Vector2(482f, 184f));

        string centerName = hasResult ? rewardName : "折扣券";
        string centerAmount = hasResult ? "x" + rewardValue : "10%";
        AddLotteryPrizeCell(screen.transform, new Vector2(12f, -12f), new Vector2(150f, 160f), hasResult ? "木材" : "宝石", hasResult ? "x80" : "x?", false);
        AddLotteryPrizeCell(screen.transform, new Vector2(166f, -12f), new Vector2(150f, 160f), centerName, centerAmount, hasResult);
        AddLotteryPrizeCell(screen.transform, new Vector2(320f, -12f), new Vector2(150f, 160f), hasResult ? "金币" : "闹钟", hasResult ? "x60" : "x1", false);

        Image chanceBox = CreateImage("LotteryChanceBox", machine.transform, Color.white);
        SetRect(chanceBox.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(190f, -300f), new Vector2(226f, 42f));
        Text chanceText = CreateMailText("ChanceText", chanceBox.transform, $"剩余 {GetLotteryChanceCount()} 次机会", 21, FontStyle.Bold, new Color(0.28f, 0.18f, 0.12f, 1f), TextAnchor.MiddleCenter);
        Stretch(chanceText.rectTransform);

        AddBellIcon(machine.transform, new Vector2(60f, -318f), new Vector2(44f, 44f));
        AddMagnifierIcon(machine.transform, new Vector2(502f, -314f), new Vector2(42f, 42f));

        Button prizeButton = CreateButton(panel.transform, "LotteryPrizeButton", string.Empty, new Color(1f, 0.92f, 0.74f, 1f), new Color(0.38f, 0.18f, 0.08f, 1f));
        SetRect(prizeButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(126f, -704f), new Vector2(96f, 92f));
        prizeButton.onClick.AddListener(() => ShowLotteryPanel("奖品池：金币、木材、石材、铁矿，后续可接入服务器奖池", rewardName, rewardValue, hasResult));
        AddGiftIcon(prizeButton.transform, new Vector2(22f, -10f), new Vector2(52f, 48f));
        Text prizeText = CreateMailText("PrizeText", prizeButton.transform, "奖品", 17, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetRect(prizeText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(14f, -60f), new Vector2(68f, 24f));

        Button start = CreateButton(panel.transform, "LotteryStartButton", isRolling ? "翻动中..." : "点击开始", new Color(0.96f, 0.32f, 0.34f, 1f), Color.white);
        if (isRolling)
        {
            start.interactable = false;
        }
        else
        {
            start.onClick.AddListener(PerformLotteryDraw);
        }
        SetRect(start.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(286f, -722f), new Vector2(210f, 62f));
        ConfigureClearButtonLabel(start, 28);

        Button ruleButton = CreateButton(panel.transform, "LotteryRuleButton", string.Empty, new Color(1f, 0.96f, 0.80f, 1f), new Color(0.38f, 0.18f, 0.08f, 1f));
        SetRect(ruleButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(560f, -704f), new Vector2(96f, 92f));
        ruleButton.onClick.AddListener(() => ShowLotteryPanel("规则：初始 100 次机会，每次抽奖消耗 1 次", rewardName, rewardValue, hasResult));
        Text exclamation = CreateMailText("RuleIcon", ruleButton.transform, "!", 44, FontStyle.Bold, new Color(0.95f, 0.22f, 0.20f, 1f), TextAnchor.MiddleCenter);
        SetRect(exclamation.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -4f), new Vector2(52f, 54f));
        Text ruleText = CreateMailText("RuleText", ruleButton.transform, "规则", 17, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetRect(ruleText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(14f, -60f), new Vector2(68f, 24f));

        Text statusText = CreateMailText("LotteryStatus", panel.transform, status, 22, FontStyle.Bold, new Color(0.34f, 0.14f, 0.07f, 1f), TextAnchor.MiddleCenter);
        SetRect(statusText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(100f, -626f), new Vector2(580f, 46f));

        SetStatus(status);
    }

    private int GetLotteryChanceCount()
    {
        return EnsureLotteryChanceCount();
    }

    private int EnsureLotteryChanceCount()
    {
        if (!PlayerPrefs.HasKey(LotteryChanceKey))
        {
            PlayerPrefs.SetInt(LotteryChanceKey, DefaultLotteryChanceCount);
            PlayerPrefs.Save();
        }

        return PlayerPrefs.GetInt(LotteryChanceKey, DefaultLotteryChanceCount);
    }

    private void AddLotteryBackground(Transform parent, Vector2 size)
    {
        for (int i = 0; i < 6; i++)
        {
            Image stripe = CreateImage("LotteryStripe", parent, i % 2 == 0 ? new Color(1f, 0.72f, 0.05f, 0.52f) : new Color(1f, 0.88f, 0.22f, 0.52f));
            SetRect(stripe.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(i * 130f, 0f), new Vector2(130f, size.y));
            stripe.raycastTarget = false;
        }

        Image bottomBand = CreateImage("LotteryBottomBand", parent, new Color(1f, 0.69f, 0.03f, 0.42f));
        SetRect(bottomBand.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -660f), new Vector2(size.x, 235f));
        bottomBand.raycastTarget = false;
    }

    private void AddLotteryBulbs(Transform parent, Vector2 size)
    {
        Color bulb = new Color(1f, 0.91f, 0.55f, 1f);
        for (int i = 0; i < 19; i++)
        {
            AddSmallBulb(parent, new Vector2(22f + i * 29f, -22f), bulb);
            AddSmallBulb(parent, new Vector2(22f + i * 29f, -size.y + 34f), bulb);
        }

        for (int i = 0; i < 12; i++)
        {
            AddSmallBulb(parent, new Vector2(18f, -54f - i * 29f), bulb);
            AddSmallBulb(parent, new Vector2(size.x - 28f, -54f - i * 29f), bulb);
        }
    }

    private void AddSmallBulb(Transform parent, Vector2 position, Color color)
    {
        Image bulb = CreateImage("LotteryBulb", parent, color);
        SetRect(bulb.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(10f, 10f));
        bulb.raycastTarget = false;
    }

    private void AddLotteryPrizeCell(Transform parent, Vector2 position, Vector2 size, string label, string amount, bool highlight)
    {
        Image cell = CreateImage("LotteryPrizeCell", parent, highlight ? new Color(1f, 0.96f, 0.80f, 1f) : Color.white);
        SetRect(cell.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        if (label.Contains("箱"))
        {
            AddMiniChest(cell.transform, new Vector2(42f, -42f), new Vector2(68f, 54f));
        }
        else if (label.Contains("闹钟"))
        {
            AddAlarmIcon(cell.transform, new Vector2(48f, -34f), new Vector2(58f, 58f));
        }
        else if (label.Contains("券") || label.Contains("%"))
        {
            AddCouponIcon(cell.transform, new Vector2(42f, -40f), new Vector2(68f, 48f));
        }
        else
        {
            Color gemColor = label.Contains("金币") ? new Color(0.97f, 0.66f, 0.18f, 1f) : new Color(0.12f, 0.56f, 0.92f, 1f);
            AddMiniGem(cell.transform, new Vector2(42f, -38f), new Vector2(68f, 58f), gemColor);
        }

        Image shadow = CreateImage("PrizeShadow", cell.transform, new Color(0.25f, 0.20f, 0.16f, 0.12f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(42f, -104f), new Vector2(70f, 12f));
        shadow.raycastTarget = false;

        Text text = CreateMailText("PrizeText", cell.transform, $"{label}\n{amount}", 17, FontStyle.Bold, new Color(0.32f, 0.18f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(12f, -118f), new Vector2(size.x - 24f, 38f));
    }

    private void AddLotteryDoodle(Transform parent, Vector2 position, string text, Color color)
    {
        Text doodle = CreateMailText("LotteryDoodle", parent, text, 34, FontStyle.Bold, color, TextAnchor.MiddleCenter);
        SetRect(doodle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(42f, 42f));
    }

    private void AddCouponIcon(Transform parent, Vector2 position, Vector2 size)
    {
        Image coupon = CreateImage("CouponIcon", parent, new Color(0.93f, 0.18f, 0.23f, 1f));
        SetRect(coupon.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        coupon.raycastTarget = false;

        Text percent = CreateMailText("CouponPercent", coupon.transform, "10%", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(percent.rectTransform);
    }

    private void AddAlarmIcon(Transform parent, Vector2 position, Vector2 size)
    {
        Image clock = CreateImage("AlarmClock", parent, new Color(1f, 0.94f, 0.78f, 1f));
        SetRect(clock.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        clock.raycastTarget = false;

        Text hand = CreateMailText("AlarmHand", clock.transform, "\u25f7", 34, FontStyle.Bold, new Color(0.94f, 0.30f, 0.22f, 1f), TextAnchor.MiddleCenter);
        Stretch(hand.rectTransform);
    }

    private void AddBellIcon(Transform parent, Vector2 position, Vector2 size)
    {
        Image bell = CreateImage("BellIcon", parent, new Color(0.97f, 0.77f, 0.22f, 1f));
        SetRect(bell.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        bell.raycastTarget = false;
    }

    private void AddMagnifierIcon(Transform parent, Vector2 position, Vector2 size)
    {
        Image lens = CreateImage("MagnifierLens", parent, new Color(1f, 0.94f, 0.84f, 1f));
        SetRect(lens.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        lens.raycastTarget = false;

        Image handle = CreateImage("MagnifierHandle", parent, new Color(0.18f, 0.14f, 0.12f, 1f));
        SetRect(handle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(size.x * 0.68f, -size.y * 0.70f), new Vector2(size.x * 0.18f, size.y * 0.52f));
        handle.raycastTarget = false;
    }

    private void AddGiftIcon(Transform parent, Vector2 position, Vector2 size)
    {
        Image box = CreateImage("GiftBox", parent, new Color(0.98f, 0.22f, 0.28f, 1f));
        SetRect(box.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(0f, -size.y * 0.25f), new Vector2(size.x, size.y * 0.75f));
        box.raycastTarget = false;

        Image ribbonV = CreateImage("GiftRibbonV", parent, Color.white);
        SetRect(ribbonV.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(size.x * 0.42f, -size.y * 0.25f), new Vector2(size.x * 0.16f, size.y * 0.75f));
        ribbonV.raycastTarget = false;

        Image ribbonH = CreateImage("GiftRibbonH", parent, Color.white);
        SetRect(ribbonH.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(0f, -size.y * 0.44f), new Vector2(size.x, size.y * 0.16f));
        ribbonH.raycastTarget = false;
    }

    private void ShowDailySignPanel(int signedDays, bool todaySigned, string status)
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("每日签到", status, status);
            return;
        }

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        if (TryShowDailySignScenePanel(signedDays, todaySigned, status))
        {
            return;
        }

        Image shadow = CreateImage("SignPanelShadow", mainPageTransform, new Color(0.08f, 0.05f, 0.03f, 0.45f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(356f, -118f), new Vector2(1216f, 820f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("SignPanel", mainPageTransform, new Color(0.86f, 0.70f, 0.42f, 1f));
        signPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(340f, -100f), new Vector2(1210f, 815f));

        AddSignPanelFrame(panel.transform, new Vector2(1210f, 815f), new Color(0.24f, 0.43f, 0.74f, 1f), new Color(0.97f, 0.78f, 0.36f, 1f));

        Text title = CreateMailText("Title", panel.transform, "每日签到", 54, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(410f, -22f), new Vector2(390f, 74f));
        Outline titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.18f, 0.25f, 0.46f, 0.95f);
        titleOutline.effectDistance = new Vector2(2f, -2f);

        Text subTitle = CreateMailText("SubTitle", panel.transform, "两周签到奖励", 22, FontStyle.Bold, new Color(0.38f, 0.24f, 0.11f, 1f), TextAnchor.MiddleCenter);
        SetRect(subTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(486f, -92f), new Vector2(240f, 34f));

        Button close = CreateButton(panel.transform, "SignCloseButton", "X", new Color(0.91f, 0.39f, 0.16f, 1f), Color.white);
        close.onClick.AddListener(HideSignPanel);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1128f, -28f), new Vector2(58f, 58f));
        ConfigureClearButtonLabel(close, 27);

        string[] rewardNames = GetDailySignRewardNames();
        string[] rewardAmounts = GetDailySignRewardAmounts();
        for (int i = 0; i < 14; i++)
        {
            int row = i / 7;
            int col = i % 7;
            Vector2 position = new Vector2(50f + col * 162f, -142f - row * 244f);
            bool claimed = i + 1 <= signedDays && signedDays > 0;
            bool highlight = todaySigned && i + 1 == signedDays;
            AddSignRewardCard(panel.transform, i + 1, rewardNames[i], rewardAmounts[i], position, new Vector2(142f, 216f), claimed, highlight, false);
        }

        Image footer = CreateImage("SignFooter", panel.transform, new Color(1f, 0.94f, 0.74f, 0.94f));
        SetRect(footer.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(62f, -662f), new Vector2(1086f, 92f));

        Text footerText = CreateMailText("FooterText", footer.transform, $"进度 {Mathf.Clamp(signedDays, 0, 14)}/14    {status}", 24, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleCenter);
        Stretch(footerText.rectTransform);

        SetStatus(status);
    }

    private void ShowNewbieSignPanel(int signedDays, string status)
    {
        if (mainPageTransform == null)
        {
            SetLeftContent("新手签到", status, status);
            return;
        }

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        if (TryShowNewbieSignScenePanel(signedDays, status))
        {
            return;
        }

        Image shadow = CreateImage("SignPanelShadow", mainPageTransform, new Color(0.08f, 0.05f, 0.03f, 0.45f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(462f, -174f), new Vector2(1016f, 662f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("SignPanel", mainPageTransform, new Color(0.91f, 0.78f, 0.48f, 1f));
        signPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(448f, -156f), new Vector2(1010f, 655f));

        AddSignPanelFrame(panel.transform, new Vector2(1010f, 655f), new Color(0.28f, 0.55f, 0.50f, 1f), new Color(0.98f, 0.71f, 0.31f, 1f));

        Text title = CreateMailText("Title", panel.transform, "新手签到", 50, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(330f, -24f), new Vector2(350f, 70f));
        Outline titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.10f, 0.35f, 0.32f, 0.95f);
        titleOutline.effectDistance = new Vector2(2f, -2f);

        Button close = CreateButton(panel.transform, "SignCloseButton", "X", new Color(0.91f, 0.39f, 0.16f, 1f), Color.white);
        close.onClick.AddListener(HideSignPanel);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(930f, -28f), new Vector2(56f, 56f));
        ConfigureClearButtonLabel(close, 27);

        string[] names = { "金币", "木材 石材", "装备宝箱" };
        string[] amounts = { "x300", "x300", "x1" };
        for (int i = 0; i < 3; i++)
        {
            Vector2 position = new Vector2(76f + i * 300f, -130f);
            bool claimed = i + 1 <= signedDays;
            bool highlight = i + 1 == signedDays && signedDays <= 3;
            AddSignRewardCard(panel.transform, i + 1, names[i], amounts[i], position, new Vector2(250f, 300f), claimed, highlight, true);
        }

        Image banner = CreateImage("NewbieBanner", panel.transform, new Color(1f, 0.96f, 0.78f, 0.96f));
        SetRect(banner.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(76f, -472f), new Vector2(850f, 110f));

        Text bannerTitle = CreateMailText("BannerTitle", banner.transform, "连续签到 3 天", 30, FontStyle.Bold, new Color(0.48f, 0.20f, 0.07f, 1f), TextAnchor.MiddleLeft);
        SetRect(bannerTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(34f, -18f), new Vector2(280f, 38f));

        Text bannerText = CreateMailText("BannerText", banner.transform, $"进度 {Mathf.Clamp(signedDays, 0, 3)}/3    {status}", 22, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetRect(bannerText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(34f, -62f), new Vector2(610f, 34f));

        AddMiniChest(banner.transform, new Vector2(690f, -22f), new Vector2(92f, 66f));
        AddMiniGem(banner.transform, new Vector2(784f, -30f), new Vector2(48f, 48f), new Color(0.20f, 0.58f, 0.95f, 1f));

        SetStatus(status);
    }

    private void AddSignRewardCard(Transform parent, int day, string rewardName, string rewardAmount, Vector2 position, Vector2 size, bool claimed, bool highlight, bool large)
    {
        Image shadow = CreateImage("SignCardShadow", parent, new Color(0.12f, 0.08f, 0.04f, 0.25f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(5f, -5f), size);
        shadow.raycastTarget = false;

        Image card = CreateImage("SignCard", parent, highlight ? new Color(1f, 0.95f, 0.70f, 1f) : new Color(1f, 0.92f, 0.74f, 1f));
        SetRect(card.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);

        Image header = CreateImage("SignCardHeader", card.transform, claimed ? new Color(0.56f, 0.39f, 0.22f, 1f) : new Color(0.93f, 0.52f, 0.20f, 1f));
        SetRect(header.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(size.x, large ? 54f : 42f));

        Text dayText = CreateMailText("DayText", header.transform, $"第{day}天", large ? 26 : 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(dayText.rectTransform);

        Vector2 iconPos = large ? new Vector2(size.x * 0.5f - 44f, -92f) : new Vector2(size.x * 0.5f - 31f, -66f);
        Vector2 iconSize = large ? new Vector2(88f, 74f) : new Vector2(62f, 54f);
        if (rewardName.Contains("箱"))
        {
            AddMiniChest(card.transform, iconPos, iconSize);
        }
        else
        {
            Color gemColor = rewardName.Contains("金币") ? new Color(0.96f, 0.64f, 0.18f, 1f) : new Color(0.16f, 0.57f, 0.93f, 1f);
            AddMiniGem(card.transform, iconPos, iconSize, gemColor);
        }

        Text reward = CreateMailText("RewardText", card.transform, $"{rewardName}\n{rewardAmount}", large ? 24 : 17, FontStyle.Bold, new Color(0.40f, 0.22f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetRect(reward.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(12f, large ? -200f : -144f), new Vector2(size.x - 24f, large ? 62f : 50f));

        if (claimed)
        {
            Text check = CreateMailText("ClaimedCheck", card.transform, "\u2713", large ? 72 : 52, FontStyle.Bold, new Color(0.42f, 0.88f, 0.18f, 1f), TextAnchor.MiddleCenter);
            SetRect(check.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x * 0.5f - 50f, large ? -86f : -60f), new Vector2(100f, 80f));

            Image chip = CreateImage("ClaimedChip", card.transform, new Color(0.38f, 0.31f, 0.22f, 0.78f));
            SetRect(chip.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x * 0.5f - 46f, large ? -258f : -186f), new Vector2(92f, 30f));
            Text claimedText = CreateMailText("ClaimedText", card.transform, "已领取", large ? 22 : 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(claimedText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x * 0.5f - 46f, large ? -258f : -186f), new Vector2(92f, 30f));
        }
    }

    private void AddSignPanelFrame(Transform parent, Vector2 size, Color frameColor, Color cornerColor)
    {
        Image inner = CreateImage("SignPanelInner", parent, new Color(1f, 0.90f, 0.62f, 0.24f));
        SetRect(inner.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -22f), size - new Vector2(44f, 44f));
        inner.raycastTarget = false;

        Image top = CreateImage("SignFrameTop", parent, frameColor);
        SetRect(top.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(size.x, 22f));
        top.raycastTarget = false;

        Image bottom = CreateImage("SignFrameBottom", parent, frameColor);
        SetRect(bottom.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(0f, -size.y + 22f), new Vector2(size.x, 22f));
        bottom.raycastTarget = false;

        Image left = CreateImage("SignFrameLeft", parent, frameColor);
        SetRect(left.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(22f, size.y));
        left.raycastTarget = false;

        Image right = CreateImage("SignFrameRight", parent, frameColor);
        SetRect(right.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x - 22f, 0f), new Vector2(22f, size.y));
        right.raycastTarget = false;

        int notchCount = Mathf.Max(1, Mathf.FloorToInt((size.x - 90f) / 126f));
        for (int i = 0; i < notchCount; i++)
        {
            Image notch = CreateImage("SignFrameNotch", parent, cornerColor);
            SetRect(notch.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(42f + i * 126f, -10f), new Vector2(46f, 10f));
            notch.raycastTarget = false;
        }
    }

    private void AddMiniGem(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image back = CreateImage("MiniGemBack", parent, Color.Lerp(color, Color.black, 0.20f));
        SetRect(back.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(5f, -6f), size);
        back.raycastTarget = false;

        Image gem = CreateImage("MiniGem", parent, color);
        SetRect(gem.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        gem.raycastTarget = false;

        Image shine = CreateImage("MiniGemShine", parent, new Color(1f, 1f, 1f, 0.38f));
        SetRect(shine.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(size.x * 0.18f, -size.y * 0.14f), size * 0.34f);
        shine.raycastTarget = false;
    }

    private void AddMiniChest(Transform parent, Vector2 position, Vector2 size)
    {
        Image baseBox = CreateImage("MiniChestBase", parent, new Color(0.58f, 0.28f, 0.12f, 1f));
        SetRect(baseBox.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(0f, -size.y * 0.34f), new Vector2(size.x, size.y * 0.58f));
        baseBox.raycastTarget = false;

        Image lid = CreateImage("MiniChestLid", parent, new Color(0.83f, 0.52f, 0.23f, 1f));
        SetRect(lid.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(size.x, size.y * 0.45f));
        lid.raycastTarget = false;

        Image lockPlate = CreateImage("MiniChestLock", parent, new Color(1f, 0.82f, 0.36f, 1f));
        SetRect(lockPlate.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(size.x * 0.40f, -size.y * 0.34f), new Vector2(size.x * 0.20f, size.y * 0.34f));
        lockPlate.raycastTarget = false;
    }

    private string[] GetDailySignRewardNames()
    {
        return new[]
        {
            "金币", "木材", "石材", "英雄箱", "铁矿", "金币", "抽奖券",
            "木材", "石材", "铁矿", "金币", "加速道具", "抽奖券", "高级英雄箱"
        };
    }

    private string[] GetDailySignRewardAmounts()
    {
        return new[]
        {
            "x100", "x120", "x120", "x1", "x80", "x200", "x1",
            "x180", "x180", "x120", "x260", "x1", "x2", "x1"
        };
    }

    private void ApplyDailySignReward(int day)
    {
        switch (day)
        {
            case 1:
                AddLocalResource("Gold", 100);
                break;
            case 2:
                AddLocalResource("Wood", 120);
                break;
            case 3:
                AddLocalResource("Stone", 120);
                break;
            case 4:
                AddLocalResource("HeroChest", 1);
                break;
            case 5:
                AddLocalResource("Iron", 80);
                break;
            case 6:
                AddLocalResource("Gold", 200);
                break;
            case 7:
                AddLocalResource("LotteryTicket", 1);
                break;
            case 8:
                AddLocalResource("Wood", 180);
                break;
            case 9:
                AddLocalResource("Stone", 180);
                break;
            case 10:
                AddLocalResource("Iron", 120);
                break;
            case 11:
                AddLocalResource("Gold", 260);
                break;
            case 12:
                AddLocalResource("SpeedItem", 1);
                break;
            case 13:
                AddLocalResource("LotteryTicket", 2);
                break;
            case 14:
                AddLocalResource("AdvancedHeroChest", 1);
                break;
        }
    }

    private void ShowSystemMailPanel(MailResponse[] mails, string message)
    {
        CaptureMailList(mails);
        mails = currentServerMails;
        if (mainPageTransform == null)
        {
            SetLeftContent("邮件", message ?? BuildMailText(mails), "已打开系统邮件");
            return;
        }

        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();

        if (TryShowSystemMailScenePanel(mails, message))
        {
            return;
        }

        Image shadow = CreateImage("SystemMailPanelShadow", mainPageTransform, new Color(0.10f, 0.05f, 0.03f, 0.42f));
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(328f, -146f), new Vector2(1292f, 768f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("SystemMailPanel", mainPageTransform, new Color(1.00f, 0.95f, 0.77f, 1f));
        mailPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(310f, -128f), new Vector2(1290f, 765f));

        AddAirmailBorder(panel.transform, new Vector2(1290f, 765f));
        AddMailPanelWash(panel.transform);

        Text icon = CreateMailText("MailIcon", panel.transform, "\u2709", 46, FontStyle.Bold, new Color(0.48f, 0.18f, 0.12f, 1f), TextAnchor.MiddleCenter);
        SetRect(icon.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(48f, -38f), new Vector2(58f, 58f));

        Text title = CreateMailText("MailTitle", panel.transform, "系统邮件", 42, FontStyle.Bold, new Color(0.34f, 0.17f, 0.10f, 1f), TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(112f, -42f), new Vector2(280f, 56f));

        Button close = CreateButton(panel.transform, "SystemMailCloseButton", "X", new Color(0.92f, 0.26f, 0.36f, 1f), Color.white);
        close.onClick.AddListener(HideSystemMailPanel);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1218f, -34f), new Vector2(52f, 52f));
        ConfigureClearButtonLabel(close, 24);

        Image leftList = CreateImage("MailListArea", panel.transform, new Color(1f, 0.84f, 0.58f, 0.95f));
        SetRect(leftList.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(56f, -128f), new Vector2(384f, 555f));

        Image listHeader = CreateImage("MailListHeader", leftList.transform, new Color(0.82f, 0.32f, 0.28f, 1f));
        SetRect(listHeader.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(16f, -16f), new Vector2(352f, 54f));

        Text listTitle = CreateMailText("Text", listHeader.transform, "邮件列表", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetRect(listTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f, -7f), new Vector2(150f, 40f));

        Button claimAll = CreateButton(listHeader.transform, "ClaimAllButton", "一键领取", new Color(1f, 0.73f, 0.25f, 1f), new Color(0.44f, 0.16f, 0.06f, 1f));
        SetRect(claimAll.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(212f, -7f), new Vector2(124f, 40f));
        ConfigureClearButtonLabel(claimAll, 18);
        claimAll.onClick.AddListener(ClaimAllMailAttachments);

        Image detail = CreateImage("MailDetailArea", panel.transform, new Color(1f, 0.98f, 0.88f, 0.98f));
        SetRect(detail.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(470f, -128f), new Vector2(620f, 555f));

        Text detailTitle = CreateMailText("MailDetailTitle", detail.transform, string.Empty, 30, FontStyle.Bold, new Color(0.33f, 0.15f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetRect(detailTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(30f, -28f), new Vector2(548f, 48f));

        Image divider = CreateImage("DetailDivider", detail.transform, new Color(0.78f, 0.42f, 0.26f, 0.55f));
        SetRect(divider.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -92f), new Vector2(564f, 4f));
        divider.raycastTarget = false;

        Text detailBody = CreateMailText("MailDetailBody", detail.transform, string.Empty, 20, FontStyle.Normal, InkColor, TextAnchor.UpperLeft);
        SetRect(detailBody.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -116f), new Vector2(552f, 215f));

        Text rewardTitle = CreateMailText("RewardTitle", detail.transform, "奖励", 24, FontStyle.Bold, new Color(0.76f, 0.22f, 0.15f, 1f), TextAnchor.MiddleLeft);
        SetRect(rewardTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(32f, -350f), new Vector2(140f, 38f));

        AddRewardSlot(detail.transform, new Vector2(32f, -400f), "金币", "x100");
        AddRewardSlot(detail.transform, new Vector2(154f, -400f), "木材", "x80");
        AddRewardSlot(detail.transform, new Vector2(276f, -400f), "石材", "x80");

        Button claim = CreateButton(detail.transform, "ClaimMailButton", "领取", new Color(0.83f, 0.30f, 0.23f, 1f), Color.white);
        SetRect(claim.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(446f, -475f), new Vector2(126f, 48f));
        ConfigureClearButtonLabel(claim, 22);
        claim.onClick.AddListener(ClaimSelectedMailAttachment);

        BuildMailListItems(leftList.transform, mails, message, detailTitle, detailBody);
        SetStatus("已打开系统邮件");
    }

    private void BuildMailListItems(Transform listParent, MailResponse[] mails, string message, Text detailTitle, Text detailBody)
    {
        if (mails == null || mails.Length == 0)
        {
            Image emptyCard = CreateImage("MailEmptyCard", listParent, new Color(1f, 0.95f, 0.76f, 1f));
            SetRect(emptyCard.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(26f, -96f), new Vector2(332f, 132f));

            Text empty = CreateMailText("Text", emptyCard.transform, "暂无邮件", 24, FontStyle.Bold, MutedColor, TextAnchor.MiddleCenter);
            Stretch(empty.rectTransform);

            detailTitle.text = "系统提示";
            detailBody.text = string.IsNullOrEmpty(message)
                ? "当前没有可展示的系统邮件。\n\n收到活动奖励、补偿奖励或系统通知后，会在这里显示。"
                : message;
            return;
        }

        int count = Mathf.Min(mails.Length, 5);
        int initialIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (mails[i] != null && mails[i].mailId == selectedMailId)
            {
                initialIndex = i;
                break;
            }
        }

        for (int i = 0; i < count; i++)
        {
            MailResponse mail = mails[i];
            bool selected = i == initialIndex;
            Button card = CreateButton(listParent, "MailItem" + i, string.Empty, selected ? new Color(1f, 0.92f, 0.45f, 1f) : new Color(1f, 0.96f, 0.78f, 1f), InkColor);
            SetRect(card.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(26f, -96f - i * 84f), new Vector2(332f, 72f));

            Image mark = CreateImage("UnreadMark", card.transform, mail.readFlag ? new Color(0.62f, 0.56f, 0.48f, 1f) : new Color(0.90f, 0.22f, 0.16f, 1f));
            SetRect(mark.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(14f, -20f), new Vector2(14f, 14f));

            string title = string.IsNullOrEmpty(mail.title) ? "系统邮件" : mail.title;
            Text itemTitle = CreateMailText("Title", card.transform, title, 19, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
            SetRect(itemTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(38f, -10f), new Vector2(258f, 28f));

            string sender = string.IsNullOrEmpty(mail.senderName) ? "系统" : mail.senderName;
            string attachment = mail.hasAttachment ? (mail.attachmentClaimed ? "附件已领" : "可领取") : "无附件";
            string type = string.IsNullOrEmpty(mail.mailType) ? "SYSTEM" : mail.mailType;
            string expiry = string.IsNullOrEmpty(mail.expireTime) ? string.Empty : $"  Expires: {mail.expireTime}";
            Text itemMeta = CreateMailText("Meta", card.transform, $"{sender}  {type}  {attachment}{expiry}", 15, FontStyle.Normal, MutedColor, TextAnchor.MiddleLeft);
            SetRect(itemMeta.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(38f, -42f), new Vector2(258f, 24f));

            MailResponse capturedMail = mail;
            card.onClick.AddListener(() => UpdateMailDetail(capturedMail, detailTitle, detailBody));
        }

        MailResponse selectedMail = FindCurrentMail(selectedMailId);
        UpdateMailDetail(selectedMail ?? mails[initialIndex], detailTitle, detailBody);
    }

    private void UpdateMailDetail(MailResponse mail, Text detailTitle, Text detailBody)
    {
        SelectMailForActions(mail);
        string title = string.IsNullOrEmpty(mail.title) ? "系统邮件" : mail.title;
        string sender = string.IsNullOrEmpty(mail.senderName) ? "系统" : mail.senderName;
        string content = string.IsNullOrEmpty(mail.content) ? "暂无详细内容，正在读取邮件详情..." : mail.content;
        string attachment = mail.hasAttachment ? (mail.attachmentClaimed ? "附件状态：已领取" : "附件状态：可领取") : "附件状态：无附件";
        string time = string.IsNullOrEmpty(mail.createTime) ? "时间：未知" : $"时间：{mail.createTime}";

        detailTitle.text = title;
        detailBody.text = $"{content}\n\n发件人：{sender}\n{time}\n{attachment}\n{BuildMailAttachmentSummary(mail)}";

        Button claimButton = mailPanelObject == null
            ? null
            : FindSceneComponent<Button>(mailPanelObject.transform, "ClaimMailButton");
        if (claimButton != null)
        {
            claimButton.interactable = mail.hasAttachment && !mail.attachmentClaimed && !mail.expired && !mailMutationRunning;
            SetSceneButtonText(claimButton, mail.attachmentClaimed ? "已领取" : "领取");
        }
    }

    private void AddAirmailBorder(Transform parent, Vector2 size)
    {
        Color red = new Color(0.84f, 0.20f, 0.20f, 1f);
        Color blue = new Color(0.18f, 0.40f, 0.70f, 1f);

        for (int i = 0; i < 25; i++)
        {
            Color color = i % 2 == 0 ? red : blue;
            Image top = CreateImage("AirmailTop", parent, color);
            SetRect(top.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f + i * 50f, -14f), new Vector2(32f, 12f));
            top.raycastTarget = false;

            Image bottom = CreateImage("AirmailBottom", parent, color);
            SetRect(bottom.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f + i * 50f, -size.y + 26f), new Vector2(32f, 12f));
            bottom.raycastTarget = false;
        }

        for (int i = 0; i < 14; i++)
        {
            Color color = i % 2 == 0 ? blue : red;
            Image left = CreateImage("AirmailLeft", parent, color);
            SetRect(left.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(14f, -34f - i * 50f), new Vector2(12f, 32f));
            left.raycastTarget = false;

            Image right = CreateImage("AirmailRight", parent, color);
            SetRect(right.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(size.x - 26f, -34f - i * 50f), new Vector2(12f, 32f));
            right.raycastTarget = false;
        }
    }

    private void AddMailPanelWash(Transform parent)
    {
        Image headerWash = CreateImage("MailHeaderWash", parent, new Color(1f, 0.76f, 0.54f, 0.24f));
        SetRect(headerWash.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(34f, -34f), new Vector2(1168f, 78f));
        headerWash.raycastTarget = false;

        Image bottomWash = CreateImage("MailBottomWash", parent, new Color(0.71f, 0.40f, 0.20f, 0.12f));
        SetRect(bottomWash.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(44f, -704f), new Vector2(1168f, 24f));
        bottomWash.raycastTarget = false;
    }

    private void AddRewardSlot(Transform parent, Vector2 position, string name, string amount)
    {
        Image slot = CreateImage("RewardSlot", parent, new Color(1f, 0.88f, 0.55f, 1f));
        SetRect(slot.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(98f, 82f));

        Image gem = CreateImage("RewardIcon", slot.transform, new Color(0.93f, 0.42f, 0.20f, 1f));
        SetRect(gem.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(30f, -10f), new Vector2(38f, 34f));
        gem.raycastTarget = false;

        Text label = CreateMailText("RewardText", slot.transform, amount, 15, FontStyle.Bold, new Color(0.36f, 0.16f, 0.07f, 1f), TextAnchor.MiddleCenter);
        SetRect(label.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(6f, -42f), new Vector2(86f, 34f));
    }

    private void AddMailTabButton(Transform parent, Vector2 position, string label, bool selected)
    {
        Color color = selected ? new Color(0.95f, 0.42f, 0.32f, 1f) : new Color(0.96f, 0.77f, 0.57f, 1f);
        Color textColor = selected ? Color.white : new Color(0.45f, 0.18f, 0.10f, 1f);
        Button button = CreateButton(parent, "MailTab" + label, "\u2709  " + label, color, textColor);
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(134f, 56f));
        ConfigureClearButtonLabel(button, 18);
        button.onClick.AddListener(() => SetStatus($"已选择{label}邮件分类"));
    }

    private void HideSystemMailPanel()
    {
        if (HideRegisteredScenePanel(HomePageScenePanelType.Mail, ref mailPanelObject))
        {
            ShowMapSelectionContent();
            return;
        }

        if (mailPanelObject == null)
        {
            return;
        }

        Transform shadow = mainPageTransform != null ? mainPageTransform.Find("SystemMailPanelShadow") : null;
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }

        Destroy(mailPanelObject);
        mailPanelObject = null;
        ShowMapSelectionContent();
    }

    private void HideSignPanel()
    {
        HideSignMakeupConfirmation();
        bool hidDailyPanel = HideRegisteredScenePanel(HomePageScenePanelType.DailySign, ref signPanelObject);
        bool hidNewbiePanel = HideRegisteredScenePanel(HomePageScenePanelType.NewbieSign, ref signPanelObject);
        if (hidDailyPanel || hidNewbiePanel)
        {
            return;
        }

        if (signPanelObject == null)
        {
            return;
        }

        Transform shadow = mainPageTransform != null ? mainPageTransform.Find("SignPanelShadow") : null;
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }

        Destroy(signPanelObject);
        signPanelObject = null;
    }

    private void HideLotteryPanel()
    {
        if (HideRegisteredScenePanel(HomePageScenePanelType.Lottery, ref lotteryPanelObject))
        {
            return;
        }

        if (lotteryPanelObject == null)
        {
            return;
        }

        Transform shadow = mainPageTransform != null ? mainPageTransform.Find("LotteryPanelShadow") : null;
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }

        Destroy(lotteryPanelObject);
        lotteryPanelObject = null;
    }

    private void HideRechargePanel()
    {
        if (HideRegisteredScenePanel(HomePageScenePanelType.Recharge, ref rechargePanelObject))
        {
            ShowMapSelectionContent();
            return;
        }

        if (rechargePanelObject == null)
        {
            return;
        }

        Transform shadow = mainPageTransform != null ? mainPageTransform.Find("RechargePanelShadow") : null;
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }

        Destroy(rechargePanelObject);
        rechargePanelObject = null;
        ShowMapSelectionContent();
    }

    private void HideTaskPanel()
    {
        if (HideRegisteredScenePanel(HomePageScenePanelType.Task, ref taskPanelObject))
        {
            return;
        }

        if (taskPanelObject == null)
        {
            return;
        }

        Transform shadow = mainPageTransform != null ? mainPageTransform.Find("TaskPanelShadow") : null;
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }

        Destroy(taskPanelObject);
        taskPanelObject = null;
    }

    private void HideInventoryPanel()
    {
        HideResourceSaleDialog();
        HideInventoryInputBlocker();
        if (HideRegisteredScenePanel(HomePageScenePanelType.Inventory, ref inventoryPanelObject))
        {
            inventoryPanelView = null;
            return;
        }

        if (inventoryPanelObject == null)
        {
            return;
        }

        Destroy(inventoryPanelObject);
        inventoryPanelObject = null;
        inventoryPanelView = null;
    }

    private void EnsureInventoryInputBlocker(Transform parent)
    {
        if (parent == null)
        {
            Debug.LogWarning("无法创建仓库输入遮挡层：父节点为空。", this);
            return;
        }

        if (inventoryInputBlockerObject == null)
        {
            Transform existing = parent.Find("InventoryInputBlocker");
            if (existing != null)
            {
                inventoryInputBlockerObject = existing.gameObject;
            }
            else
            {
                Image blocker = CreateImage("InventoryInputBlocker", parent, Color.clear);
                inventoryInputBlockerObject = blocker.gameObject;
            }
        }
        else if (inventoryInputBlockerObject.transform.parent != parent)
        {
            inventoryInputBlockerObject.transform.SetParent(parent, false);
        }

        Image blockerImage = inventoryInputBlockerObject.GetComponent<Image>();
        if (blockerImage == null)
        {
            blockerImage = inventoryInputBlockerObject.AddComponent<Image>();
        }

        blockerImage.color = Color.clear;
        blockerImage.raycastTarget = true;
        RectTransform blockerRect = inventoryInputBlockerObject.GetComponent<RectTransform>();
        if (blockerRect == null)
        {
            Debug.LogWarning("无法设置仓库输入遮挡层尺寸：RectTransform 缺失。", this);
            return;
        }

        Stretch(blockerRect);
        inventoryInputBlockerObject.transform.SetAsLastSibling();
        inventoryInputBlockerObject.SetActive(true);
    }

    private void HideInventoryInputBlocker()
    {
        if (inventoryInputBlockerObject != null)
        {
            inventoryInputBlockerObject.SetActive(false);
        }
    }

    private void HideAnnouncementPanel()
    {
        AnnouncementPanelView panel = FindSceneAnnouncementPanel();
        if (panel == null)
        {
            return;
        }

        panel.gameObject.SetActive(false);
        ShowMapSelectionContent();
    }

    private AnnouncementPanelView FindSceneAnnouncementPanel()
    {
        if (announcementPanelView != null)
        {
            return announcementPanelView;
        }

        AnnouncementPanelView[] panels = FindObjectsOfType<AnnouncementPanelView>(true);
        foreach (AnnouncementPanelView panel in panels)
        {
            if (panel.gameObject.scene == gameObject.scene)
            {
                announcementPanelView = panel;
                break;
            }
        }

        return announcementPanelView;
    }

    private bool EnsureAnnouncementPanelHierarchyActive()
    {
        if (announcementPanelView == null)
        {
            return false;
        }

        Transform current = announcementPanelView.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current = current.parent;
        }

        return announcementPanelView.isActiveAndEnabled;
    }

    private void SetLeftContent(string title, string body, string status)
    {
        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();
        HideAccountProfilePanel();
        HideFriendsPanel();

        if (contentPanelObject != null && !contentPanelObject.activeSelf)
        {
            contentPanelObject.SetActive(true);
        }

        if (mapSelectorObject != null)
        {
            mapSelectorObject.SetActive(false);
        }

        if (contentTitleText != null)
        {
            contentTitleText.gameObject.SetActive(true);
            contentTitleText.text = title;
        }

        if (contentBodyText != null)
        {
            contentBodyText.gameObject.SetActive(true);
            contentBodyText.text = body;
        }

        SetStatus(status);
    }

    private bool HasAuthSession()
    {
        return GameSession.IsAuthenticated;
    }

    private UnityWebRequest CreateAuthorizedGet(string path)
    {
        if (ApiRequestUtility.TryCreateGet(path, true, out UnityWebRequest request, out string error))
        {
            requestCreationError = string.Empty;
            return request;
        }

        requestCreationError = error;
        return null;
    }

    private IEnumerator GetInventoryList(bool equipmentOnly)
    {
        int playerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        UnityWebRequest request = CreateAuthorizedGet($"/inventory/list/{playerId}");
        if (request == null)
        {
            string errorMessage = requestCreationError;
            if (equipmentOnly)
            {
                SetLeftContent("装备", errorMessage, "接口配置错误");
            }
            else
            {
                ShowInventoryPanel(new InventoryItemResponse[0], true, errorMessage);
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = $"请求失败：{ApiRequestUtility.GetErrorMessage(request, "服务器连接失败")}\n\n接口地址：{ApiRuntimeConfig.GetDisplayBaseUrl()}";
                if (equipmentOnly)
                {
                    SetLeftContent("装备", errorMessage, "接口请求失败");
                }
                else if (inventoryPanelObject != null)
                {
                    ShowInventoryPanel(new InventoryItemResponse[0], true, errorMessage);
                }
                yield break;
            }

            InventoryListApiResponse response = ParseJson<InventoryListApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200)
            {
                string errorMessage = GetApiError(response);
                if (equipmentOnly)
                {
                    SetLeftContent("装备", errorMessage, "接口返回失败");
                }
                else if (inventoryPanelObject != null)
                {
                    ShowInventoryPanel(new InventoryItemResponse[0], true, errorMessage);
                }
                yield break;
            }

            if (equipmentOnly)
            {
                SetLeftContent("装备", BuildInventoryText(response.data, true), "已刷新装备");
            }
            else if (inventoryPanelObject != null)
            {
                ShowInventoryPanel(response.data ?? new InventoryItemResponse[0], true, "服务器背包同步完成");
            }
        }
    }

    private IEnumerator GetMailList()
    {
        int playerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        UnityWebRequest request = CreateAuthorizedGet($"/mail/list/{playerId}?limit=50");
        if (request == null)
        {
            ShowSystemMailPanel(null, requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowSystemMailPanel(null, $"请求失败：{ApiRequestUtility.GetErrorMessage(request, "服务器连接失败")}\n\n接口地址：{ApiRuntimeConfig.GetDisplayBaseUrl()}");
                yield break;
            }

            MailListApiResponse response = ParseJson<MailListApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200)
            {
                ShowSystemMailPanel(null, GetApiError(response));
                yield break;
            }

            ShowSystemMailPanel(response.data, null);
            TryConsumePendingMailRoute();
        }
    }

    private IEnumerator GetPaymentProducts()
    {
        UnityWebRequest request = CreateAuthorizedGet("/payment/product/list");
        if (request == null)
        {
            ShowRechargePanel(Array.Empty<PaymentProductResponse>(), requestCreationError, true);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowRechargePanel(Array.Empty<PaymentProductResponse>(), $"请求失败：{ApiRequestUtility.GetErrorMessage(request, "服务器连接失败")}\n接口地址：{ApiRuntimeConfig.GetDisplayBaseUrl()}", true);
                yield break;
            }

            PaymentProductListApiResponse response = ParseJson<PaymentProductListApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200)
            {
                ShowRechargePanel(Array.Empty<PaymentProductResponse>(), GetApiError(response), true);
                yield break;
            }

            PaymentProductResponse[] products = GetAvailableRechargeProducts(response.data);
            SetRechargeEntryVisible(true);
            if (products.Length == 0)
            {
                string message = response.data == null || response.data.Length == 0
                    ? "支付功能暂未开启。"
                    : "充值商品配置无效，请联系运营人员检查钻石商品配置。";
                ShowRechargePanel(products, message, true);
                yield break;
            }

            ShowRechargePanel(products, "已刷新钻石充值商品", false);
        }
    }

    private string GetApiError(BasicApiResponse response)
    {
        if (response == null)
        {
            return "接口返回数据为空或格式错误。";
        }

        return string.IsNullOrEmpty(response.message) ? $"接口返回错误：code={response.code}" : response.message;
    }

    private string BuildInventoryText(InventoryItemResponse[] items, bool equipmentOnly)
    {
        if (items == null || items.Length == 0)
        {
            return equipmentOnly ? "暂无装备物品。" : "背包暂无物品。";
        }

        string text = equipmentOnly ? "装备列表\n\n" : "背包物品\n\n";
        int count = 0;
        for (int i = 0; i < items.Length; i++)
        {
            InventoryItemResponse item = items[i];
            if (equipmentOnly && !IsEquipmentItem(item))
            {
                continue;
            }

            count++;
            text += $"{count}. {item.name} ({item.itemKey}) x{item.quantity}\n";
        }

        if (equipmentOnly && count == 0)
        {
            return "当前背包中没有装备类物品。\n\n说明：接口文档没有独立装备接口，当前从背包接口筛选装备类物品。";
        }

        return text;
    }

    private bool IsEquipmentItem(InventoryItemResponse item)
    {
        if (item == null)
        {
            return false;
        }

        string type = item.itemType ?? string.Empty;
        string key = NormalizeInventoryKey(item.itemKey);
        string name = item.name ?? string.Empty;
        return type.IndexOf("EQUIP", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("WEAPON", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("HELMET", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("ARMOR", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("BOOTS", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("GLOVES", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("ACCESSORY", StringComparison.OrdinalIgnoreCase) >= 0
            || type.IndexOf("ORNAMENT", StringComparison.OrdinalIgnoreCase) >= 0
            || key.Contains("equip")
            || key.Contains("weapon")
            || key.Contains("armor")
            || key.Contains("helmet")
            || key.Contains("boots")
            || key.Contains("gloves")
            || key.Contains("gauntlet")
            || key.Contains("shield")
            || key.Contains("accessory")
            || key.Contains("ornament")
            || key.Contains("ring")
            || key.Contains("necklace")
            || key.Contains("amulet")
            || name.Contains("装备")
            || name.Contains("武器")
            || name.Contains("防具")
            || name.Contains("头盔")
            || name.Contains("靴")
            || name.Contains("手套")
            || name.Contains("饰品")
            || name.Contains("戒")
            || name.Contains("项链")
            || name.Contains("护符");
    }

    private string BuildMailText(MailResponse[] mails)
    {
        if (mails == null || mails.Length == 0)
        {
            return "暂无邮件。";
        }

        string text = "邮件列表\n\n";
        int count = Mathf.Min(mails.Length, 20);
        for (int i = 0; i < count; i++)
        {
            MailResponse mail = mails[i];
            string read = mail.readFlag ? "已读" : "未读";
            string attachment = mail.hasAttachment ? (mail.attachmentClaimed ? "附件已领" : "有附件") : "无附件";
            text += $"{i + 1}. [{read}] {mail.title}\n   发件人：{mail.senderName}  {attachment}\n";
        }

        if (mails.Length > count)
        {
            text += $"\n仅显示前 {count} 封，共 {mails.Length} 封。";
        }

        text += "\n\n领取/删除邮件需要下一步增加邮件详情按钮列表。";
        return text;
    }

    private string BuildPaymentProductText(PaymentProductResponse[] products)
    {
        if (products == null || products.Length == 0)
        {
            return "当前没有可购买商品。\n\n如果后端已开启支付，请检查 PAYMENT_ENABLED 和商品配置。";
        }

        string text = "充值商品\n\n";
        for (int i = 0; i < products.Length; i++)
        {
            PaymentProductResponse product = products[i];
            text += $"{i + 1}. {product.name}\n   编码：{product.productCode}\n   价格：{product.priceFen / 100f:0.##} {product.currency}\n   奖励：{product.rewardItemName} x{GetShopRewardAmount(product)}\n";
        }

        text += "\n创建订单和微信支付需要商品按钮/订单确认弹窗，当前已接入商品列表接口。";
        return text;
    }

    private string BuildLocalInventoryText(string title)
    {
        return $"{title}\n\n金币 x{PlayerPrefs.GetInt("Gold", 1000)}\n木材 x{PlayerPrefs.GetInt("Wood", 500)}\n石材 x{PlayerPrefs.GetInt("Stone", 500)}\n铁矿 x{PlayerPrefs.GetInt("Iron", 300)}\n装备宝箱 x{PlayerPrefs.GetInt("EquipmentChest", 0)}\n高级抽奖券 x{PlayerPrefs.GetInt("LotteryTicket", 0)}";
    }

    private void AddLocalResource(string key, int amount)
    {
        if (HasAuthSession())
        {
            Debug.LogWarning($"账号模式已阻止本地资源变更：{key} {amount:+#;-#;0}");
            return;
        }

        int defaultValue = 0;
        if (key == "Gold")
        {
            defaultValue = 1000;
        }
        else if (key == "Wood" || key == "Stone")
        {
            defaultValue = 500;
        }
        else if (key == "Iron")
        {
            defaultValue = 300;
        }
        else if (key == "SoulCrystal")
        {
            defaultValue = 1200;
        }
        else if (key == "Crystal")
        {
            defaultValue = 427;
        }

        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, defaultValue) + amount);
    }

    private void HideContentPanel()
    {
        ShowMapSelectionContent();
    }

    private void BuildTopRow(Transform page)
    {
        GameObject row = CreateLayoutObject("TopRow", page);
        SetLayout(row, 0f, 188f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Image avatarBlock = CreateSoftBlock("AvatarBlock", row.transform);
        SetLayout(avatarBlock.gameObject, 150f, 188f);

        VerticalLayoutGroup avatarLayout = avatarBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        avatarLayout.padding = new RectOffset(12, 12, 12, 12);
        avatarLayout.spacing = 8f;
        avatarLayout.childControlHeight = false;
        avatarLayout.childControlWidth = true;
        avatarLayout.childForceExpandHeight = false;

        avatarButton = CreateButton(avatarBlock.transform, "AvatarButton", "头像", new Color(0.82f, 0.86f, 0.90f, 1f), InkColor);
        avatarButton.onClick.AddListener(TogglePersonalInfoPopup);
        SetLayout(avatarButton.gameObject, 0f, 92f);

        Text avatarName = CreateText("AvatarName", avatarBlock.transform, PlayerPrefs.GetString("PlayerNickname", "玩家"), 15, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);
        SetLayout(avatarName.gameObject, 0f, 22f);

        Text avatarTip = CreateText("AvatarTip", avatarBlock.transform, "点击查看信息", 13, FontStyle.Bold, MutedColor, TextAnchor.MiddleCenter);
        SetLayout(avatarTip.gameObject, 0f, 18f);

        Image resourceBlock = CreateImage("ResourceBlock", row.transform, new Color(1f, 0.96f, 0.82f, 0.95f));
        SetLayout(resourceBlock.gameObject, 0f, 188f);

        VerticalLayoutGroup resourceLayout = resourceBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        resourceLayout.padding = new RectOffset(20, 20, 14, 12);
        resourceLayout.spacing = 8f;
        resourceLayout.childControlHeight = false;
        resourceLayout.childControlWidth = true;
        resourceLayout.childForceExpandHeight = false;

        Text resourceTitle = CreateText("Title", resourceBlock.transform, "资源概览", 26, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(resourceTitle.gameObject, 0f, 32f);

        Text levelLine = CreateText("LevelLine", resourceBlock.transform, $"Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}", 18, FontStyle.Bold, PrimaryColor, TextAnchor.MiddleLeft);
        SetLayout(levelLine.gameObject, 0f, 22f);

        GridLayoutGroup grid = CreateLayoutObject("ResourceGrid", resourceBlock.transform).AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(230f, 28f);
        grid.spacing = new Vector2(10f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        SetLayout(grid.gameObject, 0f, 68f);

        AddResourceChip(grid.transform, "金币", PlayerPrefs.GetInt("Gold", 1000), new Color(0.80f, 0.56f, 0.10f, 1f));
        AddResourceChip(grid.transform, "木材", PlayerPrefs.GetInt("Wood", 500), new Color(0.28f, 0.52f, 0.28f, 1f));
        AddResourceChip(grid.transform, "石材", PlayerPrefs.GetInt("Stone", 500), new Color(0.42f, 0.45f, 0.49f, 1f));
        AddResourceChip(grid.transform, "铁矿", PlayerPrefs.GetInt("Iron", 300), new Color(0.44f, 0.38f, 0.58f, 1f));
    }

    private void AddResourceChip(Transform parent, string label, int value, Color accent)
    {
        Image item = CreateImage(label, parent, new Color(1f, 1f, 1f, 0.42f));
        HorizontalLayoutGroup layout = item.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 3, 3);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;

        Image mark = CreateImage("Mark", item.transform, accent);
        SetLayout(mark.gameObject, 8f, 18f);

        Text text = CreateText("Text", item.transform, $"{label} {value}", 16, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(text.gameObject, 0f, 20f);
    }

    private void BuildShortcutRow(Transform page)
    {
        GameObject row = CreateLayoutObject("ShortcutRow", page);
        SetLayout(row, 0f, 176f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Image buttonBlock = CreateSoftBlock("ButtonBlock", row.transform);
        SetLayout(buttonBlock.gameObject, 150f, 176f);

        VerticalLayoutGroup buttonLayout = buttonBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        buttonLayout.padding = new RectOffset(12, 12, 12, 12);
        buttonLayout.spacing = 6f;
        buttonLayout.childControlHeight = false;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandHeight = false;

        Button settingsButton = CreateButton(buttonBlock.transform, "SettingsButton", "设置", new Color(0.91f, 0.88f, 0.80f, 1f), InkColor);
        settingsButton.onClick.AddListener(() => SetStatus("设置功能待接入"));
        SetLayout(settingsButton.gameObject, 0f, 26f);
        ShrinkButtonLabel(settingsButton, 16);

        Button mailButton = CreateButton(buttonBlock.transform, "MailButton", "邮件", new Color(0.91f, 0.88f, 0.80f, 1f), InkColor);
        mailButton.onClick.AddListener(() => SetStatus("邮件功能待接入"));
        SetLayout(mailButton.gameObject, 0f, 26f);
        ShrinkButtonLabel(mailButton, 16);

        Button bagButton = CreateButton(buttonBlock.transform, "BagButton", "背包", new Color(0.91f, 0.88f, 0.80f, 1f), InkColor);
        bagButton.onClick.AddListener(() => SetStatus("背包功能待接入"));
        SetLayout(bagButton.gameObject, 0f, 26f);
        ShrinkButtonLabel(bagButton, 16);

        Image hintBlock = CreateSoftBlock("HintBlock", row.transform);
        SetFlexibleLayout(hintBlock.gameObject, 176f);

        VerticalLayoutGroup hintLayout = hintBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        hintLayout.padding = new RectOffset(16, 16, 14, 14);
        hintLayout.spacing = 6f;
        hintLayout.childControlHeight = false;
        hintLayout.childControlWidth = true;
        hintLayout.childForceExpandHeight = false;

        Text hintTitle = CreateText("HintTitle", hintBlock.transform, "左页信息", 22, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(hintTitle.gameObject, 0f, 30f);

        Text hintText = CreateText("HintText", hintBlock.transform, "上方查看头像和资源\n左侧使用设置、邮件、背包\n点击头像会在下方展开个人信息", 14, FontStyle.Normal, MutedColor, TextAnchor.MiddleLeft);
        SetLayout(hintText.gameObject, 0f, 74f);
    }

    private void BuildPersonalInfoPopup(Transform page)
    {
        personalInfoPopup = CreateSoftBlock("PersonalInfoPopup", page).gameObject;
        SetLayout(personalInfoPopup, 0f, 176f);

        VerticalLayoutGroup layout = personalInfoPopup.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 3f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        Text popupTitle = CreateText("Title", personalInfoPopup.transform, "个人信息", 18, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(popupTitle.gameObject, 0f, 20f);

        string nickname = PlayerPrefs.GetString("PlayerNickname", "玩家");
        string username = PlayerPrefs.GetString("LoginUsername", string.Empty);

        popupNameText = CreateText("Name", personalInfoPopup.transform, $"昵称：{nickname}", 14, FontStyle.Normal, InkColor, TextAnchor.MiddleLeft);
        SetLayout(popupNameText.gameObject, 0f, 16f);

        popupAccountText = CreateText("Account", personalInfoPopup.transform, $"账号：{username}", 14, FontStyle.Normal, InkColor, TextAnchor.MiddleLeft);
        SetLayout(popupAccountText.gameObject, 0f, 16f);

        popupModeText = CreateText("Mode", personalInfoPopup.transform, "状态：账号登录", 14, FontStyle.Bold, PrimaryColor, TextAnchor.MiddleLeft);
        SetLayout(popupModeText.gameObject, 0f, 16f);

        popupLevelText = CreateText("Level", personalInfoPopup.transform, $"等级：Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}", 14, FontStyle.Normal, InkColor, TextAnchor.MiddleLeft);
        SetLayout(popupLevelText.gameObject, 0f, 16f);

        popupResourceText = CreateText("Resource", personalInfoPopup.transform, $"金币 {PlayerPrefs.GetInt("Gold", 1000)}\n木材 {PlayerPrefs.GetInt("Wood", 500)}\n石材 {PlayerPrefs.GetInt("Stone", 500)}\n铁矿 {PlayerPrefs.GetInt("Iron", 300)}", 13, FontStyle.Normal, MutedColor, TextAnchor.MiddleLeft);
        SetLayout(popupResourceText.gameObject, 0f, 44f);

        Button closeButton = CreateButton(personalInfoPopup.transform, "CloseButton", "关闭", new Color(0.91f, 0.88f, 0.80f, 1f), InkColor);
        closeButton.onClick.AddListener(HidePersonalInfoPopup);
        SetLayout(closeButton.gameObject, 0f, 20f);

        personalInfoPopup.SetActive(false);
    }

    private void TogglePersonalInfoPopup()
    {
        if (personalInfoPopup == null)
        {
            return;
        }

        bool next = !personalInfoPopup.activeSelf;
        personalInfoPopup.SetActive(next);
        if (next)
        {
            RefreshPersonalInfoPopup();
            SetStatus("已打开个人信息");
        }
    }

    private void HidePersonalInfoPopup()
    {
        if (personalInfoPopup != null)
        {
            personalInfoPopup.SetActive(false);
        }
    }

    private void RefreshPersonalInfoPopup()
    {
        if (popupNameText == null)
        {
            return;
        }

        string nickname = PlayerPrefs.GetString("PlayerNickname", "玩家");
        string username = PlayerPrefs.GetString("LoginUsername", string.Empty);

        popupNameText.text = $"昵称：{nickname}";
        popupAccountText.text = $"账号：{username}";
        popupModeText.text = "状态：账号登录";
        popupLevelText.text = $"等级：Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}";
        popupResourceText.text = $"金币 {PlayerPrefs.GetInt("Gold", 1000)}\n木材 {PlayerPrefs.GetInt("Wood", 500)}\n石材 {PlayerPrefs.GetInt("Stone", 500)}\n铁矿 {PlayerPrefs.GetInt("Iron", 300)}";
    }

    private void BuildRightPage(Transform page)
    {
        if (Application.isPlaying || !Application.isPlaying)
        {
            BuildMapCollageRightPage(page);
            return;
        }

        Text title = CreateText("Title", page, "地图选择", 44, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(title.gameObject, 0f, 52f);

        GridLayoutGroup grid = CreateLayoutObject("AreaGrid", page).AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(322f, 116f);
        grid.spacing = new Vector2(18f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperLeft;
        SetLayout(grid.gameObject, 0f, 394f);

        for (int i = 0; i < areas.Length; i++)
        {
            AddAreaCard(grid.transform, areas[i]);
        }

        Image detail = CreateSoftBlock("AreaDetail", page);
        SetLayout(detail.gameObject, 0f, 220f);

        VerticalLayoutGroup detailLayout = detail.gameObject.AddComponent<VerticalLayoutGroup>();
        detailLayout.padding = new RectOffset(24, 24, 18, 18);
        detailLayout.spacing = 10f;
        detailLayout.childControlWidth = true;
        detailLayout.childControlHeight = false;
        detailLayout.childForceExpandHeight = false;

        selectedAreaTitle = CreateText("SelectedTitle", detail.transform, string.Empty, 31, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(selectedAreaTitle.gameObject, 0f, 42f);

        selectedAreaDescription = CreateText("SelectedDescription", detail.transform, string.Empty, 22, FontStyle.Normal, MutedColor, TextAnchor.MiddleLeft);
        SetLayout(selectedAreaDescription.gameObject, 0f, 56f);

        selectedAreaHint = CreateText("SelectedHint", detail.transform, string.Empty, 20, FontStyle.Bold, PrimaryColor, TextAnchor.MiddleLeft);
        SetLayout(selectedAreaHint.gameObject, 0f, 32f);

        Button enterButton = CreateButton(detail.transform, "EnterButton", "进入该区域", PrimaryColor, Color.white);
        enterButton.onClick.AddListener(EnterSelectedArea);
        SetLayout(enterButton.gameObject, 0f, 48f);
    }

    private void BuildMapCollageRightPage(Transform page)
    {
        VerticalLayoutGroup pageLayout = page.GetComponent<VerticalLayoutGroup>();
        if (pageLayout != null)
        {
            pageLayout.enabled = false;
        }

        Image pageImage = page.GetComponent<Image>();
        if (pageImage != null)
        {
            pageImage.color = new Color(0.53f, 0.32f, 0.17f, 1f);
        }

        AddWoodLine(page, new Vector2(36f, -34f), new Vector2(680f, 4f));
        AddWoodLine(page, new Vector2(20f, -830f), new Vector2(710f, 4f));

        AddMapImage(page, GetArea("grass"), "Sprites/Arts/png/grass.jpeg", new Vector2(20f, -62f), new Vector2(430f, 270f), -4f);
        AddMapImage(page, GetArea("forest"), "Sprites/Arts/png/forest.png", new Vector2(470f, -50f), new Vector2(310f, 370f), -1f);
        AddMapImage(page, GetArea("sea"), "Sprites/Arts/png/sea.png", new Vector2(170f, -318f), new Vector2(610f, 260f), 9f);
        AddMapImage(page, GetArea("volcano"), "Sprites/Arts/png/volcano.png", new Vector2(42f, -438f), new Vector2(210f, 430f), -4f);
        AddMapImage(page, GetArea("snow"), "Sprites/Arts/png/snow.png", new Vector2(310f, -618f), new Vector2(500f, 225f), 0f);

        Button closeButton = CreateButton(page, "RightCloseButton", "X", new Color(0.32f, 0.18f, 0.10f, 0.90f), new Color(1f, 0.86f, 0.58f, 1f));
        closeButton.onClick.AddListener(() => SetStatus("关闭按钮待接入"));
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -4f), new Vector2(24f, 24f));
        ShrinkButtonLabel(closeButton, 22);
    }

    private void AddMapImage(Transform parent, AreaInfo area, string assetPath, Vector2 position, Vector2 size, float rotation)
    {
        Image shadow = CreateImage(area.name + "MapShadow", parent, new Color(0.16f, 0.09f, 0.05f, 0.30f));
        shadow.raycastTarget = false;
        SetRect(shadow.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(10f, -10f), size);
        shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);

        Image frame = CreateImage(area.name + "MapFrame", parent, new Color(0.74f, 0.48f, 0.27f, 1f));
        frame.raycastTarget = false;
        SetRect(frame.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position + new Vector2(-7f, 7f), size + new Vector2(14f, 14f));
        frame.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);

        Button button = CreateButton(parent, area.name + "MapImage", string.Empty, area.color, InkColor);
        button.onClick.AddListener(() => SelectAreaAndEnter(area));

        RectTransform rect = button.GetComponent<RectTransform>();
        SetRect(rect, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        rect.localEulerAngles = new Vector3(0f, 0f, rotation);

        Image image = button.GetComponent<Image>();
        image.color = Color.Lerp(area.color, Color.white, 0.18f);
        Sprite sprite = LoadSpriteFromAsset(assetPath);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
        }
    }

    private Sprite LoadSpriteFromAsset(string relativePath)
    {
        string assetRelativePath = relativePath.Replace('\\', '/');
        bool isResourceIcon = assetRelativePath.StartsWith("UI/资源图标/", StringComparison.OrdinalIgnoreCase);
        if (isResourceIcon)
        {
            assetRelativePath = "_Project/Art/" + assetRelativePath;
        }
        string normalizedPath = assetRelativePath.Replace('/', Path.DirectorySeparatorChar);
        string path = Path.Combine(Application.dataPath, normalizedPath);
        if (!File.Exists(path) && !isResourceIcon)
        {
            path = Path.Combine(Application.dataPath, "_Imported", "weijun-game-project", "Assets", normalizedPath);
        }

        if (!File.Exists(path))
        {
            return null;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string assetPath = "Assets" + path.Substring(Application.dataPath.Length).Replace(Path.DirectorySeparatorChar, '/');
            Sprite assetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (assetSprite != null)
            {
                return assetSprite;
            }
        }
#endif

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        if (!texture.LoadImage(bytes))
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private AreaInfo GetArea(string landType)
    {
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i].landType == landType)
            {
                return areas[i];
            }
        }

        return areas[0];
    }

    private void AddAreaCard(Transform parent, AreaInfo area)
    {
        Button card = CreateButton(parent, area.name + "Card", string.Empty, new Color(0.94f, 0.91f, 0.84f, 1f), InkColor);
        card.onClick.AddListener(() => SelectAreaAndEnter(area));

        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 12);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Image mark = CreateImage("Mark", card.transform, area.color);
        SetLayout(mark.gameObject, 0f, 8f);

        Text title = CreateText("Title", card.transform, area.name, 28, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetLayout(title.gameObject, 0f, 36f);

        Text hint = CreateText("Hint", card.transform, area.unlockHint, 18, FontStyle.Bold, area.color, TextAnchor.MiddleLeft);
        SetLayout(hint.gameObject, 0f, 26f);
    }

    private void SelectArea(AreaInfo area, bool rememberSelection = true)
    {
        selectedArea = area;
        if (rememberSelection)
        {
            PlayerPrefs.SetString("OpenAreaLandType", area.landType);
            PlayerPrefs.Save();
        }

        if (selectedAreaTitle == null)
        {
            return;
        }

        selectedAreaTitle.text = area.name;
        selectedAreaDescription.text = area.description;
        selectedAreaHint.text = area.unlockHint;
        selectedAreaHint.color = area.color;
        RefreshMapSelectorState();
        SetStatus($"已选择：{area.name}");
    }

    private void SelectAreaAndEnter(AreaInfo area)
    {
        SelectArea(area);
        EnterArea(area.landType);
    }

    private AreaInfo GetLastArea()
    {
        string landType = PlayerPrefs.GetString("OpenAreaLandType", "grass");
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i].landType == landType)
            {
                return areas[i];
            }
        }

        return areas[0];
    }

    private void ContinueGame()
    {
        EnterArea(string.Empty);
    }

    private void EnterSelectedArea()
    {
        EnterArea(string.Empty);
    }

    private void EnterArea(string landType)
    {
        if (mapEntryRunning)
        {
            return;
        }

        if (HasAuthSession())
        {
            StartCoroutine(LoadLandSceneAndEnter(landType));
            return;
        }

        SetStatus("Please sign in before entering management mode.");
    }

    private string GetSelectedModeSceneName()
    {
        string selectedMode = PlayerPrefs.GetString("HomeSelectedMode", "build");
        return string.Equals(selectedMode, "battle", StringComparison.OrdinalIgnoreCase)
            ? IdleAdventureSceneName
            : SimulationSceneName;
    }

    private void ReturnToLogin()
    {
        if (!logoutRunning)
        {
            StartCoroutine(LogoutAndReturnToLogin());
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private Image CreateSoftBlock(string name, Transform parent)
    {
        return CreateImage(name, parent, new Color(1f, 0.98f, 0.92f, 0.78f));
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private GameObject CreateLayoutObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
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
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.font = RuntimeFontAssetProvider.Resolve();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
        label.color = color;
        label.alignment = ConvertTextAlignment(alignment);
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = size;
        label.raycastTarget = false;
        return label;
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

    private Font ResolveFont(string text)
    {
        if (defaultFont == null || string.IsNullOrEmpty(text))
        {
            return defaultFont ?? fallbackFont;
        }

        return RuntimeFontAssetProvider.Resolve();
    }

    private Text CreateMailText(string name, Transform parent, string text, int size, FontStyle style, Color color, TextAnchor alignment)
    {
        TMP_Text label = CreateText(name, parent, text, size, style, color, alignment);
        ConfigureClearText(label, size);
        return label;
    }

    private Button CreateButton(Transform parent, string name, string text, Color color, Color textColor)
    {
        Image image = CreateImage(name, parent, color);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateSelectableColors(color, Color.Lerp(color, Color.white, 0.10f), Color.Lerp(color, Color.black, 0.08f), new Color(0.70f, 0.75f, 0.82f, 1f));

        if (!string.IsNullOrEmpty(text))
        {
            if (color.a > 0.5f)
            {
                Image shine = CreateImage("ButtonShine", image.transform, new Color(1f, 0.93f, 0.66f, 0.22f));
                shine.raycastTarget = false;
                SetRect(shine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 18f));
            }

            TMP_Text label = CreateText("Text", image.transform, text, 22, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.31f, 0.14f, 0.05f, 0.65f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

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

    private void ShrinkButtonLabel(Button button, int fontSize)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            return;
        }

        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = fontSize;
    }

    private void ConfigureClearText(Text label, int fontSize)
    {
        if (label == null)
        {
            return;
        }

        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
    }

    private void ConfigureClearButtonLabel(Button button, int fontSize)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        ConfigureClearText(label, fontSize);

        if (label == null)
        {
            return;
        }

        Outline outline = label.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetLayout(GameObject obj, float preferredWidth, float preferredHeight)
    {
        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
    }

    private void SetFlexibleLayout(GameObject obj, float preferredHeight)
    {
        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 1f;
    }

    private class TaskDisplayItem
    {
        public readonly string id;
        public readonly string title;
        public readonly string description;
        public readonly int current;
        public readonly int target;
        public readonly string rewardKey;
        public readonly string rewardName;
        public readonly int rewardAmount;
        public readonly bool daily;
        public readonly Action goAction;
        public readonly bool serverBacked;
        public readonly long serverTaskId;
        public readonly string serverStatus;
        public readonly string rewardSummary;

        public bool IsComplete => serverBacked
            ? string.Equals(serverStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(serverStatus, "CLAIMED", StringComparison.OrdinalIgnoreCase)
                || current >= target
            : current >= target;

        public TaskDisplayItem(
            string id,
            string title,
            string description,
            int current,
            int target,
            string rewardKey,
            string rewardName,
            int rewardAmount,
            bool daily,
            Action goAction,
            bool serverBacked = false,
            long serverTaskId = 0,
            string serverStatus = null,
            string rewardSummary = null)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.current = current;
            this.target = target;
            this.rewardKey = rewardKey;
            this.rewardName = rewardName;
            this.rewardAmount = rewardAmount;
            this.daily = daily;
            this.goAction = goAction;
            this.serverBacked = serverBacked;
            this.serverTaskId = serverTaskId;
            this.serverStatus = serverStatus;
            this.rewardSummary = rewardSummary;
        }
    }

    #pragma warning disable 0649
    [Serializable]
    private class BasicApiResponse
    {
        public int code;
        public string message;
    }

    [Serializable]
    private class InventoryListApiResponse : BasicApiResponse
    {
        public InventoryItemResponse[] data;
    }

    [Serializable]
    private class InventoryItemResponse
    {
        public int itemId;
        public string itemKey;
        public string name;
        public string itemType;
        public string icon;
        public int quantity;
        public long playerEquipmentId;
        public int equipmentId;
        public string equipmentKey;
        public string equipmentType;
        public string quality;
        public string modelKey;
        public string description;
        public string attributesJson;
        public int level;
        public string status;
        public bool locked;
        public int healthBonus;
        public int attackBonus;
        public int defenseBonus;
        public int speedBonus;
        public string acquireTime;
    }

    private sealed class InventoryMergeStack
    {
        public string stackKey;
        public InventoryItemResponse item;
        public int sourceCount;
    }

    [Serializable]
    private class MailListApiResponse : BasicApiResponse
    {
        public MailResponse[] data;
    }

    [Serializable]
    private class MailResponse
    {
        public long mailId;
        public int playerId;
        public string mailType;
        public string title;
        public string content;
        public string senderName;
        public bool readFlag;
        public bool hasAttachment;
        public bool attachmentClaimed;
        public bool expired;
        public string expireTime;
        public string createTime;
        public MailAttachmentResponse[] attachments;
    }

    [Serializable]
    private class PaymentProductListApiResponse : BasicApiResponse
    {
        public PaymentProductResponse[] data;
    }

    [Serializable]
    private class PaymentProductResponse
    {
        public string productCode;
        public string name;
        public int priceFen;
        public string currency;
        public string rewardItemKey;
        public string rewardItemName;
        public string rewardIcon;
        public int rewardAmount;
        public int rewardQuantity;
        public string description;
    }
    #pragma warning restore 0649

    [Serializable]
    private class AreaInfo
    {
        public readonly string name;
        public readonly string landType;
        public readonly string description;
        public readonly string unlockHint;
        public readonly Color color;

        public AreaInfo(string name, string landType, string description, string unlockHint, Color color)
        {
            this.name = name;
            this.landType = landType;
            this.description = description;
            this.unlockHint = unlockHint;
            this.color = color;
        }
    }
}
