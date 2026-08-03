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
    }

    private void OnDisable()
    {
        backgroundDismissEnabled = false;
        dismissArmRoutine = null;
    }

    private void Update()
    {
        if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape) && !purchasePending) Hide();
    }

    public void Show(LandSlotController slot)
    {
        if (slot == null || (!slot.IsPurificationAvailable && !slot.IsPurchaseAvailable)) return;
        if (window == null) BuildUi();

        currentSlot = slot;
        purchasePending = false;
        purificationMode = slot.IsPurificationAvailable;
        titleText.text = purificationMode ? "Purify Land" : "Unlock Land";
        purchaseButtonText.text = purificationMode ? "Purify" : "Purchase";
        costText.gameObject.SetActive(!purificationMode);
        balanceText.gameObject.SetActive(!purificationMode);
        costText.text = purificationMode ? string.Empty : $"{slot.PurchaseGoldCost:N0} Gold";
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
            statusText.text = "Backend session is not authenticated.";
            return;
        }

        purchasePending = true;
        purchaseButton.interactable = false;
        bool requestedPurification = purificationMode;
        statusText.text = requestedPurification ? "Purifying..." : "Purchasing...";
        LandSlotController requestedSlot = currentSlot;
        System.Action<bool, string, LandSlotStateResponse> onComplete = (success, message, state) =>
        {
            purchasePending = false;
            if (success)
            {
                if (state != null) requestedSlot.ApplyRemoteUnlockState(state);
                currentSlot = null;
                gameObject.SetActive(false);
                return;
            }

            purchaseButton.interactable = true;
            statusText.text = string.IsNullOrEmpty(message)
                ? requestedPurification ? "Purification failed." : "Purchase failed."
                : message;
            if (!requestedPurification) RefreshBalance();
        };

        if (requestedPurification) service.PurifyLandSlot(requestedSlot, onComplete);
        else service.PurchaseLandSlot(requestedSlot, onComplete);
    }

    private void RefreshBalance()
    {
        BackendGameService service = BackendGameService.Instance;
        int gold = service != null && service.CurrentUser != null ? service.CurrentUser.gold : 0;
        balanceText.text = $"Balance: {gold:N0} Gold";
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

        window = CreateImage("Window", transform, new Color(0.11f, 0.13f, 0.14f, 0.98f));
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(520f, 300f);

        titleText = CreateText("Title", window.transform, "Unlock Land", 32f, FontStyles.Bold);
        SetRect(titleText.rectTransform, new Vector2(0f, 98f), new Vector2(440f, 54f));

        costText = CreateText("Cost", window.transform, string.Empty, 30f, FontStyles.Bold);
        costText.color = new Color(1f, 0.76f, 0.18f, 1f);
        SetRect(costText.rectTransform, new Vector2(0f, 34f), new Vector2(440f, 48f));

        balanceText = CreateText("Balance", window.transform, string.Empty, 20f, FontStyles.Normal);
        balanceText.color = new Color(0.75f, 0.79f, 0.8f, 1f);
        SetRect(balanceText.rectTransform, new Vector2(0f, -12f), new Vector2(440f, 36f));

        statusText = CreateText("Status", window.transform, string.Empty, 18f, FontStyles.Normal);
        statusText.color = new Color(0.95f, 0.42f, 0.35f, 1f);
        SetRect(statusText.rectTransform, new Vector2(0f, -54f), new Vector2(440f, 42f));

        Button cancelButton = CreateButton(
            "Cancel",
            window.transform,
            "Cancel",
            new Color(0.25f, 0.28f, 0.29f, 1f),
            Hide);
        SetRect((RectTransform)cancelButton.transform, new Vector2(-112f, -108f), new Vector2(190f, 54f));

        purchaseButton = CreateButton(
            "Purchase",
            window.transform,
            "Purchase",
            new Color(0.17f, 0.48f, 0.31f, 1f),
            Purchase);
        purchaseButtonText = purchaseButton.GetComponentInChildren<TMP_Text>();
        SetRect((RectTransform)purchaseButton.transform, new Vector2(112f, -108f), new Vector2(190f, 54f));
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
        text.text = value;
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
