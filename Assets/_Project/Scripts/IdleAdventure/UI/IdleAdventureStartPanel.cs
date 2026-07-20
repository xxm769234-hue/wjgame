using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureStartPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button startButton;

    public Button StartButton => startButton;
    public bool HasBindings => titleText != null && bodyText != null && startButton != null;

    public void Initialize(TextMeshProUGUI titleLabel, TextMeshProUGUI bodyLabel, Button startButtonControl)
    {
        titleText = titleLabel;
        bodyText = bodyLabel;
        startButton = startButtonControl;
    }

    public void Bind(Action onStart)
    {
        if (startButton == null) return;

        startButton.onClick.RemoveAllListeners();
        if (onStart != null) startButton.onClick.AddListener(() => onStart());
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

    public void SetContent(string title, string body)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
    }
}
