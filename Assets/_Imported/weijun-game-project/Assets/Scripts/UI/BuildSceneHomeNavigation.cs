using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hosts the existing HomeScene shortcuts while the management scene is active.
/// The buttons themselves and their listeners remain owned by HomePageUI.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildSceneHomeNavigation : MonoBehaviour
{
    private const string HomeSceneName = "HomeScene";
    private const string ManagementSceneName = "Simulation management mode";
    private const string SimulationShortcutResourcesPath = "UI/ApprovedUi/Icons/";

    private const int NavigationSortingOrder = 220;
    private const float LeftMargin = 28f;
    private const float VerticalShortcutTopMargin = 152f;
    private const float BottomMargin = 28f;
    private const float ButtonSpacing = 16f;
    private const float ShortcutSpacing = 14f;
    private const float TopRightShortcutMargin = 28f;

    private Canvas navigationCanvas;
    private RectTransform navigationRoot;
    private Scene loadedHomeScene;
    private float nextVerticalShortcutTop;

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
        if (SceneManager.GetActiveScene().name != ManagementSceneName)
        {
            return;
        }

        if (FindObjectOfType<BuildSceneHomeNavigation>() != null)
        {
            return;
        }

        new GameObject("BuildSceneHomeNavigation").AddComponent<BuildSceneHomeNavigation>();
    }

    private void Start()
    {
        StartCoroutine(LoadHomeInterface());
    }

    private IEnumerator LoadHomeInterface()
    {
        if (SceneManager.GetSceneByName(HomeSceneName).isLoaded)
        {
            loadedHomeScene = SceneManager.GetSceneByName(HomeSceneName);
        }
        else
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(HomeSceneName, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogWarning("Build scene shortcuts could not load HomeScene.", this);
                yield break;
            }

            yield return loadOperation;
            loadedHomeScene = SceneManager.GetSceneByName(HomeSceneName);
        }

        if (!loadedHomeScene.IsValid() || !loadedHomeScene.isLoaded)
        {
            Debug.LogWarning("Build scene shortcuts could not find the loaded HomeScene.", this);
            yield break;
        }

        yield return null;
        DisableHomeSceneNonPanelRoots();
        HomePageUI homePageUi = EnsureHomePageUi();
        if (homePageUi == null)
        {
            Debug.LogWarning("Build scene shortcuts could not initialize HomePageUI.", this);
            yield break;
        }

        yield return null;
        HomePageBookView bookView = FindHomeBookView();
        if (bookView == null)
        {
            Debug.LogWarning("Build scene shortcuts could not find HomePageBookView in HomeScene.", this);
            yield break;
        }

        ConfigureNavigation(bookView);
    }

    private void ConfigureNavigation(HomePageBookView bookView)
    {
        SetHomeBookInvisible(bookView);
        CreateNavigationCanvas();
        nextVerticalShortcutTop = VerticalShortcutTopMargin;
        Vector2 shortcutSize = new Vector2(96f, 108f);
        MoveShortcut(bookView.TaskButton, "TaskShortcut", true, 0, "任务", shortcutSize);
        MoveShortcut(bookView.BagButton, "BagShortcut", true, 1, "仓库", shortcutSize);
        MoveShortcut(bookView.ShopButton, "ShopShortcut", true, 2, "商店", shortcutSize);
        MoveShortcut(bookView.MailButton, "MailShortcut", true, 3, "邮件", shortcutSize);
        MoveShortcut(bookView.NoticeButton, "NoticeShortcut", true, 4, "公告", shortcutSize);
        MoveShortcut(bookView.SettingsButton, "SettingsShortcut", true, 5, "设置", shortcutSize);
        ArrangeTopRightShortcuts(bookView.MailButton, bookView.NoticeButton, bookView.SettingsButton);
    }

    private static void ArrangeTopRightShortcuts(Button mailButton, Button noticeButton, Button settingsButton)
    {
        Button[] shortcuts = { settingsButton, noticeButton, mailButton };
        float rightOffset = TopRightShortcutMargin;
        foreach (Button shortcut in shortcuts)
        {
            RectTransform rect = shortcut != null ? shortcut.transform as RectTransform : null;
            if (rect == null) continue;

            rect.localScale = Vector3.one;
            Vector2 scaledSize = Vector2.Scale(rect.rect.size, rect.localScale);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(
                -rightOffset - scaledSize.x * (1f - rect.pivot.x),
                -TopRightShortcutMargin - scaledSize.y * (1f - rect.pivot.y));
            rightOffset += scaledSize.x + ShortcutSpacing;
        }
    }

    private HomePageUI EnsureHomePageUi()
    {
        HomePageUI existing = FindObjectOfType<HomePageUI>(true);
        if (existing != null)
        {
            MoveObjectToHomeScene(existing.gameObject);
            return existing;
        }

        GameObject uiObject = new GameObject("BuildSceneHomePageUI");
        MoveObjectToHomeScene(uiObject);
        HomePageUI created = uiObject.AddComponent<HomePageUI>();
        if (created == null)
        {
            Destroy(uiObject);
            Debug.LogWarning("Build scene shortcuts could not create HomePageUI.", this);
        }

        return created;
    }

    private void MoveObjectToHomeScene(GameObject target)
    {
        if (target == null || !loadedHomeScene.IsValid() || !loadedHomeScene.isLoaded)
        {
            return;
        }

        if (target.scene != loadedHomeScene)
        {
            SceneManager.MoveGameObjectToScene(target, loadedHomeScene);
        }
    }

    private HomePageBookView FindHomeBookView()
    {
        GameObject[] roots = loadedHomeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            HomePageBookView view = roots[i].GetComponentInChildren<HomePageBookView>(true);
            if (view != null)
            {
                return view;
            }
        }

        return null;
    }

    private void DisableHomeSceneNonPanelRoots()
    {
        GameObject[] roots = loadedHomeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name != "HomePanelsCanvas")
            {
                roots[i].SetActive(false);
            }
        }
    }

    private static void SetHomeBookInvisible(HomePageBookView bookView)
    {
        if (bookView == null)
        {
            return;
        }

        CanvasGroup group = bookView.gameObject.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = bookView.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void CreateNavigationCanvas()
    {
        if (navigationCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "BuildSceneHomeNavigationCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            canvasObject.layer = uiLayer;
        }

        navigationCanvas = canvasObject.GetComponent<Canvas>();
        navigationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        navigationCanvas.sortingOrder = NavigationSortingOrder;
        navigationCanvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        navigationRoot = canvasObject.GetComponent<RectTransform>();
        navigationRoot.anchorMin = Vector2.zero;
        navigationRoot.anchorMax = Vector2.one;
        navigationRoot.offsetMin = Vector2.zero;
        navigationRoot.offsetMax = Vector2.zero;
        navigationRoot.localScale = Vector3.one;
    }

    private void MoveShortcut(
        Button button,
        string shortcutName,
        bool vertical,
        int index,
        string simulationIconName = null,
        Vector2? shortcutSizeOverride = null)
    {
        if (button == null)
        {
            Debug.LogWarning("Build scene shortcut is missing: " + shortcutName, this);
            return;
        }

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null)
        {
            Debug.LogWarning("Build scene shortcut has no RectTransform: " + shortcutName, this);
            return;
        }

        Vector2 originalSize = buttonRect.rect.size;
        Vector2 shortcutSize = shortcutSizeOverride ?? originalSize;
        Vector2 originalPivot = buttonRect.pivot;
        button.transform.SetParent(navigationRoot, false);
        button.name = shortcutName;
        buttonRect.localScale = Vector3.one;
        buttonRect.localRotation = Quaternion.identity;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        originalPivot = buttonRect.pivot;
        buttonRect.sizeDelta = shortcutSize;
        buttonRect.anchorMin = vertical ? new Vector2(0f, 1f) : Vector2.zero;
        buttonRect.anchorMax = buttonRect.anchorMin;

        if (vertical)
        {
            float top = nextVerticalShortcutTop;
            nextVerticalShortcutTop += shortcutSize.y + ShortcutSpacing;
            buttonRect.anchoredPosition = new Vector2(
                LeftMargin + shortcutSize.x * originalPivot.x,
                -top - shortcutSize.y * (1f - originalPivot.y));
        }
        else
        {
            float left = LeftMargin;
            if (index > 0)
            {
                left = GetPreviousBottomShortcutRight(index) + ButtonSpacing;
            }

            buttonRect.anchoredPosition = new Vector2(
                left + shortcutSize.x * originalPivot.x,
                BottomMargin + shortcutSize.y * originalPivot.y);
        }

        ApplySimulationShortcutIcon(button, simulationIconName);
    }

    private void ApplySimulationShortcutIcon(Button button, string resourceName)
    {
        if (button == null || string.IsNullOrEmpty(resourceName))
        {
            return;
        }

        string approvedName;
        switch (resourceName)
        {
            case "任务": approvedName = "08_tasks"; break;
            case "仓库": approvedName = "07_inventory"; break;
            case "商店": approvedName = "03_shop"; break;
            case "邮件": approvedName = "15_mail"; break;
            case "公告": approvedName = "11_announcements"; break;
            case "设置": approvedName = "09_settings"; break;
            default: return;
        }
        Sprite sprite = Resources.Load<Sprite>(SimulationShortcutResourcesPath + approvedName);
        if (sprite == null)
        {
            Debug.LogWarning("Simulation shortcut icon is missing: " + approvedName, this);
            return;
        }

        Transform iconTransform = button.transform.Find("IconPatch");
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("IconPatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = button.gameObject.layer;
            iconObject.transform.SetParent(button.transform, false);
            iconTransform = iconObject.transform;
        }
        Image targetImage = iconTransform.GetComponent<Image>();
        if (targetImage == null) targetImage = iconTransform.gameObject.AddComponent<Image>();
        targetImage.gameObject.SetActive(true);
        targetImage.overrideSprite = null;
        targetImage.sprite = sprite;
        targetImage.color = Color.white;
        targetImage.type = Image.Type.Simple;
        targetImage.preserveAspect = true;
        targetImage.useSpriteMesh = false;
        targetImage.raycastTarget = false;
        RectTransform iconRect = targetImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = iconRect.anchorMin;
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(72f, 72f);
        iconRect.anchoredPosition = new Vector2(0f, 18f);
        iconRect.localRotation = Quaternion.identity;
        iconRect.localScale = Vector3.one;
        button.targetGraphic = targetImage;
        button.transition = Selectable.Transition.ColorTint;

        Image backgroundImage = button.GetComponent<Image>();
        if (backgroundImage != null && backgroundImage != targetImage)
        {
            backgroundImage.sprite = null;
            backgroundImage.overrideSprite = null;
            backgroundImage.color = Color.clear;
            backgroundImage.raycastTarget = true;
        }

        Transform titleTransform = button.transform.Find("Title");
        if (titleTransform == null)
        {
            GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObject.layer = button.gameObject.layer;
            titleObject.transform.SetParent(button.transform, false);
            titleTransform = titleObject.transform;
        }
        Text legacyTitle = titleTransform.GetComponent<Text>();
        if (legacyTitle != null) legacyTitle.enabled = false;
        TMP_Text title = titleTransform.GetComponent<TMP_Text>();
        if (title == null) title = titleTransform.gameObject.AddComponent<TextMeshProUGUI>();
        title.gameObject.SetActive(true);
        title.enabled = true;
        title.font = RuntimeFontAssetProvider.Resolve();
        title.text = resourceName;
        title.fontSize = 20f;
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = false;
        title.color = new Color(0.22f, 0.12f, 0.07f, 1f);
        title.outlineColor = new Color32(255, 239, 207, 255);
        title.outlineWidth = 0.04f;
        title.extraPadding = true;
        title.alignment = TextAlignmentOptions.Center;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.margin = Vector4.zero;
        title.characterSpacing = 0f;
        title.wordSpacing = 0f;
        title.lineSpacing = 0f;
        title.paragraphSpacing = 0f;
        title.maxVisibleCharacters = int.MaxValue;
        title.maxVisibleWords = int.MaxValue;
        title.maxVisibleLines = 1;
        title.raycastTarget = false;
        Outline outline = title.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        title.UpdateMeshPadding();
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = titleRect.anchorMin;
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(96f, 32f);
        titleRect.anchoredPosition = new Vector2(0f, -38f);
        titleRect.localRotation = Quaternion.identity;
        titleRect.localScale = Vector3.one;
    }

    private float GetPreviousBottomShortcutRight(int index)
    {
        Button[] buttons = navigationRoot.GetComponentsInChildren<Button>(true);
        int bottomShortcutCount = 0;
        float right = LeftMargin;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || buttons[i].transform.parent != navigationRoot)
            {
                continue;
            }

            string name = buttons[i].name;
            if (name != "TaskShortcut" && name != "BagShortcut" && name != "ShopShortcut")
            {
                continue;
            }

            RectTransform rect = buttons[i].transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            right = Mathf.Max(right, GetRightEdge(rect));
            bottomShortcutCount++;
        }

        return bottomShortcutCount >= index ? right : LeftMargin;
    }

    private static float GetRightEdge(RectTransform rect)
    {
        return rect.anchoredPosition.x + rect.rect.width * (1f - rect.pivot.x);
    }
}
