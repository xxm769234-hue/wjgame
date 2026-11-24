using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IdleAdventureFormationRoster : MonoBehaviour
{
    private const string ResourceRoot = "IdleAdventureUI/FormationRoster/";
    private const float DesignWidth = 697f;
    private const float DesignHeight = 235f;
    private const float ViewWidth = 589f;
    private const float CardStep = 114f;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<Button> tabs = new List<Button>();
    private RectTransform artwork;
    private ScrollRect scroll;
    private Button previous;
    private Button next;
    private TMP_Text emptyLabel;
    private TMP_Text messageLabel;
    private IdleAdventurePreviewIconCatalog portraits;
    private IdleAdventurePlayerNpcResponse[] characters = new IdleAdventurePlayerNpcResponse[0];
    private int[] deployed = new int[0];
    private Action<int> selected;
    private int filter;
    private bool loaded;
    private bool interactable = true;

    private sealed class Card
    {
        public Button Button;
        public Image Portrait;
        public TMP_Text Name;
        public TMP_Text Level;
        public Image Stamp;
        public int DefinitionIndex;
    }

    public void Bind(Action<int> onSelected)
    {
        selected = onSelected;
    }

    public void EnsureLayout()
    {
        if (artwork == null) Build();
        foreach (RectMask2D mask in GetComponentsInChildren<RectMask2D>(true)) mask.enabled = true;
        Resize();
        PositionArrow(previous, true);
        PositionArrow(next, false);
        RefreshArrows();
    }

    public void SetCharacters(IdleAdventurePlayerNpcResponse[] source, int[] formation, bool isLoaded)
    {
        EnsureLayout();
        foreach (IdleAdventurePreviewDragItem drag in GetComponentsInChildren<IdleAdventurePreviewDragItem>(true)) drag.CancelDrag();
        characters = source ?? new IdleAdventurePlayerNpcResponse[0];
        deployed = formation != null ? (int[])formation.Clone() : new int[0];
        loaded = isLoaded;
        messageLabel.text = string.Empty;
        RefreshCards(false);
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        foreach (Card card in cards) card.Button.interactable = value;
        if (!value) foreach (IdleAdventurePreviewDragItem drag in GetComponentsInChildren<IdleAdventurePreviewDragItem>(true)) drag.CancelDrag();
    }

    public void ShowMessage(string message)
    {
        if (messageLabel != null) messageLabel.text = string.IsNullOrWhiteSpace(message) ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(message);
    }

    private void Build()
    {
        artwork = Rect("Roster Artwork", transform, 0f, 0f, DesignWidth, DesignHeight);
        artwork.anchorMin = artwork.anchorMax = artwork.pivot = new Vector2(0.5f, 0.5f);
        artwork.anchoredPosition = Vector2.zero;
        portraits = Resources.Load<IdleAdventurePreviewIconCatalog>(ResourceRoot + "Portraits");
        string[] captions = { "全部", "未上阵", "已上阵" };
        for (int index = 0; index < captions.Length; index++)
        {
            int tabIndex = index;
            Image image = Picture("Filter " + index, artwork, "TabNormal", 60f + index * 123f, 0f, 126f, 54f);
            Button tab = MakeButton(image);
            Text("Label", tab.transform, captions[index], 23f, 6f, 6f, 114f, 32f);
            tab.onClick.AddListener(() => { filter = tabIndex; RefreshCards(true); });
            tabs.Add(tab);
        }

        Image paper = Picture("Scroll Paper", artwork, "ScrollPaper", 0f, 35f, DesignWidth, 200f);
        paper.type = Image.Type.Simple;
        paper.preserveAspect = true;
        Picture("Left Tape", artwork, "LeftTape", 8f, 60f, 49f, 43f);
        messageLabel = Text("Message", artwork, string.Empty, 17f, 448f, 8f, 230f, 32f);

        Image viewport = Picture("Viewport", artwork, null, 52f, 63f, ViewWidth, 158f);
        viewport.color = Color.clear;
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = Rect("Content", viewport.transform, 0f, 0f, ViewWidth, 158f);
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.08f;
        scroll.scrollSensitivity = 28f;
        scroll.onValueChanged.AddListener(_ => RefreshArrows());

        emptyLabel = Text("Empty State", viewport.transform, "角色数据读取中", 21f, 0f, 50f, ViewWidth, 42f);
        previous = MakeButton(Picture("Previous", artwork, "Arrow", 20f, 126f, 23f, 30f));
        previous.onClick.AddListener(() => ScrollPage(-1));
        next = MakeButton(Picture("Next", artwork, "Arrow", 643f, 126f, 23f, 30f));
        next.onClick.AddListener(() => ScrollPage(1));
        Picture("Right Tape", artwork, "RightTape", 640f, 60f, 50f, 41f);
        RefreshCards(true);
    }

    private void OnRectTransformDimensionsChange()
    {
        Resize();
    }

    private void Resize()
    {
        if (artwork == null) return;
        Rect bounds = ((RectTransform)transform).rect;
        float scale = Mathf.Min(bounds.width / DesignWidth, bounds.height / DesignHeight);
        artwork.localScale = Vector3.one * Mathf.Max(0f, scale);
    }

    private void RefreshCards(bool resetPosition)
    {
        foreach (IdleAdventurePreviewDragItem drag in GetComponentsInChildren<IdleAdventurePreviewDragItem>(true)) drag.CancelDrag();
        float offset = resetPosition ? 0f : -scroll.content.anchoredPosition.x;
        int visibleCount = 0;
        for (int index = 0; index < characters.Length; index++)
        {
            IdleAdventurePlayerNpcResponse npc = characters[index];
            if (!loaded || npc == null) continue;
            bool assigned = Array.IndexOf(deployed, index) >= 0;
            if ((filter == 1 && assigned) || (filter == 2 && !assigned)) continue;
            if (visibleCount == cards.Count) cards.Add(CreateCard());
            Card card = cards[visibleCount];
            card.DefinitionIndex = index;
            card.Button.gameObject.SetActive(true);
            card.Button.interactable = interactable;
            ((RectTransform)card.Button.transform).anchoredPosition = new Vector2(visibleCount * CardStep, -3f);
            card.Button.GetComponent<Image>().sprite = Sprite(assigned ? "CardAssigned" : "Card");
            card.Portrait.sprite = ResolvePortrait(npc);
            card.Portrait.enabled = card.Portrait.sprite != null;
            card.Name.text = string.IsNullOrWhiteSpace(npc.ResolvedCombatantName) ? "伙伴" : npc.ResolvedCombatantName;
            card.Level.text = npc.level > 0 ? "Lv." + npc.level : "Lv.—";
            card.Stamp.gameObject.SetActive(assigned);
            visibleCount++;
        }
        for (int index = visibleCount; index < cards.Count; index++) cards[index].Button.gameObject.SetActive(false);
        float width = Mathf.Max(ViewWidth, visibleCount * CardStep - 12f);
        scroll.StopMovement();
        scroll.content.sizeDelta = new Vector2(width, 158f);
        scroll.content.anchoredPosition = new Vector2(-Mathf.Clamp(offset, 0f, width - ViewWidth), 0f);
        emptyLabel.gameObject.SetActive(visibleCount == 0);
        emptyLabel.text = !loaded ? "角色数据读取中" : filter == 2 ? "暂无上阵角色" : filter == 1 ? "暂无未上阵角色" : "暂无可用角色";
        for (int index = 0; index < tabs.Count; index++)
        {
            tabs[index].GetComponent<Image>().sprite = Sprite(index == filter ? "TabSelected" : index == 2 ? "TabRight" : "TabNormal");
        }
        RefreshArrows();
    }

    private Card CreateCard()
    {
        Image background = Picture("Character Card " + cards.Count, scroll.content, "Card", 0f, 3f, 102f, 151f);
        Card card = new Card { Button = MakeButton(background) };
        Image portraitClip = Picture("Portrait Clip", background.transform, null, 5f, 5f, 92f, 98f);
        portraitClip.color = Color.clear;
        portraitClip.gameObject.AddComponent<RectMask2D>();
        card.Portrait = Picture("Portrait", portraitClip.transform, null, 0f, 0f, 92f, 98f);
        card.Portrait.preserveAspect = true;
        Picture("Portrait Frame", background.transform, "PortraitFrame", 0f, 0f, 102f, 108f);
        Picture("Name Paper", background.transform, "NamePaper", 0f, 106f, 102f, 45f);
        Picture("Level Paper", background.transform, "LevelPaper", 6f, 92f, 64f, 27f);
        card.Level = Text("Level", background.transform, string.Empty, 16f, 9f, 94f, 58f, 22f);
        card.Level.color = new Color(1f, 0.95f, 0.83f);
        card.Name = Text("Name", background.transform, string.Empty, 20f, 5f, 121f, 92f, 26f);
        card.Stamp = Picture("Assigned Stamp", background.transform, "AssignedStamp", 4f, 4f, 44f, 44f);
        TMP_Text assignedLabel = Text("Label", card.Stamp.transform, "上阵", 13f, 2f, 21f, 40f, 24f);
        assignedLabel.enableAutoSizing = false;
        assignedLabel.color = new Color(0.22f, 0.15f, 0.10f);
        card.Button.onClick.AddListener(() => { if (interactable) selected?.Invoke(card.DefinitionIndex); });
        background.gameObject.AddComponent<IdleAdventurePreviewDragItem>().Bind(
            () => card.DefinitionIndex, () => interactable && loaded && card.Button.IsInteractable(), card.Portrait, scroll);
        return card;
    }

    private Sprite ResolvePortrait(IdleAdventurePlayerNpcResponse npc)
    {
        if (portraits == null) return null;
        foreach (string key in new[] { npc.icon, npc.ResolvedModelKey, npc.ResolvedCombatantKey, npc.ResolvedCombatantName, npc.ResolvedDefinitionId.ToString() })
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            string normalized = key.Trim().Replace('\\', '/');
            normalized = normalized.Substring(normalized.LastIndexOf('/') + 1);
            if (normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) normalized = normalized.Substring(0, normalized.Length - 4);
            Sprite portrait = portraits.Find(normalized);
            if (portrait != null) return portrait;
        }
        return null;
    }

    private void ScrollPage(int direction)
    {
        float overflow = scroll.content.rect.width - ViewWidth;
        if (overflow <= 0f) return;
        scroll.StopMovement();
        float offset = -scroll.content.anchoredPosition.x + direction * CardStep * 4f;
        scroll.content.anchoredPosition = new Vector2(-Mathf.Clamp(offset, 0f, overflow), 0f);
        RefreshArrows();
    }

    private void RefreshArrows()
    {
        if (previous == null || next == null) return;
        float offset = -scroll.content.anchoredPosition.x;
        float overflow = scroll.content.rect.width - ViewWidth;
        bool canMoveLeft = overflow > 0.5f && offset > 0.5f;
        bool canMoveRight = overflow > 0.5f && offset < overflow - 0.5f;
        previous.interactable = canMoveLeft;
        next.interactable = canMoveRight;
        previous.gameObject.SetActive(canMoveLeft);
        next.gameObject.SetActive(canMoveRight);
    }

    private void PositionArrow(Button button, bool left)
    {
        if (button == null || scroll == null) return;
        RectTransform rect = (RectTransform)button.transform;
        RectTransform viewport = scroll.viewport;
        float gap = 2f + rect.rect.width * 0.5f;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(
            viewport.anchoredPosition.x + (left ? -gap : viewport.rect.width + gap),
            viewport.anchoredPosition.y - viewport.rect.height * 0.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
        button.GetComponent<Image>().preserveAspect = true;
    }

    private static RectTransform Rect(string name, Transform parent, float horizontal, float vertical, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(horizontal, -vertical);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Image Picture(string name, Transform parent, string resource, float horizontal, float vertical, float width, float height)
    {
        Image image = Rect(name, parent, horizontal, vertical, width, height).gameObject.AddComponent<Image>();
        image.sprite = Sprite(resource);
        image.raycastTarget = false;
        return image;
    }

    private static Sprite Sprite(string resource)
    {
        return string.IsNullOrEmpty(resource) ? null : Resources.Load<Sprite>(ResourceRoot + resource);
    }

    private static TMP_Text Text(string name, Transform parent, string caption, float size, float horizontal, float vertical, float width, float height)
    {
        TextMeshProUGUI label = Rect(name, parent, horizontal, vertical, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = RuntimeFontAssetProvider.Resolve();
        label.text = caption;
        label.color = new Color(0.27f, 0.19f, 0.13f);
        label.fontStyle = FontStyles.Bold;
        label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = size * 0.75f;
        label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(Image image)
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.82f, 0.86f, 0.92f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.38f);
        button.colors = colors;
        return button;
    }
}
