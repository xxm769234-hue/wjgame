using System.Collections;
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

    private const int NavigationSortingOrder = 220;
    private const float LeftMargin = 28f;
    private const float BottomMargin = 28f;
    private const float ButtonSpacing = 16f;
    private const float ShortcutSpacing = 14f;

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

        SetHomeBookInvisible(bookView);
        CreateNavigationCanvas();
        nextVerticalShortcutTop = LeftMargin;
        MoveShortcut(bookView.MailButton, "MailShortcut", true, 0);
        MoveShortcut(bookView.NoticeButton, "NoticeShortcut", true, 1);
        MoveShortcut(bookView.SettingsButton, "SettingsShortcut", true, 2);
        MoveShortcut(bookView.TaskButton, "TaskShortcut", true, 3);
        MoveShortcut(bookView.BagButton, "BagShortcut", true, 4);
        MoveShortcut(bookView.ShopButton, "ShopShortcut", true, 5);
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

    private void MoveShortcut(Button button, string shortcutName, bool vertical, int index)
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
        Vector2 originalPivot = buttonRect.pivot;
        Vector3 originalScale = buttonRect.localScale;
        button.transform.SetParent(navigationRoot, false);
        button.name = shortcutName;
        buttonRect.localScale = originalScale;
        buttonRect.pivot = originalPivot;
        buttonRect.sizeDelta = originalSize;
        buttonRect.anchorMin = vertical ? new Vector2(0f, 1f) : Vector2.zero;
        buttonRect.anchorMax = buttonRect.anchorMin;

        if (vertical)
        {
            float top = nextVerticalShortcutTop;
            nextVerticalShortcutTop += originalSize.y + ShortcutSpacing;
            buttonRect.anchoredPosition = new Vector2(
                LeftMargin + originalSize.x * originalPivot.x,
                -top - originalSize.y * (1f - originalPivot.y));
        }
        else
        {
            float left = LeftMargin;
            if (index > 0)
            {
                left = GetPreviousBottomShortcutRight(index) + ButtonSpacing;
            }

            buttonRect.anchoredPosition = new Vector2(
                left + originalSize.x * originalPivot.x,
                BottomMargin + originalSize.y * originalPivot.y);
        }
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
