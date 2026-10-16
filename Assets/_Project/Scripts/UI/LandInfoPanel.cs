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
    private readonly Dictionary<int, Button> onboardingBuildingButtons = new Dictionary<int, Button>();

    /// <summary>Opens the existing building UI for the exact server-bound land instance.</summary>
    public RectTransform ResolveOnboardingTarget(LandSlotController slot, long buildingConfigId, bool completion)
    {
        if (slot == null || !slot.HasRemotePlayerLandId || panel == null) return null;
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
    public static LandInfoPanel Instance { get; private set; }
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
    private Button workTimeButton;
    private Button recipeButton;
    private ProductionWorkTimePreviewResponse workTimePreview;
    private ProductionWorkTimeRequest workTimePreviewRequest;
    private string workTimeIdempotencyKey = string.Empty;
    private ProductionRecipesResponse productionRecipes;
    private int productionRecipesBuildingId;
    private bool workStateRequestRunning;
    private bool workTimeRequestRunning;
    private bool workTimeFeatureUnavailable;
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
        EnsureProductionControls();
        panel.SetActive(false);
    }

    public void Show(LandSlotController slot)
    {
        if (slot == null || slot.CurrentLand == null) return;
        current = slot;
        ResetProductionActionsIfBuildingChanged(slot);
        LandConfig data = slot.CurrentLand;
        SetText(nameText, data.landName);
        SetText(descText, data.description);
        SetText(levelText, $"Level: {data.unlockLevel}");
        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
        RefreshProductionControls();
        panel.SetActive(true);
        shownFrame = Time.frameCount;
        PositionNearSlot(slot);
        RefreshRemoteDetails(slot);
        BackendGameService service = BackendGameService.Instance;
        if (slot.HasBuilding && service != null && service.IsAuthenticated)
        {
            EnsureWorkStateLoaded(service);
        }
    }

    public void Hide()
    {
        panel.SetActive(false);
        HideBuildingList();
        HideNpcList();
        current = null;
        workTimePreview = null;
        workTimePreviewRequest = null;
        workTimeIdempotencyKey = string.Empty;
        productionRecipes = null;
        productionRecipesBuildingId = 0;
        recipeSwitchIdempotencyKey = string.Empty;
        productionActionMessage = string.Empty;
    }

    public void RefreshIfShowing(LandSlotController slot)
    {
        if (slot == null || current != slot || panel == null || !panel.activeSelf) return;
        RefreshBuildingStatus();
        RefreshProductionStatus();
        RefreshNpcStatus();
        RefreshProductionControls();
    }

    public void SetBuildingMessage(string message)
    {
        SetText(buildingText, message);
    }

    public void SetProductionMessage(string message)
    {
        SetText(productionText, message);
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
                SetText(buildingText, $"Building detail failed: {ResultMessage(result)}");
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
                SetText(npcText, $"Production detail failed: {ResultMessage(result)}");
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

        ClearBuildingList();
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
            Button itemButton = CreateTextButton($"Building Item {visibleCount}", buildingListContent, label, new Vector2(0f, 0f), new Vector2(600f, 72f), 20f, () => SelectBuilding(captured));
            itemButton.interactable = canBuild;
            onboardingBuildingButtons[config.buildingId] = itemButton;
            if (logButtonClicks) Debug.Log($"LandInfoPanel shown building item: {config.buildingName} type={config.buildingType} id={config.buildingId} canBuild={canBuild}", this);
        }

        buildingListPanel.gameObject.SetActive(true);
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
            SetText(buildingText, $"Building failed: {reason}");
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
            SetText(buildingText, $"Upgrade failed: {reason}");
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
        RefreshProductionStatus();
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
        RefreshProductionStatus();
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

        ClearNpcList();
        LandSlotController requestedSlot = current;
        Button loading = CreateTextButton("NPC Loading", npcListContent, "Loading NPCs...", new Vector2(0f, 0f), new Vector2(600f, 52f), 22f, () => { });
        loading.interactable = false;
        npcListPanel.gameObject.SetActive(true);

        npcManager.RequestAvailableNpcsForSlot(requestedSlot, (success, message, npcs) =>
        {
            if (current != requestedSlot || npcListPanel == null || !npcListPanel.gameObject.activeSelf) return;

            ClearNpcList();
            if (!success)
            {
                Button failed = CreateTextButton("NPC Load Failed", npcListContent, $"NPC failed: {message}", new Vector2(0f, 0f), new Vector2(600f, 52f), 20f, () => { });
                failed.interactable = false;
                return;
            }

            if (npcs == null || npcs.Count == 0)
            {
                Button empty = CreateTextButton("NPC Empty", npcListContent, "No available NPC", new Vector2(0f, 0f), new Vector2(600f, 52f), 22f, () => { });
                empty.interactable = false;
                return;
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcConfig config = npcs[i];
                if (config == null) continue;

                string label = FormatNpcLabel(config);
                NpcConfig captured = config;
                Button itemButton = CreateTextButton($"NPC Item {i + 1}", npcListContent, label, new Vector2(0f, 0f), new Vector2(600f, 52f), 22f, () => SelectNpc(captured));
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
                SetText(npcText, $"NPC failed: {message}");
                return;
            }

            HideNpcList();
            RefreshProductionStatus();
            RefreshNpcStatus();
            RefreshProductionControls();
        }, out string reason))
        {
            SetText(npcText, $"NPC failed: {reason}");
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
                SetText(npcText, $"Remove NPC failed: {message}");
                return;
            }

            HideNpcList();
            RefreshProductionStatus();
            RefreshNpcStatus();
        }, out string reason))
        {
            SetText(npcText, $"Remove NPC failed: {reason}");
            return;
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

    private void RefreshProductionStatus()
    {
        if (current == null) return;

        if (!current.HasBuilding)
        {
            SetText(productionText, "Production: Build first");
            RefreshProductionControls();
            return;
        }

        IReadOnlyList<ProductionResponse> productions = current.LatestProductions;
        if (productions == null || productions.Count == 0)
        {
            SetText(productionText, current.IsBuildingCompleted ? "Production: Syncing..." : "Production: Paused");
            RefreshProductionControls();
            return;
        }

        string status = FormatProductionStatus(current, productions);
        string workState = FormatWorkState(BackendGameService.Instance != null
            ? BackendGameService.Instance.CurrentWorkState
            : null);
        if (!string.IsNullOrEmpty(workState)) status += "\n" + workState;
        if (!string.IsNullOrEmpty(productionActionMessage)) status += "\n" + productionActionMessage;
        SetText(productionText, status);
        RefreshProductionControls();
    }

    private void ResetProductionActionsIfBuildingChanged(LandSlotController slot)
    {
        int buildingId = slot != null && slot.HasBuilding ? slot.PlayerBuildingId : 0;
        if (productionRecipesBuildingId == buildingId) return;

        workTimePreview = null;
        workTimePreviewRequest = null;
        workTimeIdempotencyKey = string.Empty;
        productionRecipes = null;
        productionRecipesBuildingId = buildingId;
        recipeSwitchIdempotencyKey = string.Empty;
        productionActionMessage = string.Empty;
    }

    private void EnsureProductionControls()
    {
        if (panelRect == null) return;

        if (workTimeButton == null)
        {
            workTimeButton = CreateTextButton(
                "Production Work Time Button",
                panelRect,
                "补充工时",
                new Vector2(35f, -214f),
                new Vector2(145f, 38f),
                16f,
                OnWorkTimeButtonClicked);
        }

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

        ConfigureButtonRect(workTimeButton, new Vector2(35f, -214f), new Vector2(145f, 38f));
        ConfigureButtonRect(recipeButton, new Vector2(190f, -214f), new Vector2(145f, 38f));
        RefreshProductionControls();
    }

    private void RefreshProductionControls()
    {
        if (workTimeButton == null || recipeButton == null) return;

        BackendGameService service = BackendGameService.Instance;
        bool canUseProduction = current != null
            && current.HasBuilding
            && current.IsBuildingCompleted
            && service != null
            && service.IsAuthenticated;
        bool workTimeBusy = workStateRequestRunning || workTimeRequestRunning;
        bool recipesBusy = recipesRequestRunning || recipeSwitchRequestRunning;
        SetButtonVisible(workTimeButton, canUseProduction && !workTimeFeatureUnavailable, !workTimeBusy);
        SetButtonVisible(recipeButton, canUseProduction, !recipesBusy);

        SetButtonLabel(workTimeButton, workTimePreview != null ? "确认补充" : "补充工时");
        SetButtonLabel(recipeButton, productionRecipes != null ? "切换配方" : "读取配方");
    }

    private void OnWorkTimeButtonClicked()
    {
        if (workTimeRequestRunning || workStateRequestRunning) return;

        if (workTimePreview != null && workTimePreviewRequest != null)
        {
            StartCoroutine(ConsumeWorkTime());
            return;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            productionActionMessage = "请先登录账号";
            RefreshProductionStatus();
            return;
        }

        if (service.CurrentWorkState == null)
        {
            EnsureWorkStateLoaded(service);
            return;
        }

        ProductionFuelResponse fuel = FindAvailableFuel(service.CurrentWorkState);
        if (fuel == null)
        {
            productionActionMessage = "没有可用于补充工时的物品";
            RefreshProductionStatus();
            return;
        }

        workTimePreviewRequest = new ProductionWorkTimeRequest
        {
            itemId = fuel.itemId,
            quantity = 1L,
            expectedConfigVersion = service.CurrentWorkState.configVersion
        };
        workTimeRequestRunning = true;
        productionActionMessage = "正在预览补充工时...";
        workTimeIdempotencyKey = string.Empty;
        RefreshProductionStatus();
        service.PreviewProductionWorkTime(workTimePreviewRequest, result =>
        {
            workTimeRequestRunning = false;
            if (result == null || !result.Success || result.Data == null || !result.Data.canConsume)
            {
                workTimePreview = null;
                workTimePreviewRequest = null;
                if (result != null && result.Code == 7801) workTimeFeatureUnavailable = true;
                productionActionMessage = result == null
                    ? "补充工时预览失败"
                    : string.IsNullOrEmpty(result.Message) ? "当前无法补充工时" : result.Message;
                if (result != null && ShouldRefreshWorkState(result.Code))
                {
                    service.LoadProductionWorkState(_ => RefreshProductionStatus());
                }
            }
            else
            {
                workTimePreview = result.Data;
                productionActionMessage = $"预计增加 {FormatDuration(result.Data.requestedWorkSeconds)}，再次点击确认";
            }
            RefreshProductionStatus();
        });
    }

    private IEnumerator ConsumeWorkTime()
    {
        BackendGameService service = BackendGameService.Instance;
        if (service == null || workTimePreviewRequest == null)
        {
            workTimePreview = null;
            workTimePreviewRequest = null;
            RefreshProductionStatus();
            yield break;
        }

        workTimeRequestRunning = true;
        productionActionMessage = "正在确认补充工时...";
        RefreshProductionStatus();
        if (string.IsNullOrEmpty(workTimeIdempotencyKey))
        {
            workTimeIdempotencyKey = System.Guid.NewGuid().ToString("D");
        }
        BackendResult<ProductionWorkTimeConsumeResponse> result = null;
        yield return service.ConsumeProductionWorkTime(
            workTimePreviewRequest,
            workTimeIdempotencyKey,
            value => result = value);
        workTimeRequestRunning = false;
        if (result == null || !result.Success || result.Data == null)
        {
            productionActionMessage = result == null
                ? "补充工时失败"
                : string.IsNullOrEmpty(result.Message) ? "补充工时失败" : result.Message;
            if (result != null && result.Code > 0)
            {
                if (result.Code == 7801) workTimeFeatureUnavailable = true;
                workTimePreview = null;
                workTimePreviewRequest = null;
                workTimeIdempotencyKey = string.Empty;
            }
        }
        else
        {
            productionActionMessage = $"已补充 {FormatDuration(result.Data.grantedWorkSeconds)}";
            workTimePreview = null;
            workTimePreviewRequest = null;
            workTimeIdempotencyKey = string.Empty;
            service.RefreshInventory();
        }
        RefreshProductionStatus();
    }

    private void OnRecipeButtonClicked()
    {
        if (recipesRequestRunning || recipeSwitchRequestRunning || current == null || !current.HasBuilding) return;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated) return;

        if (productionRecipes == null || productionRecipesBuildingId != current.PlayerBuildingId)
        {
            recipesRequestRunning = true;
            productionActionMessage = "正在读取服务器配方...";
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
                productionActionMessage = "配方已切换，生产状态已刷新";
                RefreshProductionStatus();
            }
            RefreshProductionStatus();
        });
    }

    private void EnsureWorkStateLoaded(BackendGameService service)
    {
        if (workStateRequestRunning || service == null || !service.IsAuthenticated) return;

        workStateRequestRunning = true;
        productionActionMessage = "正在读取工作时间...";
        RefreshProductionStatus();
        service.LoadProductionWorkState(result =>
        {
            workStateRequestRunning = false;
            if (result != null && result.Success) workTimeFeatureUnavailable = false;
            productionActionMessage = result == null || !result.Success
                ? "读取工作时间失败"
                : "工作时间已同步，请再次点击补充";
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

    private static bool ShouldRefreshWorkState(int code)
    {
        return code == 7801
            || code == 7802
            || code == 7803
            || code == 7805
            || code == 7806
            || code == 7807
            || code == 7809;
    }

    private static ProductionFuelResponse FindAvailableFuel(ProductionWorkStateResponse state)
    {
        if (state == null || state.fuels == null) return null;
        for (int i = 0; i < state.fuels.Length; i++)
        {
            ProductionFuelResponse fuel = state.fuels[i];
            if (fuel != null && fuel.itemId > 0 && fuel.maxConsumableQuantity > 0) return fuel;
        }
        return null;
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
        if (recipes == null || recipes.recipes == null || recipes.recipes.Length == 0) return "服务器没有可用配方";

        List<string> names = new List<string>();
        for (int i = 0; i < recipes.recipes.Length; i++)
        {
            ProductionRecipeResponse recipe = recipes.recipes[i];
            if (recipe == null) continue;
            string name = string.IsNullOrEmpty(recipe.displayName) ? recipe.recipeCode : recipe.displayName;
            if (!string.IsNullOrEmpty(name)) names.Add(name + (recipe.active ? "（当前）" : string.Empty));
        }
        return names.Count == 0 ? "服务器没有可用配方" : "配方：" + string.Join("、", names.ToArray());
    }

    private static string FormatWorkState(ProductionWorkStateResponse state)
    {
        if (state == null) return string.Empty;
        string running = state.productionRunning ? "生产中" : "生产暂停";
        return $"工作时间：{FormatDuration(state.remainingWorkSeconds)}，{running}";
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
        if (text != null) text.text = label ?? string.Empty;
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
        onboardingBuildingButtons.Clear();
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
        string buildTime = config.buildSeconds > 0 ? $"{config.buildSeconds}s" : "Instant";
        List<string> costs = new List<string>();
        if (config.goldCost > 0L) costs.Add($"Gold {config.goldCost:N0}");
        if (config.woodCost > 0L) costs.Add($"Wood {config.woodCost:N0}");
        if (config.stoneCost > 0L) costs.Add($"Stone {config.stoneCost:N0}");
        if (config.ironCost > 0L) costs.Add($"Iron {config.ironCost:N0}");
        string cost = costs.Count > 0 ? string.Join(", ", costs) : config.cost > 0 ? $"Cost {config.cost:N0}" : "Free";
        string ownership = config.hasOwnershipLimit ? $"  Owned {config.ownedCount}/{config.maxOwnedCount}" : string.Empty;
        string reason = string.IsNullOrEmpty(disabledReason) ? string.Empty : $"\nUnavailable: {disabledReason}";
        return $"{config.buildingName} - {cost} - {buildTime}{ownership}{reason}";
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

    private static string FormatProductionStatus(LandSlotController slot, IReadOnlyList<ProductionResponse> productions)
    {
        if (productions == null || productions.Count == 0) return "Production: None";

        List<string> lines = new List<string>();
        string costText = FormatProductionCosts(slot != null ? slot.CurrentBuilding : null);
        for (int i = 0; i < productions.Count; i++)
        {
            ProductionResponse production = productions[i];
            if (production == null) continue;

            string itemName = !string.IsNullOrEmpty(production.itemName) ? production.itemName : production.itemKey;
            if (string.IsNullOrEmpty(itemName)) itemName = "Resource";

            string collectState = production.canCollect ? "Ready" : FormatRemaining(production.remainingSeconds);
            string bonus = production.npcProduceBonusRate > 0f ? $"  Bonus +{Mathf.RoundToInt(production.npcProduceBonusRate * 100f)}%" : string.Empty;
            string cost = string.IsNullOrEmpty(costText) ? string.Empty : $"  Cost {costText}/cycle";
            lines.Add($"{itemName}: {production.storedAmount}/{production.storageCapacity}  +{production.produceAmount}/{production.produceIntervalSeconds}s  {collectState}{bonus}{cost}");
        }

        return lines.Count == 0 ? "Production: None" : $"Production:\n{string.Join("\n", lines)}";
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
            parts.Add($"{itemName}x{cost.amount}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatRemaining(long remainingSeconds)
    {
        if (remainingSeconds <= 0) return "Waiting";
        long minutes = remainingSeconds / 60L;
        long seconds = remainingSeconds % 60L;
        return minutes > 0 ? $"Next {minutes}m {seconds}s" : $"Next {seconds}s";
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

    private static string ResultMessage<T>(BackendResult<T> result)
    {
        if (result == null) return "No backend response.";
        return string.IsNullOrEmpty(result.Message) ? $"HTTP {result.HttpStatus}" : result.Message;
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
        if (productionText == null) productionText = CreatePanelText("Production Status", panelRect, 17f);
        if (npcText == null) npcText = CreatePanelText("NPC Status", panelRect, 19f);

        ConfigureTextRect(nameText, new Vector2(0f, 142f), new Vector2(620f, 58f));
        ConfigureTextRect(descText, new Vector2(0f, 70f), new Vector2(620f, 70f));
        ConfigureTextRect(levelText, new Vector2(-185f, 8f), new Vector2(420f, 36f));
        ConfigureTextRect(buildingText, new Vector2(-135f, -42f), new Vector2(370f, 58f));
        ConfigureTextRect(productionText, new Vector2(-145f, -108f), new Vector2(370f, 72f));
        ConfigureTextRect(npcText, new Vector2(-150f, -166f), new Vector2(350f, 38f));
    }

    private static void ConfigureTextRect(TMP_Text text, Vector2 position, Vector2 size)
    {
        if (text == null) return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

    private TMP_Text CreatePanelText(string objectName, Transform parent, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.fontSize = fontSize;
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
