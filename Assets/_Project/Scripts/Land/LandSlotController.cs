using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>Owns one map shape, its mask range, and the land currently built there.</summary>
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class LandSlotController : MonoBehaviour
{
    private const int UnassignedSortingOrder = int.MinValue;
    private static int nextRuntimeSortingOffset;
    private static int nextRuntimePlayerBuildingId = 1;

    [SerializeField] private int slotId;
    [SerializeField] private SpriteRenderer landRenderer;
    [SerializeField] private SpriteMask spriteMask;
    [SerializeField] private PolygonCollider2D polygonCollider;

    [Header("Grid")]
    [SerializeField] private bool useManualGridPosition;
    [SerializeField] private Vector2Int gridPosition;

    [Header("Rendering Layers")]
    [SerializeField] private string landSortingLayer = "Land";
    [SerializeField] private int sortingOrderBase = 100;
    [SerializeField, Min(3)] private int sortingOrderStep = 10;
    [SerializeField, Min(0.001f)] private float outlineWidth = 0.06f;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private int outlineSortingOrderOffset = 2;
    [SerializeField] private string buildingSortingLayer = "Building";
    [SerializeField] private int buildingSortingOrderOffset = 5;
    [SerializeField] private int npcSortingOrderOffset = 8;

    [Header("Resource Bubble")]
    [SerializeField] private Vector2 resourceBubbleOffset = new Vector2(0f, 2.1f);
    [SerializeField] private int resourceBubbleSortingOrderOffset = 12;

    private int runtimeSortingOrder = UnassignedSortingOrder;
    private LandConfig currentLand;
    private BuildingConfig currentBuilding;
    private NpcConfig currentNpc;
    private SpriteRenderer buildingRenderer;
    private SpriteRenderer npcRenderer;
    private ResourceBubbleController resourceBubble;
    private LineRenderer outlineRenderer;
    private bool hasGridPosition;
    private int playerBuildingId;
    private int buildingLevel = 1;
    private BuildingRuntimeStatus buildingStatus = BuildingRuntimeStatus.COMPLETED;
    private float buildingStartedAt;
    private float buildingEndsAt;
    private float buildingDuration;
    private bool resourceReady;
    private float nextResourceReadyAt;
    private static Material outlineMaterial;

    public int SlotId => slotId;
    public LandConfig CurrentLand => currentLand;
    public BuildingConfig CurrentBuilding => currentBuilding;
    public NpcConfig CurrentNpc => currentNpc;
    public bool IsBuilt => currentLand != null;
    public bool HasBuilding => currentBuilding != null;
    public bool HasNpc => currentNpc != null;
    public int PlayerLandId => slotId > 0 ? slotId : GridPosition.x * 1000 + GridPosition.y + 1;
    public int PlayerBuildingId => playerBuildingId;
    public int BuildingLevel => HasBuilding ? buildingLevel : 0;
    public BuildingRuntimeStatus BuildingStatus => buildingStatus;
    public bool IsBuildingCompleted => HasBuilding && buildingStatus == BuildingRuntimeStatus.COMPLETED;
    public bool IsBuildingInProgress => HasBuilding && (buildingStatus == BuildingRuntimeStatus.BUILDING || buildingStatus == BuildingRuntimeStatus.UPGRADING);
    public float BuildingRemainingSeconds => IsBuildingInProgress ? Mathf.Max(0f, buildingEndsAt - Time.time) : 0f;
    public float BuildingProgress => IsBuildingInProgress && buildingDuration > 0f ? Mathf.Clamp01((Time.time - buildingStartedAt) / buildingDuration) : IsBuildingCompleted ? 1f : 0f;
    public bool HasResourceReady => resourceReady;
    public PolygonCollider2D Collider => polygonCollider;
    public Vector3 CenterWorld => polygonCollider != null ? ShapeHelper.GetWorldCentroid(polygonCollider) : transform.position;
    public Vector2Int GridPosition => gridPosition;
    public bool HasGridPosition => hasGridPosition || useManualGridPosition;
    public bool UsesManualGridPosition => useManualGridPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSortingOrderAllocator()
    {
        nextRuntimeSortingOffset = 0;
    }

    private void Awake()
    {
        int landSlotLayer = LayerMask.NameToLayer("LandSlot");
        if (landSlotLayer >= 0) gameObject.layer = landSlotLayer;
        SortingGroup parentSortingGroup = GetComponentInParent<SortingGroup>();
        if (parentSortingGroup != null) parentSortingGroup.enabled = false;
        ResolveReferences(false);
        if (polygonCollider != null) polygonCollider.isTrigger = true;
        hasGridPosition = useManualGridPosition;
        ConfigureRenderingLayers();
        if (landRenderer != null)
        {
            // Preserve preconfigured land when loading a saved or authored scene.
            if (landRenderer.sprite == null) landRenderer.enabled = false;
            else currentLand = null;
        }
        if (Application.isPlaying && MapManager.Instance != null) MapManager.Instance.RegisterSlot(this);
    }

    private void OnValidate()
    {
        ResolveReferences(false);
        if (polygonCollider != null) polygonCollider.isTrigger = true;
        if (useManualGridPosition) hasGridPosition = true;
        ConfigureRenderingLayers();
        ConfigureOutlineRenderer(false);
    }

    private void OnEnable()
    {
        if (Application.isPlaying && MapManager.Instance != null) MapManager.Instance.RegisterSlot(this);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && MapManager.Instance != null) MapManager.Instance.UnregisterSlot(this);
    }

    private void Update()
    {
        UpdateBuildingLifecycle();
        UpdateResourceProduction();
    }

    public bool CanBuild() => currentLand == null;

    public bool Build(LandConfig config)
    {
        if (config == null || config.landSprite == null || !CanBuild()) return false;
        ResolveReferences(true);
        if (landRenderer == null || spriteMask == null)
        {
            Debug.LogError($"LandSlot '{name}' is missing SpriteRenderer or SpriteMask.", this);
            return false;
        }
        currentLand = config;
        ConfigureRenderingLayers();
        spriteMask.enabled = true;
        landRenderer.sprite = config.landSprite;
        landRenderer.enabled = true;
        SetOutlineVisible(true);
        return true;
    }

    public LandConfig GetLand() => currentLand;

    public bool BuildBuilding(BuildingConfig config)
    {
        if (config == null || !IsBuilt || HasBuilding) return false;

        currentBuilding = config;
        playerBuildingId = AllocatePlayerBuildingId();
        buildingLevel = 1;
        StartBuildingTimer(BuildingRuntimeStatus.BUILDING, config.buildSeconds);
        ConfigureBuildingRenderer(true);
        if (buildingStatus == BuildingRuntimeStatus.COMPLETED) ScheduleNextResource(true);
        else HideResourceBubble();
        return buildingRenderer != null;
    }

    public bool AssignNpc(NpcConfig config)
    {
        if (config == null || !IsBuilt || !IsBuildingCompleted || HasNpc) return false;

        currentNpc = config;
        ConfigureNpcRenderer(true);
        if (!resourceReady) ScheduleNextResource(true);
        return npcRenderer != null;
    }

    public bool RemoveNpc()
    {
        if (!HasNpc) return false;

        if (NpcManager.Instance != null) NpcManager.Instance.UnregisterAssignment(currentNpc, this);
        currentNpc = null;
        if (npcRenderer != null) npcRenderer.enabled = false;

        resourceReady = false;
        HideResourceBubble();
        if (IsBuildingCompleted) ScheduleNextResource(true);
        return true;
    }

    public void CollectReadyResource()
    {
        TryCollectReadyResource(out _, out _);
    }

    public bool TryCollectReadyResource(out string resourceName, out int amount)
    {
        resourceName = string.Empty;
        amount = 0;
        if (!resourceReady || currentBuilding == null || !IsBuildingCompleted) return false;

        resourceName = currentBuilding.resourceName;
        amount = GetCurrentResourceAmount();
        Debug.Log($"Collected {amount} {resourceName} from '{name}'.", this);
        resourceReady = false;
        HideResourceBubble();
        ScheduleNextResource(false);
        return true;
    }

    public bool TryStartUpgrade(out string reason)
    {
        reason = string.Empty;
        if (!IsBuildingCompleted)
        {
            reason = "Only completed buildings can be upgraded.";
            return false;
        }
        if (buildingLevel >= currentBuilding.maxLevel)
        {
            reason = "Building is already at max level.";
            return false;
        }

        resourceReady = false;
        HideResourceBubble();
        StartBuildingTimer(BuildingRuntimeStatus.UPGRADING, currentBuilding.upgradeSeconds);
        ConfigureBuildingRenderer(false);
        return true;
    }

    public bool TryCancelBuildingAction(out string reason)
    {
        reason = string.Empty;
        if (!IsBuildingInProgress)
        {
            reason = "Only building or upgrading actions can be cancelled.";
            return false;
        }

        if (buildingStatus == BuildingRuntimeStatus.BUILDING)
        {
            ClearBuildingState();
            return true;
        }

        buildingStatus = BuildingRuntimeStatus.COMPLETED;
        ClearBuildingTimer();
        ConfigureBuildingRenderer(false);
        ScheduleNextResource(true);
        return true;
    }

    public bool TryCompleteBuildingAction(out string reason)
    {
        reason = string.Empty;
        if (!IsBuildingInProgress)
        {
            reason = "Building is not waiting to complete.";
            return false;
        }
        if (BuildingRemainingSeconds > 0f)
        {
            reason = $"Still needs {Mathf.CeilToInt(BuildingRemainingSeconds)}s.";
            return false;
        }

        CompleteBuildingAction();
        return true;
    }

    public bool TryDemolishBuilding(out string reason)
    {
        reason = string.Empty;
        if (!IsBuildingCompleted)
        {
            reason = "Building or upgrading buildings cannot be demolished.";
            return false;
        }

        ClearBuildingState();
        return true;
    }

    public bool CanReceiveMovedBuilding(BuildingConfig config, out string reason)
    {
        reason = string.Empty;
        if (!IsBuilt)
        {
            reason = "Target land is not placed.";
            return false;
        }
        if (HasBuilding)
        {
            reason = "Target land already has a building.";
            return false;
        }
        if (!AllowsBuildingType(config))
        {
            reason = "Target land does not allow this building type.";
            return false;
        }
        return true;
    }

    public bool TryMoveBuildingTo(LandSlotController target, out string reason)
    {
        reason = string.Empty;
        if (!IsBuildingCompleted)
        {
            reason = "Only completed buildings can be moved.";
            return false;
        }
        if (target == null || target == this)
        {
            reason = "Choose another target land.";
            return false;
        }
        if (!target.CanReceiveMovedBuilding(currentBuilding, out reason)) return false;

        target.ReceiveMovedBuilding(currentBuilding, currentNpc, playerBuildingId, buildingLevel);
        ClearBuildingState();
        return true;
    }

    public void SetRuntimeGridPosition(Vector2Int value)
    {
        if (useManualGridPosition) return;
        gridPosition = value;
        hasGridPosition = true;
    }

    private void OnMouseDown()
    {
        if (currentLand == null || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (BuildingManager.Instance != null && BuildingManager.Instance.HandleLandSlotClicked(this)) return;
        if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.Show(this);
    }

    private void ResolveReferences(bool forceRefresh = false)
    {
        if (forceRefresh || polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();
        if (forceRefresh || landRenderer == null) landRenderer = FindLandRenderer();
        if (forceRefresh || spriteMask == null) spriteMask = GetComponentInChildren<SpriteMask>(true);
    }

    private SpriteRenderer FindLandRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer candidate in renderers)
        {
            if (candidate == null || candidate.GetComponent<SpriteMask>() != null) continue;
            return candidate;
        }
        return null;
    }

    private void ConfigureRenderingLayers()
    {
        if (landRenderer == null || spriteMask == null) return;
        int order = GetRuntimeSortingOrder();
        int layerId = SortingLayer.NameToID(landSortingLayer);

        landRenderer.sortingLayerName = landSortingLayer;
        landRenderer.sortingOrder = order;
        landRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        spriteMask.enabled = true;
        spriteMask.isCustomRangeActive = true;
        spriteMask.frontSortingLayerID = layerId;
        spriteMask.backSortingLayerID = layerId;
        spriteMask.frontSortingOrder = order + 1;
        spriteMask.backSortingOrder = order - 1;
        ConfigureOutlineRenderer(false);
        ConfigureBuildingRenderer(false);
        ConfigureNpcRenderer(false);
        RefreshResourceBubble();
    }

    private int GetRuntimeSortingOrder()
    {
        if (!Application.isPlaying) return sortingOrderBase + transform.GetSiblingIndex() * sortingOrderStep;
        if (runtimeSortingOrder != UnassignedSortingOrder) return runtimeSortingOrder;
        runtimeSortingOrder = sortingOrderBase + nextRuntimeSortingOffset * sortingOrderStep;
        nextRuntimeSortingOffset++;
        if (runtimeSortingOrder > short.MaxValue)
        {
            Debug.LogWarning("Land slot sorting order exceeded Unity's recommended range. Increase sorting layers or lower the map slot count.", this);
        }
        return runtimeSortingOrder;
    }

    private void SetOutlineVisible(bool visible)
    {
        ConfigureOutlineRenderer(visible);
        if (outlineRenderer != null) outlineRenderer.enabled = visible;
    }

    private void ConfigureOutlineRenderer(bool createIfMissing)
    {
        if (polygonCollider == null) return;
        if (outlineRenderer == null)
        {
            Transform existing = transform.Find("Land Outline");
            if (existing != null) outlineRenderer = existing.GetComponent<LineRenderer>();
        }
        if (outlineRenderer == null && createIfMissing)
        {
            GameObject outlineObject = new GameObject("Land Outline");
            outlineObject.transform.SetParent(transform, false);
            outlineRenderer = outlineObject.AddComponent<LineRenderer>();
        }
        if (outlineRenderer == null) return;

        Vector2[] path = ShapeHelper.GetLargestPath(polygonCollider);
        outlineRenderer.useWorldSpace = false;
        outlineRenderer.loop = true;
        outlineRenderer.positionCount = path.Length;
        for (int i = 0; i < path.Length; i++)
        {
            outlineRenderer.SetPosition(i, new Vector3(path[i].x, path[i].y, -0.01f));
        }

        outlineRenderer.widthMultiplier = outlineWidth;
        outlineRenderer.startColor = outlineColor;
        outlineRenderer.endColor = outlineColor;
        outlineRenderer.numCornerVertices = 2;
        outlineRenderer.numCapVertices = 2;
        outlineRenderer.sortingLayerName = landSortingLayer;
        outlineRenderer.sortingOrder = GetRuntimeSortingOrder() + outlineSortingOrderOffset;
        Material material = GetOutlineMaterial();
        if (material != null) outlineRenderer.material = material;
    }

    private static Material GetOutlineMaterial()
    {
        if (outlineMaterial != null) return outlineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Default-Line");
        if (shader == null) return null;
        outlineMaterial = new Material(shader);
        return outlineMaterial;
    }

    private void ConfigureBuildingRenderer(bool createIfMissing)
    {
        if (currentBuilding == null) return;

        if (buildingRenderer == null)
        {
            Transform existing = transform.Find("Building Renderer");
            if (existing != null) buildingRenderer = existing.GetComponent<SpriteRenderer>();
        }
        if (buildingRenderer == null && createIfMissing)
        {
            GameObject buildingObject = new GameObject("Building Renderer");
            buildingObject.transform.SetParent(transform, false);
            buildingRenderer = buildingObject.AddComponent<SpriteRenderer>();
        }
        if (buildingRenderer == null) return;

        Vector3 localCenter = polygonCollider != null ? polygonCollider.transform.InverseTransformPoint(CenterWorld) : Vector3.zero;
        buildingRenderer.transform.localPosition = new Vector3(
            localCenter.x + currentBuilding.visualOffset.x,
            localCenter.y + currentBuilding.visualOffset.y,
            -0.05f);
        buildingRenderer.transform.localScale = Vector3.one * currentBuilding.visualScale;
        buildingRenderer.sprite = currentBuilding.buildingSprite != null ? currentBuilding.buildingSprite : CreateFallbackBuildingSprite(currentBuilding.fallbackColor);
        buildingRenderer.color = buildingStatus == BuildingRuntimeStatus.COMPLETED ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        buildingRenderer.maskInteraction = SpriteMaskInteraction.None;
        buildingRenderer.sortingLayerName = buildingSortingLayer;
        buildingRenderer.sortingOrder = GetRuntimeSortingOrder() + buildingSortingOrderOffset;
        buildingRenderer.enabled = true;
    }

    private void ConfigureNpcRenderer(bool createIfMissing)
    {
        if (currentNpc == null) return;

        if (npcRenderer == null)
        {
            Transform existing = transform.Find("NPC Renderer");
            if (existing != null) npcRenderer = existing.GetComponent<SpriteRenderer>();
        }
        if (npcRenderer == null && createIfMissing)
        {
            GameObject npcObject = new GameObject("NPC Renderer");
            npcObject.transform.SetParent(transform, false);
            npcRenderer = npcObject.AddComponent<SpriteRenderer>();
        }
        if (npcRenderer == null) return;

        Vector3 localCenter = polygonCollider != null ? polygonCollider.transform.InverseTransformPoint(CenterWorld) : Vector3.zero;
        npcRenderer.transform.localPosition = new Vector3(
            localCenter.x + currentNpc.visualOffset.x,
            localCenter.y + currentNpc.visualOffset.y,
            -0.08f);
        npcRenderer.transform.localScale = Vector3.one * currentNpc.visualScale;
        npcRenderer.sprite = currentNpc.npcSprite != null ? currentNpc.npcSprite : CreateFallbackNpcSprite(currentNpc.fallbackColor);
        npcRenderer.color = Color.white;
        npcRenderer.maskInteraction = SpriteMaskInteraction.None;
        npcRenderer.sortingLayerName = buildingSortingLayer;
        npcRenderer.sortingOrder = GetRuntimeSortingOrder() + npcSortingOrderOffset;
        npcRenderer.enabled = true;
    }

    private void UpdateResourceProduction()
    {
        if (resourceReady || currentBuilding == null) return;

        if (!CanProduceResource())
        {
            HideResourceBubble();
            return;
        }

        if (nextResourceReadyAt <= 0f)
        {
            ScheduleNextResource(true);
            return;
        }

        if (Time.time >= nextResourceReadyAt) SetResourceReady();
    }

    private bool CanProduceResource()
    {
        return currentBuilding != null
            && buildingStatus == BuildingRuntimeStatus.COMPLETED
            && currentBuilding.canProduceResource
            && (!currentBuilding.requireNpcForProduction || HasNpc);
    }

    private void ScheduleNextResource(bool allowImmediate)
    {
        if (!CanProduceResource())
        {
            resourceReady = false;
            nextResourceReadyAt = 0f;
            HideResourceBubble();
            return;
        }

        if (allowImmediate && currentBuilding.showResourceBubbleImmediately)
        {
            SetResourceReady();
            return;
        }

        resourceReady = false;
        HideResourceBubble();
        float seconds = Mathf.Max(0f, currentBuilding.productionSeconds);
        if (seconds <= 0f) SetResourceReady();
        else nextResourceReadyAt = Time.time + seconds;
    }

    private void SetResourceReady()
    {
        if (!CanProduceResource()) return;

        resourceReady = true;
        nextResourceReadyAt = 0f;
        RefreshResourceBubble();
    }

    private void RefreshResourceBubble()
    {
        if (!resourceReady || currentBuilding == null)
        {
            HideResourceBubble();
            return;
        }

        ResourceBubbleController bubble = EnsureResourceBubble();
        if (bubble == null) return;

        bubble.transform.localPosition = CalculateResourceBubbleLocalPosition();
        bubble.Show(
            currentBuilding.resourceName,
            GetCurrentResourceAmount(),
            currentBuilding.resourceIcon,
            currentBuilding.resourceColor,
            buildingSortingLayer,
            GetRuntimeSortingOrder() + resourceBubbleSortingOrderOffset);
    }

    private ResourceBubbleController EnsureResourceBubble()
    {
        if (resourceBubble == null)
        {
            Transform existing = transform.Find("Resource Bubble");
            if (existing != null) resourceBubble = existing.GetComponent<ResourceBubbleController>();
        }

        if (resourceBubble == null)
        {
            GameObject bubbleObject = new GameObject("Resource Bubble");
            bubbleObject.transform.SetParent(transform, false);
            resourceBubble = bubbleObject.AddComponent<ResourceBubbleController>();
        }

        resourceBubble.Initialize(this);
        return resourceBubble;
    }

    private Vector3 CalculateResourceBubbleLocalPosition()
    {
        Vector3 localCenter = polygonCollider != null ? polygonCollider.transform.InverseTransformPoint(CenterWorld) : Vector3.zero;
        return new Vector3(localCenter.x + resourceBubbleOffset.x, localCenter.y + resourceBubbleOffset.y, -0.12f);
    }

    private void HideResourceBubble()
    {
        if (resourceBubble != null) resourceBubble.HideBubble();
    }

    public bool AllowsBuildingType(BuildingConfig config)
    {
        if (currentLand == null || config == null) return false;
        string[] allowedTypes = currentLand.allowBuildingType;
        if (allowedTypes == null || allowedTypes.Length == 0) return true;

        for (int i = 0; i < allowedTypes.Length; i++)
        {
            string allowed = allowedTypes[i];
            if (string.IsNullOrEmpty(allowed) || allowed.Trim().Length == 0) continue;
            if (string.Equals(allowed, config.buildingType, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void StartBuildingTimer(BuildingRuntimeStatus status, int seconds)
    {
        buildingStatus = seconds > 0 ? status : BuildingRuntimeStatus.COMPLETED;
        if (buildingStatus == BuildingRuntimeStatus.COMPLETED)
        {
            ClearBuildingTimer();
            return;
        }

        buildingDuration = seconds;
        buildingStartedAt = Time.time;
        buildingEndsAt = buildingStartedAt + seconds;
    }

    private void UpdateBuildingLifecycle()
    {
        if (!IsBuildingInProgress || BuildingRemainingSeconds > 0f) return;
        CompleteBuildingAction();
        if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.RefreshIfShowing(this);
    }

    private void CompleteBuildingAction()
    {
        if (buildingStatus == BuildingRuntimeStatus.UPGRADING) buildingLevel++;
        buildingStatus = BuildingRuntimeStatus.COMPLETED;
        ClearBuildingTimer();
        ConfigureBuildingRenderer(false);
        ScheduleNextResource(true);
    }

    private void ClearBuildingTimer()
    {
        buildingStartedAt = 0f;
        buildingEndsAt = 0f;
        buildingDuration = 0f;
    }

    private void ReceiveMovedBuilding(BuildingConfig building, NpcConfig npc, int movedPlayerBuildingId, int movedLevel)
    {
        currentBuilding = building;
        currentNpc = npc;
        playerBuildingId = movedPlayerBuildingId;
        buildingLevel = Mathf.Max(1, movedLevel);
        buildingStatus = BuildingRuntimeStatus.COMPLETED;
        ClearBuildingTimer();
        ConfigureBuildingRenderer(true);
        ConfigureNpcRenderer(true);
        ScheduleNextResource(true);
        if (npc != null && NpcManager.Instance != null) NpcManager.Instance.RegisterAssignment(npc, this);
    }

    private void ClearBuildingState()
    {
        if (currentNpc != null && NpcManager.Instance != null) NpcManager.Instance.UnregisterAssignment(currentNpc, this);
        currentBuilding = null;
        currentNpc = null;
        playerBuildingId = 0;
        buildingLevel = 1;
        buildingStatus = BuildingRuntimeStatus.COMPLETED;
        ClearBuildingTimer();
        resourceReady = false;
        nextResourceReadyAt = 0f;
        HideResourceBubble();
        if (buildingRenderer != null) buildingRenderer.enabled = false;
        if (npcRenderer != null) npcRenderer.enabled = false;
    }

    private int GetCurrentResourceAmount()
    {
        if (currentBuilding == null) return 0;
        int bonus = Mathf.Max(0, buildingLevel - 1) * currentBuilding.resourceAmountPerLevel;
        return currentBuilding.resourceAmount + bonus;
    }

    private static int AllocatePlayerBuildingId()
    {
        return nextRuntimePlayerBuildingId++;
    }

    private static Sprite CreateFallbackBuildingSprite(Color color)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool roof = y > 38 && Mathf.Abs(x - 32) < y - 32;
                bool body = x >= 14 && x <= 50 && y >= 10 && y <= 38;
                bool door = x >= 28 && x <= 36 && y >= 10 && y <= 24;
                Color pixel = Color.clear;
                if (roof) pixel = new Color(0.55f, 0.18f, 0.12f, 1f);
                if (body) pixel = color;
                if (door) pixel = new Color(0.26f, 0.16f, 0.1f, 1f);
                texture.SetPixel(x, y, pixel);
            }
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.25f), size);
        sprite.name = "RuntimeFallbackBuilding";
        return sprite;
    }

    private static Sprite CreateFallbackNpcSprite(Color color)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - 32f;
                float dy = y - 34f;
                bool head = dx * dx + dy * dy <= 15f * 15f;
                bool body = x >= 18 && x <= 46 && y >= 7 && y <= 28;
                bool shoulder = Mathf.Abs(dx) < 22f && y >= 19 && y <= 30;
                Color pixel = Color.clear;
                if (shoulder || body) pixel = color;
                if (head) pixel = new Color(1f, 0.82f, 0.62f, 1f);
                if (head && y > 43 && Mathf.Abs(dx) < 13f) pixel = new Color(0.18f, 0.12f, 0.08f, 1f);
                texture.SetPixel(x, y, pixel);
            }
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.18f), size);
        sprite.name = "RuntimeFallbackNpc";
        return sprite;
    }
}
