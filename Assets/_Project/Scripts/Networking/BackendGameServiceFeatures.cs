using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BackendGameService
{
    private const string SceneIdKey = "weijun.backend.sceneId";
    private const string ProductionWorkTimeKeyPrefix = "weijun.backend.production.workTime.";
    private const string ProductionWorkSnapshotKeyPrefix = "weijun.backend.production.workSnapshot.";
    private const string ProductionRecipeSwitchKeyPrefix = "weijun.backend.production.recipeSwitch.";
    private const string ProductionRecipeSnapshotKeyPrefix = "weijun.backend.production.recipeSnapshot.";
    private readonly Dictionary<string, LandSlotStateResponse> purificationSnapshots = new Dictionary<string, LandSlotStateResponse>();
    private float nextProductionWorkForegroundRefreshAt;

    public LandSceneResponse[] LandScenes { get; private set; }
    public int CurrentSceneId { get; private set; }
    public long CurrentSceneConfigVersion { get; private set; }
    public long CurrentLandPolicyRevision { get; private set; }
    public IdleAdventureStaminaResponse CurrentStamina { get; private set; }
    public IdleAdventureAfkRewardStateResponse CurrentIdleAdventureAfkReward { get; private set; }
    public ProductionWorkStateResponse CurrentWorkState { get; private set; }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) RefreshProductionWorkStateAfterForeground();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) RefreshProductionWorkStateAfterForeground();
    }

    private void RefreshProductionWorkStateAfterForeground()
    {
        if (!IsAuthenticated || Time.unscaledTime < nextProductionWorkForegroundRefreshAt) return;
        nextProductionWorkForegroundRefreshAt = Time.unscaledTime + 1f;
        LoadProductionWorkState();
    }

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
        if (sceneSwitchPending)
        {
            onComplete?.Invoke(Failed<SceneSwitchResponse>("A land scene switch is already pending."));
            return null;
        }

        sceneSwitchPending = true;
        int requestSceneGeneration = AdvanceSceneRequestGeneration();
        return StartCoroutine(SwitchLandSceneRoutine(sceneId, requestSceneGeneration, onComplete));
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
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureAccelerateResponse>("Backend session is not authenticated."));
            return null;
        }
        string storageKey = AfkAccelerateKeyPrefix + PlayerId;
        string snapshotToken = GetOrCreateIdleAdventureAfkSnapshotToken();
        string key = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return AccelerateIdleAdventure(key, onComplete);
    }

    public Coroutine AccelerateIdleAdventure(string idempotencyKey, Action<BackendResult<IdleAdventureAccelerateResponse>> onComplete = null)
    {
        if (!IsAuthenticated || !Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(Failed<IdleAdventureAccelerateResponse>("Backend session or a valid idempotency key is missing."));
            return null;
        }

        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = AfkAccelerateKeyPrefix + session.PlayerId;
        SavePersistedIdempotencyKey(
            storageKey,
            idempotencyKey,
            GetOrCreateIdleAdventureAfkSnapshotToken());
        return StartCoroutine(AccelerateIdleAdventureRoutine(idempotencyKey, storageKey, session, onComplete));
    }

    public Coroutine ClearIdleAdventureStage(
        string sessionKey,
        int clearedStage,
        Action<BackendResult<IdleAdventureStageClearResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(sessionKey) || clearedStage <= 0)
        {
            onComplete?.Invoke(Failed<IdleAdventureStageClearResponse>("Backend session, stage, or session key is missing."));
            return null;
        }

        string storageKey = BuildAfkStageClearStorageKey(sessionKey, clearedStage);
        string persistedKey = GetOrCreatePersistedIdempotencyKey(storageKey);
        return ClearIdleAdventureStage(sessionKey, clearedStage, persistedKey, onComplete);
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = BuildAfkStageClearStorageKey(sessionKey, clearedStage);
        SavePersistedIdempotencyKey(storageKey, idempotencyKey);
        return StartCoroutine(ClearIdleAdventureStageRoutine(sessionKey, clearedStage, idempotencyKey, storageKey, session, onComplete));
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
        Action<BackendResult<ProductionWorkTimeConsumeResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null)
        {
            onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Backend session or request is missing."));
            return null;
        }

        string storageKey = BuildProductionWorkTimeStorageKey(request);
        string snapshotToken = GetOrCreateProductionWorkSnapshotToken(request);
        string idempotencyKey = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return ConsumeProductionWorkTime(request, idempotencyKey, onComplete);
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

        string storageKey = BuildProductionWorkTimeStorageKey(request);
        SessionRequestSnapshot session = CaptureSessionRequest();
        SavePersistedIdempotencyKey(
            storageKey,
            idempotencyKey,
            GetOrCreateProductionWorkSnapshotToken(request));
        return StartCoroutine(ConsumeProductionWorkTimeRoutine(request, idempotencyKey, storageKey, session, onComplete));
    }

    public Coroutine LoadProductionRecipes(
        long playerBuildingId,
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
        Action<BackendResult<ProductionRecipeSwitchResponse>> onComplete = null)
    {
        if (!IsAuthenticated || request == null || request.playerBuildingId <= 0 || request.recipeId <= 0)
        {
            onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Backend session or recipe request is missing."));
            return null;
        }

        string storageKey = BuildProductionRecipeSwitchStorageKey(request);
        string snapshotToken = GetOrCreateProductionRecipeSnapshotToken(request.playerBuildingId);
        string idempotencyKey = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return SwitchProductionRecipe(request, idempotencyKey, onComplete);
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

        string storageKey = BuildProductionRecipeSwitchStorageKey(request);
        SessionRequestSnapshot session = CaptureSessionRequest();
        SavePersistedIdempotencyKey(
            storageKey,
            idempotencyKey,
            GetOrCreateProductionRecipeSnapshotToken(request.playerBuildingId));
        return StartCoroutine(SwitchProductionRecipeRoutine(request, idempotencyKey, storageKey, session, onComplete));
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(PreviewNpcAutoUpgradeRoutine(playerNpcId, targetLevel, session, onComplete));
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(AutoUpgradeRoutine("npc/auto-upgrade", request, idempotencyKey, session, onComplete));
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(PreviewEquipmentAutoUpgradeRoutine(playerEquipmentId, targetLevel, session, onComplete));
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(AutoUpgradeRoutine("equipment/auto-upgrade", request, idempotencyKey, session, onComplete));
    }

    public Coroutine LoadOnboarding(Action<BackendResult<OnboardingCurrentResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<OnboardingCurrentResponse>("Backend session is not authenticated."));
            return null;
        }

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(LoadOnboardingRoutine(session, onComplete));
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(MutateOnboardingRoutine("onboarding/steps/" + Uri.EscapeDataString(stepCode) + "/ack", request, idempotencyKey, session, onComplete));
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

        SessionRequestSnapshot session = CaptureSessionRequest();
        return StartCoroutine(MutateOnboardingRoutine("onboarding/skip", request, idempotencyKey, session, onComplete));
    }

    private IEnumerator LoadLandScenesRoutine(Action<BackendResult<LandSceneResponse[]>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        int requestSceneGeneration = sceneRequestGeneration;
        BackendResult<LandSceneResponse[]> result = null;
        yield return BackendApiClient.Get<LandSceneResponse[]>(baseUrl, session.Token, $"land/scenes/list/{session.PlayerId}", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<LandSceneResponse[]>("Backend session changed while land scenes were loading."));
            yield break;
        }
        if (!IsSceneRequestGenerationCurrent(requestSceneGeneration))
        {
            onComplete?.Invoke(Failed<LandSceneResponse[]>("Land scene response was superseded by a newer scene request."));
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            LandScenes = result.Data;
            LandSceneResponse selected = FindCurrentScene(LandScenes);
            if (selected != null)
            {
                int resolvedSceneId = selected.ResolvedSceneId;
                if (resolvedSceneId != CurrentSceneId)
                {
                    CurrentSceneId = resolvedSceneId;
                    AdvanceSceneRequestGeneration();
                }
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

    private IEnumerator SwitchLandSceneRoutine(
        int sceneId,
        int requestSceneGeneration,
        Action<BackendResult<SceneSwitchResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        SceneSwitchRequest request = new SceneSwitchRequest { playerId = session.PlayerId, sceneId = sceneId };
        BackendResult<SceneSwitchResponse> result = null;
        yield return BackendApiClient.Post<SceneSwitchResponse>(baseUrl, session.Token, "land/scenes/switch", request, Guid.NewGuid().ToString("D"), value => result = value);
        if (IsSessionRequestCurrent(session)) sceneSwitchPending = false;
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<SceneSwitchResponse>("Backend session changed while the land scene was switching."));
            yield break;
        }
        if (!IsSceneRequestGenerationCurrent(requestSceneGeneration))
        {
            onComplete?.Invoke(Failed<SceneSwitchResponse>("Land scene switch was superseded by a newer request."));
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            CurrentSceneId = result.Data.sceneId > 0 ? result.Data.sceneId : sceneId;
            int appliedSceneId = CurrentSceneId;
            int appliedSceneGeneration = AdvanceSceneRequestGeneration();
            CurrentSceneConfigVersion = Math.Max(0L, result.Data.configVersion);
            CurrentLandPolicyRevision = Math.Max(0L, result.Data.ResolvedPolicyRevision);
            PlayerPrefs.SetInt(SceneIdKey, CurrentSceneId);
            PlayerPrefs.Save();
            yield return SyncMapRoutine(null);
            if (!IsSessionRequestCurrent(session)
                || !IsSceneRequestCurrent(appliedSceneId, appliedSceneGeneration))
            {
                onComplete?.Invoke(Failed<SceneSwitchResponse>("Backend session changed while the switched scene map was syncing."));
                yield break;
            }
            yield return RefreshProductionsRoutine(null);
            if (!IsSessionRequestCurrent(session)
                || !IsSceneRequestCurrent(appliedSceneId, appliedSceneGeneration))
            {
                onComplete?.Invoke(Failed<SceneSwitchResponse>("Backend session changed while switched-scene productions were refreshing."));
                yield break;
            }
            yield return LoadLandScenesRoutine(null);
            if (!IsSessionRequestCurrent(session)
                || !IsSceneRequestCurrent(appliedSceneId, appliedSceneGeneration))
            {
                onComplete?.Invoke(Failed<SceneSwitchResponse>("Backend session changed while switched-scene metadata was refreshing."));
                yield break;
            }
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator LoadStaminaRoutine(Action<BackendResult<IdleAdventureStaminaResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureStaminaResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureStaminaResponse>(baseUrl, session.Token, $"idle-adventure/players/{session.PlayerId}/stamina", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<IdleAdventureStaminaResponse>("Backend session changed while stamina was loading."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            ApplyAuthoritativeStamina(result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator AccelerateIdleAdventureRoutine(
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureAccelerateResponse>> onComplete)
    {
        BackendResult<IdleAdventureAccelerateResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureAccelerateResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/afk-reward/accelerate",
            null,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<IdleAdventureAccelerateResponse>("Backend session changed while AFK acceleration was running."));
            yield break;
        }
        if (result != null
            && result.Success
            && result.Data != null
            && !OwnsPersistedIdempotencyKey(storageKey, idempotencyKey))
        {
            onComplete?.Invoke(Failed<IdleAdventureAccelerateResponse>("AFK acceleration response was superseded by a newer request."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            ApplyAccelerateResponse(result.Data);
            yield return RefreshUserAndInventoryAfterMutation();

            BackendResult<IdleAdventureStaminaResponse> staminaResult = null;
            yield return LoadStaminaRoutine(value => staminaResult = value);
            if (staminaResult == null || !staminaResult.Success || staminaResult.Data == null)
            {
                Debug.LogWarning("AFK acceleration succeeded, but the authoritative stamina refresh failed.", this);
            }
        }
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<IdleAdventureAccelerateResponse>("Backend session changed while AFK acceleration was refreshing."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            DeletePersistedIdempotencyKey(storageKey, idempotencyKey);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator ClearIdleAdventureStageRoutine(
        string sessionKey,
        int clearedStage,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
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
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/progress/stage-clear",
            request,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<IdleAdventureStageClearResponse>("Backend session changed while stage progress was updating."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(Failed<IdleAdventureStageClearResponse>("Stage-clear response was superseded by a newer request."));
                yield break;
            }
            ApplyStageClearResponse(result.Data);
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<IdleAdventureStageClearResponse>("Backend session changed while stage rewards were refreshing."));
                yield break;
            }
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadProductionWorkStateRoutine(Action<BackendResult<ProductionWorkStateResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<ProductionWorkStateResponse> result = null;
        yield return BackendApiClient.Get<ProductionWorkStateResponse>(baseUrl, session.Token, "production/work-state", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<ProductionWorkStateResponse>("Backend session changed while production work state was loading."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            CurrentWorkState = result.Data;
            RememberProductionWorkStateSnapshots(session.PlayerId, result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator PreviewProductionWorkTimeRoutine(
        ProductionWorkTimeRequest request,
        Action<BackendResult<ProductionWorkTimePreviewResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<ProductionWorkTimePreviewResponse> result = null;
        yield return BackendApiClient.Post<ProductionWorkTimePreviewResponse>(
            baseUrl,
            session.Token,
            "production/work-time/preview",
            request,
            string.Empty,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<ProductionWorkTimePreviewResponse>("Backend session changed while production work time was previewing."));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator ConsumeProductionWorkTimeRoutine(
        ProductionWorkTimeRequest request,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<ProductionWorkTimeConsumeResponse>> onComplete)
    {
        BackendResult<ProductionWorkTimeConsumeResponse> result = null;
        yield return BackendApiClient.Post<ProductionWorkTimeConsumeResponse>(
            baseUrl,
            session.Token,
            "production/work-time/consume",
            request,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Backend session changed while production work time was being consumed."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null && result.Data.workState != null)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Production work-time response was superseded by a newer request."));
                yield break;
            }
            CurrentWorkState = result.Data.workState;
            RememberProductionWorkStateSnapshots(session.PlayerId, result.Data.workState);
        }
        if (result != null && result.Success)
        {
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Backend session changed while production resources were refreshing."));
                yield break;
            }
        }
        if (result != null && result.Code == 7805)
        {
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Backend session changed while production resources were reconciling."));
                yield break;
            }
        }
        if (result != null && (result.Code == 7801
            || result.Code == 7802
            || result.Code == 7803
            || result.Code == 7805
            || result.Code == 7806
            || result.Code == 7807
            || result.Code == 7809))
        {
            yield return LoadProductionWorkStateRoutine(null);
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<ProductionWorkTimeConsumeResponse>("Backend session changed while production work state was reconciling."));
                yield break;
            }
        }
        if (result != null && result.Code == 7808)
        {
            DeletePersistedIdempotencyKey(storageKey, idempotencyKey);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadProductionRecipesRoutine(
        long playerBuildingId,
        Action<BackendResult<ProductionRecipesResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<ProductionRecipesResponse> result = null;
        yield return BackendApiClient.Get<ProductionRecipesResponse>(baseUrl, session.Token, $"production/recipes/{playerBuildingId}", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<ProductionRecipesResponse>("Backend session changed while production recipes were loading."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            RememberProductionRecipeSnapshot(session.PlayerId, result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator SwitchProductionRecipeRoutine(
        ProductionRecipeSwitchRequest request,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<ProductionRecipeSwitchResponse>> onComplete)
    {
        int sceneId = CurrentSceneId;
        int requestSceneGeneration = sceneRequestGeneration;
        BackendResult<ProductionRecipeSwitchResponse> result = null;
        yield return BackendApiClient.Post<ProductionRecipeSwitchResponse>(baseUrl, session.Token, "production/recipes/switch", request, idempotencyKey, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Backend session changed while the production recipe was switching."));
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Production recipe response was superseded by a newer scene request."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            if (result.Data.activeRecipe == null)
            {
                onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Production recipe response did not include the active recipe."));
                yield break;
            }
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Production recipe response was superseded by a newer request."));
                yield break;
            }
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForBuilding(
                    request.playerBuildingId,
                    result.Data.productions ?? new ProductionResponse[0]);
            }
            RotateProductionRecipeSnapshotToken(session.PlayerId, request.playerBuildingId);
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session)
                || !IsSceneRequestCurrent(sceneId, requestSceneGeneration))
            {
                onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Production recipe response was superseded while resources were refreshing."));
                yield break;
            }
        }
        if (result != null && (result.Code == 7810 || result.Code == 7811 || result.Code == 7812))
        {
            yield return LoadProductionRecipesRoutine(request.playerBuildingId, null);
            if (!IsSessionRequestCurrent(session)
                || !IsSceneRequestCurrent(sceneId, requestSceneGeneration))
            {
                onComplete?.Invoke(Failed<ProductionRecipeSwitchResponse>("Production recipe response was superseded while recipes were reconciling."));
                yield break;
            }
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator PreviewNpcAutoUpgradeRoutine(
        long playerNpcId,
        int targetLevel,
        SessionRequestSnapshot session,
        Action<BackendResult<AutoUpgradePreviewResponse>> onComplete)
    {
        BackendResult<AutoUpgradePreviewResponse> result = null;
        yield return BackendApiClient.Get<AutoUpgradePreviewResponse>(baseUrl, session.Token, $"npc/{playerNpcId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<AutoUpgradePreviewResponse>("Backend session changed while the NPC auto-upgrade preview was loading."));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator PreviewEquipmentAutoUpgradeRoutine(
        long playerEquipmentId,
        int targetLevel,
        SessionRequestSnapshot session,
        Action<BackendResult<AutoUpgradePreviewResponse>> onComplete)
    {
        BackendResult<AutoUpgradePreviewResponse> result = null;
        yield return BackendApiClient.Get<AutoUpgradePreviewResponse>(baseUrl, session.Token, $"equipment/{playerEquipmentId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<AutoUpgradePreviewResponse>("Backend session changed while the equipment auto-upgrade preview was loading."));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator AutoUpgradeRoutine(
        string path,
        AutoUpgradeRequest request,
        string idempotencyKey,
        SessionRequestSnapshot session,
        Action<BackendResult<AutoUpgradeResponse>> onComplete)
    {
        BackendResult<AutoUpgradeResponse> result = null;
        yield return BackendApiClient.Post<AutoUpgradeResponse>(baseUrl, session.Token, path, request, idempotencyKey, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<AutoUpgradeResponse>("Backend session changed while auto-upgrade was executing."));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            if (result.Data.items != null) ApplyInventorySnapshot(result.Data.items);
            if (result.Data.npcs != null) PlayerNpcs = result.Data.npcs;
            if (result.Data.remainingGold >= 0L)
            {
                if (CurrentUser == null) CurrentUser = new UserResponse();
                CurrentUser.gold = ToSafeInt(Math.Max(0L, result.Data.remainingGold));
                SetResourcePreference("Gold", CurrentUser.gold);
                PlayerPrefs.Save();
            }
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<AutoUpgradeResponse>("Backend session changed while auto-upgrade resources were refreshing."));
                yield break;
            }
        }
        if (result != null && (result.Code == 6106 || result.Code == 6108 || result.Code == 6207 || result.Code == 6209))
        {
            yield return RequestUserRoutine(null);
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<AutoUpgradeResponse>("Backend session changed while auto-upgrade state was reconciling."));
                yield break;
            }
            yield return RequestInventoryRoutine(null);
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(Failed<AutoUpgradeResponse>("Backend session changed while auto-upgrade inventory was reconciling."));
                yield break;
            }
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadOnboardingRoutine(
        SessionRequestSnapshot session,
        Action<BackendResult<OnboardingCurrentResponse>> onComplete)
    {
        BackendResult<OnboardingCurrentResponse> result = null;
        yield return BackendApiClient.Get<OnboardingCurrentResponse>(baseUrl, session.Token, "onboarding/current", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<OnboardingCurrentResponse>("Backend session changed while onboarding was loading."));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator MutateOnboardingRoutine(
        string path,
        OnboardingMutationRequest request,
        string idempotencyKey,
        SessionRequestSnapshot session,
        Action<BackendResult<OnboardingMutationResponse>> onComplete)
    {
        BackendResult<OnboardingMutationResponse> result = null;
        yield return BackendApiClient.Post<OnboardingMutationResponse>(baseUrl, session.Token, path, request, idempotencyKey, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<OnboardingMutationResponse>("Backend session changed while onboarding was updating."));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private void ApplyAccelerateResponse(IdleAdventureAccelerateResponse response)
    {
        if (response == null) return;
        RememberIdleAdventureAfkSnapshot(response.state);
        if (response.stamina != null) ApplyAuthoritativeStamina(response.stamina);
        if (response.items != null)
        {
            ApplyInventorySnapshot(ConvertIdleAdventureInventory(response.items));
        }

        ApplyIdleAdventureAfkStateToSources(response.state);

        int highestStage = response.highestStage > 0 ? response.highestStage : response.highest_stage;
        if (highestStage > 0)
        {
            PlayerPrefs.SetInt("weijun.idleAdventure.highestClearedStage", Mathf.Max(0, highestStage));
            PlayerPrefs.Save();
        }

        if (response.progress != null && response.progress.ResolvedHighestStage > 0)
        {
            PlayerPrefs.SetInt(
                "weijun.idleAdventure.highestClearedStage",
                Mathf.Max(0, response.progress.ResolvedHighestStage));
            PlayerPrefs.Save();
        }
    }

    private void ApplyStageClearResponse(IdleAdventureStageClearResponse response)
    {
        if (response == null) return;
        IdleAdventureProgressResponse progress = response.ResolvedProgress;
        if (progress != null)
        {
            response.progress = progress;
            int highestStage = progress.ResolvedHighestStage;
            if (highestStage > 0)
            {
                PlayerPrefs.SetInt(
                    "weijun.idleAdventure.highestClearedStage",
                    Mathf.Max(0, highestStage));
                PlayerPrefs.Save();

                IdleAdventureAfkRewardSource[] sources = FindObjectsByType<IdleAdventureAfkRewardSource>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != null) sources[i].SetHighestClearedStageFromProgress(highestStage);
                }
            }
        }
        if (response.stamina != null) ApplyAuthoritativeStamina(response.stamina);
        if (response.items != null)
        {
            ApplyInventorySnapshot(ConvertIdleAdventureInventory(response.items));
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

    private string BuildAfkStageClearStorageKey(string sessionKey, int clearedStage)
    {
        string normalizedSessionKey = string.IsNullOrWhiteSpace(sessionKey)
            ? "unknown"
            : sessionKey.Trim();
        return AfkStageClearKeyPrefix + PlayerId + "." + normalizedSessionKey + "." + clearedStage;
    }

    private string BuildProductionWorkTimeStorageKey(ProductionWorkTimeRequest request)
    {
        return ProductionWorkTimeKeyPrefix
            + PlayerId
            + "."
            + request.itemId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "."
            + request.quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "."
            + request.expectedConfigVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string BuildProductionRecipeSwitchStorageKey(ProductionRecipeSwitchRequest request)
    {
        return ProductionRecipeSwitchKeyPrefix
            + PlayerId
            + "."
            + request.playerBuildingId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "."
            + request.recipeId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "."
            + request.expectedRecipeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string GetOrCreateProductionWorkSnapshotToken(ProductionWorkTimeRequest request)
    {
        long itemId = request == null ? 0L : request.itemId;
        return GetOrCreatePersistedIdempotencyKey(BuildProductionWorkSnapshotStorageKey(PlayerId, itemId));
    }

    private static string BuildProductionWorkSnapshotStorageKey(int playerId, long itemId)
    {
        return ProductionWorkSnapshotKeyPrefix
            + playerId
            + "."
            + itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void RememberProductionWorkStateSnapshots(int playerId, ProductionWorkStateResponse state)
    {
        if (playerId <= 0 || state == null || state.fuels == null) return;
        for (int index = 0; index < state.fuels.Length; index++)
        {
            ProductionFuelResponse fuel = state.fuels[index];
            if (fuel == null || fuel.itemId <= 0L) continue;
            RememberProductionWorkSnapshot(
                playerId,
                fuel.itemId,
                state.configVersion,
                state.maxWorkSeconds,
                state.dailyRemainingGainSeconds,
                fuel.ownedQuantity,
                fuel.workSecondsPerItem);
        }
    }

    private static void RememberProductionWorkSnapshot(
        int playerId,
        long itemId,
        long configVersion,
        long maxWorkSeconds,
        long dailyRemainingGainSeconds,
        long ownedQuantity,
        long workSecondsPerItem)
    {
        string canonical = configVersion
            + "|" + maxWorkSeconds
            + "|" + dailyRemainingGainSeconds
            + "|" + itemId
            + "|" + ownedQuantity
            + "|" + workSecondsPerItem;
        RememberPersistedSnapshot(
            BuildProductionWorkSnapshotStorageKey(playerId, itemId),
            ComputeSnapshotFingerprint(canonical));
    }

    private string GetOrCreateProductionRecipeSnapshotToken(long playerBuildingId)
    {
        return GetOrCreatePersistedIdempotencyKey(
            ProductionRecipeSnapshotKeyPrefix + PlayerId + "." + playerBuildingId);
    }

    private static void RememberProductionRecipeSnapshot(int playerId, ProductionRecipesResponse state)
    {
        if (playerId <= 0 || state == null || state.playerBuildingId <= 0L) return;
        string canonical = state.playerBuildingId
            + "|" + state.activeRecipeId
            + "|" + state.activeRecipeVersion;
        RememberPersistedSnapshot(
            ProductionRecipeSnapshotKeyPrefix + playerId + "." + state.playerBuildingId,
            ComputeSnapshotFingerprint(canonical));
    }

    private static void RotateProductionRecipeSnapshotToken(int playerId, long playerBuildingId)
    {
        if (playerId <= 0 || playerBuildingId <= 0L) return;
        SavePersistedIdempotencyKey(
            ProductionRecipeSnapshotKeyPrefix + playerId + "." + playerBuildingId,
            Guid.NewGuid().ToString("D"));
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
