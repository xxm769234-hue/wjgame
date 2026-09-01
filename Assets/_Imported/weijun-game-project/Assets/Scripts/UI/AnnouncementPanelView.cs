using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class AnnouncementPanelView : MonoBehaviour
{
    [Header("Editable Artwork")]
    [SerializeField] private Image dimmerImage;
    [SerializeField] private Image paperImage;
    [SerializeField] private Image titleBannerImage;
    [SerializeField] private Image confirmButtonImage;

    [Header("Announcement Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text versionText;
    [SerializeField] private TMP_Text publishDateText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text scrollHintText;

    [Header("Scrolling")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;

    [Header("Actions")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button confirmButton;

    private Coroutine resetScrollCoroutine;

    public void SetAnnouncement(string title, string version, string publishDate, string body)
    {
        SetText(titleText, title);
        SetText(versionText, version);
        SetText(publishDateText, publishDate);
        SetText(bodyText, body);

        if (resetScrollCoroutine != null)
        {
            StopCoroutine(resetScrollCoroutine);
        }

        resetScrollCoroutine = StartCoroutine(ResetScrollNextFrame());
    }

    public void BindClose(UnityAction closeAction)
    {
        Bind(closeButton, closeAction);
        Bind(confirmButton, closeAction);
    }

    private IEnumerator ResetScrollNextFrame()
    {
        yield return null;

        if (bodyText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyText.rectTransform);
        }

        if (contentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        resetScrollCoroutine = null;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        Image dimmer,
        Image paper,
        Image titleBanner,
        Image confirmBackground,
        TMP_Text title,
        TMP_Text version,
        TMP_Text publishDate,
        TMP_Text body,
        TMP_Text scrollHint,
        ScrollRect announcementScrollRect,
        RectTransform announcementContent,
        Button close,
        Button confirm)
    {
        dimmerImage = dimmer;
        paperImage = paper;
        titleBannerImage = titleBanner;
        confirmButtonImage = confirmBackground;
        titleText = title;
        versionText = version;
        publishDateText = publishDate;
        bodyText = body;
        scrollHintText = scrollHint;
        scrollRect = announcementScrollRect;
        contentRoot = announcementContent;
        closeButton = close;
        confirmButton = confirm;
    }
#endif
}
