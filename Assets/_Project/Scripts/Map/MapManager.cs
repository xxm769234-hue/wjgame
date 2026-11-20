using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Owns map coordinates, slot registration, and backend snapshot application.</summary>
public sealed class MapManager : MonoBehaviour
{
    private const string SimulationSceneName = "Simulation management mode";
    private const string SimulationShapeResourcesPath = "SimulationLandShapes";
    private const int SimulationShapeCount = 32;

    public static MapManager Instance { get; private set; }

    [Header("Grid")]
    [SerializeField] private Vector2Int mapSize = new Vector2Int(100, 100);
    [SerializeField, Min(0.01f)] private float cellSize = 1f;
    [SerializeField] private Vector2 gridOriginWorld = Vector2.zero;
    [SerializeField] private bool autoFitGridOriginToSceneSlots = true;

    [Header("Registration")]
    [SerializeField] private bool autoRegisterSceneSlots = true;

    [Header("Runtime Landslot Layout")]
    [SerializeField] private bool buildEighteenUnitLayoutOnStart = true;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private string landslotTemplateName = "Landslot";
    [SerializeField, Min(1)] private int sideRows = 2;
    [SerializeField, Min(1)] private int sideColumns = 2;
    [SerializeField, Min(0f)] private float unitHorizontalGap = 0.35f;
    [SerializeField, Min(0f)] private float unitVerticalGap = 0.25f;
    [SerializeField, Min(0f)] private float sideGap = 1.5f;
    [SerializeField, Min(0f)] private float simulationShapeHorizontalGap = 0.12f;
    [SerializeField, Min(0f)] private float simulationShapeVerticalGap = 0.12f;
    [SerializeField] private Vector2 simulationBackgroundPadding = new Vector2(3.2f, 1.75f);
    [SerializeField] private Vector2 simulationShapeOffsetInBackground = new Vector2(0f, 0.6f);
    [SerializeField, Min(0f)] private float mapTopSpacing = 3.5f;
    [SerializeField] private string mapBackgroundName = "MapBackground";
    [SerializeField] private Vector2 mapBackgroundPadding = new Vector2(2f, 2f);
    [SerializeField] private int mapBackgroundSortingOrder = -100;

    private readonly Dictionary<Vector2Int, LandSlotController> slotsByGrid = new Dictionary<Vector2Int, LandSlotController>();
    private readonly HashSet<Vector2Int> occupiedSlots = new HashSet<Vector2Int>();
    private Bounds runtimeMapBounds;
    private float runtimeUnitWidth = 7.4390066f;
    private float runtimeUnitHeight = 5.2010057f;
    private int runtimePlayerLevel = 1;
    private int runtimeUnlockedUnitCount;
    private int runtimePlacedLandCount;

    public Vector2Int MapSize => mapSize;
    public float CellSize => cellSize;
    public Vector2 GridOriginWorld => gridOriginWorld;
    public IReadOnlyCollection<Vector2Int> OccupiedSlots => occupiedSlots;
    public Bounds RuntimeMapBounds => runtimeMapBounds;
    public float RuntimeUnitWidth => runtimeUnitWidth;
    public float RuntimeUnitHeight => runtimeUnitHeight;
    public int UnlockedUnitCount => runtimeUnlockedUnitCount;
    public int UnlockedLandSlotCount => runtimeUnlockedUnitCount * LandUnlockRules.SlotsPerUnit;
    public int PlacedLandCount => runtimePlacedLandCount;

