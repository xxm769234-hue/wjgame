using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Read-only presentation for an occupied land slot.</summary>
public sealed class LandInfoPanel : MonoBehaviour
{
    public static LandInfoPanel Instance { get; private set; }
    [SerializeField] private GameObject panel;
    [SerializeField] private Vector2 panelSize = new Vector2(700f, 400f);
    [SerializeField] private Vector2 panelOffset = new Vector2(380f, 40f);
    [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text buildingText;
    [SerializeField] private TMP_Text npcText;
    [SerializeField] private Button buildButton;
    [SerializeField] private Button assignNpcButton;
    [SerializeField] private Button removeNpcButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button demolishButton;
    [SerializeField] private Button cancelActionButton;
    [SerializeField] private Button completeActionButton;
    [SerializeField] private RectTransform buildingListPanel;
    [SerializeField] private RectTransform buildingListContent;
    [SerializeField] private RectTransform npcListPanel;
    [SerializeField] private RectTransform npcListContent;
    private LandSlotController current;
    private RectTransform panelRect;
    private RectTransform canvasRect;
    private Canvas rootCanvas;
    private Camera worldCamera;
    private BuildingManager buildingManager;
    private NpcManager npcManager;
    private int shownFrame = -1;

    private void Awake()
    {
        Instance = this;
        if (panel == null) panel = gameObject;
        panelRect = panel.GetComponent<RectTransform>();
        rootCanvas = panel.GetComponentInParent<Canvas>();
        if (rootCanvas != null) canvasRect = rootCanvas.transform as RectTransform;
        worldCamera = Camera.main;
        buildingManager = BuildingManager.EnsureInstance();
        npcManager = NpcManager.EnsureInstance();
        ConfigureFloatingPanel();
        EnsureBuildControls();
        EnsureBuildingActionControls();
        EnsureNpcControls();
        panel.SetActive(false);
    }

    public void Show(LandSlotController slot)
    {
        if (slot == null || slot.CurrentLand == null) return;
        current = slot;
        LandConfig data = slot.CurrentLand;
        SetText(nameText, data.landName);
        SetText(descText, data.description);
        SetText(levelText, $"Level: {data.unlockLevel}");
        RefreshBuildingStatus();
        RefreshNpcStatus();
        panel.SetActive(true);
        shownFrame = Time.frameCount;
        PositionNearSlot(slot);
    }

    public void Hide()
    {
        panel.SetActive(false);
        HideBuildingList();
        HideNpcList();
        current = null;
    }

    public void RefreshIfShowing(LandSlotController slot)
    {
        if (slot == null || current != slot || panel == null || !panel.activeSelf) return;
        RefreshBuildingStatus();
        RefreshNpcStatus();
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf || Time.frameCount == shownFrame) return;

        if (current != null && current.IsBuildingInProgress)
        {
            RefreshBuildingStatus();
        }

        if (TryGetPointerDownPosition(out Vector2 screenPosition) && !IsPointerInsidePanel(screenPosition))
        {
            Hide();
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    public void ToggleBuildingList()
    {
        if (current == null || current.HasBuilding) return;
        HideNpcList();
        if (buildingListPanel != null && buildingListPanel.gameObject.activeSelf)
        {
            HideBuildingList();
            return;
        }

        ShowBuildingList();
    }

    private void ShowBuildingList()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        EnsureBuildControls();
        if (buildingListPanel == null || buildingListContent == null || buildingManager == null) return;

        ClearBuildingList();
        IReadOnlyList<BuildingConfig> buildings = buildingManager.AvailableBuildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingConfig config = buildings[i];
            if (config == null) continue;

            string label = FormatBuildingLabel(config);
            BuildingConfig captured = config;
            Button itemButton = CreateTextButton($"Building Item {i + 1}", buildingListContent, label, new Vector2(0f, 0f), new Vector2(600f, 58f), 24f, () => SelectBuilding(captured));
            if (!buildingManager.CanBuildOnSlot(current, config, out _)) itemButton.interactable = false;
        }

        buildingListPanel.gameObject.SetActive(true);
    }

