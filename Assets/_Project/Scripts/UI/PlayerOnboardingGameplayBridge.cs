using System;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

/// <summary>
/// Resolves onboarding anchors against the loaded management map. Business actions use existing controllers.
/// Invoked by the imported UI assembly through its reflection boundary.
/// </summary>
[Preserve]
public sealed class PlayerOnboardingGameplayBridge : MonoBehaviour
{
    private static PlayerOnboardingGameplayBridge _instance;
    private Step _step;
    private LandSlotController _slot;
    private LandConfig _land;
    private RectTransform _worldTarget;
    private Canvas _canvas;
    private Button _placeButton;
    private bool _sceneRequestPending;
    private float _nextSceneRequestTime;
    private string _focusedStep;
    private int _playerId;
    private string _token;

    /// <summary>Returns the actual UI control or a screen-space hit target over the selected world slot.</summary>
    [Preserve]
    public static RectTransform ResolveTarget(string stepJson, int playerId, string token)
    {
        if (!IsSessionCurrent(playerId, token)) return null;
        MapManager map = MapManager.Instance;
        if (map == null || !map.isActiveAndEnabled || map.gameObject.scene.name != "Simulation management mode") return null;
        if (_instance == null)
            _instance = map.gameObject.GetComponent<PlayerOnboardingGameplayBridge>() ?? map.gameObject.AddComponent<PlayerOnboardingGameplayBridge>();
        if (_instance._playerId != playerId || _instance._token != token)
        {
            _instance._step = null;
            _instance._slot = null;
            _instance._playerId = playerId;
            _instance._token = token;
        }
        Step step = JsonUtility.FromJson<Step>(stepJson);
        return step == null ? null : _instance.Resolve(step);
    }

    /// <summary>Client acknowledgements require an actually loaded HOME map or visible free slot.</summary>
    [Preserve]
    public static bool IsStepReady(string stepCode, int playerId, string token)
    {
        if (!IsSessionCurrent(playerId, token)) return false;
        if (_instance == null || !_instance.isActiveAndEnabled || !_instance.IsHomeMapReady()) return false;
        if (stepCode == "OPEN_HOME_MAP") return true;
        return stepCode == "FOCUS_FREE_LAND" && _instance._step != null
            && _instance._step.stepCode == stepCode && _instance._worldTarget != null
            && _instance._worldTarget.gameObject.activeInHierarchy && _instance._slot != null;
    }

    private RectTransform Resolve(Step step)
    {
        if (_step == null || _step.stepCode != step.stepCode)
        {
            _slot = null;
            _land = null;
            _focusedStep = null;
            if (_worldTarget != null) _worldTarget.gameObject.SetActive(false);
        }
        _step = step;
        if (!EnsureHomeMap()) return null;
        if (step.stepCode == "OPEN_HOME_MAP") return null;
        bool freeLand = step.stepCode == "FOCUS_FREE_LAND" || step.stepCode == "PLACE_FARM_LAND";
        bool start = step.stepCode == "START_FARM_BUILD";
        bool complete = step.stepCode == "COMPLETE_FARM_BUILD";
        if (!freeLand && !start && !complete) return null;
        Context context = step.businessContext;
        if (!freeLand && (context == null || context.playerLandId <= 0
            || context.buildingConfigId <= 0 || (complete && context.playerBuildingId <= 0))) return null;
        if (step.stepCode == "PLACE_FARM_LAND")
        {
            if (context == null || context.landConfigId <= 0) return null;
            _land = FindLand(context.landConfigId);
            if (_land == null) return null;
        }
        if (!MatchesSlot(_slot, context, freeLand))
        {
            _slot = null;
            LandSlotController[] slots = FindObjectsOfType<LandSlotController>();
            foreach (LandSlotController candidate in slots)
            {
                if (!MatchesSlot(candidate, context, freeLand)) continue;
                if (_slot == null || candidate.SlotId < _slot.SlotId) _slot = candidate;
            }
        }
        if (_slot == null) return null;
        PlayerPrefs.SetInt("weijun.onboarding.slotId", _slot.SlotId);
        FocusSlotOnce();
        if (!freeLand)
        {
            if (_worldTarget != null) _worldTarget.gameObject.SetActive(false);
            LandInfoPanel panel = LandInfoPanel.Instance;
            return panel == null ? null : panel.ResolveOnboardingTarget(_slot, context.buildingConfigId, complete);
        }
        EnsureWorldTarget();
        _worldTarget.gameObject.SetActive(true);
        _placeButton.interactable = step.stepCode == "PLACE_FARM_LAND" && _land != null && BuildManager.Instance != null;
        UpdateWorldTarget();
        return _worldTarget;
    }

    private bool MatchesSlot(LandSlotController slot, Context context, bool freeLand)
    {
        if (slot == null || !slot.gameObject.activeInHierarchy || slot.gameObject.scene != gameObject.scene) return false;
        if (freeLand)
        {
            if (slot.IsBuilt || !slot.IsPlacementUnlocked) return false;
            return _land == null || (MapManager.Instance != null && MapManager.Instance.CanPlace(slot, _land, out _));
        }
        return context != null && slot.RemotePlayerLandId == context.playerLandId
            && (context.playerBuildingId <= 0 || slot.PlayerBuildingId == context.playerBuildingId)
            && (slot.CurrentBuilding == null || slot.CurrentBuilding.buildingId == context.buildingConfigId);
    }

