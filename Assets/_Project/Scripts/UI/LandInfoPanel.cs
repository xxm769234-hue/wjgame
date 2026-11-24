using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Read-only presentation for an occupied land slot.</summary>
public sealed class LandInfoPanel : MonoBehaviour
{
    public static LandInfoPanel Instance { get; private set; }
    private readonly Dictionary<int, Button> onboardingBuildingButtons = new Dictionary<int, Button>();

    public RectTransform ResolveSettlementOnboardingTarget(LandSlotController slot)
    {
        if (slot == null || !slot.IsBuildingCompleted || slot.HasNpc || panel == null) return null;
        if (current != slot || !panel.activeInHierarchy) Show(slot);
        if (npcListPanel == null || !npcListPanel.gameObject.activeInHierarchy) ShowNpcList();
        return npcListPanel != null && npcListPanel.gameObject.activeInHierarchy ? npcListPanel : null;
    }

    public RectTransform ResolveOnboardingTarget(LandSlotController slot, long buildingConfigId, bool completion)
    {
        if (slot == null || !slot.HasRemotePlayerLandId || slot.CurrentLand == null || panel == null) return null;
        if (current != slot || !panel.activeInHierarchy) Show(slot);
        if (completion)
        {
            return completeActionButton == null ? null : completeActionButton.GetComponent<RectTransform>();
        }
        if (buildingListPanel == null || !buildingListPanel.gameObject.activeInHierarchy) ShowBuildingList();
        return buildingConfigId > 0 && buildingConfigId <= int.MaxValue
            && onboardingBuildingButtons.TryGetValue((int)buildingConfigId, out Button button) && button != null
            ? button.GetComponent<RectTransform>() : null;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private Vector2 panelSize = new Vector2(700f, 400f);
    [SerializeField] private Vector2 panelOffset = new Vector2(380f, 40f);
    [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text buildingText;
    [SerializeField] private TMP_Text productionText;
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
    [SerializeField] private bool logButtonClicks = true;
    private Image panelImage;
    private Image landPreviewImage;
    private Image terrainBadge;
    private Button closeButton;
    private TMP_Text titleText;
    private Texture2D controlsTexture;
    private Texture2D popupsTexture;
    private readonly Dictionary<string, Sprite> panelSkinSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private static readonly Color PanelTextColor = new Color(0.30f, 0.16f, 0.11f, 1f);
    private static readonly Color PanelMutedTextColor = new Color(0.42f, 0.27f, 0.18f, 1f);
    private static readonly Color PanelPrimaryTextColor = new Color(0.24f, 0.11f, 0.06f, 1f);
    private static readonly Dictionary<string, Rect> ControlAtlasRects = new Dictionary<string, Rect>(StringComparer.Ordinal)
    {
        { "ButtonPrimary", new Rect(16, 1536 - 16 - 182, 370, 182) },
        { "ButtonSecondary", new Rect(402, 1536 - 16 - 182, 369, 182) },
        { "ButtonDanger", new Rect(787, 1536 - 16 - 182, 370, 182) },
        { "ButtonDisabled", new Rect(1173, 1536 - 16 - 182, 369, 182) },
        { "ButtonCompact", new Rect(1558, 1536 - 16 - 146, 268, 146) },
        { "ButtonSquare", new Rect(16, 1536 - 214 - 200, 201, 200) },
        { "Close", new Rect(233, 1536 - 214 - 158, 158, 158) },
        { "BadgeGreen", new Rect(407, 1536 - 214 - 134, 302, 134) },
        { "BadgeBlue", new Rect(725, 1536 - 214 - 134, 301, 134) },
        { "BadgeOrange", new Rect(1042, 1536 - 214 - 134, 302, 134) },
        { "BadgeGray", new Rect(1360, 1536 - 214 - 133, 301, 133) },
        { "StatusGreen", new Rect(1677, 1536 - 214 - 178, 328, 178) },
        { "StatusOrange", new Rect(16, 1536 - 430 - 178, 330, 178) },
        { "StatusBlue", new Rect(362, 1536 - 430 - 178, 328, 178) },
        { "StatusGray", new Rect(706, 1536 - 430 - 178, 328, 178) },
        { "ProgressEmpty", new Rect(1050, 1536 - 430 - 88, 375, 88) },
        { "ProgressFill", new Rect(1441, 1536 - 430 - 88, 375, 88) },
        { "Upgrade", new Rect(1832, 1536 - 430 - 200, 199, 200) },
        { "Move", new Rect(16, 1536 - 646 - 199, 197, 199) },
        { "AssignNpc", new Rect(229, 1536 - 646 - 200, 200, 200) },
        { "Demolish", new Rect(445, 1536 - 646 - 197, 194, 197) },
        { "Inspect", new Rect(655, 1536 - 646 - 189, 187, 189) },
        { "Terrain", new Rect(858, 1536 - 646 - 106, 232, 106) },
        { "Wheat", new Rect(1106, 1536 - 646 - 197, 166, 197) },
        { "SyncDot", new Rect(1288, 1536 - 646 - 103, 104, 103) },
    };
    private static readonly Dictionary<string, Rect> PopupAtlasRects = new Dictionary<string, Rect>(StringComparer.Ordinal)
    {
        { "BuildingListPanel", new Rect(16, 1536 - 16 - 808, 807, 808) },
        { "NpcListPanel", new Rect(839, 1536 - 16 - 793, 588, 793) },
        { "ListItem", new Rect(16, 1536 - 840 - 330, 1010, 330) },
        { "ListItemPaper", new Rect(66, 1536 - 890 - 218, 210, 218) },
        { "ConfirmPanel", new Rect(1042, 1536 - 840 - 421, 569, 421) },
    };
    private Button recipeButton;
    private ProductionRecipesResponse productionRecipes;
    private int productionRecipesBuildingId;
    private bool recipesRequestRunning;
    private bool recipeSwitchRequestRunning;
    private string recipeSwitchIdempotencyKey = string.Empty;
    private string productionActionMessage = string.Empty;
    private LandSlotController current;
    private RectTransform panelRect;
    private RectTransform canvasRect;
    private Canvas rootCanvas;
    private Camera worldCamera;
    private BuildingManager buildingManager;
    private NpcManager npcManager;
    private int shownFrame = -1;
    private readonly List<ScrollRect> informationScrolls = new List<ScrollRect>();
    private Vector2 lastCanvasSize;

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
        ConfigurePanelSkin();
        ConfigureFloatingPanel();
        EnsureBuildControls();
        EnsureBuildingActionControls();
        EnsureNpcControls();
        EnsureProductionControls();
        ApplyPanelSkinToControls();
        panel.SetActive(false);
    }

    public void Show(LandSlotController slot)
    {
        if (slot == null || slot.CurrentLand == null) return;
        current = slot;
        ResetProductionActionsIfBuildingChanged(slot);
        LandConfig data = slot.CurrentLand;
        SetText(nameText, data.landName);

        SetText(levelText, $"解锁等级 {data.unlockLevel}");
        RefreshPreviewImages(slot);
        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
        RefreshProductionControls();
        panel.SetActive(true);
        panelRect.SetAsLastSibling();
        HideBuildingList();
        HideNpcList();
        Canvas.ForceUpdateCanvases();
        foreach (ScrollRect scroll in informationScrolls) scroll.verticalNormalizedPosition = 1f;
        shownFrame = Time.frameCount;
        PositionNearSlot(slot);
        RefreshRemoteDetails(slot);
    }

    public void Hide()
    {
        panel.SetActive(false);
        HideBuildingList();
        HideNpcList();
        current = null;
        productionRecipes = null;
        productionRecipesBuildingId = 0;
        recipeSwitchIdempotencyKey = string.Empty;
        productionActionMessage = string.Empty;
    }

    public void RefreshIfShowing(LandSlotController slot)
    {
        if (slot == null || current != slot || panel == null || !panel.activeSelf) return;
        RefreshPreviewImages(slot);
        RefreshBuildingStatus();
        RefreshBuildingListIfShowing();
        RefreshProductionStatus();
        RefreshNpcStatus();
        RefreshProductionControls();
    }

    public void RefreshBuildingListIfShowing()
    {
        if (current == null || current.HasBuilding || panel == null || !panel.activeSelf
            || buildingListPanel == null || !buildingListPanel.gameObject.activeSelf) return;
        ShowBuildingList();
    }

    public void SetBuildingMessage(string message)
    {
        SetText(buildingText, FrontendTextLocalization.NormalizePlayerMessage(message));
    }

    public void SetProductionMessage(string message)
    {
        SetText(productionText, FrontendTextLocalization.NormalizePlayerMessage(message));
    }

    private void RefreshRemoteDetails(LandSlotController slot)
    {
        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated || slot == null || !slot.HasBuilding) return;

        service.LoadBuildingDetail(slot, result =>
        {
            if (current != slot) return;
            if (result == null || !result.Success)
            {
                SetText(buildingText, $"建筑详情读取失败：{ResultMessage(result)}");
                return;
            }
            RefreshBuildingStatus();
            RefreshProductionStatus();
        });

        service.LoadProductionDetail(slot, result =>
        {
            if (current != slot) return;
            if (result == null || !result.Success)
            {
                SetText(productionText, $"生产详情读取失败：{ResultMessage(result)}");
                return;
            }
            RefreshProductionStatus();
            RefreshNpcStatus();
            RefreshProductionControls();
        });
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf || Time.frameCount == shownFrame) return;