    private void SelectBuilding(BuildingConfig config)
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryBuildOnSlot(current, config, out string reason))
        {
            SetText(buildingText, $"Building failed: {reason}");
            return;
        }

        HideBuildingList();
        RefreshBuildingStatus();
        RefreshNpcStatus();
    }

    private void OnUpgradeClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryStartUpgrade(current, out string reason))
        {
            SetText(buildingText, $"Upgrade failed: {reason}");
            return;
        }

        RefreshBuildingStatus();
        RefreshNpcStatus();
    }

    private void OnMoveClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.BeginMoveBuilding(current, out string reason))
        {
            SetText(buildingText, $"Move failed: {reason}");
            return;
        }

        Hide();
    }

    private void OnDemolishClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryDemolishBuilding(current, out string reason))
        {
            SetText(buildingText, $"Demolish failed: {reason}");
            return;
        }

        RefreshBuildingStatus();
        RefreshNpcStatus();
    }

    private void OnCancelActionClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryCancelBuildingAction(current, out string reason))
        {
            SetText(buildingText, $"Cancel failed: {reason}");
            return;
        }

        RefreshBuildingStatus();
        RefreshNpcStatus();
    }

    private void OnCompleteActionClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryCompleteBuildingAction(current, out string reason))
        {
            SetText(buildingText, $"Complete failed: {reason}");
            return;
        }

        RefreshBuildingStatus();
        RefreshNpcStatus();
    }

    public void ToggleNpcList()
    {
        if (current == null || !current.HasBuilding || current.HasNpc) return;
        HideBuildingList();
        if (npcListPanel != null && npcListPanel.gameObject.activeSelf)
        {
            HideNpcList();
            return;
        }

        ShowNpcList();
    }

    private void ShowNpcList()
    {
        if (npcManager == null) npcManager = NpcManager.EnsureInstance();
        EnsureNpcControls();
        if (npcListPanel == null || npcListContent == null || npcManager == null) return;

        ClearNpcList();
        IReadOnlyList<NpcConfig> npcs = npcManager.AvailableNpcs;
        for (int i = 0; i < npcs.Count; i++)
        {
            NpcConfig config = npcs[i];
            if (config == null) continue;

            string label = FormatNpcLabel(config);
            NpcConfig captured = config;
            Button itemButton = CreateTextButton($"NPC Item {i + 1}", npcListContent, label, new Vector2(0f, 0f), new Vector2(600f, 52f), 22f, () => SelectNpc(captured));
            if (!npcManager.CanAssignToSlot(current, config, out _)) itemButton.interactable = false;
        }

        npcListPanel.gameObject.SetActive(true);
    }

    private void SelectNpc(NpcConfig config)
    {
        if (npcManager == null) npcManager = NpcManager.EnsureInstance();
        if (npcManager == null || current == null) return;

        if (!npcManager.TryAssignToSlot(current, config, out string reason))
        {
            SetText(npcText, $"NPC failed: {reason}");
            return;
        }

        HideNpcList();
        RefreshNpcStatus();
    }

    private void OnRemoveNpcClicked()
    {
        if (npcManager == null) npcManager = NpcManager.EnsureInstance();
        if (npcManager == null || current == null) return;

        if (!npcManager.TryRemoveFromSlot(current, out string reason))
        {
            SetText(npcText, $"Remove NPC failed: {reason}");
            return;
        }

        HideNpcList();
        RefreshNpcStatus();
    }

    private void RefreshBuildingStatus()
    {
        if (current == null) return;

        if (current.HasBuilding)
        {
            SetText(buildingText, FormatBuildingStatus(current));
            if (buildButton != null) buildButton.gameObject.SetActive(false);
            SetBuildingActionButtonsVisible(true);
            HideBuildingList();
        }
        else
        {
            SetText(buildingText, "Building: None");
            if (buildButton != null) buildButton.gameObject.SetActive(true);
            SetBuildingActionButtonsVisible(false);
        }
    }

    private void RefreshNpcStatus()
    {
        if (current == null) return;

        if (!current.HasBuilding)
        {
            SetText(npcText, "NPC: Build first");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(false);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(false);
            HideNpcList();
        }
        else if (!current.IsBuildingCompleted)
        {
            SetText(npcText, "NPC: Wait for completed building");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(false);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(false);
            HideNpcList();
        }
        else if (current.HasNpc)
        {
            SetText(npcText, $"NPC: {current.CurrentNpc.npcName} ({current.CurrentNpc.role})");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(false);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(true);
            HideNpcList();
        }
        else
        {
            SetText(npcText, "NPC: None");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(true);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(false);
        }
    }

    private void HideBuildingList()
    {
        if (buildingListPanel != null) buildingListPanel.gameObject.SetActive(false);
    }

    private void HideNpcList()
    {
        if (npcListPanel != null) npcListPanel.gameObject.SetActive(false);
    }

    private void ClearBuildingList()
    {
        if (buildingListContent == null) return;

        for (int i = buildingListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(buildingListContent.GetChild(i).gameObject);
        }
    }

    private void ClearNpcList()
    {
        if (npcListContent == null) return;

        for (int i = npcListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(npcListContent.GetChild(i).gameObject);
        }
    }

    private static string FormatBuildingLabel(BuildingConfig config)
    {
        string buildTime = config.buildSeconds > 0 ? $"{config.buildSeconds}s" : "Instant";
        string cost = config.cost > 0 ? $"Cost {config.cost}" : "Free";
        string suffix = config.canBuild ? string.Empty : $" ({config.disabledReason})";
        return $"{config.buildingName} - {cost} - {buildTime}{suffix}";
    }

    private static string FormatBuildingStatus(LandSlotController slot)
    {
        BuildingConfig building = slot.CurrentBuilding;
        if (building == null) return "Building: None";

        string text = $"Building: {building.buildingName}  ID:{slot.PlayerBuildingId}\nLevel: {slot.BuildingLevel}/{building.maxLevel}  Status: {slot.BuildingStatus}";
        if (slot.IsBuildingInProgress)
        {
            text += $"  {Mathf.CeilToInt(slot.BuildingRemainingSeconds)}s  {Mathf.RoundToInt(slot.BuildingProgress * 100f)}%";
        }
        return text;
    }

    private static string FormatNpcLabel(NpcConfig config)
    {
        string role = IsBlank(config.role) ? "Resident" : config.role;
        string requirement = IsBlank(config.requiredBuildingType) ? "Any building" : $"Needs {config.requiredBuildingType}";
        string suffix = config.canAssign ? string.Empty : $" ({config.disabledReason})";
        return $"{config.npcName} - {role} - {requirement}{suffix}";
    }

    private static bool IsBlank(string value)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
    }

    private static bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsPointerInsidePanel(Vector2 screenPosition)
    {
        if (panelRect == null) return false;

        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPosition, uiCamera)) return true;
        if (buildingListPanel != null
            && buildingListPanel.gameObject.activeSelf
            && RectTransformUtility.RectangleContainsScreenPoint(buildingListPanel, screenPosition, uiCamera)) return true;
        return npcListPanel != null
            && npcListPanel.gameObject.activeSelf
            && RectTransformUtility.RectangleContainsScreenPoint(npcListPanel, screenPosition, uiCamera);
    }

    private void ConfigureFloatingPanel()
    {
        if (panelRect == null) return;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        if (npcText == null) npcText = CreatePanelText("NPC Status", panelRect);

        ConfigureTextRect(nameText, new Vector2(0f, 142f), new Vector2(620f, 58f), 38f);
        ConfigureTextRect(descText, new Vector2(0f, 70f), new Vector2(620f, 70f), 24f);
        ConfigureTextRect(levelText, new Vector2(-185f, 5f), new Vector2(420f, 42f), 24f);
        ConfigureTextRect(buildingText, new Vector2(-125f, -55f), new Vector2(390f, 76f), 22f);
        ConfigureTextRect(npcText, new Vector2(-140f, -115f), new Vector2(360f, 46f), 24f);
    }

    private static void ConfigureTextRect(TMP_Text text, Vector2 position, Vector2 size, float fontSize)
    {
        if (text == null) return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.fontSize = fontSize;
        text.enableWordWrapping = true;
    }

    private void EnsureBuildControls()
    {
        if (panelRect == null) return;

        if (buildButton == null)
        {
            buildButton = CreateTextButton("Build Building Button", panelRect, "Build", new Vector2(220f, -55f), new Vector2(170f, 50f), 24f, ToggleBuildingList);
        }
        ConfigureButtonRect(buildButton, new Vector2(220f, -55f), new Vector2(170f, 50f));

        if (buildingListPanel == null)
        {
            GameObject listObject = new GameObject("Building List Panel", typeof(RectTransform), typeof(Image));
            listObject.transform.SetParent(panelRect, false);
            buildingListPanel = listObject.GetComponent<RectTransform>();
            buildingListPanel.anchorMin = new Vector2(0.5f, 0.5f);
            buildingListPanel.anchorMax = new Vector2(0.5f, 0.5f);
            buildingListPanel.pivot = new Vector2(0.5f, 1f);
            buildingListPanel.anchoredPosition = new Vector2(0f, -190f);
            buildingListPanel.sizeDelta = new Vector2(640f, 180f);

            Image image = listObject.GetComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            image.raycastTarget = true;
        }

        if (buildingListContent == null)
        {
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(buildingListPanel, false);
            buildingListContent = contentObject.GetComponent<RectTransform>();
            buildingListContent.anchorMin = new Vector2(0f, 0f);
            buildingListContent.anchorMax = new Vector2(1f, 1f);
            buildingListContent.offsetMin = new Vector2(16f, 12f);
            buildingListContent.offsetMax = new Vector2(-16f, -12f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
        }

        buildingListPanel.gameObject.SetActive(false);
    }

    private void EnsureBuildingActionControls()
    {
        if (panelRect == null) return;

        if (upgradeButton == null) upgradeButton = CreateTextButton("Upgrade Building Button", panelRect, "Upgrade", new Vector2(240f, 15f), new Vector2(135f, 42f), 18f, OnUpgradeClicked);
        if (moveButton == null) moveButton = CreateTextButton("Move Building Button", panelRect, "Move", new Vector2(240f, -32f), new Vector2(135f, 42f), 18f, OnMoveClicked);
        if (demolishButton == null) demolishButton = CreateTextButton("Demolish Building Button", panelRect, "Demolish", new Vector2(240f, -79f), new Vector2(135f, 42f), 18f, OnDemolishClicked);
        if (cancelActionButton == null) cancelActionButton = CreateTextButton("Cancel Building Action Button", panelRect, "Cancel", new Vector2(240f, -126f), new Vector2(135f, 42f), 18f, OnCancelActionClicked);
        if (completeActionButton == null) completeActionButton = CreateTextButton("Complete Building Action Button", panelRect, "Complete", new Vector2(240f, -173f), new Vector2(135f, 42f), 18f, OnCompleteActionClicked);

        ConfigureButtonRect(upgradeButton, new Vector2(240f, 15f), new Vector2(135f, 42f));
        ConfigureButtonRect(moveButton, new Vector2(240f, -32f), new Vector2(135f, 42f));
        ConfigureButtonRect(demolishButton, new Vector2(240f, -79f), new Vector2(135f, 42f));
        ConfigureButtonRect(cancelActionButton, new Vector2(240f, -126f), new Vector2(135f, 42f));
        ConfigureButtonRect(completeActionButton, new Vector2(240f, -173f), new Vector2(135f, 42f));
        SetBuildingActionButtonsVisible(false);
    }

    private void SetBuildingActionButtonsVisible(bool hasBuilding)
    {
        bool completed = hasBuilding && current != null && current.IsBuildingCompleted;
        bool inProgress = hasBuilding && current != null && current.IsBuildingInProgress;
        bool canUpgrade = completed && current.BuildingLevel < current.CurrentBuilding.maxLevel;

        SetButtonVisible(upgradeButton, canUpgrade, true);
        SetButtonVisible(moveButton, completed, true);
        SetButtonVisible(demolishButton, completed, true);
        SetButtonVisible(cancelActionButton, inProgress, true);
        SetButtonVisible(completeActionButton, inProgress, current != null && current.BuildingRemainingSeconds <= 0f);
    }

    private static void SetButtonVisible(Button button, bool visible, bool interactable)
    {
        if (button == null) return;
        button.gameObject.SetActive(visible);
        button.interactable = interactable;
    }

    private void EnsureNpcControls()
    {
        if (panelRect == null) return;

        if (assignNpcButton == null)
        {
            assignNpcButton = CreateTextButton("Assign NPC Button", panelRect, "Assign NPC", new Vector2(220f, -115f), new Vector2(170f, 44f), 20f, ToggleNpcList);
        }
        ConfigureButtonRect(assignNpcButton, new Vector2(220f, -115f), new Vector2(170f, 44f));

        if (removeNpcButton == null)
        {
            removeNpcButton = CreateTextButton("Remove NPC Button", panelRect, "Remove NPC", new Vector2(220f, -164f), new Vector2(170f, 44f), 19f, OnRemoveNpcClicked);
        }
        ConfigureButtonRect(removeNpcButton, new Vector2(220f, -164f), new Vector2(170f, 44f));
        removeNpcButton.gameObject.SetActive(false);

        if (npcListPanel == null)
        {
            GameObject listObject = new GameObject("NPC List Panel", typeof(RectTransform), typeof(Image));
            listObject.transform.SetParent(panelRect, false);
            npcListPanel = listObject.GetComponent<RectTransform>();
            npcListPanel.anchorMin = new Vector2(0.5f, 0.5f);
            npcListPanel.anchorMax = new Vector2(0.5f, 0.5f);
            npcListPanel.pivot = new Vector2(0.5f, 1f);
            npcListPanel.anchoredPosition = new Vector2(0f, -170f);
            npcListPanel.sizeDelta = new Vector2(640f, 180f);

            Image image = listObject.GetComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            image.raycastTarget = true;
        }

        if (npcListContent == null)
        {
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(npcListPanel, false);
            npcListContent = contentObject.GetComponent<RectTransform>();
            npcListContent.anchorMin = new Vector2(0f, 0f);
            npcListContent.anchorMax = new Vector2(1f, 1f);
            npcListContent.offsetMin = new Vector2(16f, 12f);
            npcListContent.offsetMax = new Vector2(-16f, -12f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
        }

        npcListPanel.gameObject.SetActive(false);
    }

    private TMP_Text CreatePanelText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        return text;
    }

    private static void ConfigureButtonRect(Button button, Vector2 position, Vector2 size)
    {
        if (button == null) return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private Button CreateTextButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, float fontSize, UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.42f, 0.78f, 0.95f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;

        return button;
    }

    private void PositionNearSlot(LandSlotController slot)
    {
        if (slot == null || panelRect == null || canvasRect == null) return;

        if (worldCamera == null) worldCamera = Camera.main;
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        Vector3 screenPosition = worldCamera != null ? worldCamera.WorldToScreenPoint(slot.CenterWorld) : Input.mousePosition;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPosition)) return;

        Vector2 target = localPosition + panelOffset;
        panelRect.anchoredPosition = ClampToCanvas(target);
    }

    private Vector2 ClampToCanvas(Vector2 anchoredPosition)
    {
        Rect canvas = canvasRect.rect;
        Vector2 halfPanel = panelSize * 0.5f;

        float minX = canvas.xMin + halfPanel.x + screenPadding.x;
        float maxX = canvas.xMax - halfPanel.x - screenPadding.x;
        float minY = canvas.yMin + halfPanel.y + screenPadding.y;
        float maxY = canvas.yMax - halfPanel.y - screenPadding.y;

        if (minX > maxX) anchoredPosition.x = 0f;
        else anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);

        if (minY > maxY) anchoredPosition.y = 0f;
        else anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);

        return anchoredPosition;
    }
}
