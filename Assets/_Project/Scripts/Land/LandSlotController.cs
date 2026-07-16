using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>Owns one map shape, its mask range, and the land currently built there.</summary>
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class LandSlotController : MonoBehaviour
{
    private const int UnassignedSortingOrder = int.MinValue;
    private static int nextRuntimeSortingOffset;

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
    private int runtimePlayerLandId;
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
    private bool remoteCompletionPending;
    private float nextRemoteCompletionAttemptAt;
    private bool resourceReady;
    private string remoteResourceName;
    private int remoteResourceAmount;
    private readonly List<ProductionResponse> latestProductions = new List<ProductionResponse>();
    private static Material outlineMaterial;

    public int SlotId => slotId;
    public bool HasRemotePlayerLandId => runtimePlayerLandId > 0;
    public int RemotePlayerLandId => runtimePlayerLandId;
    public LandConfig CurrentLand => currentLand;
    public BuildingConfig CurrentBuilding => currentBuilding;
    public NpcConfig CurrentNpc => currentNpc;
    public bool IsBuilt => currentLand != null;
    public bool HasBuilding => currentBuilding != null;
    public bool HasNpc => currentNpc != null;
    public int PlayerLandId => runtimePlayerLandId > 0 ? runtimePlayerLandId : slotId > 0 ? slotId : GridPosition.x * 1000 + GridPosition.y + 1;
    public int PlayerBuildingId => playerBuildingId;
    public int BuildingLevel => HasBuilding ? buildingLevel : 0;
    public BuildingRuntimeStatus BuildingStatus => buildingStatus;
    public bool IsBuildingCompleted => HasBuilding && buildingStatus == BuildingRuntimeStatus.COMPLETED;
    public bool IsBuildingInProgress => HasBuilding && (buildingStatus == BuildingRuntimeStatus.BUILDING || buildingStatus == BuildingRuntimeStatus.UPGRADING);
    public float BuildingRemainingSeconds => IsBuildingInProgress ? Mathf.Max(0f, buildingEndsAt - Time.time) : 0f;
    public float BuildingProgress => IsBuildingInProgress && buildingDuration > 0f ? Mathf.Clamp01((Time.time - buildingStartedAt) / buildingDuration) : IsBuildingCompleted ? 1f : 0f;
    public bool HasResourceReady => resourceReady;
    public IReadOnlyList<ProductionResponse> LatestProductions => latestProductions;
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

    public LandConfig GetLand() => currentLand;

    public void CollectReadyResource()
    {
        BackendGameService service = BackendGameService.Instance;
        if (service != null && service.IsAuthenticated)
        {
            service.CollectProduction(this, (success, message) =>
            {
                if (!success) Debug.LogWarning($"Backend production collect failed: {message}", this);
            });
            return;
        }

        Debug.LogWarning("Backend production collect failed: Backend session is not authenticated.", this);
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

    public void SetRuntimeGridPosition(Vector2Int value)
    {
        if (useManualGridPosition) return;
        gridPosition = value;
        hasGridPosition = true;
    }

    public void ApplyRemoteLand(LandConfig config, LandTileResponse response)
    {
        if (response != null)
        {
            runtimePlayerLandId = response.playerLandId;
            if (!useManualGridPosition)
            {
                gridPosition = new Vector2Int(response.positionX, response.positionY);
                hasGridPosition = true;
            }
        }

        ForceBuildLand(config);
    }

    public void ApplyRemoteBuilding(BuildingConfig config, BuildingResponse response)
    {
        if (config == null || response == null || !IsBuilt) return;

        if (playerBuildingId != response.playerBuildingId) latestProductions.Clear();
        currentBuilding = config;
        playerBuildingId = response.playerBuildingId;
        buildingLevel = Mathf.Max(1, response.level);
        buildingStatus = RuntimeConfigFactory.ParseBuildingStatus(response.status);
        ApplyRemoteBuildingTimer(response.remainingTime, response.progress);
        ConfigureBuildingRenderer(true);

        HideResourceBubble();
    }

    public void ApplyRemoteProduction(ProductionResponse response)
    {
        if (response == null || response.playerBuildingId != playerBuildingId) return;

        remoteResourceName = string.IsNullOrEmpty(response.itemName) ? response.itemKey : response.itemName;
        remoteResourceAmount = Mathf.Max(0, response.storedAmount);
        UpsertProduction(response);
        ApplyRemoteSettledNpc(response.settledNpc);
        resourceReady = response.canCollect && remoteResourceAmount > 0;
        if (resourceReady)
        {
            RefreshResourceBubble();
        }
        else
        {
            ClearRemoteProductionState(response.remainingSeconds);
        }
    }

    public void ClearRemoteProductionState(int remainingSeconds = 0)
    {
        resourceReady = false;
        remoteResourceAmount = 0;
        HideResourceBubble();
    }

    private void UpsertProduction(ProductionResponse response)
    {
        for (int i = 0; i < latestProductions.Count; i++)
        {
            ProductionResponse existing = latestProductions[i];
            if (existing == null) continue;

            bool sameItemId = response.itemId > 0 && existing.itemId == response.itemId;
            bool sameItemKey = !string.IsNullOrEmpty(response.itemKey) && string.Equals(existing.itemKey, response.itemKey, System.StringComparison.OrdinalIgnoreCase);
            if (sameItemId || sameItemKey)
            {
                latestProductions[i] = response;
                return;
            }
        }

        latestProductions.Add(response);
    }

    private void ApplyRemoteSettledNpc(SettledNpcResponse settledNpc)
    {
        if (settledNpc != null && settledNpc.playerNpcId > 0)
        {
            currentNpc = NpcManager.CreateRuntimeNpc(settledNpc);
            ConfigureNpcRenderer(true);
            return;
        }

        currentNpc = null;
        if (npcRenderer != null) npcRenderer.enabled = false;
    }

    public void ClearLandStateFromBackend()
    {
        ClearBuildingState();
        runtimePlayerLandId = 0;
        currentLand = null;
        if (landRenderer != null) landRenderer.enabled = false;
        SetOutlineVisible(false);
    }

    public void ClearBuildingStateFromBackend()
    {
        ClearBuildingState();
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
        if (!resourceReady) HideResourceBubble();
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
            GetDisplayedResourceName(),
            GetDisplayedResourceAmount(),
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

    private void UpdateBuildingLifecycle()
    {
        if (!IsBuildingInProgress || BuildingRemainingSeconds > 0f) return;
        TryRequestRemoteBuildingSync();
    }

    private void TryRequestRemoteBuildingSync()
    {
        if (remoteCompletionPending || Time.unscaledTime < nextRemoteCompletionAttemptAt) return;
        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated) return;

        remoteCompletionPending = true;
        nextRemoteCompletionAttemptAt = Time.unscaledTime + 5f;
        service.SyncBuildingStatus(result =>
        {
            remoteCompletionPending = false;
            if (result == null || !result.Success)
            {
                Debug.LogWarning($"Backend building sync failed: {ResultMessage(result)}. {BuildDebugSummary()}", this);
            }
            if (LandInfoPanel.Instance != null) LandInfoPanel.Instance.RefreshIfShowing(this);
        });
    }

    private string BuildDebugSummary()
    {
        string buildingName = currentBuilding != null ? currentBuilding.buildingName : "None";
        int buildingId = currentBuilding != null ? currentBuilding.buildingId : 0;
        return $"playerBuildingId={playerBuildingId}, buildingId={buildingId}, buildingName={buildingName}, playerLandId={PlayerLandId}, grid={GridPosition}, status={buildingStatus}, remaining={BuildingRemainingSeconds:0.00}s, progress={BuildingProgress:0.00}";
    }

    private static string ResultMessage<T>(BackendResult<T> result)
    {
        if (result == null) return "No backend response.";
        return string.IsNullOrEmpty(result.Message) ? $"HTTP {result.HttpStatus}" : result.Message;
    }

    private void ClearBuildingTimer()
    {
        buildingStartedAt = 0f;
        buildingEndsAt = 0f;
        buildingDuration = 0f;
    }

    private void ClearBuildingState()
    {
        currentBuilding = null;
        currentNpc = null;
        playerBuildingId = 0;
        buildingLevel = 1;
        buildingStatus = BuildingRuntimeStatus.COMPLETED;
        remoteCompletionPending = false;
        nextRemoteCompletionAttemptAt = 0f;
        ClearBuildingTimer();
        resourceReady = false;
        remoteResourceName = string.Empty;
        remoteResourceAmount = 0;
        latestProductions.Clear();
        HideResourceBubble();
        if (buildingRenderer != null) buildingRenderer.enabled = false;
        if (npcRenderer != null) npcRenderer.enabled = false;
    }

    private string GetDisplayedResourceName()
    {
        return !string.IsNullOrEmpty(remoteResourceName) ? remoteResourceName : currentBuilding != null ? currentBuilding.resourceName : string.Empty;
    }

    private int GetDisplayedResourceAmount()
    {
        return remoteResourceAmount;
    }

    private void ForceBuildLand(LandConfig config)
    {
        if (config == null) return;

        ResolveReferences(true);
        currentLand = config;
        ConfigureRenderingLayers();
        if (spriteMask != null) spriteMask.enabled = true;
        if (landRenderer != null)
        {
            if (config.landSprite != null) landRenderer.sprite = config.landSprite;
            landRenderer.enabled = landRenderer.sprite != null;
        }
        SetOutlineVisible(true);
    }

    private void ApplyRemoteBuildingTimer(int remainingSeconds, float progress)
    {
        if (buildingStatus == BuildingRuntimeStatus.COMPLETED)
        {
            ClearBuildingTimer();
            return;
        }

        float normalizedProgress = Mathf.Clamp01(progress);
        int safeRemainingSeconds = Mathf.Max(0, remainingSeconds);
        buildingEndsAt = Time.time + safeRemainingSeconds;
        buildingDuration = normalizedProgress > 0f && normalizedProgress < 1f
            ? safeRemainingSeconds / Mathf.Max(0.001f, 1f - normalizedProgress)
            : Mathf.Max(1f, safeRemainingSeconds);
        buildingStartedAt = buildingEndsAt - buildingDuration;
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