    private static LandConfig FindLand(long configId)
    {
        BackendGameService service = BackendGameService.Instance;
        if (service == null || service.RemoteAvailableLands == null) return null;
        foreach (LandConfig land in service.RemoteAvailableLands)
            if (land != null && land.landId == configId) return land;
        return null;
    }

    private bool IsHomeMapReady()
    {
        if (!IsSessionCurrent(_playerId, _token)) return false;
        BackendGameService service = BackendGameService.Instance;
        if (_sceneRequestPending || service == null || !service.IsAuthenticated || service.LandScenes == null) return false;
        foreach (LandSceneResponse scene in service.LandScenes)
            if (scene != null && scene.ResolvedSceneId == service.CurrentSceneId
                && string.Equals(scene.ResolvedSceneKey, "HOME", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsSessionCurrent(int playerId, string token)
    {
        BackendGameService service = BackendGameService.Instance;
        return service != null && service.IsAuthenticated && service.PlayerId == playerId
            && string.Equals(service.Token, token, StringComparison.Ordinal);
    }

    private bool EnsureHomeMap()
    {
        if (IsHomeMapReady()) return true;
        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated || _sceneRequestPending || Time.unscaledTime < _nextSceneRequestTime) return false;
        _nextSceneRequestTime = Time.unscaledTime + 5f;
        _sceneRequestPending = true;
        if (service.LandScenes == null)
        {
            service.LoadLandScenes(result => { if (this != null) _sceneRequestPending = false; });
            return false;
        }
        foreach (LandSceneResponse scene in service.LandScenes)
        {
            if (scene == null || !scene.IsEnterable || !string.Equals(scene.ResolvedSceneKey, "HOME", StringComparison.OrdinalIgnoreCase)) continue;
            service.SwitchLandScene(scene.ResolvedSceneId, result =>
            {
                if (this == null) return;
                _sceneRequestPending = false;
                _slot = null;
                if (result == null || !result.Success) Debug.LogWarning("Onboarding HOME map switch failed; waiting to retry.", this);
            });
            return false;
        }
        _sceneRequestPending = false;
        return false;
    }

    private void FocusSlotOnce()
    {
        Camera camera = Camera.main;
        if (camera == null || _slot == null || _focusedStep == _step.stepCode) return;
        Vector3 viewport = camera.WorldToViewportPoint(_slot.CenterWorld);
        if (viewport.z > 0f && viewport.x >= 0.2f && viewport.x <= 0.8f && viewport.y >= 0.2f && viewport.y <= 0.8f)
        {
            _focusedStep = _step.stepCode;
            return;
        }
        camera.transform.position = new Vector3(_slot.CenterWorld.x, _slot.CenterWorld.y, camera.transform.position.z);
        _focusedStep = _step.stepCode;
    }

    private void EnsureWorldTarget()
    {
        if (_worldTarget != null) return;
        GameObject canvas = new GameObject("OnboardingWorldTargetCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        _canvas = canvas.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32759;
        GameObject target = new GameObject("OnboardingLandTarget", typeof(RectTransform), typeof(Image), typeof(Button));
        target.transform.SetParent(canvas.transform, false);
        _worldTarget = target.GetComponent<RectTransform>();
        _worldTarget.anchorMin = _worldTarget.anchorMax = Vector2.zero;
        Image image = target.GetComponent<Image>();
        image.color = Color.clear;
        _placeButton = target.GetComponent<Button>();
        _placeButton.targetGraphic = image;
        _placeButton.transition = Selectable.Transition.None;
        _placeButton.onClick.AddListener(PlaceLand);
    }

    private void PlaceLand()
    {
        if (_step == null || _step.stepCode != "PLACE_FARM_LAND" || !IsHomeMapReady()
            || !MatchesSlot(_slot, _step.businessContext, true) || BuildManager.Instance == null) return;
        BuildManager.Instance.PlaceOnboardingLand(_slot, _land);
    }

    private void LateUpdate()
    {
        if (_worldTarget == null) return;
        bool active = _step != null && PlayerPrefs.GetString("weijun.onboarding.stepCode", string.Empty) == _step.stepCode
            && (_step.stepCode == "PLACE_FARM_LAND" || _step.stepCode == "FOCUS_FREE_LAND") && IsHomeMapReady();
        _worldTarget.gameObject.SetActive(active);
        if (active) UpdateWorldTarget();
    }

    private void UpdateWorldTarget()
    {
        if (_worldTarget == null || _slot == null || Camera.main == null) return;
        Bounds bounds = _slot.Collider != null ? _slot.Collider.bounds : new Bounds(_slot.CenterWorld, Vector3.one);
        Vector3 minimum = Camera.main.WorldToScreenPoint(bounds.min);
        Vector3 maximum = Camera.main.WorldToScreenPoint(bounds.max);
        _worldTarget.anchoredPosition = (minimum + maximum) * 0.5f;
        _worldTarget.sizeDelta = new Vector2(Mathf.Abs(maximum.x - minimum.x), Mathf.Abs(maximum.y - minimum.y));
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    [Serializable]
    private sealed class Step
    {
        public string stepCode;
        public Context businessContext;
    }

    [Serializable]
    private sealed class Context
    {
        public long landConfigId;
        public long buildingConfigId;
        public long playerLandId;
        public long playerBuildingId;
    }
}
