using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureStartPanel : MonoBehaviour
{
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
        if (stageSelectionText != null) stageSelectionText.text = content;
    }

    public void SetStageSelectionInteractable(bool interactable)
    {
        if (previousStageButton != null) previousStageButton.interactable = interactable;
        if (nextStageButton != null) nextStageButton.interactable = interactable;
    }

    public void SetContent(string title, string body)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
    }
}
