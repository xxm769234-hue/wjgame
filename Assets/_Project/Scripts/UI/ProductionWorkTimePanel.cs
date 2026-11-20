using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ProductionWorkTimePanel : MonoBehaviour
{
    private const string SimulationSceneName = "Simulation management mode";
    private static readonly Vector2 PopupDesignSize = new Vector2(620f, 620f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    private static void EnsureForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != SimulationSceneName) return;
        if (FindFirstObjectByType<ProductionWorkTimePanel>(FindObjectsInactive.Include) != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;
        GameObject panelObject = new GameObject("ProductionWorkTimePanel", typeof(RectTransform));
        panelObject.transform.SetParent(canvas.transform, false);
        panelObject.AddComponent<ProductionWorkTimePanel>();
    }

    [SerializeField] private Vector2 buttonPosition = new Vector2(420f, 76f);
    [SerializeField] private Vector2 buttonSize = new Vector2(84f, 84f);
    [SerializeField] private Vector2 panelSize = new Vector2(620f, 620f);

    private Canvas rootCanvas;
    private RectTransform canvasRect;
    private Button openButton;
    private Image openButtonIcon;
    private GameObject popupObject;
    private RectTransform popupRect;
    private TMP_Text stateText;
    private TMP_Text selectionText;
    private TMP_Text quantityText;
    private TMP_Text previewText;
    private TMP_Text statusText;
    private RectTransform fuelContent;
    private Button previewButton;
    private Button confirmButton;
    private Button decreaseButton;
    private Button increaseButton;
    private Button maximumButton;
    private Texture2D controlsTexture;
    private Texture2D popupsTexture;
    private Sprite resourceCardSprite;
    private ProductionWorkTimeIconCatalog resourceIcons;
    private readonly Dictionary<string, Sprite> skinSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly Dictionary<string, Sprite> resourceIconCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private Vector2 lastCanvasSize;
    private static readonly Color PaperTextColor = new Color(0.30f, 0.16f, 0.11f, 1f);
    private static readonly Color MutedTextColor = new Color(0.34f, 0.22f, 0.14f, 1f);

    private static readonly Dictionary<string, Rect> ControlAtlasRects = new Dictionary<string, Rect>(StringComparer.Ordinal)
    {
        { "ButtonPrimary", new Rect(16, 1536 - 16 - 182, 370, 182) },
        { "ButtonSecondary", new Rect(402, 1536 - 16 - 182, 369, 182) },
        { "ButtonCompact", new Rect(1558, 1536 - 16 - 146, 268, 146) },
        { "ButtonSquare", new Rect(16, 1536 - 214 - 200, 201, 200) },
        { "Close", new Rect(233, 1536 - 214 - 158, 158, 158) }
    };

    private static readonly Dictionary<string, Rect> PopupAtlasRects = new Dictionary<string, Rect>(StringComparer.Ordinal)
    {
        { "BuildingListPanel", new Rect(16, 1536 - 16 - 808, 807, 808) }
    };

    private readonly List<Button> fuelButtons = new List<Button>();
    private ProductionWorkStateResponse workState;
    private ProductionFuelResponse selectedFuel;
    private long selectedQuantity;
    private ProductionWorkTimePreviewResponse workTimePreview;
    private ProductionWorkTimeRequest workTimePreviewRequest;
    private string workTimeIdempotencyKey = string.Empty;
    private bool stateRequestRunning;
    private bool workTimeRequestRunning;

    public RectTransform ResolveOnboardingTarget()
    {
        if (popupObject == null || popupRect == null) return null;
        if (!popupObject.activeInHierarchy) OpenPopup();
        return popupRect;
    }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != SimulationSceneName)
        {
            enabled = false;
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            enabled = false;
            return;
        }

        canvasRect = rootCanvas.transform as RectTransform;
        BuildUi();
    }

    private void Start()
    {
        if (BackendGameService.Instance != null && BackendGameService.Instance.IsAuthenticated)
        {
            LoadWorkState();
        }
    }

    private void BuildUi()
    {
        openButton = CreateButton(
            "Global Production Work Time Button",
            rootCanvas.transform,
            "补充工时",
            new Color(0.18f, 0.42f, 0.78f, 0.95f),
            OpenPopup);
        ConfigureOpenButtonVisual();

        popupObject = CreateImage("Global Production Work Time Popup", rootCanvas.transform, Color.white);
        popupRect = popupObject.GetComponent<RectTransform>();
        ConfigurePopupSkin();
        SetCenterRect(popupRect, Vector2.zero, PopupDesignSize);

        TMP_Text title = CreateText("Title", popupObject.transform, "补充工时", 26f, TextAlignmentOptions.Midline,
            Color.white, new Vector2(0f, 277f), new Vector2(280f, 48f));
        title.fontStyle = FontStyles.Bold;
        SmallButtonIconUtility.ApplyOutlinedLabel(title);
        stateText = CreateText("Work State", popupObject.transform, "正在读取工作时间...", 20f,
            TextAlignmentOptions.Center, PaperTextColor, new Vector2(0f, 208f), new Vector2(480f, 64f));
        CreateText("Fuel Heading", popupObject.transform, "选择补充资源", 18f, TextAlignmentOptions.MidlineLeft,
            MutedTextColor, new Vector2(0f, 161f), new Vector2(516f, 28f));

        BuildFuelList();

        selectionText = CreateText("Selection", popupObject.transform, "请选择补充物品", 17f,
            TextAlignmentOptions.Center, PaperTextColor, new Vector2(0f, -95f), new Vector2(516f, 26f));
        selectionText.enableAutoSizing = true;
        selectionText.fontSizeMin = 14f;
        selectionText.fontSizeMax = 17f;
        selectionText.enableWordWrapping = false;
        selectionText.overflowMode = TextOverflowModes.Ellipsis;
        decreaseButton = CreateButton("Decrease Quantity", popupObject.transform, "−", Color.white, DecreaseQuantity);
        SetCenterRect(decreaseButton.GetComponent<RectTransform>(), new Vector2(-136f, -134f), new Vector2(42f, 42f));
        Image quantityBackground = CreateImage("Quantity Background", popupObject.transform, Color.white).GetComponent<Image>();
        quantityBackground.sprite = resourceCardSprite;
        quantityBackground.type = Image.Type.Sliced;
        quantityBackground.pixelsPerUnitMultiplier = 2f;
        quantityBackground.raycastTarget = false;
        SetCenterRect(quantityBackground.rectTransform, new Vector2(-42f, -134f), new Vector2(112f, 42f));
        quantityText = CreateText("Quantity", popupObject.transform, "0", 23f, TextAlignmentOptions.Center,
            PaperTextColor, new Vector2(-42f, -134f), new Vector2(102f, 38f));
        quantityText.enableAutoSizing = true;
        quantityText.fontSizeMin = 12f;
        quantityText.fontSizeMax = 23f;
        quantityText.enableWordWrapping = false;
        increaseButton = CreateButton("Increase Quantity", popupObject.transform, "+", Color.white, IncreaseQuantity);
        SetCenterRect(increaseButton.GetComponent<RectTransform>(), new Vector2(52f, -134f), new Vector2(42f, 42f));
        maximumButton = CreateButton("Maximum Quantity", popupObject.transform, "最大", Color.white, SetMaximumQuantity);
        SetCenterRect(maximumButton.GetComponent<RectTransform>(), new Vector2(150f, -134f), new Vector2(106f, 42f));

        previewText = CreateText("Preview", popupObject.transform, string.Empty, 17f, TextAlignmentOptions.Center,
            MutedTextColor, new Vector2(0f, -182f), new Vector2(516f, 40f));
        statusText = CreateText("Status", popupObject.transform, string.Empty, 15f, TextAlignmentOptions.Center,
            new Color(0.62f, 0.20f, 0.08f, 1f), new Vector2(0f, -224f), new Vector2(516f, 42f));
        ConfigureStatusScroll();

        Button closeButton = CreateButton("Close Production Work Time", popupObject.transform, string.Empty, Color.white, ClosePopup);
        SetCenterRect(closeButton.GetComponent<RectTransform>(), new Vector2(261f, 249f), new Vector2(38f, 38f));
        Image closeIcon = CreateImage("Close Icon", closeButton.transform, Color.white).GetComponent<Image>();
        closeIcon.sprite = GetSkinSprite("Close", false);
        closeIcon.preserveAspect = true;
        closeIcon.raycastTarget = false;
        SetCenterRect(closeIcon.rectTransform, Vector2.zero, new Vector2(24f, 24f));
        previewButton = CreateButton("Preview Production Work Time", popupObject.transform, "预览补充", Color.white, PreviewWorkTime);
        SetCenterRect(previewButton.GetComponent<RectTransform>(), new Vector2(-126f, -272f), new Vector2(212f, 44f));
        confirmButton = CreateButton("Confirm Production Work Time", popupObject.transform, "确认补充", Color.white, ConfirmWorkTime);
        SetCenterRect(confirmButton.GetComponent<RectTransform>(), new Vector2(126f, -272f), new Vector2(212f, 44f));

        ApplyButtonSkin(decreaseButton, "ButtonSquare");
        ApplyButtonSkin(increaseButton, "ButtonSquare");
        ApplyButtonSkin(maximumButton, "ButtonCompact");
        ApplyButtonSkin(closeButton, "ButtonSquare");
        ApplyButtonSkin(previewButton, "ButtonSecondary");
        ApplyButtonSkin(confirmButton, "ButtonPrimary");

        FitPopupToCanvas();
        popupObject.SetActive(false);
        RefreshUi();
    }

    private void BuildFuelList()
    {
        GameObject listObject = CreateImage("Fuel List", popupObject.transform, Color.clear);
        RectTransform listRect = listObject.GetComponent<RectTransform>();
        SetCenterRect(listRect, new Vector2(0f, 36f), new Vector2(532f, 220f));
        RectMask2D mask = listObject.AddComponent<RectMask2D>();
        mask.enabled = true;
        ScrollRect scroll = listObject.AddComponent<ScrollRect>();
        scroll.viewport = listRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject contentObject = new GameObject("Fuel Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(listObject.transform, false);
        fuelContent = contentObject.GetComponent<RectTransform>();
        fuelContent.anchorMin = new Vector2(0f, 1f);
        fuelContent.anchorMax = new Vector2(1f, 1f);
        fuelContent.pivot = new Vector2(0.5f, 1f);
        fuelContent.anchoredPosition = Vector2.zero;
        fuelContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 2, 2);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = fuelContent;
    }

    private void OpenPopup()
    {
        popupRect.SetAsLastSibling();
        FitPopupToCanvas();
        popupObject.SetActive(true);
        SetStatus(string.Empty);
        LoadWorkState();
    }

    private void ClosePopup()
    {
        popupObject.SetActive(false);
        workTimePreview = null;
        workTimePreviewRequest = null;
        workTimeIdempotencyKey = string.Empty;
        SetStatus(string.Empty);
        RefreshUi();
    }

    private void LoadWorkState()
    {
        BackendGameService service = BackendGameService.Instance;
        if (stateRequestRunning) return;
        if (service == null || !service.IsAuthenticated)
        {
            SetStatus("请先登录账号");
            RefreshUi();
            return;
        }

        stateRequestRunning = true;
        SetStatus("正在读取工作时间...");
        RefreshUi();
        service.LoadProductionWorkState(result =>
        {
            stateRequestRunning = false;
            if (result != null && result.Success && result.Data != null)
            {
                workState = result.Data;
                RebindSelectedFuel();
                SetStatus(string.Empty);
            }
            else
            {
                SetStatus(result == null || string.IsNullOrEmpty(result.Message)
                    ? "工作时间读取失败"
                    : result.Message);
            }
            RefreshUi();
        });
    }

    private void SelectFuel(ProductionFuelResponse fuel)
    {
        if (fuel == null || fuel.itemId <= 0L || fuel.maxConsumableQuantity <= 0L) return;
        selectedFuel = fuel;
        selectedQuantity = 1L;
        workTimePreview = null;
        workTimePreviewRequest = null;
        workTimeIdempotencyKey = string.Empty;
        SetStatus(string.Empty);
        RefreshUi();
    }

    private void DecreaseQuantity()
    {
        if (selectedFuel == null) return;
        selectedQuantity = Math.Max(1L, selectedQuantity - 1L);
        ClearPreviewAfterSelectionChange();
    }

    private void IncreaseQuantity()
    {
        if (selectedFuel == null) return;
        selectedQuantity = Math.Min(selectedFuel.maxConsumableQuantity, selectedQuantity + 1L);
        ClearPreviewAfterSelectionChange();
    }

    private void SetMaximumQuantity()
    {
        if (selectedFuel == null) return;
        selectedQuantity = selectedFuel.maxConsumableQuantity;
        ClearPreviewAfterSelectionChange();
    }

    private void ClearPreviewAfterSelectionChange()
    {
        workTimePreview = null;
        workTimePreviewRequest = null;
        workTimeIdempotencyKey = string.Empty;
        SetStatus(string.Empty);
        RefreshUi();
    }

    private void PreviewWorkTime()
    {
        BackendGameService service = BackendGameService.Instance;
        if (workTimeRequestRunning || stateRequestRunning) return;
        if (service == null || !service.IsAuthenticated)
        {
            SetStatus("请先登录账号");
            RefreshUi();
            return;
        }
        if (workState == null)
        {
            LoadWorkState();
            return;
        }
        RebindSelectedFuel();
        if (selectedFuel == null || selectedQuantity <= 0L)
        {
            SetStatus("请选择补充物品和数量");
            RefreshUi();
            return;
        }
        selectedQuantity = Math.Min(selectedQuantity, selectedFuel.maxConsumableQuantity);
        if (selectedQuantity <= 0L)
        {
            SetStatus("当前物品不可补充工作时间");
            RefreshUi();
            return;
        }

        workTimePreviewRequest = new ProductionWorkTimeRequest
        {
            itemId = selectedFuel.itemId,
            quantity = selectedQuantity,
            expectedConfigVersion = workState.configVersion
        };
        workTimePreview = null;
        workTimeIdempotencyKey = string.Empty;
        workTimeRequestRunning = true;
        SetStatus("正在预览补充...");
        RefreshUi();
        service.PreviewProductionWorkTime(workTimePreviewRequest, result =>
        {
            workTimeRequestRunning = false;
            if (result != null && result.Success && result.Data != null && result.Data.canConsume)
            {
                workTimePreview = result.Data;
                SetStatus(string.Empty);
            }
            else
            {
                workTimePreview = null;
                workTimePreviewRequest = null;
                SetStatus(result == null
                    ? "补充预览失败"
                    : string.IsNullOrEmpty(result.Message)
                        ? (result.Data == null ? "补充预览失败" : result.Data.unavailableReason)
                        : result.Message);
                RefreshStateAfterServerConflict(service, result == null ? 0 : result.Code);
            }
            RefreshUi();
        });
    }

    private void ConfirmWorkTime()
    {
        BackendGameService service = BackendGameService.Instance;
        if (workTimeRequestRunning || workTimePreviewRequest == null || workTimePreview == null) return;
        if (service == null || !service.IsAuthenticated)
        {
            SetStatus("请先登录账号");
            RefreshUi();
            return;
        }

        if (string.IsNullOrEmpty(workTimeIdempotencyKey))
        {
            workTimeIdempotencyKey = Guid.NewGuid().ToString("D");
        }
        workTimeRequestRunning = true;
        SetStatus("正在确认补充...");
        RefreshUi();
        service.ConsumeProductionWorkTime(workTimePreviewRequest, workTimeIdempotencyKey, result =>
        {
            workTimeRequestRunning = false;
            if (result != null && result.Success && result.Data != null)
            {
                if (result.Data.workState != null)
                {
                    workState = result.Data.workState;
                    RebindSelectedFuel();
                }
                SetStatus($"已补充 {FormatDuration(result.Data.grantedWorkSeconds)}");
                workTimePreview = null;
                workTimePreviewRequest = null;
                workTimeIdempotencyKey = string.Empty;
                service.RefreshInventory();
                service.RefreshProductions();
                if (result.Data.grantedWorkSeconds > 0) PlayerOnboardingGameplayBridge.NotifyBusinessSucceeded();
            }
            else
            {
                SetStatus(result == null || string.IsNullOrEmpty(result.Message)
                    ? "补充工作时间失败"
                    : result.Message);
                if (result != null && result.Code > 0)
                {
                    workTimePreview = null;
                    workTimePreviewRequest = null;
                    workTimeIdempotencyKey = string.Empty;
                    RefreshStateAfterServerConflict(service, result.Code);
                }
            }
            RefreshUi();
        });
    }

    private void RefreshStateAfterServerConflict(BackendGameService service, int code)
    {
        if (service == null) return;
        if (code == 7801 || code == 7802 || code == 7803 || code == 7805
            || code == 7806 || code == 7807 || code == 7809 || code == 7813)
        {
            LoadWorkState();
        }
    }

    private void RebindSelectedFuel()
    {
        if (workState == null || workState.fuels == null || selectedFuel == null)
        {
            return;
        }
        ProductionFuelResponse replacement = null;
        for (int i = 0; i < workState.fuels.Length; i++)
        {
            ProductionFuelResponse fuel = workState.fuels[i];
            if (fuel != null && fuel.itemId == selectedFuel.itemId)
            {
                replacement = fuel;
                break;
            }
        }
        selectedFuel = replacement;
        if (selectedFuel == null || selectedFuel.maxConsumableQuantity <= 0L)
        {
            selectedFuel = null;
            selectedQuantity = 0L;
        }
        else
        {
            selectedQuantity = Math.Max(1L, Math.Min(selectedQuantity <= 0L ? 1L : selectedQuantity, selectedFuel.maxConsumableQuantity));
        }
    }

    private void RefreshUi()
    {
        if (openButton != null)
        {
            BackendGameService service = BackendGameService.Instance;
            openButton.interactable = service != null && service.IsAuthenticated;
        }
        if (popupObject == null) return;

        stateText.text = workState == null
            ? "正在查看剩余工时……"
            : $"剩余工时  {FormatDuration(workState.remainingWorkSeconds)}\n<size=16>上限 {FormatDuration(workState.maxWorkSeconds)}  ·  {(workState.productionRunning ? "生产中" : "生产暂停")}</size>";
        RefreshFuelButtons();

        if (selectedFuel == null)
        {
            selectionText.text = "请选择补充物品";
            quantityText.text = "0";
            previewText.text = "消耗资源，为全部生产建筑补充工时";
        }
        else
        {
            selectionText.text = $"{ResolveFuelName(selectedFuel)}  ·  每个增加 {FormatDuration(selectedFuel.workSecondsPerItem)}";
            quantityText.text = selectedQuantity.ToString();
            previewText.text = workTimePreview == null
                ? "请选择数量后预览"
                : $"预计增加 {FormatDuration(workTimePreview.requestedWorkSeconds)}，补充后剩余 {FormatDuration(workTimePreview.afterRemainingWorkSeconds)}";
        }

        bool hasSelection = selectedFuel != null && selectedQuantity > 0L;
        bool busy = stateRequestRunning || workTimeRequestRunning;
        if (decreaseButton != null) decreaseButton.interactable = hasSelection && !busy && selectedQuantity > 1L;
        if (increaseButton != null) increaseButton.interactable = hasSelection && !busy && selectedQuantity < selectedFuel.maxConsumableQuantity;
        if (maximumButton != null) maximumButton.interactable = hasSelection && !busy;
        if (previewButton != null) previewButton.interactable = hasSelection && !busy;
        if (confirmButton != null) confirmButton.interactable = workTimePreview != null && workTimePreviewRequest != null && !busy;
    }

    private void ConfigureOpenButtonVisual()
    {
        if (openButton == null) return;

        RectTransform buttonRect = openButton.GetComponent<RectTransform>();
        float iconWidth = Mathf.Min(buttonSize.x, buttonSize.y);
        float totalHeight = iconWidth + 36f;
        Vector2 safePosition = new Vector2(buttonPosition.x, Mathf.Max(buttonPosition.y, totalHeight * 0.5f + 24f));
        SetBottomCenterRect(buttonRect, safePosition, new Vector2(Mathf.Max(104f, iconWidth), totalHeight));

        Image buttonImage = openButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = null;
            buttonImage.color = Color.clear;
            buttonImage.raycastTarget = true;
        }

        ColorBlock buttonColors = openButton.colors;
        buttonColors.normalColor = Color.white;
        buttonColors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        buttonColors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        buttonColors.selectedColor = Color.white;
        buttonColors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        buttonColors.colorMultiplier = 1f;
        openButton.colors = buttonColors;

        TMP_Text label = openButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.font = RuntimeFontAssetProvider.Resolve();
            label.text = FrontendTextLocalization.Normalize("补充工时");
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            SmallButtonIconUtility.ApplyNavigationLabel(label);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = Vector4.zero;
            label.characterSpacing = 0f;
            label.lineSpacing = 0f;
            label.paragraphSpacing = 0f;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(buttonRect.sizeDelta.x, 32f);
            labelRect.anchoredPosition = new Vector2(0f, -(iconWidth + 4f) * 0.5f);
        }

        Transform existingIcon = openButton.transform.Find("Icon");
        GameObject iconObject = existingIcon != null
            ? existingIcon.gameObject
            : new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existingIcon == null) iconObject.transform.SetParent(openButton.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = iconRect.anchorMin;
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconWidth, iconWidth);
        iconRect.anchoredPosition = new Vector2(0f, 18f);

        openButtonIcon = iconObject.GetComponent<Image>();
        openButtonIcon.sprite = Resources.Load<Sprite>("UI/Simulation/ProductionWorkTimeButton");
        openButtonIcon.color = Color.white;
        openButtonIcon.preserveAspect = true;
        openButtonIcon.raycastTarget = false;
        openButton.targetGraphic = openButtonIcon;
    }

    private void RefreshFuelButtons()
    {
        for (int i = fuelContent.childCount - 1; i >= 0; i--)
        {
            fuelContent.GetChild(i).gameObject.SetActive(false);
            Destroy(fuelContent.GetChild(i).gameObject);
        }
        fuelButtons.Clear();
        if (workState == null || workState.fuels == null || workState.fuels.Length == 0)
        {
            CreateText("Empty Fuel", fuelContent, "暂无可用补充物品", 18f, TextAlignmentOptions.Center, new Color(0.42f, 0.27f, 0.18f, 1f), Vector2.zero, new Vector2(500f, 44f));
            return;
        }

        for (int i = 0; i < workState.fuels.Length; i++)
        {
            ProductionFuelResponse fuel = workState.fuels[i];
            if (fuel == null || fuel.itemId <= 0L) continue;
            ProductionFuelResponse captured = fuel;
            string itemName = ResolveFuelName(fuel);
            Button button = CreateFuelButton(captured, itemName);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 68f;
            element.preferredHeight = 68f;
            button.interactable = fuel.maxConsumableQuantity > 0L;
            if (selectedFuel != null && selectedFuel.itemId == fuel.itemId)
            {
                button.GetComponent<Image>().color = new Color(1f, 0.89f, 0.64f, 1f);
            }
            fuelButtons.Add(button);
        }
    }

    private Button CreateFuelButton(ProductionFuelResponse fuel, string itemName)
    {
        GameObject buttonObject = CreateImage($"Fuel {fuel.itemId}", fuelContent, Color.white);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(() => SelectFuel(fuel));
        ConfigurePaperButtonColors(button);
        ColorBlock rowColors = button.colors;
        rowColors.disabledColor = new Color(0.94f, 0.92f, 0.88f, 1f);
        button.colors = rowColors;
        ApplyListItemSkin(button);

        Image icon = CreateImage("Resource Icon", button.transform, Color.white).GetComponent<Image>();
        SetLeftCenterRect(icon.rectTransform, 16f, new Vector2(60f, 54f));
        icon.sprite = LoadResourceIcon(fuel.itemKey, fuel.itemName, fuel.icon);
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text resourceName = CreateText("Resource Name", button.transform, itemName, 21f, TextAlignmentOptions.MidlineLeft,
            PaperTextColor, Vector2.zero, Vector2.zero);
        SetLeftCenterRect(resourceName.rectTransform, 90f, new Vector2(244f, 36f));
        resourceName.rectTransform.anchoredPosition += Vector2.up * 13f;
        resourceName.fontStyle = FontStyles.Bold;
        resourceName.enableAutoSizing = true;
        resourceName.fontSizeMin = 18f;
        resourceName.fontSizeMax = 21f;
        resourceName.enableWordWrapping = false;
        resourceName.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text detail = CreateText("Resource Detail", button.transform,
            $"每个 +{FormatDuration(fuel.workSecondsPerItem)}", 17f, TextAlignmentOptions.MidlineLeft,
            MutedTextColor, Vector2.zero, Vector2.zero);
        SetLeftCenterRect(detail.rectTransform, 90f, new Vector2(244f, 28f));
        detail.rectTransform.anchoredPosition -= Vector2.up * 15f;
        detail.enableWordWrapping = false;
        detail.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text inventory = CreateText("Resource Inventory", button.transform,
            $"拥有 {fuel.ownedQuantity}\n{(fuel.maxConsumableQuantity > 0L ? $"可用 {fuel.maxConsumableQuantity}" : "暂无可用资源")}",
            16f, TextAlignmentOptions.MidlineRight, MutedTextColor, Vector2.zero, Vector2.zero);
        SetLeftCenterRect(inventory.rectTransform, 350f, new Vector2(164f, 48f));
        inventory.enableAutoSizing = true;
        inventory.fontSizeMin = 12f;
        inventory.fontSizeMax = 16f;
        inventory.enableWordWrapping = false;
        return button;
    }

    private static string ResolveFuelName(ProductionFuelResponse fuel)
    {
        if (fuel == null) return "补充物品";
        if (!string.IsNullOrWhiteSpace(fuel.itemName)) return FrontendTextLocalization.Normalize(fuel.itemName);
        string knownName = NormalizeIconKey(fuel.itemKey, fuel.itemName, fuel.icon);
        return string.IsNullOrWhiteSpace(knownName) ? "补充物品" : knownName;
    }

    private void ConfigurePopupSkin()
    {
        if (popupObject == null) return;
        Image image = popupObject.GetComponent<Image>();
        popupsTexture = Resources.Load<Texture2D>("UI/LandInfoPanelPopups");
        controlsTexture = Resources.Load<Texture2D>("UI/LandInfoPanelControls");
        resourceCardSprite = Resources.Load<Sprite>("UI/WorkTimeResourceCard");
        resourceIcons = Resources.Load<ProductionWorkTimeIconCatalog>("UI/ProductionWorkTimeIcons");
        image.sprite = GetSkinSprite("BuildingListPanel", true);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private void ApplyListItemSkin(Button button)
    {
        Image image = button.GetComponent<Image>();
        Sprite sprite = resourceCardSprite;
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.color = Color.white;
        image.raycastTarget = true;
    }

    private void ApplyButtonSkin(Button button, string skinName)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        Sprite sprite = GetSkinSprite(skinName, false);
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.color = Color.white;
        button.targetGraphic = image;
        ConfigurePaperButtonColors(button);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Color.white;
            SmallButtonIconUtility.ApplyOutlinedLabel(label);
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 18f;
            label.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private Sprite GetSkinSprite(string skinName, bool popup)
    {
        if (skinSprites.TryGetValue(skinName, out Sprite cached) && cached != null) return cached;
        Texture2D texture = popup ? popupsTexture : controlsTexture;
        Dictionary<string, Rect> rects = popup ? PopupAtlasRects : ControlAtlasRects;
        if (texture == null || !rects.TryGetValue(skinName, out Rect source)) return null;
        float scaleX = texture.width / 2048f;
        float scaleY = texture.height / 1536f;
        Rect textureRect = new Rect(source.x * scaleX, source.y * scaleY, source.width * scaleX, source.height * scaleY);
        Vector4 border = skinName.StartsWith("Button", StringComparison.Ordinal)
            ? new Vector4(32f * scaleX, 32f * scaleY, 32f * scaleX, 32f * scaleY) : Vector4.zero;
        Sprite sprite = Sprite.Create(texture, textureRect, new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        skinSprites[skinName] = sprite;
        return sprite;
    }

    private Sprite LoadResourceIcon(string itemKey, string itemName, string iconKey)
    {
        if (resourceIcons == null) return null;
        string cacheKey = itemKey + "|" + itemName + "|" + iconKey;
        if (resourceIconCache.TryGetValue(cacheKey, out Sprite cached)) return cached;
        if (!string.IsNullOrWhiteSpace(iconKey))
        {
            string directPath = iconKey.Replace("\\", "/").Trim();
            int queryIndex = directPath.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0) directPath = directPath.Substring(0, queryIndex);
            int extensionIndex = directPath.LastIndexOf('/');
            string directName = extensionIndex >= 0 ? directPath.Substring(extensionIndex + 1) : directPath;
            int suffixIndex = directName.LastIndexOf('.');
            if (suffixIndex >= 0) directName = directName.Substring(0, suffixIndex);
            cached = resourceIcons.Find(directName);
        }
        if (cached == null) cached = resourceIcons.Find(NormalizeIconKey(itemKey, itemName, iconKey));
        resourceIconCache[cacheKey] = cached;
        return cached;
    }

    private static string NormalizeIconKey(string itemKey, string itemName, string iconKey)
    {
        string value = (itemKey + " " + iconKey + " " + itemName).ToLowerInvariant();
        if (value.Contains("gold") || value.Contains("金币")) return "金币";
        if (value.Contains("diamond") || value.Contains("crystal") || value.Contains("万能彩墨") || value.Contains("钻石")) return "万能彩墨";
        if (value.Contains("plank") || value.Contains("木板")) return "木板";
        if (value.Contains("wood") || value.Contains("木材")) return "木材";
        if (value.Contains("stone") || value.Contains("石材")) return "石材";
        if (value.Contains("iron") || value.Contains("ore") || value.Contains("矿")) return "矿石";
        if (value.Contains("wheat") || value.Contains("小麦")) return "小麦";
        if (value.Contains("grilled_fish") || value.Contains("roast_fish") || value.Contains("cooked_fish") || value.Contains("烤鱼")) return "烤鱼";
        if (value.Contains("grilled_meat") || value.Contains("roast_meat") || value.Contains("cooked_meat") || value.Contains("烤肉")) return "烤肉";
        if (value.Contains("fish") || value.Contains("鱼")) return "鱼";
        if (value.Contains("meat") || value.Contains("肉")) return "肉";
        if (value.Contains("bread") || value.Contains("food") || value.Contains("面包") || value.Contains("食物")) return "面包";
        if (value.Contains("metal") || value.Contains("金属")) return "金属";
        if (value.Contains("purification") || value.Contains("净化剂")) return "净化剂";
        if (value.Contains("stamina") || value.Contains("体力")) return "体力";
        return string.Empty;
    }

    private static void ConfigurePaperButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.87f, 1f);
        colors.pressedColor = new Color(0.85f, 0.76f, 0.62f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.78f, 0.75f, 0.70f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = string.IsNullOrWhiteSpace(message)
            ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(message);
        ScrollRect scroll = statusText != null ? statusText.GetComponentInParent<ScrollRect>() : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void ConfigureStatusScroll()
    {
        GameObject viewportObject = new GameObject("Status Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(popupObject.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetCenterRect(viewport, statusText.rectTransform.anchoredPosition, statusText.rectTransform.sizeDelta);
        viewportObject.GetComponent<Image>().color = Color.clear;
        statusText.transform.SetParent(viewport, false);
        RectTransform content = statusText.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        statusText.alignment = TextAlignmentOptions.Top;
        statusText.enableWordWrapping = true;
        statusText.overflowMode = TextOverflowModes.Overflow;
        ContentSizeFitter fitter = statusText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return imageObject;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = FrontendTextLocalization.Normalize(value ?? string.Empty);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        SetCenterRect(text.rectTransform, position, size);
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color color,
        UnityAction action)
    {
        GameObject buttonObject = CreateImage(objectName, parent, color);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;
        button.onClick.AddListener(action);

        TMP_Text text = CreateText("Label", buttonObject.transform, label, 18f, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.zero);
        SmallButtonIconUtility.ApplyOutlinedLabel(text);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(8f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    private static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetBottomCenterRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetLeftCenterRect(RectTransform rect, float x, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = size;
    }

    private void FitPopupToCanvas()
    {
        if (popupRect == null || canvasRect == null) return;
        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;
        lastCanvasSize = canvasSize;
        popupRect.sizeDelta = PopupDesignSize;
        float preferredScale = Mathf.Min(panelSize.x / PopupDesignSize.x, panelSize.y / PopupDesignSize.y);
        preferredScale = Mathf.Max(preferredScale, Mathf.Min(canvasSize.x * 0.46f / PopupDesignSize.x, canvasSize.y * 0.74f / PopupDesignSize.y));
        float scale = Mathf.Min(preferredScale, (canvasSize.x - 32f) / PopupDesignSize.x, (canvasSize.y - 32f) / PopupDesignSize.y);
        popupRect.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    private void LateUpdate()
    {
        if (popupObject != null && popupObject.activeSelf && canvasRect != null && lastCanvasSize != canvasRect.rect.size)
            FitPopupToCanvas();
    }

    private void OnDestroy()
    {
        if (popupObject != null) Destroy(popupObject);
        if (openButton != null) Destroy(openButton.gameObject);
        foreach (Sprite sprite in skinSprites.Values)
        {
            if (sprite != null) Destroy(sprite);
        }
    }

    private static string FormatDuration(long seconds)
    {
        long safeSeconds = Math.Max(0L, seconds);
        long hours = safeSeconds / 3600L;
        long minutes = (safeSeconds % 3600L) / 60L;
        long remainder = safeSeconds % 60L;
        return hours > 0L
            ? $"{hours}小时{minutes}分"
            : minutes > 0L ? $"{minutes}分{remainder}秒" : $"{remainder}秒";
    }
}
