using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime confirmation panel for purifying or purchasing one land slot.</summary>
public sealed class LandSlotPurchasePanel : MonoBehaviour
{
    public static LandSlotPurchasePanel Instance { get; private set; }

    private GameObject window;
    private TMP_Text titleText;
    private TMP_Text costText;
    private TMP_Text balanceText;
    private TMP_Text statusText;
    private Button purchaseButton;
    private TMP_Text purchaseButtonText;
    private LandSlotController currentSlot;
    private bool purchasePending;
    private bool purificationMode;
    private bool backgroundDismissEnabled;
    private Coroutine dismissArmRoutine;
    private Sprite confirmationSprite;
    private RectTransform canvasRect;

    public RectTransform ResolveOnboardingTarget(LandSlotController slot)
    {
        if (slot == null || !slot.IsPurificationAvailable || slot.PurificationItemAmount <= 0) return null;
        if (currentSlot != slot || !gameObject.activeInHierarchy) Show(slot);
        return window != null && window.activeInHierarchy ? window.transform as RectTransform : null;
    }

    public static LandSlotPurchasePanel EnsureInstance()
    {
        if (Instance != null) return Instance;

        LandSlotPurchasePanel existing = FindFirstObjectByType<LandSlotPurchasePanel>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Runtime UI Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        GameObject panelObject = new GameObject(
            "Land Slot Purchase Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        LandSlotPurchasePanel panel = panelObject.AddComponent<LandSlotPurchasePanel>();
        panel.BuildUi();
        panelObject.SetActive(false);
        return panel;
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (confirmationSprite != null) Destroy(confirmationSprite);
    }

    private void OnDisable()
    {
        backgroundDismissEnabled = false;
        dismissArmRoutine = null;
    }

    private void Update()
    {
        if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape) && !purchasePending && !PlayerOnboardingInputLock.IsGameplayInputBlocked) Hide();
        if (window != null && canvasRect != null)
        {
            float scale = Mathf.Min(1f, (canvasRect.rect.width - 32f) / 569f, (canvasRect.rect.height - 32f) / 421f);
            window.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }

    public void Show(LandSlotController slot)
    {
        if (slot == null || (!slot.IsPurificationAvailable && !slot.IsPurchaseAvailable)) return;
        if (window == null) BuildUi();

        currentSlot = slot;
        purchasePending = false;
        purificationMode = slot.IsPurificationAvailable;
        titleText.text = purificationMode ? "净化地块" : "解锁地块";
        purchaseButtonText.text = purificationMode ? "净化" : "购买";
        costText.gameObject.SetActive(true);
        balanceText.gameObject.SetActive(!purificationMode);
        costText.text = purificationMode
            ? BuildPurificationCostText(slot)
            : $"{slot.PurchaseGoldCost:N0} 金币";
        statusText.text = string.Empty;
        if (!purificationMode) RefreshBalance();
        purchaseButton.interactable = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        backgroundDismissEnabled = false;
        if (dismissArmRoutine != null) StopCoroutine(dismissArmRoutine);
        dismissArmRoutine = StartCoroutine(ArmBackgroundDismissal());
    }

    public void Hide()
    {
        if (purchasePending) return;
        currentSlot = null;
        gameObject.SetActive(false);
    }

    private void Purchase()
    {
        if (purchasePending || currentSlot == null) return;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            statusText.text = "请先登录账号。";
            return;
        }

