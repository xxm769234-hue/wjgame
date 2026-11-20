using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureStageEndPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button endModeButton;
    [SerializeField] private TextMeshProUGUI continueButtonText;
    [SerializeField] private TextMeshProUGUI endModeButtonText;
    private readonly List<KeyValuePair<string, int>> rewards = new List<KeyValuePair<string, int>>();
    private readonly Dictionary<string, string> rewardNames = new Dictionary<string, string>();
    private readonly List<RewardCard> rewardCards = new List<RewardCard>();
    private IdleAdventurePreviewIconCatalog rewardIcons;
    private RectTransform rewardContent;
    private TextMeshProUGUI rewardHeading;
    private bool showRewards;

    public Button ContinueButton => continueButton;
    public Button EndModeButton => endModeButton;
    public bool HasBindings => titleText != null && bodyText != null && continueButton != null && endModeButton != null;

    public void Initialize(TextMeshProUGUI titleLabel, TextMeshProUGUI bodyLabel, Button continueButtonControl, Button endModeButtonControl)
    {
        titleText = titleLabel;
        bodyText = bodyLabel;
        continueButton = continueButtonControl;
        endModeButton = endModeButtonControl;
        continueButtonText = continueButton != null ? continueButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        endModeButtonText = endModeButton != null ? endModeButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        ConfigureTextLayout();
    }

    public void Bind(Action onContinue, Action onEndMode)
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            if (onContinue != null) continueButton.onClick.AddListener(() => onContinue());
        }

        if (endModeButton != null)
        {
            endModeButton.onClick.RemoveAllListeners();
            if (onEndMode != null) endModeButton.onClick.AddListener(() => onEndMode());
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        ConfigureTextLayout();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetContinueInteractable(bool interactable)
    {
        if (continueButton != null) continueButton.interactable = interactable;
    }

    public void SetEndModeInteractable(bool interactable)
    {
        if (endModeButton != null) endModeButton.interactable = interactable;
    }

    public void SetContent(string title, string body)
    {
        showRewards = false;
        if (rewardContent != null) rewardContent.gameObject.SetActive(false);
        if (titleText != null) titleText.text = FrontendTextLocalization.Normalize(title);
        if (bodyText != null)
        {
            bodyText.text = FrontendTextLocalization.Normalize(body);
            ConfigureTextLayout();
        }
    }

    public void SetContent(string title, string body, IReadOnlyDictionary<string, int> pendingRewards,
        IReadOnlyDictionary<string, string> displayNames)
    {
        rewards.Clear();
        rewardNames.Clear();
        if (pendingRewards != null)
        {
            foreach (KeyValuePair<string, int> reward in pendingRewards)
                if (!string.IsNullOrWhiteSpace(reward.Key) && reward.Value > 0) rewards.Add(reward);
        }
        if (displayNames != null)
            foreach (KeyValuePair<string, string> name in displayNames) rewardNames[name.Key] = name.Value;
        showRewards = true;
        if (titleText != null) titleText.text = FrontendTextLocalization.Normalize(title);
        if (bodyText != null) bodyText.text = FrontendTextLocalization.Normalize(body);
        ConfigureTextLayout();
        if (rewardContent != null) rewardContent.anchoredPosition = Vector2.zero;
    }

    public void SetButtonLabels(string continueLabel, string endModeLabel)
    {
        if (continueButtonText == null && continueButton != null)
        {
            continueButtonText = continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (endModeButtonText == null && endModeButton != null)
        {
            endModeButtonText = endModeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (continueButtonText != null) continueButtonText.text = FrontendTextLocalization.Normalize(continueLabel);
        if (endModeButtonText != null) endModeButtonText.text = FrontendTextLocalization.Normalize(endModeLabel);
    }

    private void ConfigureTextLayout()
    {
        if (showRewards && titleText != null && bodyText != null) EnsureRewardContent();
        IdleAdventureDialogLayout.Apply(titleText, bodyText, continueButton, endModeButton, showRewards ? rewardContent : null);
        if (showRewards) LayoutRewardCards();
    }

    private void EnsureRewardContent()
    {
        if (rewardIcons == null) rewardIcons = Resources.Load<IdleAdventurePreviewIconCatalog>("IdleAdventureUI/PreviewIcons");
        if (rewardContent == null)
        {
            rewardContent = new GameObject("Settlement Reward Content", typeof(RectTransform)).GetComponent<RectTransform>();
            rewardContent.SetParent(titleText.transform.parent, false);
            rewardHeading = CreateRewardText("Reward Heading", rewardContent, 20f, FontStyles.Bold);
        }
        rewardContent.gameObject.SetActive(true);
        for (int index = rewardCards.Count; index < rewards.Count; index++)
        {
            RectTransform cardRoot = new GameObject("Reward Card " + index, typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<RectTransform>();
            cardRoot.SetParent(rewardContent, false);
            Image background = cardRoot.GetComponent<Image>();
            background.color = new Color(0.83f, 0.72f, 0.51f, 0.24f);
            background.raycastTarget = false;
            Outline border = cardRoot.GetComponent<Outline>();
            border.effectColor = new Color(0.52f, 0.36f, 0.19f, 0.3f);
            border.effectDistance = new Vector2(1f, -1f);
            Image icon = new GameObject("Reward Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(cardRoot, false);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            rewardCards.Add(new RewardCard
            {
                root = cardRoot,
                icon = icon,
                quantity = CreateRewardText("Reward Quantity", cardRoot, 25f, FontStyles.Bold),
                fallbackName = CreateRewardText("Reward Name", cardRoot, 20f, FontStyles.Normal)
            });
        }
    }

    private void LayoutRewardCards()
    {
        if (rewardContent == null || bodyText == null) return;
        const float contentWidth = 606f;
        const float cardWidth = 190f;
        const float cardHeight = 84f;
        const float gap = 12f;
        const float gridTop = 64f;
        Place(bodyText.rectTransform, 0f, 0f, contentWidth, 30f);
        bodyText.fontSize = 21f;
        bodyText.lineSpacing = 0f;
        bodyText.margin = Vector4.zero;
        bodyText.alignment = TextAlignmentOptions.Center;
        Place(rewardHeading.rectTransform, 0f, 32f, contentWidth, 26f);
        rewardHeading.text = rewards.Count > 0 ? "待领取奖励" : "本次暂无待领取奖励";
        int columns = Mathf.Min(3, Mathf.Max(1, rewards.Count));
        float gridLeft = (contentWidth - (columns * cardWidth + (columns - 1) * gap)) * 0.5f;
        for (int index = 0; index < rewardCards.Count; index++)
        {
            RewardCard card = rewardCards[index];
            card.root.gameObject.SetActive(index < rewards.Count);
            if (index >= rewards.Count) continue;
            KeyValuePair<string, int> reward = rewards[index];
            string name = rewardNames.TryGetValue(reward.Key, out string displayName) && !string.IsNullOrWhiteSpace(displayName)
                ? displayName : "奖励";
            Sprite icon = rewardIcons != null ? rewardIcons.Find(reward.Key) ?? rewardIcons.Find(name) : null;
            Place(card.root, gridLeft + (index % columns) * (cardWidth + gap), gridTop + (index / columns) * (cardHeight + gap), cardWidth, cardHeight);
            card.icon.sprite = icon;
            card.icon.gameObject.SetActive(icon != null);
            card.fallbackName.gameObject.SetActive(icon == null);
            card.quantity.text = "× " + reward.Value.ToString("N0");
            card.quantity.enableAutoSizing = true;
            card.quantity.fontSizeMin = 16f;
            card.quantity.fontSizeMax = 25f;
            card.quantity.enableWordWrapping = false;
            card.quantity.alignment = TextAlignmentOptions.MidlineLeft;
            if (icon != null)
            {
                Place(card.icon.rectTransform, 10f, 16f, 52f, 52f);
                Place(card.quantity.rectTransform, 69f, 15f, 113f, 54f);
            }
            else
            {
                card.fallbackName.text = FrontendTextLocalization.Normalize(name);
                card.fallbackName.enableWordWrapping = true;
                card.fallbackName.enableAutoSizing = true;
                card.fallbackName.fontSizeMin = 16f;
                card.fallbackName.fontSizeMax = 20f;
                Place(card.fallbackName.rectTransform, 9f, 8f, 172f, 34f);
                Place(card.quantity.rectTransform, 9f, 44f, 172f, 31f);
                card.quantity.alignment = TextAlignmentOptions.Center;
            }
        }
        int rows = rewards.Count == 0 ? 0 : (rewards.Count + columns - 1) / columns;
        float contentHeight = Mathf.Max(232f, gridTop + rows * (cardHeight + gap));
        rewardContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
    }

    private TextMeshProUGUI CreateRewardText(string objectName, Transform parent, float size, FontStyles style)
    {
        TextMeshProUGUI text = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.font = bodyText.font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = new Color(0.28f, 0.18f, 0.12f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private sealed class RewardCard
    {
        public RectTransform root;
        public Image icon;
        public TextMeshProUGUI quantity;
        public TextMeshProUGUI fallbackName;
    }
}
