using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ResourceCollectionManager : MonoBehaviour
{
    private const string SimulationSceneName = "Simulation management mode";

    public static ResourceCollectionManager Instance { get; private set; }

    [Header("Collect All Button")]
    [SerializeField] private bool createRuntimeButton = true;
    [SerializeField] private string buttonObjectName = "Collect All Resources Button";
    [SerializeField] private Vector2 buttonPosition = new Vector2(210f, 70f);
    [SerializeField] private Vector2 buttonSize = new Vector2(72f, 72f);
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private Button collectAllButton;
    private TMP_Text collectAllButtonText;
    private float nextRefreshAt;

    public static ResourceCollectionManager EnsureInstance()
    {
        if (SceneManager.GetActiveScene().name != SimulationSceneName) return null;
        if (Instance != null) return Instance;

        ResourceCollectionManager existing = FindFirstObjectByType<ResourceCollectionManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("ResourceCollectionManager");
        return managerObject.AddComponent<ResourceCollectionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (collectAllButton != null) collectAllButton.gameObject.SetActive(false);
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
    {
        RefreshButtonState();
    }

    private void Start()
    {
        RefreshButtonState();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshAt) return;
        nextRefreshAt = Time.unscaledTime + refreshInterval;
        RefreshButtonState();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int CountReadyResources()
    {
        int count = 0;
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].HasResourceReady) count++;
        }
        return count;
    }

    public void OnClickCollectAll()
    {
        if (SceneManager.GetActiveScene().name != SimulationSceneName) return;
        if (PlayerOnboardingInputLock.IsLocked)
        {
            Debug.LogWarning("Onboarding currently locks resource collection input.", this);
            return;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            Debug.LogWarning("Backend collect-all failed: Backend session is not authenticated.", this);
            RefreshButtonState();
            return;
        }

        if (service.IsAuthenticated)
        {
            if (collectAllButton != null) collectAllButton.interactable = false;
            service.CollectAllProductions((success, message, collectedCount) =>
            {
                if (!success) Debug.LogWarning($"Backend collect-all failed: {message}", this);
                else
                {
                    Debug.Log($"Backend collect-all gathered {collectedCount} item type(s).", this);
                    if (collectedCount > 0) PlayerOnboardingGameplayBridge.NotifyBusinessSucceeded();
                }
                RefreshButtonState();
            });
            return;
        }
    }

    private void EnsureCollectAllButton()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != SimulationSceneName) return;
        if (collectAllButton != null) return;

        Canvas canvas = null;
        foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == activeScene && candidate.isRootCanvas)
            {
                canvas = candidate;
                break;
            }
        }
        if (canvas == null)
        {
            Debug.LogWarning("ResourceCollectionManager could not create Collect All button because no simulation Canvas exists.", this);
            return;
        }

        Transform existing = canvas.transform.Find(buttonObjectName);
        if (existing != null)
        {
            collectAllButton = existing.GetComponent<Button>();
            collectAllButtonText = existing.GetComponentInChildren<TMP_Text>(true);
            if (collectAllButton != null)
            {
                collectAllButton.onClick.RemoveListener(OnClickCollectAll);
                collectAllButton.onClick.AddListener(OnClickCollectAll);
                ConfigureButtonRect(collectAllButton.GetComponent<RectTransform>());
                SmallButtonIconUtility.ApplyIconButtonWithLabel(collectAllButton, "一键收取", "一键收取");
                return;
            }
        }

        GameObject buttonObject = new GameObject(buttonObjectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        ConfigureButtonRect(rect);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.52f, 0.28f, 0.95f);
        image.raycastTarget = true;

        collectAllButton = buttonObject.GetComponent<Button>();
        collectAllButton.targetGraphic = image;
        collectAllButton.onClick.AddListener(OnClickCollectAll);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        collectAllButtonText = textObject.GetComponent<TMP_Text>();
        collectAllButtonText.font = RuntimeFontAssetProvider.Resolve();
        collectAllButtonText.fontSize = 24f;
        collectAllButtonText.color = Color.white;
        collectAllButtonText.alignment = TextAlignmentOptions.Center;
        collectAllButtonText.enableWordWrapping = true;
        SmallButtonIconUtility.ApplyIconButtonWithLabel(collectAllButton, "一键收取", "一键收取");
    }

    private void ConfigureButtonRect(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = buttonPosition;
        rect.sizeDelta = buttonSize;
    }

    private void RefreshButtonState()
    {
        if (SceneManager.GetActiveScene().name != SimulationSceneName)
        {
            if (collectAllButton != null) collectAllButton.gameObject.SetActive(false);
            return;
        }

        if (createRuntimeButton) EnsureCollectAllButton();

        int readyCount = CountReadyResources();
        if (collectAllButtonText != null)
        {
            collectAllButtonText.text = "一键收取";
        }
        if (collectAllButton != null)
        {
            collectAllButton.gameObject.SetActive(true);
            collectAllButton.interactable = readyCount > 0;
        }
    }

}
