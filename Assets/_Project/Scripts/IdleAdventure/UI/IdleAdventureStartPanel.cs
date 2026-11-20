using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureStartPanel : MonoBehaviour
{
    private const string DisplayTitle = "闯关模式";

    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI stageSelectionText;
    [SerializeField] private Button previousStageButton;
    [SerializeField] private Button nextStageButton;
    [SerializeField] private Button startButton;

    public Button StartButton => startButton;
    public bool HasBindings => titleText != null
        && bodyText != null
        && stageSelectionText != null
        && previousStageButton != null
        && nextStageButton != null
        && startButton != null;

    public void Initialize(
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI bodyLabel,
        TextMeshProUGUI stageSelectionLabel,
        Button previousStageButtonControl,
        Button nextStageButtonControl,
        Button startButtonControl)
    {
        titleText = titleLabel;
        bodyText = bodyLabel;
        stageSelectionText = stageSelectionLabel;
        previousStageButton = previousStageButtonControl;
        nextStageButton = nextStageButtonControl;
        startButton = startButtonControl;
        ConfigureBodyText();
        ConfigureStageSelectionText();
    }

    public void Bind(Action onStart)
    {
        if (startButton == null) return;

        startButton.onClick.RemoveAllListeners();
        if (onStart != null) startButton.onClick.AddListener(() => onStart());
    }

    public void BindStageSelection(Action onPreviousStage, Action onNextStage)
    {
        if (previousStageButton != null)
        {
            previousStageButton.onClick.RemoveAllListeners();
            if (onPreviousStage != null) previousStageButton.onClick.AddListener(() => onPreviousStage());
        }

        if (nextStageButton != null)
        {
            nextStageButton.onClick.RemoveAllListeners();
            if (onNextStage != null) nextStageButton.onClick.AddListener(() => onNextStage());
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        SceneBackgroundMusic.SetPreparationVisible(true);
    }

    private void OnDisable()
    {
        SceneBackgroundMusic.SetPreparationVisible(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
    }

    public void SetStageSelection(string content)
    {
        if (stageSelectionText != null)
        {
            stageSelectionText.text = FrontendTextLocalization.Normalize(content);
            ConfigureStageSelectionText();
        }
    }

    public void SetStageSelectionInteractable(bool interactable)
    {
        if (previousStageButton != null) previousStageButton.interactable = interactable;
        if (nextStageButton != null) nextStageButton.interactable = interactable;
    }

    public void SetContent(string title, string body)
    {
        if (titleText != null)
        {
            titleText.color = Color.black;
            titleText.text = FrontendTextLocalization.Normalize(DisplayTitle);
        }
        if (bodyText != null)
        {
            bodyText.color = Color.black;
            bodyText.text = string.IsNullOrWhiteSpace(body) ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(body);
            ConfigureBodyText();
        }
    }

    private void ConfigureBodyText()
    {
        if (bodyText == null) return;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Ellipsis;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 12f;
        bodyText.fontSizeMax = Mathf.Min(24f, bodyText.fontSizeMax > 0f ? bodyText.fontSizeMax : 24f);
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.margin = new Vector4(12f, 4f, 12f, 4f);
    }

    private void ConfigureStageSelectionText()
    {
        if (stageSelectionText == null) return;
        stageSelectionText.enableWordWrapping = false;
        stageSelectionText.overflowMode = TextOverflowModes.Ellipsis;
        stageSelectionText.enableAutoSizing = true;
        stageSelectionText.fontSizeMin = 12f;
        stageSelectionText.fontSizeMax = Mathf.Min(18f, stageSelectionText.fontSizeMax > 0f ? stageSelectionText.fontSizeMax : 18f);
        stageSelectionText.margin = new Vector4(8f, 2f, 8f, 2f);
    }
}
