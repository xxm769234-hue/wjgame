using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventurePausePanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TextMeshProUGUI resumeButtonText;
    [SerializeField] private TextMeshProUGUI exitButtonText;

    public bool HasBindings => titleText != null && bodyText != null && resumeButton != null && exitButton != null;
    public bool IsVisible => gameObject.activeSelf;

    public void Initialize(TextMeshProUGUI titleLabel, TextMeshProUGUI bodyLabel, Button resumeButtonControl, Button exitButtonControl)
    {
        titleText = titleLabel;
        bodyText = bodyLabel;
        resumeButton = resumeButtonControl;
        exitButton = exitButtonControl;
        resumeButtonText = resumeButton != null ? resumeButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        exitButtonText = exitButton != null ? exitButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    public void Bind(Action onResume, Action onExit)
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            if (onResume != null) resumeButton.onClick.AddListener(() => onResume());
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            if (onExit != null) exitButton.onClick.AddListener(() => onExit());
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
        if (resumeButton != null) resumeButton.interactable = interactable;
        if (exitButton != null) exitButton.interactable = interactable;
    }

    public void SetContent(string title, string body)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
    }

    public void SetButtonLabels(string resumeLabel, string exitLabel)
    {
        if (resumeButtonText == null && resumeButton != null)
        {
            resumeButtonText = resumeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (exitButtonText == null && exitButton != null)
        {
            exitButtonText = exitButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (resumeButtonText != null) resumeButtonText.text = resumeLabel;
        if (exitButtonText != null) exitButtonText.text = exitLabel;
    }
}
