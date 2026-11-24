using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IdleAdventureAfkRewardPanel : MonoBehaviour
{
    private const string Art = "IdleAdventureUI/Afk/";
    private const int LayoutVersion = 5;
    private const float WindowWidth = 850f;
    private const float WindowHeight = 520f;
    private static readonly Color Ink = new Color(0.28f, 0.18f, 0.12f, 1f);
    private static readonly Color MutedInk = new Color(0.46f, 0.38f, 0.29f, 1f);

    [SerializeField] private int layoutVersion;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private RectTransform window;
    [SerializeField] private GameObject modal;
    [SerializeField] private Button entryButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button collectButton;
    [SerializeField] private Button accelerateButton;
    [SerializeField] private Image entryIcon;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image accumulationFill;
    [SerializeField] private Image staminaIcon;
    [SerializeField] private Image receiptIcon;
    [SerializeField] private GameObject rewardContent;
    [SerializeField] private GameObject rulesContent;
    [SerializeField] private Button rulesButton;
    [SerializeField] private TextMeshProUGUI rulesText;
    [SerializeField] private TextMeshProUGUI iconText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI itemText;
    [SerializeField] private TextMeshProUGUI accelerationText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private IdleAdventureAfkRewardSource source;

    private UnityAction accelerateAction;
    private IdleAdventurePreviewIconCatalog icons;
    private Rect lastSafeArea;
    private Vector2 lastScreenSize;
    private float statusExpiresAt;

    public Button AccelerateButton => accelerateButton;
    public bool IsVisible => modal != null && modal.activeSelf;

    public RectTransform ResolveOnboardingTarget()
    {
        if (!IsVisible) Show();
        return window != null && window.gameObject.activeInHierarchy ? window : null;
    }

    public void Initialize(IdleAdventureAfkRewardSource rewardSource, IdleAdventureRewardLedger ledger, UnityAction accelerate)
    {
        if (source != null) source.FeedbackChanged -= HandleFeedback;
        source = rewardSource;
        RectTransform root = (RectTransform)transform;
        Stretch(root);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        Image rootImage = GetComponent<Image>();
        if (rootImage != null) { rootImage.color = Color.clear; rootImage.raycastTarget = false; }

        IdleAdventureBookPagePerspective[] legacyPerspective =
            GetComponentsInChildren<IdleAdventureBookPagePerspective>(true);
        foreach (IdleAdventureBookPagePerspective effect in legacyPerspective)
        {
            if (effect == null) continue;
            effect.enabled = false;
            if (Application.isPlaying) Destroy(effect);
            else DestroyImmediate(effect);
        }

        if (layoutVersion != LayoutVersion || entryButton == null || modal == null || collectButton == null)
        {
            for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
            {
                GameObject child = transform.GetChild(childIndex).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            Build();
            layoutVersion = LayoutVersion;
        }

        entryButton.onClick.RemoveListener(Show);
        entryButton.onClick.AddListener(Show);
        closeButton.onClick.RemoveListener(Hide);
        closeButton.onClick.AddListener(Hide);
        backdropButton.onClick.RemoveListener(Hide);
        backdropButton.onClick.AddListener(Hide);
        rulesButton.onClick.RemoveListener(ToggleRules);
        rulesButton.onClick.AddListener(ToggleRules);
        if (accelerateAction != null) accelerateButton.onClick.RemoveListener(accelerateAction);
        accelerateAction = accelerate;
        if (accelerateAction != null) accelerateButton.onClick.AddListener(accelerateAction);

        icons = Resources.Load<IdleAdventurePreviewIconCatalog>("IdleAdventureUI/PreviewIcons");
        source.Initialize(ledger, collectButton, entryIcon, accumulationFill, iconText, amountText, timerText,
            collectButton.GetComponentInChildren<TextMeshProUGUI>(true));
        source.FeedbackChanged += HandleFeedback;
        source.SetIconSprites(Resources.Load<Sprite>("UI/ApprovedUi/Icons/AfkAvailable") ?? Resources.Load<Sprite>("UI/SmallIcons/挂机奖励"),
            Resources.Load<Sprite>("UI/ApprovedUi/Icons/AfkUnavailable") ?? Resources.Load<Sprite>(Art + "RewardIconGray"));
        UpdateLayout();
        RefreshRewardDetails();
    }

    public void Show()
    {
        if (modal == null) return;
        modal.SetActive(true);
        modal.transform.SetAsLastSibling();
        SetRulesVisible(false);
        ExpireStatus();
        UpdateLayout();
        RefreshRewardDetails();
        if (Application.isPlaying && source != null) source.TryLoadServerState();
    }

    public void Hide()
    {
        if (modal != null) modal.SetActive(false);
    }

    public void SetStatus(string message, float duration = 2.4f)
    {
        if (statusText == null) return;
        statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(message);
        statusExpiresAt = duration > 0f ? Time.realtimeSinceStartup + duration : 0f;
        receiptIcon.gameObject.SetActive(false);
        timerText.gameObject.SetActive(string.IsNullOrEmpty(message));
        Place(statusText.rectTransform, 85f, 300f, 680f, 32f);
    }

    private void HandleFeedback(string message, bool pending)
    {
        SetStatus(message, pending ? 0f : 2.4f);
    }

    public void SetAccelerationState(bool available, string unavailableReason)
    {
        if (accelerateButton == null) return;
        accelerateButton.interactable = available;
        staminaIcon.sprite = icons != null ? icons.Find("stamina") : null;
        bool showIcon = available && staminaIcon.sprite != null;
        staminaIcon.gameObject.SetActive(showIcon);
        accelerationText.text = available ? (showIcon ? "消耗" : "消耗体力") : unavailableReason;
        accelerationText.alignment = TextAlignmentOptions.Center;
        Place(accelerationText.rectTransform, showIcon ? 524f : 449f, 355f, showIcon ? 48f : 236f, 34f);
    }

    public void ShowAccelerationResult(IdleAdventureAccelerateResponse response)
    {
        if (response == null) return;
        List<IdleAdventureRewardItemResponse> rewards = new List<IdleAdventureRewardItemResponse>();
        IdleAdventureRewardItemResponse[] total = NonEmpty(response.totalRewards, response.total_rewards);
        if (total != null) rewards.AddRange(total);
        else
        {
            IdleAdventureRewardItemResponse[] natural = NonEmpty(response.naturalRewards, response.natural_rewards);
            IdleAdventureRewardItemResponse[] accelerated = NonEmpty(response.accelerationRewards, response.acceleration_rewards);
            if (natural != null) rewards.AddRange(natural);
            if (accelerated != null) rewards.AddRange(accelerated);
        }

        Dictionary<string, long> quantities = new Dictionary<string, long>();
        Dictionary<string, string> names = new Dictionary<string, string>();
        string firstKey = null;
        foreach (IdleAdventureRewardItemResponse reward in rewards)
        {
            if (reward == null || reward.quantity <= 0L) continue;
            string name = reward.ResolvedDisplayName;
            string key = reward.ResolvedItemKey;
            if (string.IsNullOrEmpty(name)) name = key == source.ItemKey ? source.DisplayName : key;
            if (string.IsNullOrEmpty(name)) name = "奖励";
            if (string.IsNullOrEmpty(key)) key = name;
            if (firstKey == null) firstKey = key;
            names[key] = name;
            quantities.TryGetValue(key, out long previous);
            quantities[key] = previous > long.MaxValue - reward.quantity ? long.MaxValue : previous + reward.quantity;
        }

        List<string> entries = new List<string>();
        foreach (KeyValuePair<string, long> reward in quantities)
            entries.Add($"{names[reward.Key]} × {reward.Value:N0}");
        string prefix = response.ResolvedIdempotentReplay ? "已到账：" : "已获得 ";
        SetStatus(entries.Count > 0 ? prefix + string.Join("、", entries) : "奖励已到账");
        Sprite sprite = firstKey != null && quantities.Count == 1 && icons != null ? icons.Find(firstKey) : null;
        if (sprite == null) return;
        receiptIcon.sprite = sprite;
        receiptIcon.gameObject.SetActive(true);
        float width = Mathf.Min(600f, statusText.GetPreferredValues(statusText.text).x + 8f);
        float left = (WindowWidth - width - 34f) * 0.5f;
        Place(receiptIcon.rectTransform, left, 302f, 28f, 28f);
        Place(statusText.rectTransform, left + 34f, 300f, width, 32f);
    }

    private static IdleAdventureRewardItemResponse[] NonEmpty(IdleAdventureRewardItemResponse[] first, IdleAdventureRewardItemResponse[] second)
    {
        return first != null && first.Length > 0 ? first : second;
    }

    private void ToggleRules() => SetRulesVisible(!rulesContent.activeSelf);

    private void SetRulesVisible(bool visible)
    {
        rulesContent.SetActive(visible);
        rewardContent.SetActive(!visible);
        rulesButton.GetComponentInChildren<TextMeshProUGUI>().text = visible ? "返回收益" : "收益说明";
    }

    private void ExpireStatus()
    {
        if (statusExpiresAt > 0f && Time.realtimeSinceStartup >= statusExpiresAt) SetStatus(string.Empty, 0f);
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (source != null) source.FeedbackChanged -= HandleFeedback;
    }

    private void LateUpdate()
    {
        if (safeArea == null) return;
        if (lastSafeArea != Screen.safeArea || lastScreenSize != new Vector2(Screen.width, Screen.height)) UpdateLayout();
        if (!IsVisible) return;
        ExpireStatus();
        RefreshRewardDetails();
        if (Application.isPlaying && Input.GetKeyDown(KeyCode.Escape) && !PlayerOnboardingInputLock.IsGameplayInputBlocked)
        {
            if (rulesContent.activeSelf) SetRulesVisible(false);
            else Hide();
        }
    }

    private void UpdateLayout()
    {
        if (safeArea == null) return;
        lastScreenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        lastSafeArea = Screen.safeArea;
        Rect area = lastSafeArea.width > 0f && lastSafeArea.height > 0f
            ? lastSafeArea : new Rect(Vector2.zero, lastScreenSize);
        safeArea.anchorMin = new Vector2(area.xMin / lastScreenSize.x, area.yMin / lastScreenSize.y);
        safeArea.anchorMax = new Vector2(area.xMax / lastScreenSize.x, area.yMax / lastScreenSize.y);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        if (window != null)
        {
            RectTransform root = (RectTransform)transform;
            float width = root.rect.width * (safeArea.anchorMax.x - safeArea.anchorMin.x);
            float height = root.rect.height * (safeArea.anchorMax.y - safeArea.anchorMin.y);
            float scale = Mathf.Min(1f, Mathf.Max(0.1f, (width - 48f) / WindowWidth), Mathf.Max(0.1f, (height - 48f) / WindowHeight));
            window.localScale = Vector3.one * scale;
            window.anchorMin = window.anchorMax = (safeArea.anchorMin + safeArea.anchorMax) * 0.5f;
        }
    }

    private void RefreshRewardDetails()
    {
        if (source == null || itemText == null) return;
        itemText.text = source.DisplayName;
        rulesText.text = source.HasServerSnapshot
            ? $"每小时产出 {source.QuantityPerCycle * (3600d / source.ProductionIntervalSeconds):N0} {source.DisplayName}，每 {source.ProductionIntervalSeconds:0} 秒结算一次。\n最多储存 {source.MaxStoredQuantity:N0} {source.DisplayName}，存满后暂停积累，请及时领取。\n快速收益消耗体力，获得的奖励直接到账，无需再次领取。"
            : "收益规则同步中，请稍候。";
        if (icons == null) icons = Resources.Load<IdleAdventurePreviewIconCatalog>("IdleAdventureUI/PreviewIcons");
        Sprite sprite = icons != null ? icons.Find(source.ItemKey) ?? icons.Find(source.DisplayName) : null;
        itemIcon.sprite = sprite;
        itemIcon.enabled = sprite != null;
    }

    private void Build()
    {
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(transform, false);
        Stretch(safeArea);

        entryButton = MakeButton("AFK Reward Entry", safeArea, string.Empty, null);
        RectTransform entry = entryButton.GetComponent<RectTransform>();
        entry.anchorMin = entry.anchorMax = entry.pivot = new Vector2(1f, 0f);
        entry.anchoredPosition = new Vector2(-24f, 24f);
        entry.sizeDelta = new Vector2(136f, 154f);
        entryButton.image.color = Color.clear;
        entryButton.transition = Selectable.Transition.None;
        entryIcon = MakeImage("Reward Icon", entry, null);
        Place(entryIcon.rectTransform, 12f, 0f, 112f, 118f);
        entryIcon.preserveAspect = true;
        iconText = MakeText("Icon Text", entryIcon.transform, "奖", 30f);
        Stretch(iconText.rectTransform);
        iconText.gameObject.SetActive(Resources.Load<Sprite>("UI/SmallIcons/挂机奖励") == null);
        TextMeshProUGUI entryLabel = MakeText("Entry Label", entry, "挂机奖励", 24f);
        Place(entryLabel.rectTransform, 0f, 118f, 136f, 38f);
        SmallButtonIconUtility.ApplyOutlinedLabel(entryLabel);

        Image backdrop = MakeImage("AFK Reward Detail Panel", transform, null);
        backdrop.color = new Color(0f, 0f, 0f, 0.46f);
        backdrop.raycastTarget = true;
        Stretch(backdrop.rectTransform);
        modal = backdrop.gameObject;
        backdropButton = modal.AddComponent<Button>();
        backdropButton.targetGraphic = backdrop;
        backdropButton.transition = Selectable.Transition.None;
        Image paper = MakeImage("Paper Window", backdrop.transform, Resources.Load<Sprite>(Art + "Panel"));
        paper.raycastTarget = true;
        Button paperBlocker = paper.gameObject.AddComponent<Button>();
        paperBlocker.targetGraphic = paper;
        paperBlocker.transition = Selectable.Transition.None;
        window = paper.rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.anchoredPosition = Vector2.zero;
        window.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        TextMeshProUGUI title = MakeText("Title", window, "挂机奖励", 34f);
        Place(title.rectTransform, 285f, 31f, 280f, 52f);
        title.fontStyle = FontStyles.Bold;
        closeButton = MakeButton("Close Button", window, string.Empty, Resources.Load<Sprite>(Art + "Close"));
        Place(closeButton.GetComponent<RectTransform>(), 744f, 34f, 62f, 62f);
        closeButton.image.type = Image.Type.Simple;
        closeButton.image.preserveAspect = true;

        rulesButton = MakeButton("Rules Button", window, "收益说明", Resources.Load<Sprite>("UI/ApprovedUi/Controls/SecondaryButton"));
        Place(rulesButton.GetComponent<RectTransform>(), 606f, 40f, 122f, 46f);
        rulesButton.transition = Selectable.Transition.ColorTint;
        TextMeshProUGUI rulesLabel = rulesButton.GetComponentInChildren<TextMeshProUGUI>();
        rulesLabel.fontSize = 21f;
        rulesLabel.fontStyle = FontStyles.Bold;
        rulesLabel.color = Ink;
        rulesLabel.outlineWidth = 0f;

        rewardContent = new GameObject("Reward Content", typeof(RectTransform));
        rewardContent.transform.SetParent(window, false);
        Stretch((RectTransform)rewardContent.transform);
        itemIcon = MakeImage("Reward Item Icon", rewardContent.transform, null);
        Place(itemIcon.rectTransform, 375f, 104f, 100f, 100f);
        itemIcon.preserveAspect = true;
        itemText = MakeText("Item Name", rewardContent.transform, string.Empty, 24f);
        Place(itemText.rectTransform, 175f, 208f, 500f, 38f);
        itemText.fontStyle = FontStyles.Bold;
        Image accumulationTrack = MakeImage("Accumulation Track", rewardContent.transform, Resources.Load<Sprite>(Art + "ProgressTrack"));
        Place(accumulationTrack.rectTransform, 165f, 255f, 520f, 34f);
        accumulationTrack.type = Image.Type.Sliced;
        accumulationTrack.pixelsPerUnitMultiplier = 2f;
        accumulationFill = MakeImage("Accumulation Fill", accumulationTrack.transform, Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel"));
        Place(accumulationFill.rectTransform, 9f, 9f, 502f, 16f);
        accumulationFill.type = Image.Type.Filled;
        accumulationFill.fillMethod = Image.FillMethod.Horizontal;
        accumulationFill.fillOrigin = 0;
        accumulationFill.color = new Color(0.94f, 0.62f, 0.25f, 1f);
        amountText = MakeText("Amount Text", accumulationTrack.transform, "同步中", 19f);
        Stretch(amountText.rectTransform);
        amountText.fontStyle = FontStyles.Bold;
        amountText.color = Color.white;
        SmallButtonIconUtility.ApplyOutlinedLabel(amountText);
        amountText.enableAutoSizing = true;
        amountText.fontSizeMin = 14f;
        amountText.fontSizeMax = 19f;
        timerText = MakeText("Timer Text", rewardContent.transform, string.Empty, 18f);
        Place(timerText.rectTransform, 175f, 300f, 500f, 28f);
        timerText.color = MutedInk;

        rulesContent = new GameObject("Rules Content", typeof(RectTransform));
        rulesContent.transform.SetParent(window, false);
        Stretch((RectTransform)rulesContent.transform);
        TextMeshProUGUI rulesTitle = MakeText("Rules Title", rulesContent.transform, "收益说明", 24f);
        Place(rulesTitle.rectTransform, 125f, 112f, 600f, 40f);
        rulesTitle.fontStyle = FontStyles.Bold;
        rulesText = MakeText("Rules Text", rulesContent.transform, string.Empty, 21f, TextAlignmentOptions.TopLeft);
        Place(rulesText.rectTransform, 135f, 168f, 580f, 170f);
        rulesText.enableWordWrapping = true;
        rulesText.lineSpacing = 7f;
        rulesContent.SetActive(false);

        staminaIcon = MakeImage("Stamina Icon", window, null);
        Place(staminaIcon.rectTransform, 576f, 355f, 34f, 34f);
        staminaIcon.preserveAspect = true;
        accelerationText = MakeText("Acceleration Text", window, string.Empty, 20f);
        Place(accelerationText.rectTransform, 449f, 355f, 236f, 34f);
        accelerationText.color = Ink;

        collectButton = MakeButton("Collect Reward Button", window, "领取奖励", Resources.Load<Sprite>(Art + "ButtonPrimary"));
        Place(collectButton.GetComponent<RectTransform>(), 165f, 393f, 236f, 66f);
        accelerateButton = MakeButton("Accelerate Button", window, "快速收益", Resources.Load<Sprite>(Art + "ButtonSecondary"));
        Place(accelerateButton.GetComponent<RectTransform>(), 449f, 393f, 236f, 66f);
        receiptIcon = MakeImage("Receipt Icon", rewardContent.transform, null);
        receiptIcon.preserveAspect = true;
        receiptIcon.gameObject.SetActive(false);
        statusText = MakeText("Action Status", rewardContent.transform, string.Empty, 19f);
        Place(statusText.rectTransform, 85f, 300f, 680f, 32f);
        statusText.color = MutedInk;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 14f;
        statusText.fontSizeMax = 19f;
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        modal.SetActive(false);
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = value;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, string value, Sprite sprite)
    {
        Image image = MakeImage(name, parent, sprite);
        image.raycastTarget = true;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 2f;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = button.spriteState;
        state.disabledSprite = Resources.Load<Sprite>(Art + "ButtonDisabled");
        button.spriteState = state;
        if (!string.IsNullOrEmpty(value))
        {
            TextMeshProUGUI label = MakeText("Label", image.transform, value, 25f);
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            Stretch(label.rectTransform);
            SmallButtonIconUtility.ApplyOutlinedLabel(label);
        }
        return button;
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