        if (canvasRect != null && lastCanvasSize != canvasRect.rect.size) PositionNearSlot(current);

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
        if (target != null) target.text = FrontendTextLocalization.Normalize(value);
    }

    private void ConfigurePanelSkin()
    {
        if (panelRect == null) return;

        panelImage = panel.GetComponent<Image>();
        if (panelImage == null) panelImage = panel.AddComponent<Image>();
        panelImage.sprite = Resources.Load<Sprite>("UI/LandInfoPanelBase");
        panelImage.color = Color.white;
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = true;
        panelImage.raycastTarget = true;
        if (panelImage.sprite != null)
            panelSize = new Vector2(700f, 700f * panelImage.sprite.rect.height / panelImage.sprite.rect.width);

        controlsTexture = Resources.Load<Texture2D>("UI/LandInfoPanelControls");
        popupsTexture = Resources.Load<Texture2D>("UI/LandInfoPanelPopups");
        panelSkinSprites.Clear();

        closeButton = EnsureCloseButton();

        landPreviewImage = EnsurePreviewImage("Land Preview", ArtPoint(425f, 618f), new Vector2(163f, 174f));
        terrainBadge = EnsurePreviewImage("Terrain Type Stamp", ArtPoint(1133f, 343f), new Vector2(20f, 20f));
        CreateArchiveStamp("Building Stamp", 1585f, 343f, Resources.Load<Sprite>("UI/SmallIcons/建造副"));
        CreateArchiveStamp("Production Stamp", 1133f, 692f, Resources.Load<Sprite>("UI/SmallIcons/一键收取"));
        CreateArchiveStamp("Resident Stamp", 1585f, 692f, GetPanelSkinSprite("AssignNpc"));
        foreach (string legacy in new[] { "Building Preview", "NPC Preview" })
        {
            Transform previous = panelRect.Find(legacy);
            if (previous != null) previous.gameObject.SetActive(false);
        }
    }

    private void CreateArchiveStamp(string name, float x, float y, Sprite sprite)
    {
        Image stamp = EnsurePreviewImage(name, ArtPoint(x, y), new Vector2(17f, 17f));
        stamp.sprite = sprite;
        stamp.enabled = sprite != null;
    }

    private Button EnsureCloseButton()
    {
        Transform existing = panelRect.Find("Close Panel Button");
        Button button;
        if (existing != null)
        {
            button = existing.GetComponent<Button>();
        }
        else
        {
            GameObject buttonObject = new GameObject("Close Panel Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panelRect, false);
            button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(Hide);

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.2f, 0.2f);
            iconRect.anchorMax = new Vector2(0.8f, 0.8f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = GetPanelSkinSprite("Close");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        if (button == null) return null;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = ArtPoint(1985f, 97f);
        rect.sizeDelta = new Vector2(36f, 36f);
        return button;
    }

    private Image EnsurePreviewImage(string objectName, Vector2 position, Vector2 size)
    {
        Transform existing = panelRect.Find(objectName);
        GameObject previewObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(Image));
        if (existing == null) previewObject.transform.SetParent(panelRect, false);

        RectTransform rect = previewObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = previewObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private void RefreshPreviewImages(LandSlotController slot)
    {
        if (slot == null || slot.CurrentLand == null) return;

        if (landPreviewImage == null) return;
        string description = FrontendTextLocalization.RemoveSyncNotices(slot.CurrentLand.description);
        terrainBadge.sprite = LandTypeIconCatalog.GetIcon(slot.CurrentLand);
        terrainBadge.enabled = terrainBadge.sprite != null;
        string terrain = TranslateLandType(slot.CurrentLand.LandTypeKey);
        SetText(descText, CardLead(terrain) + "\n" + (string.IsNullOrWhiteSpace(description)
            ? (slot.HasBuilding ? "已开发地块" : "可建造建筑") : description));
        Sprite scene = slot.ArchiveScene;
        landPreviewImage.sprite = scene;
        landPreviewImage.material = LandSceneMotion.ForImage(scene);
        landPreviewImage.gameObject.SetActive(scene != null);
    }

    private Sprite GetPanelSkinSprite(string name, bool popup = false)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (panelSkinSprites.TryGetValue(name, out Sprite cached) && cached != null) return cached;

        Texture2D texture = popup ? popupsTexture : controlsTexture;
        Dictionary<string, Rect> rects = popup ? PopupAtlasRects : ControlAtlasRects;
        if (texture == null || !rects.TryGetValue(name, out Rect atlasRect)) return null;

        float scaleX = texture.width / 2048f;
        float scaleY = texture.height / 1536f;
        Rect textureRect = new Rect(atlasRect.x * scaleX, atlasRect.y * scaleY,
            atlasRect.width * scaleX, atlasRect.height * scaleY);
        Vector4 border = (!popup && name.StartsWith("Button", StringComparison.Ordinal)) || name == "ListItemPaper"
            ? new Vector4(32f * scaleX, 32f * scaleY, 32f * scaleX, 32f * scaleY)
            : Vector4.zero;
        Sprite sprite = Sprite.Create(
            texture,
            textureRect,
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);
        sprite.name = $"LandInfoPanel_{name}";
        panelSkinSprites[name] = sprite;
        return sprite;
    }

    private void ApplyPanelSkinToControls()
    {
        ApplyButtonSkin(closeButton, "ButtonSquare");
        ApplyButtonSkin(buildButton, "ButtonPrimary");
        ApplyButtonSkin(assignNpcButton, "ButtonPrimary");
        ApplyButtonSkin(removeNpcButton, "ButtonDanger");
        ApplyButtonSkin(upgradeButton, "ButtonPrimary");
        ApplyButtonSkin(moveButton, "ButtonSecondary");
        ApplyButtonSkin(demolishButton, "ButtonDanger");
        ApplyButtonSkin(cancelActionButton, "ButtonSecondary");
        ApplyButtonSkin(completeActionButton, "ButtonPrimary");
        ApplyButtonSkin(recipeButton, "ButtonCompact");
        ApplyPopupSkin(buildingListPanel, "BuildingListPanel");
        ApplyPopupSkin(npcListPanel, "NpcListPanel");
    }

    private void ApplyButtonSkin(Button button, string skinName)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();
        if (image == null) return;
        Sprite sprite = GetPanelSkinSprite(skinName);
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 5f;
        image.preserveAspect = false;
        image.color = Color.white;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            SmallButtonIconUtility.ApplyOutlinedLabel(label);
            label.raycastTarget = false;
            label.fontSize = 13f;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 13f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.offsetMin = new Vector2(7f, 4f);
            label.rectTransform.offsetMax = new Vector2(-7f, -4f);
        }
        EnsureButtonIcon(button);
    }

    private void ApplyPopupSkin(RectTransform popup, string skinName)
    {
        if (popup == null) return;
        Image image = popup.GetComponent<Image>();
        if (image == null) return;
        Sprite sprite = GetPanelSkinSprite(skinName, true);
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private void ApplyListItemSkin(Button button, string skinName)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();
        if (image == null) return;
        Sprite sprite = GetPanelSkinSprite("ListItemPaper", true);
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4f;
        image.preserveAspect = false;
        image.color = Color.white;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        ConfigurePanelTextStyle(text, PanelTextColor, TextAlignmentOptions.TopLeft);
        text.fontSize = 14f;
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Normal;
        text.margin = new Vector4(4f, 4f, 4f, 4f);
        text.overflowMode = TextOverflowModes.Overflow;
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        float availableWidth = ((RectTransform)button.transform.parent).rect.width - 28f;
        layout.preferredHeight = Mathf.Max(64f, text.GetPreferredValues(text.text, Mathf.Max(100f, availableWidth), Mathf.Infinity).y + 22f);
        button.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.preferredHeight);
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger != null) Destroy(trigger);
    }

    private void EnsureButtonIcon(Button button)
    {
        if (button == null) return;
        string iconName = GetButtonIconName(button.name);
        if (string.IsNullOrEmpty(iconName)) return;

        Transform iconTransform = button.transform.Find("Skin Icon");
        GameObject iconObject = iconTransform != null ? iconTransform.gameObject : new GameObject("Skin Icon", typeof(RectTransform), typeof(Image));
        if (iconTransform == null) iconObject.transform.SetParent(button.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);
        iconRect.sizeDelta = new Vector2(19f, 19f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = GetPanelSkinSprite(iconName);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        RectTransform labelRect = label.rectTransform;
        labelRect.offsetMin = new Vector2(29f, 4f);
        labelRect.offsetMax = new Vector2(-7f, -4f);
    }

    private static string GetButtonIconName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return string.Empty;
        if (objectName.Contains("Build Building")) return "Terrain";
        if (objectName.Contains("Upgrade Building")) return "Upgrade";
        if (objectName.Contains("Move Building")) return "Move";
        if (objectName.Contains("Demolish Building")) return "Demolish";
        if (objectName.Contains("Cancel Building")) return "Close";
        if (objectName.Contains("Complete Building")) return "Inspect";
        return string.Empty;
    }

    public void ToggleBuildingList()
    {
        if (logButtonClicks) Debug.Log($"LandInfoPanel ToggleBuildingList clicked. current={(current != null ? current.name : "null")}, hasBuilding={current != null && current.HasBuilding}", this);
        if (current == null || current.HasBuilding) return;
        HideNpcList();
        if (buildingListPanel != null && buildingListPanel.gameObject.activeSelf)
        {
            if (logButtonClicks) Debug.Log("LandInfoPanel hiding building list.", this);
            HideBuildingList();
            return;
        }

        ShowBuildingList();
    }

    private void ShowBuildingList()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        EnsureBuildControls();
        if (buildingListPanel == null || buildingListContent == null || buildingManager == null)
        {
            Debug.LogWarning($"LandInfoPanel cannot show building list. panel={buildingListPanel != null}, content={buildingListContent != null}, manager={buildingManager != null}", this);
            return;
        }

        buildingListPanel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ClearBuildingList();
        onboardingBuildingButtons.Clear();
        IReadOnlyList<BuildingConfig> buildings = buildingManager.AvailableBuildings;
        if (logButtonClicks) Debug.Log($"LandInfoPanel showing building list. availableBuildings={buildings.Count}", this);
        int visibleCount = 0;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingConfig config = buildings[i];
            if (config == null) continue;
            if (current == null || !current.AllowsBuildingType(config))
            {
                if (logButtonClicks) Debug.Log($"LandInfoPanel hidden incompatible building item: {config.buildingName} type={config.buildingType} requiredLand={config.requiredLandType}", this);
                continue;
            }

            bool canBuild = buildingManager.CanBuildOnSlot(current, config, out string disabledReason);
            string label = FormatBuildingLabel(config, canBuild ? string.Empty : disabledReason);
            BuildingConfig captured = config;
            visibleCount++;
            Button itemButton = CreateTextButton($"Building Item {visibleCount}", buildingListContent, label, new Vector2(0f, 0f), new Vector2(220f, 64f), 16f, () => SelectBuilding(captured));
            ApplyListItemSkin(itemButton, "ListItem");
            itemButton.interactable = canBuild;
            onboardingBuildingButtons[config.buildingId] = itemButton;
            if (logButtonClicks) Debug.Log($"LandInfoPanel shown building item: {config.buildingName} type={config.buildingType} id={config.buildingId} canBuild={canBuild}", this);
        }

        buildingListPanel.gameObject.SetActive(true);
        buildingListPanel.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        buildingListPanel.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
    }

    private void SelectBuilding(BuildingConfig config)
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (logButtonClicks) Debug.Log($"LandInfoPanel SelectBuilding invoked. building={(config != null ? config.buildingName : "null")}, current={(current != null ? current.name : "null")}", this);
        if (buildingManager == null || current == null)
        {
            Debug.LogWarning($"LandInfoPanel SelectBuilding aborted. manager={buildingManager != null}, current={current != null}", this);
            return;
        }

        if (!buildingManager.TryBuildOnSlot(current, config, out string reason))
        {
            Debug.LogWarning($"LandInfoPanel build click reached BuildingManager but failed before request. building={(config != null ? config.buildingName : "null")}, reason={reason}", this);
            SetText(buildingText, "建造失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }

        if (logButtonClicks) Debug.Log($"LandInfoPanel build request started for {config.buildingName}.", this);
        HideBuildingList();
        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
    }

    private void OnUpgradeClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryStartUpgrade(current, out string reason))
        {
            SetText(buildingText, "升级失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }

        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
    }

    private void OnMoveClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.BeginMoveBuilding(current, out string reason))
        {
            SetText(buildingText, "移动失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
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
            SetText(buildingText, "拆除失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }

        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
    }

    private void OnCancelActionClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryCancelBuildingAction(current, out string reason))
        {
            SetText(buildingText, "取消失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }

        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
    }

    private void OnCompleteActionClicked()
    {
        if (buildingManager == null) buildingManager = BuildingManager.EnsureInstance();
        if (buildingManager == null || current == null) return;

        if (!buildingManager.TryCompleteBuildingAction(current, out string reason))
        {
            SetText(buildingText, "完成失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }

        RefreshBuildingStatus();
        RefreshProductionStatus();
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

        npcListPanel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ClearNpcList();
        LandSlotController requestedSlot = current;
        Button loading = CreateTextButton("NPC Loading", npcListContent, "正在读取居民……", new Vector2(0f, 0f), new Vector2(600f, 52f), 22f, () => { });
        ApplyListItemSkin(loading, "ListItem");
        loading.interactable = false;
        npcListPanel.gameObject.SetActive(true);
        npcListPanel.SetAsLastSibling();

        npcManager.RequestAvailableNpcsForSlot(requestedSlot, (success, message, npcs) =>
        {
            if (current != requestedSlot || npcListPanel == null || !npcListPanel.gameObject.activeSelf) return;

            ClearNpcList();
            if (!success)
            {
                Button failed = CreateTextButton("NPC Load Failed", npcListContent, "居民读取失败：" + FrontendTextLocalization.NormalizePlayerMessage(message), new Vector2(0f, 0f), new Vector2(600f, 52f), 20f, () => { });
                ApplyListItemSkin(failed, "ListItem");
                failed.interactable = false;
                return;
            }

            if (npcs == null || npcs.Count == 0)
            {
                Button empty = CreateTextButton("NPC Empty", npcListContent, "暂无可用居民", new Vector2(0f, 0f), new Vector2(600f, 52f), 22f, () => { });
                ApplyListItemSkin(empty, "ListItem");
                empty.interactable = false;
                return;
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcConfig config = npcs[i];
                if (config == null) continue;

                string label = FormatNpcLabel(config);
                NpcConfig captured = config;
                Button itemButton = CreateTextButton($"NPC Item {i + 1}", npcListContent, label, new Vector2(0f, 0f), new Vector2(180f, 64f), 15f, () => SelectNpc(captured));
                ApplyListItemSkin(itemButton, "ListItem");
                if (!npcManager.CanAssignToSlot(current, config, out _)) itemButton.interactable = false;
            }
        });
    }

    private void SelectNpc(NpcConfig config)
    {
        if (npcManager == null) npcManager = NpcManager.EnsureInstance();
        if (npcManager == null || current == null) return;

        if (!npcManager.TryAssignToSlot(current, config, (success, message) =>
        {
            if (!success)
            {
                SetText(npcText, "居民操作失败：" + FrontendTextLocalization.NormalizePlayerMessage(message));
                return;
            }

            HideNpcList();
            RefreshProductionStatus();
            RefreshNpcStatus();
            RefreshProductionControls();
            PlayerOnboardingGameplayBridge.NotifyBusinessSucceeded();
        }, out string reason))
        {
            SetText(npcText, "居民操作失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }
    }

    private void OnRemoveNpcClicked()
    {
        if (npcManager == null) npcManager = NpcManager.EnsureInstance();
        if (npcManager == null || current == null) return;

        if (!npcManager.TryRemoveFromSlot(current, (success, message) =>
        {
            if (!success)
            {
                SetText(npcText, "移除居民失败：" + FrontendTextLocalization.NormalizePlayerMessage(message));
                return;
            }

            HideNpcList();
            RefreshProductionStatus();
            RefreshNpcStatus();
        }, out string reason))
        {
            SetText(npcText, "移除居民失败：" + FrontendTextLocalization.NormalizePlayerMessage(reason));
            return;
        }
    }

    private static string CardLead(string value) => "<b><color=#633A24>" + value + "</color></b>";

    private static string TranslateLandType(string key)
    {
        switch ((key ?? "").ToUpperInvariant())
        {
            case "GRASSLAND": return "草原";
            case "OCEAN": case "WATER": return "海域";
            case "FOREST": return "森林";
            case "SNOW": case "SNOWFIELD": return "雪原";
            case "MOUNTAIN": return "山地";
            case "VOLCANO": return "火山";
            default: return "地貌";
        }
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
            SetText(buildingText, CardLead("尚未建造") + "\n可选择建筑");
            if (buildButton != null) buildButton.gameObject.SetActive(true);
            SetBuildingActionButtonsVisible(false);
        }
    }

    private void RefreshNpcStatus()
    {
        if (current == null) return;

        if (!current.HasBuilding)
        {
            SetText(npcText, CardLead("暂无居民") + "\n建成后可入驻");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(false);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(false);
            HideNpcList();
        }
        else if (!current.IsBuildingCompleted)
        {
            SetText(npcText, "暂不可分配\n请等待建筑完成");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(false);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(false);
            HideNpcList();
        }
        else if (current.HasNpc)
        {
            SetText(npcText, $"{CardLead(current.CurrentNpc.npcName)}  <color=#526839>已入驻</color>\n{TranslateDisplayLabel(current.CurrentNpc.role, "居民")}");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(false);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(true);
            HideNpcList();
        }
        else
        {
            SetText(npcText, CardLead("暂无居民") + "\n可分配居民");
            if (assignNpcButton != null) assignNpcButton.gameObject.SetActive(true);
            if (removeNpcButton != null) removeNpcButton.gameObject.SetActive(false);
        }
    }

    private void RefreshProductionStatus()
    {
        if (current == null) return;

        if (!current.HasBuilding)
        {
            SetText(productionText, CardLead("暂无产出") + "\n建成后开始生产");
            RefreshProductionControls();
            return;
        }

        IReadOnlyList<ProductionResponse> productions = current.LatestProductions;
        if (productions == null || productions.Count == 0)
        {
            SetText(productionText, current.IsBuildingCompleted ? "正在查看产出……" : "生产已暂停");
            RefreshProductionControls();
            return;
        }

        string status = FormatProductionStatus(current, productions);
        string workState = FormatWorkState(BackendGameService.Instance != null
            ? BackendGameService.Instance.CurrentWorkState
            : null);
        if (!string.IsNullOrEmpty(workState)) status += "\n" + workState;
        if (!string.IsNullOrEmpty(productionActionMessage)) status += "\n" + FrontendTextLocalization.NormalizePlayerMessage(productionActionMessage);
        SetText(productionText, status);
        RefreshProductionControls();
    }

    private void ResetProductionActionsIfBuildingChanged(LandSlotController slot)
    {
        int buildingId = slot != null && slot.HasBuilding ? slot.PlayerBuildingId : 0;
        if (productionRecipesBuildingId == buildingId) return;

        productionRecipes = null;
        productionRecipesBuildingId = buildingId;
        recipeSwitchIdempotencyKey = string.Empty;
        productionActionMessage = string.Empty;
    }

    private void EnsureProductionControls()
    {
        if (panelRect == null) return;

        if (recipeButton == null)
        {
            recipeButton = CreateTextButton(
                "Production Recipe Button",
                panelRect,
                "读取配方",
                new Vector2(190f, -214f),
                new Vector2(145f, 38f),
                16f,
                OnRecipeButtonClicked);
        }

        ConfigureButtonRect(recipeButton, ArtPoint(1024f, 1101f), new Vector2(116f, 27f));
        ApplyButtonSkin(recipeButton, "ButtonCompact");
        RefreshProductionControls();
    }

    private void RefreshProductionControls()
    {
        if (recipeButton == null) return;

        BackendGameService service = BackendGameService.Instance;
        bool canUseProduction = current != null
            && current.HasBuilding
            && current.IsBuildingCompleted
            && service != null
            && service.IsAuthenticated;
        bool recipesBusy = recipesRequestRunning || recipeSwitchRequestRunning;
        SetButtonVisible(recipeButton, canUseProduction, !recipesBusy);

        SetButtonLabel(recipeButton, productionRecipes != null ? "切换配方" : "读取配方");
    }

    private void OnRecipeButtonClicked()
    {
        if (recipesRequestRunning || recipeSwitchRequestRunning || current == null || !current.HasBuilding) return;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated) return;

        if (productionRecipes == null || productionRecipesBuildingId != current.PlayerBuildingId)
        {
            recipesRequestRunning = true;
            productionActionMessage = "正在查看配方……";
            RefreshProductionStatus();
            service.LoadProductionRecipes(current.PlayerBuildingId, result =>
            {
                recipesRequestRunning = false;
                if (result == null || !result.Success || result.Data == null)
                {
                    productionRecipes = null;
                    productionActionMessage = result == null
                        ? "读取配方失败"
                        : string.IsNullOrEmpty(result.Message) ? "读取配方失败" : result.Message;
                }
                else
                {
                    productionRecipes = result.Data;
                    productionRecipesBuildingId = current == null ? 0 : current.PlayerBuildingId;
                    productionActionMessage = FormatRecipeSummary(productionRecipes);
                }
                RefreshProductionStatus();
            });
            return;
        }

        ProductionRecipeResponse target = FindSwitchTarget(productionRecipes);
        if (target == null)
        {
            productionActionMessage = "当前没有可切换的配方";
            RefreshProductionStatus();
            return;
        }

        recipeSwitchRequestRunning = true;
        productionActionMessage = "正在切换配方...";
        if (string.IsNullOrEmpty(recipeSwitchIdempotencyKey))
        {
            recipeSwitchIdempotencyKey = System.Guid.NewGuid().ToString("D");
        }
        RefreshProductionStatus();
        ProductionRecipeSwitchRequest request = new ProductionRecipeSwitchRequest
        {
            playerBuildingId = current.PlayerBuildingId,
            recipeId = target.recipeId,
            expectedRecipeVersion = productionRecipes.activeRecipeVersion
        };
        service.SwitchProductionRecipe(request, recipeSwitchIdempotencyKey, result =>
        {
            recipeSwitchRequestRunning = false;
            if (result == null || !result.Success || result.Data == null)
            {
                productionActionMessage = result == null
                    ? "切换配方失败"
                    : string.IsNullOrEmpty(result.Message) ? "切换配方失败" : result.Message;
                if (result != null && (result.Code == 7810 || result.Code == 7811 || result.Code == 7812))
                {
                    productionRecipes = null;
                    recipeSwitchIdempotencyKey = string.Empty;
                    ReloadRecipesAfterConflict(
                        service,
                        request.playerBuildingId,
                        result.Code == 7811 ? "配方版本已变化，请检查新配方后再次确认。" : productionActionMessage);
                    if (result.Code == 7812) service.SyncMap();
                    return;
                }
                if (result != null && result.Code == 7813)
                {
                    productionRecipes = null;
                    recipeSwitchIdempotencyKey = string.Empty;
                }
            }
            else
            {
                productionRecipes = null;
                recipeSwitchIdempotencyKey = string.Empty;
                productionActionMessage = "配方已切换";
                RefreshProductionStatus();
            }
            RefreshProductionStatus();
        });
    }

    private void ReloadRecipesAfterConflict(BackendGameService service, long playerBuildingId, string message)
    {
        if (service == null || !service.IsAuthenticated || playerBuildingId <= 0L) return;

        recipesRequestRunning = true;
        service.LoadProductionRecipes(playerBuildingId, result =>
        {
            recipesRequestRunning = false;
            if (current == null || current.PlayerBuildingId != playerBuildingId) return;

            if (result != null && result.Success && result.Data != null)
            {
                productionRecipes = result.Data;
                productionRecipesBuildingId = current.PlayerBuildingId;
                productionActionMessage = message;
            }
            else
            {
                productionRecipes = null;
                productionActionMessage = result == null || string.IsNullOrEmpty(result.Message)
                    ? "重新读取配方失败。"
                    : result.Message;
            }
            RefreshProductionStatus();
        });
    }

    private static ProductionRecipeResponse FindSwitchTarget(ProductionRecipesResponse recipes)
    {
        if (recipes == null || recipes.recipes == null) return null;
        for (int i = 0; i < recipes.recipes.Length; i++)
        {
            ProductionRecipeResponse recipe = recipes.recipes[i];
            if (recipe != null && recipe.recipeId > 0 && recipe.recipeId != recipes.activeRecipeId) return recipe;
        }
        return null;
    }

    private static string FormatRecipeSummary(ProductionRecipesResponse recipes)
    {
        if (recipes == null || recipes.recipes == null || recipes.recipes.Length == 0) return "暂无可用配方";

        List<string> names = new List<string>();
        for (int i = 0; i < recipes.recipes.Length; i++)
        {
            ProductionRecipeResponse recipe = recipes.recipes[i];
            if (recipe == null) continue;
            string name = string.IsNullOrEmpty(recipe.displayName) ? "生产配方" : recipe.displayName;
            if (!string.IsNullOrEmpty(name)) names.Add(name + (recipe.active ? "（当前）" : string.Empty));
        }
        return names.Count == 0 ? "暂无可用配方" : "配方：" + string.Join("、", names.ToArray());
    }

    private static string FormatWorkState(ProductionWorkStateResponse state)
    {
        if (state == null) return string.Empty;
        string running = state.productionRunning ? "生产中" : "生产暂停";
        return $"剩余工时 {FormatDuration(state.remainingWorkSeconds)}\n{running}";
    }

    private static string FormatDuration(long seconds)
    {
        long safeSeconds = Math.Max(0L, seconds);
        long hours = safeSeconds / 3600L;
        long minutes = (safeSeconds % 3600L) / 60L;
        long remainder = safeSeconds % 60L;
        return hours > 0L ? $"{hours}小时{minutes}分" : minutes > 0L ? $"{minutes}分{remainder}秒" : $"{remainder}秒";
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = FrontendTextLocalization.Normalize(label);
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

    private static string FormatBuildingLabel(BuildingConfig config, string disabledReason)
    {
        string buildTime = config.buildSeconds > 0 ? FormatDuration(config.buildSeconds) : "立即完成";
        List<string> costs = new List<string>();
        if (config.goldCost > 0L) costs.Add($"金币 {config.goldCost:N0}");
        if (config.woodCost > 0L) costs.Add($"木材 {config.woodCost:N0}");
        if (config.stoneCost > 0L) costs.Add($"石材 {config.stoneCost:N0}");
        if (config.ironCost > 0L) costs.Add($"铁矿 {config.ironCost:N0}");
        string cost = costs.Count > 0 ? string.Join("、", costs) : config.cost > 0 ? $"消耗 {config.cost:N0}" : "免费";
        string ownership = config.hasOwnershipLimit ? $"  已拥有 {config.ownedCount}/{config.maxOwnedCount}" : string.Empty;
        string reason = string.IsNullOrEmpty(disabledReason) ? string.Empty : "\n不可用：" + FrontendTextLocalization.NormalizePlayerMessage(disabledReason);
        return $"{config.buildingName}\n{cost}\n建造：{buildTime}{ownership}{reason}";
    }

    private static string FormatBuildingStatus(LandSlotController slot)
    {
        BuildingConfig building = slot.CurrentBuilding;
        if (building == null) return "尚未建造";

        string text = $"{CardLead(building.buildingName)}  <color=#865519>等级 {slot.BuildingLevel}/{building.maxLevel}</color>\n{FormatBuildingRuntimeStatus(slot.BuildingStatus)}";
        if (slot.IsBuildingInProgress)
        {
            text += $"\n剩余 {FormatDuration(Mathf.CeilToInt(slot.BuildingRemainingSeconds))}\n进度 {Mathf.RoundToInt(slot.BuildingProgress * 100f)}%";
        }
        return text;
    }

    private static string FormatProductionStatus(LandSlotController slot, IReadOnlyList<ProductionResponse> productions)
    {
        if (productions == null || productions.Count == 0) return "暂无产出";

        List<string> lines = new List<string>();
        string costText = FormatProductionCosts(slot != null ? slot.CurrentBuilding : null);
        for (int i = 0; i < productions.Count; i++)
        {
            ProductionResponse production = productions[i];
            if (production == null) continue;

            string itemName = !string.IsNullOrEmpty(production.itemName) ? production.itemName : production.itemKey;
            if (string.IsNullOrEmpty(itemName)) itemName = "资源";
            itemName = TranslateResourceLabel(itemName);

            string collectState = production.canCollect ? "可收取" : FormatRemaining(production.remainingSeconds);
            string bonus = production.npcProduceBonusRate > 0f && (slot == null || !slot.HasNpc || !(slot.CurrentNpc.role ?? "").Contains($"{Mathf.RoundToInt(production.npcProduceBonusRate * 100f)}%"))
                ? $"\n居民加成 +{Mathf.RoundToInt(production.npcProduceBonusRate * 100f)}%" : string.Empty;
            string cost = string.IsNullOrEmpty(costText) ? string.Empty : $"\n每轮消耗 {costText}";
            lines.Add($"{CardLead(itemName)}  {production.storedAmount}/{production.storageCapacity}\n每{FormatCardInterval(production.produceIntervalSeconds)} +{production.produceAmount}\n{collectState}{bonus}{cost}");
        }

        return lines.Count == 0 ? "暂无产出" : string.Join("\n\n", lines);
    }

    private static string FormatProductionCosts(BuildingConfig building)
    {
        if (building == null || building.productionCosts == null || building.productionCosts.Length == 0) return string.Empty;

        List<string> parts = new List<string>();
        for (int i = 0; i < building.productionCosts.Length; i++)
        {
            ProductionResourceCost cost = building.productionCosts[i];
            if (cost == null || !cost.IsValid) continue;

            string itemName = string.IsNullOrEmpty(cost.itemName) ? cost.itemKey : cost.itemName;
            itemName = TranslateResourceLabel(itemName);
            parts.Add($"{itemName}×{cost.amount}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatCardInterval(long seconds)
    {
        if (seconds >= 3600 && seconds % 3600 == 0) return (seconds / 3600) + "小时";
        if (seconds >= 60 && seconds % 60 == 0) return (seconds / 60) + "分钟";
        return FormatDuration(seconds);
    }

    private static string FormatRemaining(long remainingSeconds)
    {
        if (remainingSeconds <= 0) return "等待中";
        long minutes = remainingSeconds / 60L;
        long seconds = remainingSeconds % 60L;
        return seconds == 0 ? $"下次 {minutes}分钟" : minutes > 0 ? $"下次 {minutes}分{seconds}秒" : $"下次 {seconds}秒";
    }

    private static string FormatNpcLabel(NpcConfig config)
    {
        string role = IsBlank(config.role) ? "居民" : TranslateDisplayLabel(config.role, "居民");
        string requirement = IsBlank(config.requiredBuildingType) ? "任意建筑" : $"需要{TranslateDisplayLabel(config.requiredBuildingType, "该类型建筑")}";
        string suffix = config.canAssign ? string.Empty : $"（{TranslateDisplayLabel(config.disabledReason, "暂不可分配")}）";
        return $"{config.npcName} - {role} - {requirement}{suffix}";
    }

    private static string FormatBuildingRuntimeStatus(BuildingRuntimeStatus status)
    {
        switch (status)
        {
            case BuildingRuntimeStatus.BUILDING: return "建造中";
            case BuildingRuntimeStatus.UPGRADING: return "升级中";
            case BuildingRuntimeStatus.COMPLETED: return "已完成";
            default: return "未知";
        }
    }

    private static string TranslateResourceLabel(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string key = value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        switch (key)
        {
            case "gold": return "金币";
            case "wood": return "木材";
            case "stone": return "石材";
            case "iron":
            case "ore": return "铁矿";
            case "wheat": return "小麦";
            case "fish": return "鱼";
            case "meat": return "肉";
            case "food":
            case "bread": return "食物";
            case "metal": return "金属";
            case "stamina": return "体力";
            case "crystal":
            case "diamond": return "钻石";
            default: return TranslateDisplayLabel(value, "资源");
        }
    }

    private static string TranslateDisplayLabel(string value, string fallback)
    {
        if (string.IsNullOrEmpty(value)) return fallback;
        string normalized = value.Trim().ToUpperInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        switch (normalized)
        {
            case "FARM": return "农场";
            case "MINE": return "矿场";
            case "LUMBERMILL": return "伐木场";
            case "WORKSHOP": return "工坊";
            case "HOUSE": return "住宅";
            case "RESIDENTIAL": return "住宅";
            case "DEFAULT": return "默认";
            case "NPC": return "居民";
            case "AVAILABLE": return "可用";
            case "UNAVAILABLE": return "不可用";
            case "LOCKED": return "已锁定";
            case "UNKNOWN": return "未知";
            default:
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z')) return fallback;
                }
                return value;
        }
    }

    private static bool IsBlank(string value)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
    }

    private static string ResultMessage<T>(BackendResult<T> result)
    {
        return FrontendTextLocalization.NormalizePlayerMessage(result == null ? null : result.Message, "暂时无法读取，请稍后重试。");
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
        if (IsPointerInsideActiveRectOrChildren(buildingListPanel, screenPosition, uiCamera)) return true;
        return IsPointerInsideActiveRectOrChildren(npcListPanel, screenPosition, uiCamera);
    }

    private static bool IsPointerInsideActiveRectOrChildren(RectTransform root, Vector2 screenPosition, Camera uiCamera)
    {
        if (root == null || !root.gameObject.activeInHierarchy) return false;
        if (RectTransformUtility.RectangleContainsScreenPoint(root, screenPosition, uiCamera)) return true;

        RectTransform[] children = root.GetComponentsInChildren<RectTransform>(false);
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform child = children[i];
            if (child == null || child == root) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(child, screenPosition, uiCamera)) return true;
        }

        return false;
    }

    private void ConfigureFloatingPanel()
    {
        if (panelRect == null) return;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        if (titleText == null)
        {
            titleText = CreatePanelText("Panel Title", panelRect);
            SetText(titleText, "地块档案");
        }
        if (productionText == null) productionText = CreatePanelText("Production Status", panelRect);
        if (npcText == null) npcText = CreatePanelText("NPC Status", panelRect);

        ConfigureTextRect(titleText, ArtPoint(1024f, 70f), new Vector2(160f, 30f), 22f);
        ConfigureTextRect(nameText, ArtPoint(885f, 207f), new Vector2(460f, 34f), 23f);
        ConfigureTextRect(levelText, ArtPoint(425f, 936f), new Vector2(160f, 22f), 14f);
        ConfigurePanelTextStyle(titleText, new Color(1f, 0.92f, 0.74f), TextAlignmentOptions.Center);
        ConfigurePanelTextStyle(nameText, PanelPrimaryTextColor, TextAlignmentOptions.Center);
        if (nameText != null)
        {
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 14f;
            nameText.fontSizeMax = 23f;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
        }
        ConfigurePanelTextStyle(descText, PanelTextColor, TextAlignmentOptions.TopLeft);
        ConfigurePanelTextStyle(levelText, PanelMutedTextColor, TextAlignmentOptions.Center);
        ConfigurePanelTextStyle(buildingText, PanelTextColor, TextAlignmentOptions.Left);
        ConfigurePanelTextStyle(productionText, PanelTextColor, TextAlignmentOptions.Left);
        ConfigurePanelTextStyle(npcText, PanelTextColor, TextAlignmentOptions.Left);

        SetVerticalAlignment(titleText, VerticalAlignmentOptions.Middle);
        SetVerticalAlignment(nameText, VerticalAlignmentOptions.Middle);
        SetVerticalAlignment(descText, VerticalAlignmentOptions.Top);
        SetVerticalAlignment(levelText, VerticalAlignmentOptions.Middle);
        SetVerticalAlignment(buildingText, VerticalAlignmentOptions.Top);
        SetVerticalAlignment(productionText, VerticalAlignmentOptions.Top);
        SetVerticalAlignment(npcText, VerticalAlignmentOptions.Top);

        ConfigureInformationText(descText, new Rect(780f, 394f, 375f, 204f));
        ConfigureInformationText(buildingText, new Rect(1236f, 394f, 375f, 204f));
        ConfigureInformationText(productionText, new Rect(780f, 740f, 375f, 240f));
        ConfigureInformationText(npcText, new Rect(1236f, 740f, 375f, 240f));

        if (levelText != null) levelText.transform.SetAsLastSibling();
    }

    private Vector2 ArtPoint(float horizontal, float vertical)
    {
        float scale = panelSize.x / 2048f;
        return new Vector2(horizontal * scale - panelSize.x * 0.5f, panelSize.y * 0.5f - vertical * scale);
    }

    private void ConfigureInformationText(TMP_Text text, Rect artworkBounds)
    {
        if (text == null) return;
        GameObject area = new GameObject(text.name + " Scroll Area", typeof(RectTransform));
        area.transform.SetParent(panelRect, false);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        areaRect.anchoredPosition = ArtPoint(artworkBounds.center.x, artworkBounds.center.y);
        areaRect.sizeDelta = artworkBounds.size * (panelSize.x / 2048f);
        text.fontSize = 12f;
        text.richText = true;
        text.color = new Color32(81, 59, 39, 255);
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Normal;
        text.lineSpacing = 1.5f;
        text.margin = new Vector4(2f, 1f, 3f, 1f);
        text.overflowMode = TextOverflowModes.Masking;
        informationScrolls.Add(ConfigureScrollArea(areaRect, text.rectTransform, Vector2.zero, Vector2.zero));
    }

    private ScrollRect ConfigureScrollArea(RectTransform host, RectTransform content, Vector2 insetMin, Vector2 insetMax)
    {
        ScrollRect scroll = host.GetComponent<ScrollRect>();
        if (scroll != null) return scroll;
        scroll = host.gameObject.AddComponent<ScrollRect>();
        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(host, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = insetMin;
        viewport.offsetMax = new Vector2(insetMax.x - 8f, insetMax.y);
        viewportObject.GetComponent<Image>().color = Color.clear;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = 18f;

        GameObject scrollbarObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(host, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.offsetMin = new Vector2(insetMax.x - 2f, insetMin.y);
        scrollbarRect.offsetMax = new Vector2(insetMax.x, insetMax.y);
        scrollbarObject.GetComponent<Image>().color = new Color(0.35f, 0.20f, 0.10f, 0.12f);
        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(scrollbarRect, false);
        Image handle = handleObject.GetComponent<Image>();
        handle.color = new Color(0.48f, 0.27f, 0.14f, 0.82f);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private void ConfigureSelectionList(RectTransform list, RectTransform content, string title, bool npc)
    {
        list.pivot = new Vector2(0.5f, 0.5f);
        list.anchoredPosition = new Vector2(0f, -8f);
        list.sizeDelta = npc ? new Vector2(240f, 324f) : new Vector2(320f, 320f);
        if (list.GetComponent<ScrollRect>() != null) return;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        ConfigureScrollArea(list, content, new Vector2(22f, 40f), new Vector2(-22f, -58f));
        TMP_Text heading = CreatePanelText("List Title", list);
        ConfigureTextRect(heading, new Vector2(0f, list.sizeDelta.y * 0.5f - 25f), new Vector2(list.sizeDelta.x - 86f, 30f), 18f);
        ConfigurePanelTextStyle(heading, npc ? PanelPrimaryTextColor : new Color(1f, 0.92f, 0.74f), TextAlignmentOptions.Center);
        SetText(heading, title);
        Button dismiss = CreateTextButton("Close List", list, string.Empty,
            new Vector2(list.sizeDelta.x * 0.5f - 29f, list.sizeDelta.y * 0.5f - 27f), new Vector2(25f, 25f), 14f,
            () => list.gameObject.SetActive(false));
        Image dismissImage = dismiss.GetComponent<Image>();
        dismissImage.sprite = GetPanelSkinSprite("Close");
        dismissImage.color = Color.white;
        dismissImage.preserveAspect = true;
        TMP_Text hint = CreatePanelText("List Scroll Hint", list);
        ConfigureTextRect(hint, new Vector2(0f, -list.sizeDelta.y * 0.5f + 26f), new Vector2(list.sizeDelta.x - 46f, 20f), 11f);
        ConfigurePanelTextStyle(hint, PanelMutedTextColor, TextAlignmentOptions.Center);
        SetText(hint, "上下滑动查看 · 点击条目选择");
    }

    private static void SetVerticalAlignment(TMP_Text text, VerticalAlignmentOptions alignment)
    {
        if (text != null) text.verticalAlignment = alignment;
    }

    private static void ConfigurePanelTextStyle(TMP_Text text, Color color, TextAlignmentOptions alignment)
    {
        if (text == null) return;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
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
        text.enableAutoSizing = false;
        text.enableWordWrapping = true;
    }

    private void EnsureBuildControls()
    {
        if (panelRect == null) return;

        if (buildButton == null)
        {
            buildButton = CreateTextButton("Build Building Button", panelRect, "建造", new Vector2(220f, -55f), new Vector2(170f, 50f), 24f, ToggleBuildingList);
        }
        ConfigureButtonRect(buildButton, ArtPoint(1806f, 425f), new Vector2(78f, 38f));
        ApplyButtonSkin(buildButton, "ButtonPrimary");

        if (buildingListPanel == null)
        {
            GameObject listObject = new GameObject("Building List Panel", typeof(RectTransform), typeof(Image));
            listObject.transform.SetParent(panelRect, false);
            buildingListPanel = listObject.GetComponent<RectTransform>();
            buildingListPanel.anchorMin = new Vector2(0.5f, 0.5f);
            buildingListPanel.anchorMax = new Vector2(0.5f, 0.5f);
            buildingListPanel.pivot = new Vector2(0.5f, 1f);
            buildingListPanel.anchoredPosition = new Vector2(0f, -182f);
            buildingListPanel.sizeDelta = new Vector2(220f, 250f);

            Image image = listObject.GetComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            image.raycastTarget = true;
            ApplyPopupSkin(buildingListPanel, "BuildingListPanel");
        }

        if (buildingListContent == null)
        {
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(buildingListPanel, false);
            buildingListContent = contentObject.GetComponent<RectTransform>();
            buildingListContent.anchorMin = new Vector2(0f, 0f);
            buildingListContent.anchorMax = new Vector2(1f, 1f);
            buildingListContent.offsetMin = new Vector2(8f, 12f);
            buildingListContent.offsetMax = new Vector2(-8f, -12f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
        }

        ConfigureSelectionList(buildingListPanel, buildingListContent, "选择建筑", false);
        buildingListPanel.gameObject.SetActive(false);
    }

    private void EnsureBuildingActionControls()
    {
        if (panelRect == null) return;

        if (upgradeButton == null) upgradeButton = CreateTextButton("Upgrade Building Button", panelRect, "升级", new Vector2(240f, 15f), new Vector2(135f, 42f), 18f, OnUpgradeClicked);
        if (moveButton == null) moveButton = CreateTextButton("Move Building Button", panelRect, "移动", new Vector2(240f, -32f), new Vector2(135f, 42f), 18f, OnMoveClicked);
        if (demolishButton == null) demolishButton = CreateTextButton("Demolish Building Button", panelRect, "拆除", new Vector2(240f, -79f), new Vector2(135f, 42f), 18f, OnDemolishClicked);
        if (cancelActionButton == null) cancelActionButton = CreateTextButton("Cancel Building Action Button", panelRect, "取消", new Vector2(240f, -126f), new Vector2(135f, 42f), 18f, OnCancelActionClicked);
        if (completeActionButton == null) completeActionButton = CreateTextButton("Complete Building Action Button", panelRect, "完成", new Vector2(240f, -173f), new Vector2(135f, 42f), 18f, OnCompleteActionClicked);

        ConfigureButtonRect(upgradeButton, ArtPoint(1806f, 425f), new Vector2(78f, 38f));
        ConfigureButtonRect(moveButton, ArtPoint(1806f, 565f), new Vector2(78f, 38f));
        ConfigureButtonRect(demolishButton, ArtPoint(1806f, 895f), new Vector2(78f, 38f));
        ConfigureButtonRect(cancelActionButton, ArtPoint(1806f, 705f), new Vector2(78f, 38f));
        ConfigureButtonRect(completeActionButton, ArtPoint(1806f, 565f), new Vector2(78f, 38f));
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
            assignNpcButton = CreateTextButton("Assign NPC Button", panelRect, "分配居民", new Vector2(220f, -115f), new Vector2(170f, 44f), 20f, ToggleNpcList);
        }
        ConfigureButtonRect(assignNpcButton, ArtPoint(1806f, 705f), new Vector2(78f, 38f));

        if (removeNpcButton == null)
        {
            removeNpcButton = CreateTextButton("Remove NPC Button", panelRect, "移除居民", new Vector2(220f, -164f), new Vector2(170f, 44f), 19f, OnRemoveNpcClicked);
        }
        ConfigureButtonRect(removeNpcButton, ArtPoint(1806f, 705f), new Vector2(78f, 38f));
        removeNpcButton.gameObject.SetActive(false);

        if (npcListPanel == null)
        {
            GameObject listObject = new GameObject("NPC List Panel", typeof(RectTransform), typeof(Image));
            listObject.transform.SetParent(panelRect, false);
            npcListPanel = listObject.GetComponent<RectTransform>();
            npcListPanel.anchorMin = new Vector2(0.5f, 0.5f);
            npcListPanel.anchorMax = new Vector2(0.5f, 0.5f);
            npcListPanel.pivot = new Vector2(0.5f, 1f);
            npcListPanel.anchoredPosition = new Vector2(0f, -182f);
            npcListPanel.sizeDelta = new Vector2(180f, 280f);

            Image image = listObject.GetComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            image.raycastTarget = true;
            ApplyPopupSkin(npcListPanel, "NpcListPanel");
        }

        if (npcListContent == null)
        {
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(npcListPanel, false);
            npcListContent = contentObject.GetComponent<RectTransform>();
            npcListContent.anchorMin = new Vector2(0f, 0f);
            npcListContent.anchorMax = new Vector2(1f, 1f);
            npcListContent.offsetMin = new Vector2(8f, 12f);
            npcListContent.offsetMax = new Vector2(-8f, -12f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
        }

        ConfigureSelectionList(npcListPanel, npcListContent, "选择居民", true);
        npcListPanel.gameObject.SetActive(false);
    }

    private TMP_Text CreatePanelText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
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
        EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener(_ =>
        {
            if (logButtonClicks) Debug.Log($"LandInfoPanel button pointerDown: {objectName} label={label} interactable={button.interactable}", this);
        });
        trigger.triggers.Add(pointerDown);
        button.onClick.AddListener(() =>
        {
            if (logButtonClicks) Debug.Log($"LandInfoPanel button onClick: {objectName} label={label} interactable={button.interactable}", this);
            action?.Invoke();
        });

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = FrontendTextLocalization.Normalize(label);
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return button;
    }

    private void PositionNearSlot(LandSlotController slot)
    {
        if (slot == null || panelRect == null || canvasRect == null) return;

        lastCanvasSize = canvasRect.rect.size;
        Vector2 availableSize = lastCanvasSize - screenPadding * 2f;
        float preferredScale = Mathf.Max(1.30f, Mathf.Min(lastCanvasSize.x * 0.60f / panelSize.x, lastCanvasSize.y * 0.67f / panelSize.y));
        float scale = Mathf.Min(preferredScale, availableSize.x / panelSize.x, availableSize.y / panelSize.y);
        panelRect.localScale = Vector3.one * Mathf.Max(0.1f, scale);

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
        Vector2 halfPanel = Vector2.Scale(panelRect.rect.size, panelRect.localScale) * 0.5f;

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
