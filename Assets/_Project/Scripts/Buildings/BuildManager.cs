using UnityEngine;

/// <summary>
/// Owns the build session and preview lifetime. UI movement belongs to BuildPanelController.
/// </summary>
public sealed class BuildManager : MonoBehaviour
{
    public static BuildManager Instance { get; private set; }

    [Header("UI command target (does not contain build logic)")]
    [SerializeField] private BuildPanelController panelController;
    [Header("World preview prefab")]
    [SerializeField] private GameObject landPreviewPrefab;
    [SerializeField] private Camera worldCamera;

    public LandConfig CurrentLand { get; private set; }
    public bool IsBuildMode { get; private set; }
    private LandPreview activePreview;
    private MapManager mapManager;
    private bool placementPending;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (worldCamera == null) worldCamera = Camera.main;
        mapManager = MapManager.EnsureInstance();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void OnClickBuildButton()
    {
        if (!PlayerOnboardingInputLock.IsTargetAllowed("BUILDING_FARM_CARD"))
        {
            Debug.LogWarning("Onboarding currently blocks this building entry.", this);
            return;
        }

        if (panelController == null) { Debug.LogError("BuildPanelController is not assigned.", this); return; }
        panelController.TogglePanel();
    }

    /// <summary>Called by a UI drag item. Replaces an unfinished preview safely.</summary>
    public LandPreview BeginDrag(LandConfig config)
    {
        if (!PlayerOnboardingInputLock.IsInputAllowed("PLACE_FARM_LAND", "FREE_LAND"))
        {
            Debug.LogWarning("Onboarding currently locks build input.", this);
            return null;
        }

        if (config == null || landPreviewPrefab == null)
        {
            Debug.LogError("LandConfig or LandPreview prefab is not assigned.", this);
            return null;
        }

        EndBuild(true);
        CurrentLand = config;
        IsBuildMode = true;
        GameObject previewObject = Instantiate(landPreviewPrefab);
        activePreview = previewObject.GetComponent<LandPreview>();
        if (activePreview == null)
        {
            Debug.LogError("LandPreview prefab must contain a LandPreview component.", previewObject);
            Destroy(previewObject);
            EndBuild(false);
            return null;
        }
        activePreview.PlacementRequested += HandlePlacementRequested;
        activePreview.Cancelled += HandlePreviewCancelled;
        activePreview.Initialize(config, worldCamera);
        return activePreview;
    }

    /// <summary>Uses the normal placement transaction for the onboarding-selected slot and configuration.</summary>
    public void PlaceOnboardingLand(LandSlotController slot, LandConfig config)
    {
        if (!PlayerOnboardingInputLock.IsTargetOnly || config == null || slot == null) return;
        HandlePlacementRequested(slot, config);
    }

    private void HandlePlacementRequested(LandSlotController slot, LandConfig config)
    {
        if (!PlayerOnboardingInputLock.IsSlotAllowed(slot) || config == null
            || !PlayerOnboardingInputLock.IsConfigAllowed("land", config.landId)) return;
        if (mapManager == null) mapManager = MapManager.EnsureInstance();
        string reason = "MapManager is missing.";
        if (mapManager == null || !mapManager.CanPlace(slot, config, out reason))
        {
            Debug.LogWarning($"Land placement failed: {reason}", slot);
            EndBuild(false);
            return;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            Debug.LogWarning("Land placement failed: Backend session is not authenticated.", slot);
            EndBuild(false);
            return;
        }

        if (placementPending)
        {
            Debug.LogWarning("Land placement is already pending.", slot);
            EndBuild(false);
            return;
        }

        placementPending = true;
        service.PlaceLand(slot, config, (success, message) =>
        {
            placementPending = false;
            if (!success) Debug.LogWarning($"Backend land placement failed: {message}", slot);
        });
        EndBuild(false);
    }

    private void HandlePreviewCancelled() => EndBuild(false);

    public void ExitBuild() => EndBuild(true);

    private void EndBuild(bool destroyPreview)
    {
        if (activePreview != null)
        {
            activePreview.PlacementRequested -= HandlePlacementRequested;
            activePreview.Cancelled -= HandlePreviewCancelled;
            if (destroyPreview) Destroy(activePreview.gameObject);
        }
        activePreview = null;
        CurrentLand = null;
        IsBuildMode = false;
    }
}