        purchasePending = true;
        purchaseButton.interactable = false;
        bool requestedPurification = purificationMode;
        statusText.text = requestedPurification ? "正在净化……" : "正在购买……";
        LandSlotController requestedSlot = currentSlot;
        System.Action<BackendResult<LandSlotStateResponse>> onComplete = result =>
        {
            purchasePending = false;
            bool success = result != null && result.Success && result.Data != null;
            if (success)
            {
                requestedSlot.ApplyRemoteUnlockState(result.Data);
                currentSlot = null;
                gameObject.SetActive(false);
                PlayerOnboardingGameplayBridge.NotifyBusinessSucceeded();
                return;
            }

            string message = result == null ? string.Empty : result.Message;
            if (!requestedPurification && result != null && result.Code == 7601)
            {
                purchasePending = true;
                statusText.text = "消耗已变化，正在更新……";
                service.SyncMap((refreshSuccess, refreshMessage) =>
                {
                    purchasePending = false;
                    if (currentSlot != requestedSlot) return;
                    RefreshFromAuthoritativeSlot(
                        requestedSlot,
                        refreshSuccess
                            ? "价格已变化，请确认新的消耗后重试。"
                            : "消耗更新失败：" + FrontendTextLocalization.NormalizePlayerMessage(refreshMessage),
                        refreshSuccess);
                });
                return;
            }
            if (!requestedPurification && result != null && result.Code == 7602)
            {
                purchaseButton.interactable = false;
                statusText.text = "暂时无法解锁，正在重新查看……";
                StartCoroutine(RefreshPurchasePolicyAfterDelay(service, requestedSlot));
                return;
            }

            purchaseButton.interactable = requestedPurification
                ? requestedSlot.IsPurificationAvailable
                : requestedSlot.IsPurchaseAvailable;
            statusText.text = FrontendTextLocalization.NormalizePlayerMessage(message,
                requestedPurification ? "净化失败，请稍后重试。" : "购买失败，请稍后重试。");
            if (!requestedPurification) RefreshBalance();
        };

