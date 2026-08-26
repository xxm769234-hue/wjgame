using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BackendGameService
{
    private const string SceneIdKey = "weijun.backend.sceneId";
    private readonly Dictionary<string, LandSlotStateResponse> purificationSnapshots = new Dictionary<string, LandSlotStateResponse>();

    public LandSceneResponse[] LandScenes { get; private set; }
    public int CurrentSceneId { get; private set; }
    public long CurrentSceneConfigVersion { get; private set; }
    public long CurrentLandPolicyRevision { get; private set; }
    public IdleAdventureStaminaResponse CurrentStamina { get; private set; }
    public ProductionWorkStateResponse CurrentWorkState { get; private set; }

    public Coroutine LoadLandScenes(Action<BackendResult<LandSceneResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<LandSceneResponse[]>("Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadLandScenesRoutine(onComplete));
    }

    public Coroutine SwitchLandScene(int sceneId, Action<BackendResult<SceneSwitchResponse>> onComplete = null)
    {
        if (!IsAuthenticated || sceneId <= 0)
        {
            onComplete?.Invoke(Failed<SceneSwitchResponse>("Backend session or scene id is missing."));
            return null;
        }

        return StartCoroutine(SwitchLandSceneRoutine(sceneId, onComplete));
    }

    public Coroutine LoadStamina(Action<BackendResult<IdleAdventureStaminaResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureStaminaResponse>("Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadStaminaRoutine(onComplete));
    }

    public Coroutine AccelerateIdleAdventure(Action<BackendResult<IdleAdventureAccelerateResponse>> onComplete = null)
    {
        return AccelerateIdleAdventure(Guid.NewGuid().ToString("D"), onComplete);
    }

    public Coroutine AccelerateIdleAdventure(string idempotencyKey, Action<BackendResult<IdleAdventureAccelerateResponse>> onComplete = null)
    {
        if (!IsAuthenticated || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<IdleAdventureAccelerateResponse>("Backend session or a valid idempotency key is missing."));
            return null;
        }

        return StartCoroutine(AccelerateIdleAdventureRoutine(idempotencyKey, onComplete));
    }

    public Coroutine ClearIdleAdventureStage(
        string sessionKey,
        int clearedStage,
        string idempotencyKey,
        Action<BackendResult<IdleAdventureStageClearResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey) || clearedStage <= 0 || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<IdleAdventureStageClearResponse>("Backend session, stage, session key, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(ClearIdleAdventureStageRoutine(sessionKey, clearedStage, idempotencyKey, onComplete));
    }

    public Coroutine LoadProductionWorkState(Action<BackendResult<ProductionWorkStateResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<ProductionWorkStateResponse>("Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadProductionWorkStateRoutine(onComplete));
    }

    public Coroutine PreviewProductionWorkTime(
        ProductionWorkTimeRequest request,
        Action<BackendResult<ProductionWorkTimePreviewResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null)
        {
            onComplete?.Invoke(Failed<ProductionWorkTimePreviewResponse>("Backend session or work-time request is missing."));
            return null;
        }

        return StartCoroutine(PreviewProductionWorkTimeRoutine(request, onComplete));
    }

    public Coroutine ConsumeProductionWorkTime(
        ProductionWorkTimeRequest request,
        string idempotencyKey,
        Action<BackendResult<ProductionWorkTimeConsumeResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Backend session, request, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(ConsumeProductionWorkTimeRoutine(request, idempotencyKey, onComplete));
    }

    public Coroutine LoadProductionRecipes(
        int playerBuildingId,
        Action<BackendResult<ProductionRecipesResponse>> onComplete = null)
    {
        if (!IsAuthenticated || playerBuildingId <= 0)
        {
            onComplete?.Invoke(Failed<ProductionRecipesResponse>("Backend session or player building id is missing."));
            return null;
        }

        return StartCoroutine(LoadProductionRecipesRoutine(playerBuildingId, onComplete));
    }

    public Coroutine SwitchProductionRecipe(
        ProductionRecipeSwitchRequest request,
        string idempotencyKey,
        Action<BackendResult<ProductionRecipeSwitchResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null || request.playerBuildingId <= 0 || request.recipeId <= 0 || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Backend session, recipe request, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(SwitchProductionRecipeRoutine(request, idempotencyKey, onComplete));
    }

    public Coroutine PreviewNpcAutoUpgrade(
        long playerNpcId,
        int targetLevel,
        Action<BackendResult<AutoUpgradePreviewResponse>> onComplete = null)
    {
        if (!IsAuthenticated || playerNpcId <= 0 || targetLevel <= 0)
        {
            onComplete?.Invoke(Failed<AutoUpgradePreviewResponse>("Backend session, NPC id, or target level is missing."));
            return null;
        }

        return StartCoroutine(PreviewNpcAutoUpgradeRoutine(playerNpcId, targetLevel, onComplete));
    }

    public Coroutine AutoUpgradeNpc(
        AutoUpgradeRequest request,
        string idempotencyKey,
        Action<BackendResult<AutoUpgradeResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null || request.playerNpcId <= 0 || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<AutoUpgradeResponse>("Backend session, NPC request, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(AutoUpgradeRoutine("npc/auto-upgrade", request, idempotencyKey, onComplete));
    }

    public Coroutine PreviewEquipmentAutoUpgrade(
        long playerEquipmentId,
        int targetLevel,
        Action<BackendResult<AutoUpgradePreviewResponse>> onComplete = null)
    {
        if (!IsAuthenticated || playerEquipmentId <= 0 || targetLevel <= 0)
        {
            onComplete?.Invoke(Failed<AutoUpgradePreviewResponse>("Backend session, equipment id, or target level is missing."));
            return null;
        }

        return StartCoroutine(PreviewEquipmentAutoUpgradeRoutine(playerEquipmentId, targetLevel, onComplete));
    }

    public Coroutine AutoUpgradeEquipment(
        AutoUpgradeRequest request,
        string idempotencyKey,
        Action<BackendResult<AutoUpgradeResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null || request.playerEquipmentId <= 0 || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<AutoUpgradeResponse>("Backend session, equipment request, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(AutoUpgradeRoutine("equipment/auto-upgrade", request, idempotencyKey, onComplete));
    }

    public Coroutine LoadOnboarding(Action<BackendResult<OnboardingCurrentResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<OnboardingCurrentResponse>("Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadOnboardingRoutine(onComplete));
    }

    public Coroutine AcknowledgeOnboardingStep(
        string stepCode,
        OnboardingMutationRequest request,
        string idempotencyKey,
        Action<BackendResult<OnboardingMutationResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(stepCode) || request == null || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<OnboardingMutationResponse>("Backend session, step, request, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(MutateOnboardingRoutine("onboarding/steps/" + Uri.EscapeDataString(stepCode) + "/ack", request, idempotencyKey, onComplete));
    }

    public Coroutine SkipOnboarding(
        OnboardingMutationRequest request,
        string idempotencyKey,
        Action<BackendResult<OnboardingMutationResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<OnboardingMutationResponse>("Backend session, request, or idempotency key is missing."));
            return null;
        }

        return StartCoroutine(MutateOnboardingRoutine("onboarding/skip", request, idempotencyKey, onComplete));
    }

    private IEnumerator LoadLandScenesRoutine(Action<BackendResult<LandSceneResponse[]>> onComplete)
    {
        BackendResult<LandSceneResponse[]> result = null;
        yield return BackendApiClient.Get<LandSceneResponse[]>(baseUrl, Token, $"land/scenes/list/{PlayerId}", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            LandScenes = result.Data;
            LandSceneResponse selected = FindCurrentScene(LandScenes);
            if (selected != null)
            {
                CurrentSceneId = selected.ResolvedSceneId;
                CurrentSceneConfigVersion = selected.ResolvedConfigVersion;
                CurrentLandPolicyRevision = selected.ResolvedPolicyRevision;
                PlayerPrefs.SetInt(SceneIdKey, CurrentSceneId);
                PlayerPrefs.Save();
            }
        }
        else
        {
            Debug.LogWarning("Backend scene list failed: " + ResultMessage(result), this);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator SwitchLandSceneRoutine(int sceneId, Action<BackendResult<SceneSwitchResponse>> onComplete)
    {
        SceneSwitchRequest request = new SceneSwitchRequest { playerId = PlayerId, sceneId = sceneId };
        BackendResult<SceneSwitchResponse> result = null;
        yield return BackendApiClient.Post<SceneSwitchResponse>(baseUrl, Token, "land/scenes/switch", request, Guid.NewGuid().ToString("D"), value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            CurrentSceneId = result.Data.sceneId > 0 ? result.Data.sceneId : sceneId;
            CurrentSceneConfigVersion = Math.Max(0L, result.Data.configVersion);
            CurrentLandPolicyRevision = Math.Max(0L, result.Data.ResolvedPolicyRevision);
            PlayerPrefs.SetInt(SceneIdKey, CurrentSceneId);
            PlayerPrefs.Save();
            yield return SyncMapRoutine(null);
            yield return RefreshProductionsRoutine(null);
            yield return LoadLandScenesRoutine(null);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator LoadStaminaRoutine(Action<BackendResult<IdleAdventureStaminaResponse>> onComplete)
    {
        BackendResult<IdleAdventureStaminaResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureStaminaResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/stamina", value => result = value);
        if (result != null && result.Success && result.Data != null)
        {
            ApplyAuthoritativeStamina(result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator AccelerateIdleAdventureRoutine(string idempotencyKey, Action<BackendResult<IdleAdventureAccelerateResponse>> onComplete)
    {
        BackendResult<IdleAdventureAccelerateResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureAccelerateResponse>(
            baseUrl,
            Token,
            $"idle-adventure/players/{PlayerId}/afk-reward/accelerate",
            null,
            idempotencyKey,
            value => result = value);
        ApplyAccelerateResponse(result != null ? result.Data : null);
        onComplete?.Invoke(result);
    }

    private IEnumerator ClearIdleAdventureStageRoutine(
        string sessionKey,
        int clearedStage,
        string idempotencyKey,
        Action<BackendResult<IdleAdventureStageClearResponse>> onComplete)
    {
        IdleAdventureStageClearRequest request = new IdleAdventureStageClearRequest
        {
            session_key = sessionKey,
            cleared_stage = clearedStage
        };
        BackendResult<IdleAdventureStageClearResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureStageClearResponse>(
            baseUrl,
            Token,
            $"idle-adventure/players/{PlayerId}/progress/stage-clear",
            request,
            idempotencyKey,
            value => result = value);
        ApplyStageClearResponse(result != null ? result.Data : null);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadProductionWorkStateRoutine(Action<BackendResult<ProductionWorkStateResponse>> onComplete)
    {
        BackendResult<ProductionWorkStateResponse> result = null;
        yield return BackendApiClient.Get<ProductionWorkStateResponse>(baseUrl, Token, "production/work-state", value => result = value);
        if (result != null && result.Success) CurrentWorkState = result.Data;
        onComplete?.Invoke(result);
    }

    private IEnumerator PreviewProductionWorkTimeRoutine(
        ProductionWorkTimeRequest request,
        Action<BackendResult<ProductionWorkTimePreviewResponse>> onComplete)
    {
        request.sceneId = CurrentSceneId;
        BackendResult<ProductionWorkTimePreviewResponse> result = null;
        yield return BackendApiClient.Post<ProductionWorkTimePreviewResponse>(
            baseUrl,
            Token,
            "production/work-time/preview",
            request,
            string.Empty,
            value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator ConsumeProductionWorkTimeRoutine(
        ProductionWorkTimeRequest request,
        string idempotencyKey,
        Action<BackendResult<ProductionWorkTimeConsumeResponse>> onComplete)
    {
        request.sceneId = CurrentSceneId;
        BackendResult<ProductionWorkTimeConsumeResponse> result = null;
        yield return BackendApiClient.Post<ProductionWorkTimeConsumeResponse>(
            baseUrl,
            Token,
            "production/work-time/consume",
            request,
            idempotencyKey,
            value => result = value);
        if (result != null && result.Success && result.Data != null) CurrentWorkState = result.Data.workState;
        if (result != null && (result.Code == 7803 || result.Code == 7805 || result.Code == 7809)) yield return LoadProductionWorkStateRoutine(null);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadProductionRecipesRoutine(
        int playerBuildingId,
        Action<BackendResult<ProductionRecipesResponse>> onComplete)
    {
        BackendResult<ProductionRecipesResponse> result = null;
        yield return BackendApiClient.Get<ProductionRecipesResponse>(baseUrl, Token, AddSceneQuery($"production/recipes/{playerBuildingId}"), value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator SwitchProductionRecipeRoutine(
        ProductionRecipeSwitchRequest request,
        string idempotencyKey,
        Action<BackendResult<ProductionRecipeSwitchResponse>> onComplete)
    {
        request.sceneId = CurrentSceneId;
        BackendResult<ProductionRecipeSwitchResponse> result = null;
        yield return BackendApiClient.Post<ProductionRecipeSwitchResponse>(baseUrl, Token, "production/recipes/switch", request, idempotencyKey, value => result = value);
        if (result != null && result.Success && result.Data != null && MapManager.Instance != null)
        {
            MapManager.Instance.ApplyRemoteProductions(result.Data.productions);
        }
        if (result != null && (result.Code == 7811 || result.Code == 7812))
        {
            yield return LoadProductionRecipesRoutine(request.playerBuildingId, null);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator PreviewNpcAutoUpgradeRoutine(
        long playerNpcId,
        int targetLevel,
        Action<BackendResult<AutoUpgradePreviewResponse>> onComplete)
    {
        BackendResult<AutoUpgradePreviewResponse> result = null;
        yield return BackendApiClient.Get<AutoUpgradePreviewResponse>(baseUrl, Token, $"npc/{playerNpcId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator PreviewEquipmentAutoUpgradeRoutine(
        long playerEquipmentId,
        int targetLevel,
        Action<BackendResult<AutoUpgradePreviewResponse>> onComplete)
    {
        BackendResult<AutoUpgradePreviewResponse> result = null;
        yield return BackendApiClient.Get<AutoUpgradePreviewResponse>(baseUrl, Token, $"equipment/{playerEquipmentId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator AutoUpgradeRoutine(
        string path,
        AutoUpgradeRequest request,
        string idempotencyKey,
        Action<BackendResult<AutoUpgradeResponse>> onComplete)
    {
        BackendResult<AutoUpgradeResponse> result = null;
        yield return BackendApiClient.Post<AutoUpgradeResponse>(baseUrl, Token, path, request, idempotencyKey, value => result = value);
        if (result != null && result.Success && result.Data != null)
        {
            if (result.Data.items != null) CurrentInventory = result.Data.items;
            if (result.Data.npcs != null) PlayerNpcs = result.Data.npcs;
            if (CurrentUser == null) CurrentUser = new UserResponse();
            CurrentUser.items = CurrentInventory;
        }
        if (result != null && (result.Code == 6106 || result.Code == 6108 || result.Code == 6207 || result.Code == 6209))
        {
            yield return RequestUserRoutine(null);
            yield return RequestInventoryRoutine(null);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadOnboardingRoutine(Action<BackendResult<OnboardingCurrentResponse>> onComplete)
    {
        BackendResult<OnboardingCurrentResponse> result = null;
        yield return BackendApiClient.Get<OnboardingCurrentResponse>(baseUrl, Token, "onboarding/current", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator MutateOnboardingRoutine(
        string path,
        OnboardingMutationRequest request,
        string idempotencyKey,
        Action<BackendResult<OnboardingMutationResponse>> onComplete)
    {
        BackendResult<OnboardingMutationResponse> result = null;
        yield return BackendApiClient.Post<OnboardingMutationResponse>(baseUrl, Token, path, request, idempotencyKey, value => result = value);
        onComplete?.Invoke(result);
    }

    private void ApplyAccelerateResponse(IdleAdventureAccelerateResponse response)
    {
        if (response == null) return;
        if (response.stamina != null) ApplyAuthoritativeStamina(response.stamina);
        if (response.items != null)
        {
            CurrentInventory = ConvertIdleAdventureInventory(response.items);
            if (CurrentUser == null) CurrentUser = new UserResponse();
            CurrentUser.items = CurrentInventory;
        }

        if (response.highest_stage > 0)
        {
            PlayerPrefs.SetInt("weijun.idleAdventure.highestClearedStage", Mathf.Max(0, response.highest_stage));
            PlayerPrefs.Save();
        }

        if (response.progress != null && response.progress.highest_stage > 0)
        {
            PlayerPrefs.SetInt(
                "weijun.idleAdventure.highestClearedStage",
                Mathf.Max(0, response.progress.highest_stage));
            PlayerPrefs.Save();
        }
    }

    private void ApplyStageClearResponse(IdleAdventureStageClearResponse response)
    {
        if (response == null) return;
        if (response.stamina != null) ApplyAuthoritativeStamina(response.stamina);
        if (response.items != null)
        {
            CurrentInventory = ConvertIdleAdventureInventory(response.items);
            if (CurrentUser == null) CurrentUser = new UserResponse();
            CurrentUser.items = CurrentInventory;
        }
    }

    private LandSceneResponse FindCurrentScene(LandSceneResponse[] scenes)
    {
        int savedSceneId = PlayerPrefs.GetInt(SceneIdKey, 0);
        LandSceneResponse firstEnterable = null;
        if (scenes != null)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                LandSceneResponse scene = scenes[i];
                if (scene == null) continue;
                if (scene.ResolvedSceneId == savedSceneId && scene.IsEnterable) return scene;
                if (scene.current && scene.IsEnterable) firstEnterable = scene;
                if (firstEnterable == null && scene.IsEnterable) firstEnterable = scene;
            }
        }
        return firstEnterable;
    }

    private string AddSceneQuery(string path)
    {
        if (CurrentSceneId <= 0) return path;
        return path + (path.Contains("?") ? "&" : "?") + "sceneId=" + CurrentSceneId;
    }

    private LandSlotStateResponse FindPurificationSnapshot(int positionX, int positionY)
    {
        purificationSnapshots.TryGetValue(BuildPurificationKey(positionX, positionY), out LandSlotStateResponse state);
        return state;
    }

    private static string BuildPurificationKey(int positionX, int positionY)
    {
        return positionX + ":" + positionY;
    }

    private static BackendResult<T> Failed<T>(string message)
    {
        return new BackendResult<T>(false, 0, 0, message, default);
    }

    private void ApplyAuthoritativeStamina(IdleAdventureStaminaResponse stamina)
    {
        if (stamina == null) return;
        CurrentStamina = stamina;
        PlayerPrefs.SetInt("PlayerStamina", Mathf.Clamp((int)Math.Min(int.MaxValue, stamina.CurrentStamina), 0, int.MaxValue));
        PlayerPrefs.Save();
    }
}
