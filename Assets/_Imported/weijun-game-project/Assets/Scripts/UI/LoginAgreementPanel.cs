using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LoginAgreementPanel : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.28f, 0.17f, 0.10f);
    private const float CardWidth = 1040f;
    private const float CardHeight = CardWidth * 1216f / 1294f;
    private Vector2 lastRootSize;
    private RectTransform root;
    private RectTransform scrollbarRect;
    private Scrollbar scrollbar;

    public RectTransform Card { get; private set; }
    public RectTransform Viewport { get; private set; }
    public RectTransform Content { get; private set; }
    public TMP_Text Body { get; private set; }
    public ScrollRect Scroll { get; private set; }
    public Button ConfirmButton { get; private set; }
    public Button CloseButton { get; private set; }

    public static LoginAgreementPanel Create(Transform parent, string name, string title,
        string action, UnityAction confirmed, UnityAction closed)
    {
        Image shade = MakeImage(parent, name, new Color(0.08f, 0.05f, 0.03f, 0.66f));
        Place(shade.rectTransform, Vector2.zero, Vector2.one);
        LoginAgreementPanel panel = shade.gameObject.AddComponent<LoginAgreementPanel>();
        panel.root = shade.rectTransform;
        Image paper = MakeImage(shade.transform, "Paper", Color.white, "Panel");
        panel.Card = paper.rectTransform;
        panel.Card.anchorMin = panel.Card.anchorMax = new Vector2(0.5f, 0.5f);
        panel.Card.sizeDelta = new Vector2(CardWidth, CardHeight);
        TMP_Text heading = MakeText(paper.transform, "Title", title, 38f, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        Place(heading.rectTransform, new Vector2(0.29f, 0.857f), new Vector2(0.71f, 0.943f));

        Image viewport = MakeImage(paper.transform, "Viewport", Color.clear);
        panel.Viewport = viewport.rectTransform;
        Place(panel.Viewport, new Vector2(0.10f, 0.215f), new Vector2(0.895f, 0.826f));
        viewport.gameObject.AddComponent<RectMask2D>();
        panel.Content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.Content.SetParent(viewport.transform, false);
        panel.Content.anchorMin = new Vector2(0f, 1f);
        panel.Content.anchorMax = Vector2.one;
        panel.Content.pivot = new Vector2(0.5f, 1f);
        panel.Content.anchoredPosition = Vector2.zero;
        panel.Content.sizeDelta = Vector2.zero;
        panel.Body = MakeText(panel.Content, "Body", string.Empty, 25f, TextAlignmentOptions.TopLeft);
        panel.Body.lineSpacing = 10f;
        panel.Body.paragraphSpacing = 10f;
        Place(panel.Body.rectTransform, Vector2.zero, Vector2.one);
        panel.Body.rectTransform.offsetMin = new Vector2(4f, 12f);
        panel.Body.rectTransform.offsetMax = new Vector2(-12f, -4f);
        panel.Scroll = viewport.gameObject.AddComponent<ScrollRect>();
        panel.Scroll.viewport = panel.Viewport;
        panel.Scroll.content = panel.Content;
        panel.Scroll.horizontal = false;
        panel.Scroll.movementType = ScrollRect.MovementType.Clamped;
        panel.Scroll.scrollSensitivity = 35f;

        Image rail = MakeImage(paper.transform, "Scrollbar", Color.white, "ScrollTrack");
        panel.scrollbarRect = rail.rectTransform;
        Place(rail.rectTransform, new Vector2(0.920f, 0.215f), new Vector2(0.936f, 0.826f));
        panel.scrollbar = rail.gameObject.AddComponent<Scrollbar>();
        Image handle = MakeImage(rail.transform, "Handle", Color.white, "ScrollThumb");
        Place(handle.rectTransform, Vector2.zero, Vector2.one);
        panel.scrollbar.handleRect = handle.rectTransform;
        panel.scrollbar.targetGraphic = handle;
        panel.scrollbar.direction = Scrollbar.Direction.BottomToTop;
        panel.Scroll.verticalScrollbar = panel.scrollbar;

        Image actionImage = MakeImage(paper.transform, "ConfirmButton", Color.white, "Button");
        Place(actionImage.rectTransform, new Vector2(431f / 1294f, 100f / 1216f),
            new Vector2(868f / 1294f, 242f / 1216f));
        panel.ConfirmButton = actionImage.gameObject.AddComponent<Button>();
        panel.ConfirmButton.targetGraphic = actionImage;
        panel.ConfirmButton.onClick.AddListener(confirmed);
        TMP_Text actionText = MakeText(actionImage.transform, "Label", action, 32f, TextAlignmentOptions.Center);
        actionText.fontStyle = FontStyles.Bold;
        actionText.color = new Color(1f, 0.96f, 0.83f);
        Place(actionText.rectTransform, new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.85f));

        Image closeImage = MakeImage(paper.transform, "CloseButton", Color.clear);
        Place(closeImage.rectTransform, new Vector2(0.86f, 0.83f), new Vector2(0.925f, 0.92f));
        panel.CloseButton = closeImage.gameObject.AddComponent<Button>();
        panel.CloseButton.targetGraphic = closeImage;
        panel.CloseButton.onClick.AddListener(closed);
        TMP_Text closeText = MakeText(closeImage.transform, "Label", "×", 44f, TextAlignmentOptions.Center);
        Place(closeText.rectTransform, Vector2.zero, Vector2.one);
        LoginCanvasStyle.ApplyDialogCloseButton(panel.CloseButton);
        panel.RefreshLayout();
        return panel;
    }

    public void SetBody(string value, bool richText = true)
    {
        Body.richText = richText;
        Body.text = value ?? string.Empty;
        RefreshLayout();
        Scroll.verticalNormalizedPosition = 1f;
        scrollbar.SetValueWithoutNotify(1f);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshLayout();
        Scroll.StopMovement();
        Scroll.verticalNormalizedPosition = 1f;
        scrollbar.SetValueWithoutNotify(1f);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void ConfigureForm()
    {
        Place(Viewport, new Vector2(0.10f, 0.455f), new Vector2(0.895f, 0.826f));
        Place(scrollbarRect, new Vector2(0.920f, 0.455f), new Vector2(0.936f, 0.826f));
        Body.fontSize = 24f;
        RefreshLayout();
    }

    public TMP_InputField AddInput(string name, string label, string placeholder, float bottom, int limit)
    {
        TMP_Text caption = MakeText(Card, name + "Label", label, 25f, TextAlignmentOptions.MidlineLeft);
        Place(caption.rectTransform, new Vector2(0.115f, bottom), new Vector2(0.29f, bottom + 0.065f));
        Image border = MakeImage(Card, name, new Color(0.58f, 0.40f, 0.23f, 0.65f));
        Place(border.rectTransform, new Vector2(0.29f, bottom), new Vector2(0.885f, bottom + 0.065f));
        Image field = MakeImage(border.transform, "Fill", new Color(1f, 0.965f, 0.86f));
        Place(field.rectTransform, Vector2.zero, Vector2.one);
        field.rectTransform.offsetMin = Vector2.one * 2f;
        field.rectTransform.offsetMax = Vector2.one * -2f;
        field.raycastTarget = false;
        TMP_InputField input = border.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = field;
        input.characterLimit = limit;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        RectTransform textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        textArea.SetParent(border.transform, false);
        Place(textArea, Vector2.zero, Vector2.one);
        textArea.offsetMin = new Vector2(16f, 5f);
        textArea.offsetMax = new Vector2(-16f, -5f);
        input.textViewport = textArea;
        TMP_Text text = MakeText(textArea, "Text", string.Empty, 25f, TextAlignmentOptions.MidlineLeft);
        text.enableWordWrapping = false;
        Place(text.rectTransform, Vector2.zero, Vector2.one);
        input.textComponent = text;
        TMP_Text hint = MakeText(textArea, "Placeholder", placeholder, 23f, TextAlignmentOptions.MidlineLeft);
        hint.color = new Color(0.56f, 0.46f, 0.34f);
        hint.enableWordWrapping = false;
        Place(hint.rectTransform, Vector2.zero, Vector2.one);
        input.placeholder = hint;
        return input;
    }

    public TMP_Text AddStatus()
    {
        TMP_Text status = MakeText(Card, "Status", string.Empty, 21f, TextAlignmentOptions.Center);
        Place(status.rectTransform, new Vector2(0.10f, 0.207f), new Vector2(0.895f, 0.275f));
        return status;
    }

    public void RefreshLayout()
    {
        if (root == null || Body == null) return;
        lastRootSize = root.rect.size;
        float scale = Mathf.Min(1f, Mathf.Min((lastRootSize.x - 40f) / CardWidth, (lastRootSize.y - 40f) / CardHeight));
        Card.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        Card.ForceUpdateRectTransforms();
        Viewport.ForceUpdateRectTransforms();
        Content.ForceUpdateRectTransforms();
        float height = Body.GetPreferredValues(Body.text, Mathf.Max(1f, Viewport.rect.width - 16f), Mathf.Infinity).y + 24f;
        Content.sizeDelta = new Vector2(0f, Mathf.Max(Viewport.rect.height, height));
        Body.rectTransform.ForceUpdateRectTransforms();
        Body.ForceMeshUpdate(true);
        if (Body.textBounds.size.y > 0f)
        {
            height = Body.rectTransform.rect.yMax - Body.textBounds.min.y + 28f;
            Content.sizeDelta = new Vector2(0f, Mathf.Max(Viewport.rect.height, height));
        }
        scrollbar.size = Mathf.Clamp01(Viewport.rect.height / Mathf.Max(1f, Content.sizeDelta.y));
        scrollbar.SetValueWithoutNotify(Scroll.verticalNormalizedPosition);
        scrollbar.gameObject.SetActive(height > Viewport.rect.height + 1f);
    }

    private void LateUpdate()
    {
        if (root != null && root.rect.size != lastRootSize) RefreshLayout();
    }

    private static Image MakeImage(Transform parent, string name, Color color, string resource = null)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        if (resource != null) image.sprite = Resources.Load<Sprite>("UI/LoginAgreement/" + resource);
        return image;
    }

    private static TMP_Text MakeText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(parent, false);
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = value;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