    public static MapManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        MapManager existing = FindFirstObjectByType<MapManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("MapManager");
        return managerObject.AddComponent<MapManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (buildEighteenUnitLayoutOnStart) BuildRuntimeLandslotLayout();
        if (autoRegisterSceneSlots) RebuildSlotCache();
        ConfigureCameraController();
        ResourceCollectionManager.EnsureInstance();
        BackendGameService.EnsureInstance().Connect();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return ShapeHelper.WorldToGrid(worldPosition, gridOriginWorld, cellSize);
    }

    public Vector3 GridToWorld(Vector2Int gridPosition, float z = 0f)
    {
        return ShapeHelper.GridToWorld(gridPosition, gridOriginWorld, cellSize, z);
    }

    public Vector3 SnapWorldToGrid(Vector3 worldPosition, float z = 0f)
    {
        return ShapeHelper.SnapWorldToGrid(worldPosition, gridOriginWorld, cellSize, z);
    }

    public bool IsWithinBounds(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0
            && gridPosition.y >= 0
            && gridPosition.x < mapSize.x
            && gridPosition.y < mapSize.y;
    }

    public bool IsOccupied(Vector2Int gridPosition)
    {
        return occupiedSlots.Contains(gridPosition);
    }

    public void RegisterSlot(LandSlotController slot)
    {
        if (slot == null) return;
        if (slot.HasGridPosition && slotsByGrid.TryGetValue(slot.GridPosition, out LandSlotController alreadyRegistered) && alreadyRegistered == slot)
        {
            if (slot.IsBuilt) occupiedSlots.Add(slot.GridPosition);
            return;
        }

        RemoveSlotReferences(slot);

        Vector2Int gridPosition = ResolveGridPosition(slot, slotsByGrid);
        slot.SetRuntimeGridPosition(gridPosition);
        slotsByGrid[gridPosition] = slot;
        if (slot.IsBuilt) occupiedSlots.Add(gridPosition);
    }

    public void UnregisterSlot(LandSlotController slot)
    {
        if (slot == null || !slot.HasGridPosition) return;

        Vector2Int gridPosition = slot.GridPosition;
        if (slotsByGrid.TryGetValue(gridPosition, out LandSlotController registered) && registered == slot)
        {
            slotsByGrid.Remove(gridPosition);
        }
        if (slot.IsBuilt) occupiedSlots.Remove(gridPosition);
    }

    public void RebuildSlotCache()
    {
        slotsByGrid.Clear();
        occupiedSlots.Clear();

        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        if (autoFitGridOriginToSceneSlots && slots.Length > 0) FitGridOrigin(slots);

        Dictionary<Vector2Int, LandSlotController> assigned = new Dictionary<Vector2Int, LandSlotController>();
        foreach (LandSlotController slot in slots)
        {
            if (slot == null) continue;

            Vector2Int gridPosition = ResolveGridPosition(slot, assigned);
            slot.SetRuntimeGridPosition(gridPosition);
            assigned[gridPosition] = slot;
            slotsByGrid[gridPosition] = slot;

            if (slot.IsBuilt) occupiedSlots.Add(gridPosition);
        }
    }

    public bool CanPlace(LandSlotController slot, LandConfig config, out string reason)
    {
        reason = string.Empty;
        if (slot == null)
        {
            reason = "请将地块放在小镇的可用位置。";
            return false;
        }
        if (!slot.IsPlacementUnlocked)
        {
            reason = "此处尚未解锁。";
            return false;
        }
        if (config == null)
        {
            reason = "暂时无法选择这块地，请重新选择。";
            return false;
        }
        if (!config.canPlace)
        {
            reason = FrontendTextLocalization.NormalizePlayerMessage(config.disabledReason, "这块地暂时无法放置。");
            return false;
        }

        EnsureRegistered(slot);
        Vector2Int gridPosition = slot.GridPosition;
        if (!IsWithinBounds(gridPosition))
        {
            reason = "请将地块放在小镇范围内。";
            return false;
        }
        if (slot.IsBuilt || occupiedSlots.Contains(gridPosition))
        {
            reason = "当前地块已被占用。";
            return false;
        }

        return true;
    }

    public bool TryGetSlot(Vector2Int gridPosition, out LandSlotController slot)
    {
        return slotsByGrid.TryGetValue(gridPosition, out slot);
    }

    public void MarkOccupied(LandSlotController slot)
    {
        if (slot == null) return;
        EnsureRegistered(slot);
        if (occupiedSlots.Add(slot.GridPosition)) runtimePlacedLandCount++;
    }

    public void ApplyRemoteMapData(MapDataResponse mapData)
    {
        if (mapData == null) return;

        int resolvedWidth = mapData.landGridWidth > 0 ? mapData.landGridWidth : mapData.width;
        int resolvedHeight = mapData.landGridHeight > 0 ? mapData.landGridHeight : mapData.height;
        mapSize = new Vector2Int(Mathf.Max(1, resolvedWidth), Mathf.Max(1, resolvedHeight));
        RebuildSlotCache();
        ClearRemoteSlotState();

        ApplyRemoteLandSlotStates(mapData.landSlots);
        ApplyRemoteLands(mapData.lands);

        ApplyRemoteBuildings(mapData.buildings);
    }

    private void ApplyRemoteLands(LandTileResponse[] lands)
    {
        if (lands == null || lands.Length == 0) return;

        runtimePlacedLandCount = 0;
        for (int i = 0; i < lands.Length; i++)
        {
            if (lands[i] != null) runtimePlacedLandCount++;
        }

        List<LandTileResponse> legacyCoordinateLands = new List<LandTileResponse>();
        for (int i = 0; i < lands.Length; i++)
        {
            LandTileResponse land = lands[i];
            if (land == null) continue;

            Vector2Int position = new Vector2Int(land.positionX, land.positionY);
            if (!TryGetSlot(position, out LandSlotController slot) || slot.IsBuilt)
            {
                legacyCoordinateLands.Add(land);
                continue;
            }

            slot.ApplyRemoteLand(RuntimeConfigFactory.ResolveLand(land), land);
            occupiedSlots.Add(slot.GridPosition);
        }

        if (legacyCoordinateLands.Count == 0) return;

        LandSlotController[] fallbackSlots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        System.Array.Sort(fallbackSlots, CompareLegacyFallbackSlots);
        int fallbackIndex = 0;
        for (int i = 0; i < legacyCoordinateLands.Count; i++)
        {
            while (fallbackIndex < fallbackSlots.Length
                && (fallbackSlots[fallbackIndex] == null || fallbackSlots[fallbackIndex].IsBuilt))
            {
                fallbackIndex++;
            }

            if (fallbackIndex >= fallbackSlots.Length)
            {
                Debug.LogWarning(
                    $"Map snapshot contains {legacyCoordinateLands.Count - i} legacy lands that do not fit the current layout.",
                    this);
                break;
            }

            LandSlotController slot = fallbackSlots[fallbackIndex++];
            LandTileResponse land = legacyCoordinateLands[i];
            slot.ApplyRemoteLand(RuntimeConfigFactory.ResolveLand(land), land);
            occupiedSlots.Add(slot.GridPosition);
        }
    }

    private void ApplyRemoteLandSlotStates(LandSlotStateResponse[] landSlots)
    {
        if (landSlots == null) return;

        for (int i = 0; i < landSlots.Length; i++)
        {
            LandSlotStateResponse state = landSlots[i];
            if (state == null) continue;

            Vector2Int position = new Vector2Int(state.positionX, state.positionY);
            if (TryGetSlot(position, out LandSlotController slot)) slot.ApplyRemoteUnlockState(state);
        }
    }

    private static int CompareLegacyFallbackSlots(LandSlotController left, LandSlotController right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        // Fill later units first so legacy data never consumes a newly unlocked early slot.
        int unitOrder = right.UnlockUnitIndex.CompareTo(left.UnlockUnitIndex);
        if (unitOrder != 0) return unitOrder;

        int verticalOrder = left.GridPosition.y.CompareTo(right.GridPosition.y);
        return verticalOrder != 0 ? verticalOrder : left.GridPosition.x.CompareTo(right.GridPosition.x);
    }

    public void ApplyRemoteBuildings(BuildingResponse[] buildings)
    {
        if (buildings != null)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingResponse building = buildings[i];
                if (building == null) continue;

                LandSlotController slot = FindSlotForBuilding(building);
                if (slot == null) continue;

                slot.ApplyRemoteBuilding(RuntimeConfigFactory.ResolveBuilding(building), building);
            }
        }
    }

    public void ApplyRemoteProductions(ProductionResponse[] productions)
    {
        if (productions == null) return;

        for (int i = 0; i < productions.Length; i++)
        {
            ProductionResponse production = productions[i];
            if (production == null) continue;

            LandSlotController slot = FindSlotByPlayerBuildingId(production.playerBuildingId);
            if (slot != null) slot.ApplyRemoteProduction(production);
        }
    }

    public void ApplyRemoteProductionSnapshotForScene(ProductionResponse[] productions, int sceneId)
    {
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            LandSlotController slot = slots[slotIndex];
            if (slot == null || !slot.HasBuilding) continue;

            List<ProductionResponse> buildingProductions = new List<ProductionResponse>();
            if (productions != null)
            {
                for (int productionIndex = 0; productionIndex < productions.Length; productionIndex++)
                {
                    ProductionResponse production = productions[productionIndex];
                    if (production == null || production.playerBuildingId != slot.PlayerBuildingId) continue;
                    if (sceneId > 0 && production.sceneId > 0 && production.sceneId != sceneId) continue;
                    buildingProductions.Add(production);
                }
            }
            slot.ReplaceRemoteProductions(buildingProductions.ToArray());
        }
    }

    public void ApplyRemoteProductionSnapshotForBuilding(long playerBuildingId, ProductionResponse[] productions)
    {
        LandSlotController slot = FindSlotByPlayerBuildingId(playerBuildingId);
        if (slot == null) return;

        List<ProductionResponse> buildingProductions = new List<ProductionResponse>();
        if (productions != null)
        {
            for (int index = 0; index < productions.Length; index++)
            {
                ProductionResponse production = productions[index];
                if (production != null && production.playerBuildingId == playerBuildingId)
                {
                    buildingProductions.Add(production);
                }
            }
        }
        slot.ReplaceRemoteProductions(buildingProductions.ToArray());
    }

    public void ClearReadyRemoteProductions()
    {
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].HasResourceReady) slots[i].ClearRemoteProductionState();
        }
    }

    public void BuildRuntimeLandslotLayout()
    {
        Transform mapRoot = ResolveMapRoot();
        if (mapRoot == null) return;

        List<GameObject> units = FindLandslotUnits(mapRoot);
        if (units.Count == 0) return;

        GameObject template = FindTemplateUnit(units);
        if (template == null) return;

        for (int i = 0; i < units.Count; i++)
        {
            DisableExcessUnitSlots(units[i]);
        }

        Bounds unitLocalBounds = CalculateUnitLocalBounds(template);
        if (unitLocalBounds.size.x <= 0.0001f || unitLocalBounds.size.y <= 0.0001f) return;

        runtimeUnitWidth = unitLocalBounds.size.x;
        runtimeUnitHeight = unitLocalBounds.size.y;
        int targetCount = sideRows * sideColumns * 2;
        int activeSlotsPerUnit = template.GetComponentsInChildren<LandSlotController>(false).Length;
        EnsureLocalMapCapacity(targetCount, activeSlotsPerUnit);

        while (units.Count < targetCount)
        {
            GameObject clone = Instantiate(template, mapRoot);
            clone.name = $"{landslotTemplateName}_{units.Count + 1:00}";
            units.Add(clone);
        }

        for (int i = units.Count - 1; i >= targetCount; i--)
        {
            if (units[i] == template) continue;
            Destroy(units[i]);
            units.RemoveAt(i);
        }

        float cellX = unitLocalBounds.size.x + unitHorizontalGap;
        float cellY = unitLocalBounds.size.y + unitVerticalGap;
        float blockCenterDistance = sideColumns * cellX + sideGap;
        Bounds combinedBounds = new Bounds();
        bool hasBounds = false;

        int index = 0;
        for (int side = 0; side < 2; side++)
        {
            float sideCenterX = side == 0 ? -blockCenterDistance * 0.5f : blockCenterDistance * 0.5f;
            for (int row = 0; row < sideRows; row++)
            {
                for (int column = 0; column < sideColumns; column++)
                {
                    if (index >= units.Count) break;

                    GameObject unit = units[index];
                    unit.name = $"{landslotTemplateName}_{index + 1:00}";
                    unit.SetActive(true);

                    float localX = sideCenterX + (column - (sideColumns - 1) * 0.5f) * cellX;
                    float localY = ((sideRows - 1) * 0.5f - row) * cellY;
                    Vector3 targetCenter = new Vector3(localX, localY, 0f);
                    unit.transform.localPosition = targetCenter - unitLocalBounds.center;
                    unit.transform.localRotation = Quaternion.identity;
                    unit.transform.localScale = Vector3.one;

                    Bounds unitWorldBounds = CalculateWorldBounds(unit);
                    if (!hasBounds)
                    {
                        combinedBounds = unitWorldBounds;
                        hasBounds = true;
                    }
                    else combinedBounds.Encapsulate(unitWorldBounds);

                    index++;
                }
            }
        }

        if (hasBounds)
        {
            runtimeMapBounds = AddTopSpacing(combinedBounds, mapTopSpacing);
            runtimeMapBounds.Expand(new Vector3(
                Mathf.Max(0f, mapBackgroundPadding.x) * 2f,
                Mathf.Max(0f, mapBackgroundPadding.y) * 2f,
                0f));
        }

        ApplyRuntimeLandUnlockLayout(units, ResolveCurrentPlayerLevel());
        ApplySimulationLandShapeLayout(mapRoot);
        RebuildSlotCache();
    }

    public void RefreshLandUnlockState(int playerLevel)
    {
        runtimePlayerLevel = Mathf.Max(1, playerLevel);
        Transform mapRoot = ResolveMapRoot();
        if (mapRoot == null) return;

        List<GameObject> units = FindLandslotUnits(mapRoot);
        if (units.Count > 0) ApplyRuntimeLandUnlockLayout(units, runtimePlayerLevel);
    }

    private void EnsureRegistered(LandSlotController slot)
    {
        if (slot == null) return;
        if (slot.HasGridPosition && slotsByGrid.TryGetValue(slot.GridPosition, out LandSlotController registered) && registered == slot) return;
        RegisterSlot(slot);
    }

    private void ClearRemoteSlotState()
    {
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].ClearLandStateFromBackend();
            slots[i].ResetRemotePurchaseState();
        }
        occupiedSlots.Clear();
        runtimePlacedLandCount = 0;
    }

    private LandSlotController FindSlotForBuilding(BuildingResponse building)
    {
        LandSlotController slot = FindSlotByPlayerLandId(building.landId);
        if (slot != null) return slot;

        Vector2Int position = new Vector2Int(building.positionX, building.positionY);
        return TryGetSlot(position, out slot) ? slot : null;
    }

    private LandSlotController FindSlotByPlayerLandId(int playerLandId)
    {
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].HasRemotePlayerLandId && slots[i].RemotePlayerLandId == playerLandId) return slots[i];
        }
        return null;
    }

    private LandSlotController FindSlotByPlayerBuildingId(long playerBuildingId)
    {
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].PlayerBuildingId == playerBuildingId) return slots[i];
        }
        return null;
    }

    private Vector2Int ResolveGridPosition(LandSlotController slot, IDictionary<Vector2Int, LandSlotController> assigned)
    {
        bool usesAssignedPosition = slot.UsesManualGridPosition || slot.UsesRuntimeLayoutGridPosition;
        Vector2Int requested = usesAssignedPosition ? slot.GridPosition : WorldToGrid(slot.CenterWorld);
        if (usesAssignedPosition) return requested;

        requested = ClampToBounds(requested);
        if (!assigned.TryGetValue(requested, out LandSlotController existing) || existing == slot) return requested;
        return FindNearestFreeGrid(requested, assigned, slot);
    }

    private Vector2Int FindNearestFreeGrid(Vector2Int requested, IDictionary<Vector2Int, LandSlotController> assigned, LandSlotController requestingSlot)
    {
        int maxRadius = Mathf.Max(mapSize.x, mapSize.y);
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = requested.x - radius; x <= requested.x + radius; x++)
            {
                for (int y = requested.y - radius; y <= requested.y + radius; y++)
                {
                    if (Mathf.Abs(x - requested.x) != radius && Mathf.Abs(y - requested.y) != radius) continue;

                    Vector2Int candidate = new Vector2Int(x, y);
                    if (!IsWithinBounds(candidate)) continue;
                    if (assigned.TryGetValue(candidate, out LandSlotController existing) && existing != requestingSlot) continue;
                    return candidate;
                }
            }
        }

        return requested;
    }

    private void RemoveSlotReferences(LandSlotController slot)
    {
        if (slot == null) return;

        Vector2Int foundKey = default;
        bool found = false;
        foreach (KeyValuePair<Vector2Int, LandSlotController> pair in slotsByGrid)
        {
            if (pair.Value != slot) continue;
            foundKey = pair.Key;
            found = true;
            break;
        }

        if (!found) return;
        slotsByGrid.Remove(foundKey);
        occupiedSlots.Remove(foundKey);
    }

    private Vector2Int ClampToBounds(Vector2Int gridPosition)
    {
        return new Vector2Int(
            Mathf.Clamp(gridPosition.x, 0, Mathf.Max(0, mapSize.x - 1)),
            Mathf.Clamp(gridPosition.y, 0, Mathf.Max(0, mapSize.y - 1)));
    }

    private void FitGridOrigin(LandSlotController[] slots)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].UsesManualGridPosition) continue;

            Vector3 center = slots[i].CenterWorld;
            min.x = Mathf.Min(min.x, center.x);
            min.y = Mathf.Min(min.y, center.y);
        }

        if (float.IsInfinity(min.x) || float.IsInfinity(min.y)) return;
        gridOriginWorld = new Vector2(
            Mathf.Floor(min.x / cellSize) * cellSize,
            Mathf.Floor(min.y / cellSize) * cellSize);
    }

    private Transform ResolveMapRoot()
    {
        GameObject root = GameObject.Find(mapRootName);
        if (root != null) return root.transform;

        GameObject createdRoot = new GameObject(mapRootName);
        return createdRoot.transform;
    }

    private List<GameObject> FindLandslotUnits(Transform mapRoot)
    {
        List<GameObject> units = new List<GameObject>();
        for (int i = 0; i < mapRoot.childCount; i++)
        {
            Transform child = mapRoot.GetChild(i);
            if (child.GetComponentsInChildren<LandSlotController>(false).Length == 0) continue;
            units.Add(child.gameObject);
        }
        return units;
    }

    private GameObject FindTemplateUnit(List<GameObject> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null && units[i].name.StartsWith(landslotTemplateName)) return units[i];
        }
        return units.Count > 0 ? units[0] : null;
    }

    private void ApplyRuntimeLandUnlockLayout(List<GameObject> units, int playerLevel)
    {
        if (units == null || units.Count == 0) return;

        runtimePlayerLevel = Mathf.Max(1, playerLevel);
        units.Sort(CompareUnitsForUnlockOrder);
        runtimeUnlockedUnitCount = LandUnlockRules.GetUnlockedUnitCount(runtimePlayerLevel, units.Count);
        int unitsPerRow = Mathf.Max(1, sideColumns * 2);

        for (int layoutUnitIndex = 0; layoutUnitIndex < units.Count; layoutUnitIndex++)
        {
            GameObject unit = units[layoutUnitIndex];
            if (unit == null) continue;

            LandSlotController[] unitSlots = unit.GetComponentsInChildren<LandSlotController>(false);
            System.Array.Sort(unitSlots, CompareSlotsForLogicalPosition);
            if (unitSlots.Length != LandUnlockRules.SlotsPerUnit)
            {
                Debug.LogWarning(
                    $"Land unit {unit.name} contains {unitSlots.Length} active slots; expected {LandUnlockRules.SlotsPerUnit}.",
                    unit);
            }

            int unitColumn = layoutUnitIndex % unitsPerRow;
            int unitRow = layoutUnitIndex / unitsPerRow;
            int unlockUnitIndex = LandUnlockRules.ResolveUnitIndex(unitColumn, unitRow);
            int requiredPlayerLevel = LandUnlockRules.GetRequiredPlayerLevel(unlockUnitIndex);
            bool unlocked = LandUnlockRules.IsLevelUnlocked(runtimePlayerLevel, unlockUnitIndex);
            bool requiresGoldPurchase = LandUnlockRules.RequiresGoldPurchase(unlockUnitIndex);
            int assignableSlotCount = Mathf.Min(unitSlots.Length, LandUnlockRules.SlotsPerUnit);
            for (int slotIndex = 0; slotIndex < assignableSlotCount; slotIndex++)
            {
                LandSlotController slot = unitSlots[slotIndex];
                if (slot == null) continue;

                int localColumn = slotIndex % LandUnlockRules.UnitSideLength;
                int localRow = slotIndex / LandUnlockRules.UnitSideLength;
                slot.SetRuntimeLayoutGridPosition(new Vector2Int(
                    unitColumn * LandUnlockRules.UnitSideLength + localColumn,
                    unitRow * LandUnlockRules.UnitSideLength + localRow));
                slot.SetRuntimeUnlockState(
                    unlockUnitIndex,
                    requiredPlayerLevel,
                    unlocked,
                    requiresGoldPurchase);
            }
        }
    }

    private int CompareUnitsForUnlockOrder(GameObject left, GameObject right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        Vector3 leftAnchor = left.transform.localPosition;
        Vector3 rightAnchor = right.transform.localPosition;
        int verticalOrder = leftAnchor.y.CompareTo(rightAnchor.y);
        if (verticalOrder != 0) return verticalOrder;

        int horizontalOrder = leftAnchor.x.CompareTo(rightAnchor.x);
        if (horizontalOrder != 0) return horizontalOrder;

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private static int CompareSlotsForLogicalPosition(LandSlotController left, LandSlotController right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        Vector3 leftAnchor = left.transform.localPosition;
        Vector3 rightAnchor = right.transform.localPosition;
        int verticalOrder = leftAnchor.y.CompareTo(rightAnchor.y);
        if (verticalOrder != 0) return verticalOrder;

        int horizontalOrder = leftAnchor.x.CompareTo(rightAnchor.x);
        if (horizontalOrder != 0) return horizontalOrder;

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private static void DisableExcessUnitSlots(GameObject unit)
    {
        if (unit == null) return;

        LandSlotController[] slots = unit.GetComponentsInChildren<LandSlotController>(true);
        System.Array.Sort(slots, CompareSlotsForLogicalPosition);
        for (int i = LandUnlockRules.SlotsPerUnit; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].gameObject.SetActive(false);
        }
    }

    private static int ResolveCurrentPlayerLevel()
    {
        BackendGameService service = BackendGameService.Instance;
        return service != null && service.CurrentUser != null ? Mathf.Max(1, service.CurrentUser.level) : 1;
    }

    private Bounds CalculateUnitLocalBounds(GameObject unit)
    {
        PolygonCollider2D[] colliders = unit.GetComponentsInChildren<PolygonCollider2D>(true);
        Bounds bounds = new Bounds();
        bool hasPoint = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            PolygonCollider2D polygon = colliders[i];
            if (polygon == null || !polygon.gameObject.activeInHierarchy) continue;

            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                Vector2[] path = polygon.GetPath(pathIndex);
                for (int pointIndex = 0; pointIndex < path.Length; pointIndex++)
                {
                    Vector3 world = polygon.transform.TransformPoint(path[pointIndex] + polygon.offset);
                    Vector3 local = unit.transform.InverseTransformPoint(world);
                    if (!hasPoint)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        hasPoint = true;
                    }
                    else bounds.Encapsulate(local);
                }
            }
        }

        return bounds;
    }

    private Bounds CalculateWorldBounds(GameObject unit)
    {
        PolygonCollider2D[] colliders = unit.GetComponentsInChildren<PolygonCollider2D>(true);
        Bounds bounds = new Bounds(unit.transform.position, Vector3.zero);
        bool hasPoint = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            PolygonCollider2D polygon = colliders[i];
            if (polygon == null || !polygon.gameObject.activeInHierarchy) continue;

            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                Vector2[] path = polygon.GetPath(pathIndex);
                for (int pointIndex = 0; pointIndex < path.Length; pointIndex++)
                {
                    Vector3 world = polygon.transform.TransformPoint(path[pointIndex] + polygon.offset);
                    if (!hasPoint)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                        hasPoint = true;
                    }
                    else bounds.Encapsulate(world);
                }
            }
        }

        return bounds;
    }

    private static Bounds AddTopSpacing(Bounds bounds, float topSpacing)
    {
        if (topSpacing <= 0.0001f) return bounds;

        Vector3 center = bounds.center;
        Vector3 size = bounds.size;
        center.y += topSpacing * 0.5f;
        size.y += topSpacing;
        return new Bounds(center, size);
    }

    private void ResizeMapBackground(Bounds mapBounds)
    {
        if (mapBounds.size.x <= 0.0001f || mapBounds.size.y <= 0.0001f) return;

        SpriteRenderer backgroundRenderer = FindMapBackgroundRenderer();
        if (backgroundRenderer == null || backgroundRenderer.sprite == null) return;

        Vector3 center = mapBounds.center;
        Transform backgroundTransform = backgroundRenderer.transform;
        backgroundTransform.position = new Vector3(center.x, center.y, backgroundTransform.position.z);

        Vector3 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;

        Vector3 parentScale = backgroundTransform.parent != null ? backgroundTransform.parent.lossyScale : Vector3.one;
        float parentScaleX = Mathf.Abs(parentScale.x) <= 0.0001f ? 1f : Mathf.Abs(parentScale.x);
        float parentScaleY = Mathf.Abs(parentScale.y) <= 0.0001f ? 1f : Mathf.Abs(parentScale.y);
        float targetWorldWidth = mapBounds.size.x;
        float targetWorldHeight = mapBounds.size.y;

        Camera camera = Camera.main;
        if (camera != null && camera.orthographic)
        {
            float viewportHeight = camera.orthographicSize * 2f;
            float viewportWidth = viewportHeight * Mathf.Max(0.01f, camera.aspect);
            targetWorldWidth = Mathf.Max(targetWorldWidth, viewportWidth);
            targetWorldHeight = Mathf.Max(targetWorldHeight, viewportHeight);
        }

        backgroundTransform.localScale = new Vector3(
            targetWorldWidth / (spriteSize.x * parentScaleX),
            targetWorldHeight / (spriteSize.y * parentScaleY),
            backgroundTransform.localScale.z);
        backgroundRenderer.sortingOrder = mapBackgroundSortingOrder;
    }

    private SpriteRenderer FindMapBackgroundRenderer()
    {
        GameObject background = GameObject.Find(mapBackgroundName);
        if (background != null)
        {
            SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
            if (renderer != null) return renderer;
        }

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].name == mapBackgroundName) return renderers[i];
        }

        return null;
    }

    private void EnsureLocalMapCapacity(int unitCount, int slotsPerUnit)
    {
        int minimumSlots = unitCount * Mathf.Max(1, slotsPerUnit);
        int side = Mathf.CeilToInt(Mathf.Sqrt(minimumSlots)) + 10;
        mapSize = new Vector2Int(Mathf.Max(mapSize.x, side), Mathf.Max(mapSize.y, side));
    }

    private void ConfigureCameraController()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        MapCameraController controller = camera.GetComponent<MapCameraController>();
        if (controller == null) controller = camera.gameObject.AddComponent<MapCameraController>();
        controller.Configure(runtimeMapBounds, runtimeUnitWidth, runtimeUnitHeight);
        ResizeMapBackground(runtimeMapBounds);
    }

    private void ApplySimulationLandShapeLayout(Transform mapRoot)
    {
        if (SceneManager.GetActiveScene().name != SimulationSceneName) return;

        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        if (slots.Length == 0) return;

        Sprite[] sprites = new Sprite[SimulationShapeCount];
        int loadedCount = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i] = Resources.Load<Sprite>($"{SimulationShapeResourcesPath}/{i + 1}");
            if (sprites[i] != null) loadedCount++;
        }

        if (loadedCount != SimulationShapeCount)
        {
            Debug.LogWarning(
                $"Simulation land shape resources loaded {loadedCount}/{SimulationShapeCount}.",
                this);
        }

        int pageColumns = Mathf.Max(1, sideColumns * LandUnlockRules.UnitSideLength);
        int pageRows = Mathf.Max(1, sideRows * LandUnlockRules.UnitSideLength);
        int totalColumns = pageColumns * 2;
        int pageShapeCount = pageColumns * pageRows;
        float[][] rowColumnCenters = new float[2 * pageRows][];
        float[] rowSpans = new float[2 * pageRows];
        float[] rowHeights = new float[pageRows];

        for (int rowFromTop = 0; rowFromTop < pageRows; rowFromTop++)
        {
            rowHeights[rowFromTop] = AverageSpriteHeight(sprites, rowFromTop, pageColumns, pageRows);
            for (int page = 0; page < 2; page++)
            {
                int rowIndex = page * pageRows + rowFromTop;
                rowColumnCenters[rowIndex] = ResolveRowColumnCenters(
                    sprites,
                    page * pageShapeCount + rowFromTop * pageColumns,
                    pageColumns,
                    simulationShapeHorizontalGap,
                    out rowSpans[rowIndex]);
            }
        }

        float pageWidth = 0f;
        for (int page = 0; page < 2; page++)
        {
            for (int rowFromTop = 0; rowFromTop < pageRows; rowFromTop++)
            {
                pageWidth = Mathf.Max(pageWidth, rowSpans[page * pageRows + rowFromTop]);
            }
        }

        float pageCenterDistance = pageWidth + Mathf.Max(0f, sideGap);
        float[] rowCenters = ResolveTopDownCenters(rowHeights, simulationShapeVerticalGap);
        float totalHeight = 0f;
        for (int i = 0; i < rowHeights.Length; i++) totalHeight += Mathf.Max(0.01f, rowHeights[i]);

        for (int i = 0; i < slots.Length; i++)
        {
            LandSlotController slot = slots[i];
            if (slot == null) continue;

            Vector2Int gridPosition = slot.GridPosition;
            int column = gridPosition.x;
            int rowFromTop = pageRows - 1 - gridPosition.y;
            if (column < 0 || column >= totalColumns || rowFromTop < 0 || rowFromTop >= pageRows)
            {
                slot.SetSimulationShapeSprite(null, slot.transform.position);
                continue;
            }

            int page = column / pageColumns;
            int pageColumn = column % pageColumns;
            int rowIndex = page * pageRows + rowFromTop;
            int spriteIndex = page * pageShapeCount + rowFromTop * pageColumns + pageColumn;
            float pageCenterX = page == 0 ? -pageCenterDistance * 0.5f : pageCenterDistance * 0.5f;
            float rowSpineOffset = (pageWidth - rowSpans[rowIndex]) * 0.5f;
            if (page == 1) rowSpineOffset = -rowSpineOffset;
            float centerX = pageCenterX + rowColumnCenters[rowIndex][pageColumn] + rowSpineOffset;
            float centerY = rowCenters[rowFromTop];
            Vector3 centerWorld = mapRoot.TransformPoint(new Vector3(centerX, centerY, 0f));

            slot.SetSimulationShapeSprite(
                spriteIndex >= 0 && spriteIndex < sprites.Length ? sprites[spriteIndex] : null,
                centerWorld);
        }

        Bounds shapeBounds = CalculateWorldBounds(mapRoot.gameObject);
        if (shapeBounds.size.x > 0.0001f && shapeBounds.size.y > 0.0001f)
        {
            runtimeUnitWidth = pageWidth / Mathf.Max(1, sideColumns);
            runtimeUnitHeight = totalHeight / Mathf.Max(1, sideRows);
            runtimeMapBounds = shapeBounds;
            runtimeMapBounds.Expand(new Vector3(
                Mathf.Max(0f, simulationBackgroundPadding.x) * 2f,
                Mathf.Max(0f, simulationBackgroundPadding.y) * 2f,
                0f));
            runtimeMapBounds.center -= new Vector3(
                simulationShapeOffsetInBackground.x,
                simulationShapeOffsetInBackground.y,
                0f);
        }
    }

    private static float[] ResolveRowColumnCenters(
        Sprite[] sprites,
        int firstSpriteIndex,
        int pageColumns,
        float horizontalGap,
        out float rowSpan)
    {
        float[] centers = new float[pageColumns];
        if (pageColumns == 0)
        {
            rowSpan = 0f;
            return centers;
        }

        for (int column = 1; column < pageColumns; column++)
        {
            Sprite left = GetSprite(sprites, firstSpriteIndex + column - 1);
            Sprite right = GetSprite(sprites, firstSpriteIndex + column);
            float pitch = ResolveSharedHorizontalPitch(left, right);
            if (pitch <= 0.0001f)
            {
                float leftWidth = left != null ? left.bounds.size.x : 1f;
                float rightWidth = right != null ? right.bounds.size.x : 1f;
                pitch = (leftWidth + rightWidth) * 0.5f;
            }
            centers[column] = centers[column - 1] + pitch + Mathf.Max(0f, horizontalGap);
        }

        Sprite first = GetSprite(sprites, firstSpriteIndex);
        Sprite last = GetSprite(sprites, firstSpriteIndex + pageColumns - 1);
        float leftEdge = ResolveSpriteHorizontalEdge(first, false);
        float rightEdge = centers[pageColumns - 1] + ResolveSpriteHorizontalEdge(last, true);
        rowSpan = Mathf.Max(0.01f, rightEdge - leftEdge);
        float offset = -(leftEdge + rightEdge) * 0.5f;
        for (int column = 0; column < centers.Length; column++) centers[column] += offset;
        return centers;
    }

    private static float ResolveSharedHorizontalPitch(Sprite left, Sprite right)
    {
        if (left == null || right == null) return 0f;
        return ResolveSpriteHorizontalEdge(left, true) - ResolveSpriteHorizontalEdge(right, false);
    }

    private static float ResolveSpriteHorizontalEdge(Sprite sprite, bool rightEdge)
    {
        if (sprite == null) return rightEdge ? 0.5f : -0.5f;

        int shapeCount = sprite.GetPhysicsShapeCount();
        float edge = rightEdge ? float.NegativeInfinity : float.PositiveInfinity;
        bool foundIntersection = false;
        List<Vector2> path = new List<Vector2>();
        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            path.Clear();
            sprite.GetPhysicsShape(shapeIndex, path);
            for (int pointIndex = 0; pointIndex < path.Count; pointIndex++)
            {
                Vector2 start = path[pointIndex];
                Vector2 end = path[(pointIndex + 1) % path.Count];
                if (Mathf.Abs(start.y) <= 0.0001f)
                {
                    edge = rightEdge ? Mathf.Max(edge, start.x) : Mathf.Min(edge, start.x);
                    foundIntersection = true;
                }
                if (Mathf.Abs(end.y) <= 0.0001f)
                {
                    edge = rightEdge ? Mathf.Max(edge, end.x) : Mathf.Min(edge, end.x);
                    foundIntersection = true;
                }
                if ((start.y < 0f && end.y > 0f) || (start.y > 0f && end.y < 0f))
                {
                    float t = -start.y / (end.y - start.y);
                    float x = Mathf.Lerp(start.x, end.x, t);
                    edge = rightEdge ? Mathf.Max(edge, x) : Mathf.Min(edge, x);
                    foundIntersection = true;
                }
            }
        }

        if (foundIntersection) return edge;

        Bounds bounds = sprite.bounds;
        return rightEdge ? bounds.max.x : bounds.min.x;
    }

    private static float[] ResolveTopDownCenters(float[] sizes, float verticalGap)
    {
        if (sizes == null || sizes.Length == 0) return new float[0];

        float totalSize = 0f;
        for (int i = 0; i < sizes.Length; i++) totalSize += Mathf.Max(0.01f, sizes[i]);
        totalSize += Mathf.Max(0f, verticalGap) * Mathf.Max(0, sizes.Length - 1);

        float[] centers = new float[sizes.Length];
        float cursor = totalSize * 0.5f;
        for (int i = 0; i < sizes.Length; i++)
        {
            float size = Mathf.Max(0.01f, sizes[i]);
            centers[i] = cursor - size * 0.5f;
            cursor -= size + Mathf.Max(0f, verticalGap);
        }
        return centers;
    }

    private static Sprite GetSprite(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    private static float AverageSpriteHeight(Sprite[] sprites, int rowFromTop, int pageColumns, int pageRows)
    {
        float total = 0f;
        int count = 0;
        int pageShapeCount = pageColumns * pageRows;
        for (int page = 0; page < 2; page++)
        {
            for (int column = 0; column < pageColumns; column++)
            {
                int index = page * pageShapeCount + rowFromTop * pageColumns + column;
                if (index < 0 || index >= sprites.Length || sprites[index] == null) continue;
                total += sprites[index].bounds.size.y;
                count++;
            }
        }
        return count > 0 ? total / count : 1f;
    }

}