        if (requestedPurification) service.PurifyLandSlot(requestedSlot, onComplete);
        else service.PurchaseLandSlot(requestedSlot, onComplete);
    }

    private IEnumerator RefreshPurchasePolicyAfterDelay(BackendGameService service, LandSlotController slot)
    {
        yield return new WaitForSecondsRealtime(5f);
        if (service == null || !service.IsAuthenticated || currentSlot != slot) yield break;

        purchasePending = true;
        service.SyncMap((success, message) =>
        {
            purchasePending = false;
            if (currentSlot != slot) return;
            RefreshFromAuthoritativeSlot(
                slot,
                success
                    ? "解锁条件已更新。"
                    : "解锁条件更新失败：" + FrontendTextLocalization.NormalizePlayerMessage(message),
                success);
        });
    }

    private void RefreshFromAuthoritativeSlot(LandSlotController slot, string message, bool allowInteraction)
    {
        if (slot == null || currentSlot != slot) return;

        purificationMode = slot.IsPurificationAvailable;
        titleText.text = purificationMode ? "净化地块" : "解锁地块";
        purchaseButtonText.text = purificationMode ? "净化" : "购买";
        costText.text = purificationMode
            ? BuildPurificationCostText(slot)
            : $"{slot.PurchaseGoldCost:N0} 金币";
        balanceText.gameObject.SetActive(!purificationMode);
        if (!purificationMode) RefreshBalance();
        purchaseButton.interactable = allowInteraction
            && (purificationMode ? slot.IsPurificationAvailable : slot.IsPurchaseAvailable);
        statusText.text = FrontendTextLocalization.NormalizePlayerMessage(message);
    }

    private void RefreshBalance()
    {
        BackendGameService service = BackendGameService.Instance;
        long gold = service != null && service.CurrentUser != null ? service.CurrentUser.gold : 0L;
        balanceText.text = $"余额：{gold:N0} 金币";
    }

    private static string BuildPurificationCostText(LandSlotController slot)
    {
        if (slot == null || slot.PurificationItemAmount <= 0)
        {
            return "无法获取净化消耗";
        }

        string itemName = string.IsNullOrEmpty(slot.PurificationItemKey)
            ? "净化材料"
            : FrontendTextLocalization.NormalizePlayerMessage(FrontendTextLocalization.Normalize(slot.PurificationItemKey), "净化材料");
        return $"{itemName} ×{slot.PurificationItemAmount}";
    }

    private IEnumerator ArmBackgroundDismissal()
    {
        yield return null;
        while (Input.GetMouseButton(0) || Input.touchCount > 0) yield return null;
        yield return null;
        backgroundDismissEnabled = true;
        dismissArmRoutine = null;
    }

    private void DismissFromBackground()
    {
        if (backgroundDismissEnabled) Hide();
    }

    private void BuildUi()
    {
        if (window != null) return;

        RectTransform rootRect = (RectTransform)transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image rootImage = GetComponent<Image>();
        rootImage.color = Color.clear;
        rootImage.raycastTarget = false;

        GameObject background = CreateImage(
            "Background",
            transform,
            new Color(0.03f, 0.04f, 0.05f, 0.66f));
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Button dismissArea = background.AddComponent<Button>();
        dismissArea.transition = Selectable.Transition.None;
        dismissArea.onClick.AddListener(DismissFromBackground);

        window = CreateImage("Window", transform, new Color(1f, 0.94f, 0.82f, 0.98f));
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(569f, 421f);
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;

        Image windowImage = window.GetComponent<Image>();
        Texture2D popupAtlas = Resources.Load<Texture2D>("UI/LandInfoPanelPopups");
        if (windowImage != null && popupAtlas != null)
        {
            float scaleX = popupAtlas.width / 2048f;
            float scaleY = popupAtlas.height / 1536f;
            confirmationSprite = Sprite.Create(popupAtlas,
                new Rect(1042f * scaleX, 275f * scaleY, 569f * scaleX, 421f * scaleY),
                new Vector2(0.5f, 0.5f), 100f);
            windowImage.sprite = confirmationSprite;
            windowImage.type = Image.Type.Simple;
            windowImage.preserveAspect = true;
            windowImage.color = Color.white;
        }

        titleText = CreateText("Title", window.transform, "解锁地块", 32f, FontStyles.Bold);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 22f;
        titleText.fontSizeMax = 32f;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.color = new Color(0.30f, 0.16f, 0.11f, 1f);
        SetRect(titleText.rectTransform, new Vector2(0f, 164f), new Vector2(390f, 48f));

        costText = CreateText("Cost", window.transform, string.Empty, 30f, FontStyles.Bold);
        costText.color = new Color(0.43f, 0.24f, 0.08f, 1f);
        costText.enableAutoSizing = true;
        costText.fontSizeMin = 18f;
        costText.fontSizeMax = 30f;
        costText.enableWordWrapping = false;
        costText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(costText.rectTransform, new Vector2(0f, 76f), new Vector2(460f, 52f));

        balanceText = CreateText("Balance", window.transform, string.Empty, 20f, FontStyles.Normal);
        balanceText.color = new Color(0.42f, 0.27f, 0.18f, 1f);
        balanceText.enableAutoSizing = true;
        balanceText.fontSizeMin = 16f;
        balanceText.fontSizeMax = 20f;
        balanceText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(balanceText.rectTransform, new Vector2(0f, 25f), new Vector2(460f, 32f));

        statusText = CreateText("Status", window.transform, string.Empty, 18f, FontStyles.Normal);
        statusText.color = new Color(0.62f, 0.20f, 0.08f, 1f);
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 12f;
        statusText.fontSizeMax = 18f;
        statusText.enableWordWrapping = true;
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(statusText.rectTransform, new Vector2(0f, -32f), new Vector2(460f, 60f));

        Button cancelButton = CreateButton(
            "取消",
            window.transform,
            "取消",
            confirmationSprite != null ? Color.clear : new Color(0.82f, 0.72f, 0.59f, 1f),
            Hide);
        SetRect((RectTransform)cancelButton.transform, new Vector2(-122f, -114f), new Vector2(192f, 76f));

        purchaseButton = CreateButton(
            "购买",
            window.transform,
            "购买",
            confirmationSprite != null ? Color.clear : new Color(0.91f, 0.75f, 0.46f, 1f),
            Purchase);
        purchaseButtonText = purchaseButton.GetComponentInChildren<TMP_Text>();
        SetRect((RectTransform)purchaseButton.transform, new Vector2(111f, -114f), new Vector2(192f, 76f));
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        value.transform.SetParent(parent, false);
        value.GetComponent<Image>().color = color;
        return value;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = FrontendTextLocalization.Normalize(value);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Color color,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateImage(name, parent, color);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;
        button.onClick.AddListener(action);

        TMP_Text text = CreateText("Label", buttonObject.transform, label, 22f, FontStyles.Bold);
        text.color = new Color(0.30f, 0.16f, 0.11f, 1f);
        button.targetGraphic = text;
        ColorBlock paperColors = button.colors;
        paperColors.normalColor = Color.white;
        paperColors.highlightedColor = new Color(1f, 0.88f, 0.64f, 1f);
        paperColors.pressedColor = new Color(0.68f, 0.55f, 0.40f, 1f);
        paperColors.selectedColor = Color.white;
        paperColors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = paperColors;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
