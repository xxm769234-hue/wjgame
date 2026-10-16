using System;
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
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
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

        if (continueButtonText != null) continueButtonText.text = continueLabel;
        if (endModeButtonText != null) endModeButtonText.text = endModeLabel;
    }
}
