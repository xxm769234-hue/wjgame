using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureFormationPanel : MonoBehaviour
{
    private const int FormationRows = 3;
    private const int MaxPartySize = 6;

    [SerializeField] private RectTransform formationWindow;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button autoFillButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Image[] slotBackgrounds = new Image[0];
    [SerializeField] private TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[0];
    [SerializeField] private IdleAdventureFormationSlotDropTarget[] slotDropTargets = new IdleAdventureFormationSlotDropTarget[0];
    [SerializeField] private Image[] npcBackgrounds = new Image[0];
    [SerializeField] private TextMeshProUGUI[] npcLabels = new TextMeshProUGUI[0];
    [SerializeField] private IdleAdventureFormationDragItem[] npcDragItems = new IdleAdventureFormationDragItem[0];
    [SerializeField] private Canvas ownerCanvas;

    public bool HasBindings =>
        formationWindow != null &&
        statusText != null &&
        openButton != null &&
        closeButton != null &&
        autoFillButton != null &&
        clearButton != null &&
        slotBackgrounds != null &&
        slotLabels != null &&
        npcBackgrounds != null &&
        npcLabels != null &&
        slotDropTargets != null &&
        npcDragItems != null &&
        slotBackgrounds.Length >= 6 &&
        slotLabels.Length >= 6 &&
        slotDropTargets.Length >= 6 &&
        npcBackgrounds.Length >= 6 &&
        npcLabels.Length >= 6 &&
        npcDragItems.Length >= 6 &&
        HasArrayBindings(slotBackgrounds, 6) &&
        HasArrayBindings(slotLabels, 6) &&
        HasArrayBindings(slotDropTargets, 6) &&
        HasArrayBindings(npcBackgrounds, 6) &&
        HasArrayBindings(npcLabels, 6) &&
        HasArrayBindings(npcDragItems, 6);

    public void Initialize(RectTransform window, TextMeshProUGUI statusLabel, Button open, Button close, Button autoFill, Button clear, Image[] slots, TextMeshProUGUI[] slotTexts, IdleAdventureFormationSlotDropTarget[] dropTargets, Image[] npcs, TextMeshProUGUI[] npcTexts, IdleAdventureFormationDragItem[] dragItems, Canvas canvas)
    {
        formationWindow = window;
        statusText = statusLabel;
        openButton = open;
        closeButton = close;
        autoFillButton = autoFill;
        clearButton = clear;
        slotBackgrounds = slots ?? new Image[0];
        slotLabels = slotTexts ?? new TextMeshProUGUI[0];
        slotDropTargets = dropTargets ?? new IdleAdventureFormationSlotDropTarget[0];
        npcBackgrounds = npcs ?? new Image[0];
        npcLabels = npcTexts ?? new TextMeshProUGUI[0];
        npcDragItems = dragItems ?? new IdleAdventureFormationDragItem[0];
        ownerCanvas = canvas;
    }

    public void Bind(Action onOpen, Action onClose, Action onAutoFill, Action onClear, Action<int, int> onDropNpc, Action<int> onClearSlot)
    {
        ConfigureRaycastTargets();

        BindButton(openButton, onOpen);
        BindButton(closeButton, onClose);
        BindButton(autoFillButton, onAutoFill);
        BindButton(clearButton, onClear);

        for (int i = 0; i < slotDropTargets.Length; i++)
        {
            if (slotDropTargets[i] != null) slotDropTargets[i].Initialize(i, onDropNpc, onClearSlot);
        }

        for (int i = 0; i < npcDragItems.Length; i++)
        {
            if (npcDragItems[i] != null) npcDragItems[i].Initialize(i, ownerCanvas);
        }
    }

    public void ShowFormationWindow()
    {
        if (formationWindow != null) formationWindow.gameObject.SetActive(true);
    }

    public void HideFormationWindow()
    {
        if (formationWindow != null) formationWindow.gameObject.SetActive(false);
    }

    public void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    public void SetSlot(int slotIndex, string label, bool occupied, bool frontColumn)
    {
        if (slotIndex < 0 || slotIndex >= slotBackgrounds.Length || slotIndex >= slotLabels.Length) return;
        slotLabels[slotIndex].text = label;
        slotBackgrounds[slotIndex].color = occupied
            ? (frontColumn ? new Color(0.16f, 0.34f, 0.42f, 0.92f) : new Color(0.13f, 0.25f, 0.36f, 0.92f))
            : new Color(0.08f, 0.09f, 0.10f, 0.82f);
    }

    public void ApplySlotLayout(bool frontColumnOnRight)
    {
        int count = Mathf.Min(MaxPartySize, slotBackgrounds != null ? slotBackgrounds.Length : 0);
        for (int i = 0; i < count; i++)
        {
            if (slotBackgrounds[i] == null) continue;

            bool frontColumn = i < FormationRows;
            int row = Mathf.Clamp(i % FormationRows, 0, FormationRows - 1);
            int column = frontColumn == frontColumnOnRight ? 1 : 0;
            float left = column == 0 ? 0.05f : 0.53f;
            float right = column == 0 ? 0.47f : 0.95f;
            float top = 0.92f - row * 0.30f;
            float bottom = top - 0.22f;

            RectTransform rectTransform = slotBackgrounds[i].rectTransform;
            rectTransform.anchorMin = new Vector2(left, bottom);
            rectTransform.anchorMax = new Vector2(right, top);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }

    public void SetNpc(int npcIndex, string label, bool available, bool assigned)
    {
        if (npcIndex < 0 || npcIndex >= npcBackgrounds.Length || npcIndex >= npcLabels.Length) return;
        npcBackgrounds[npcIndex].gameObject.SetActive(available);
        if (!available) return;

        npcLabels[npcIndex].text = label;
        npcBackgrounds[npcIndex].color = assigned
            ? new Color(0.12f, 0.13f, 0.14f, 0.58f)
            : new Color(0.18f, 0.28f, 0.34f, 0.94f);
    }

    private static void BindButton(Button button, Action action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(() => action());
    }

    private static bool HasArrayBindings<T>(T[] values, int requiredCount) where T : UnityEngine.Object
    {
        if (values == null || values.Length < requiredCount) return false;
        for (int i = 0; i < requiredCount; i++)
        {
            if (values[i] == null) return false;
        }

        return true;
    }

    private void ConfigureRaycastTargets()
    {
        SetParentImageRaycast(slotBackgrounds, false);
        SetParentImageRaycast(npcBackgrounds, false);

        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] != null) slotBackgrounds[i].raycastTarget = true;
            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null) slotLabels[i].raycastTarget = false;
        }

        for (int i = 0; i < npcBackgrounds.Length; i++)
        {
            if (npcBackgrounds[i] != null) npcBackgrounds[i].raycastTarget = true;
            if (npcLabels != null && i < npcLabels.Length && npcLabels[i] != null) npcLabels[i].raycastTarget = false;
        }
    }

    private static void SetParentImageRaycast(Image[] images, bool raycastTarget)
    {
        if (images == null || images.Length == 0 || images[0] == null || images[0].transform.parent == null) return;

        Image parentImage = images[0].transform.parent.GetComponent<Image>();
        if (parentImage != null) parentImage.raycastTarget = raycastTarget;
    }
}
