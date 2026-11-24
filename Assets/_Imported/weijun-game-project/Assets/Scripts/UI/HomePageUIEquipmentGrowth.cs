using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const string EquipmentMaterialPolicy = "SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH";
    private const int EquipmentMaterialPageSize = 4;
    private const string EquipmentGrowthStoragePrefix = "EquipmentGrowth.Pending.v1.";
    private static readonly Dictionary<long, EquipmentGrowthRequest> PendingEquipmentGrowth = new Dictionary<long, EquipmentGrowthRequest>();
    private long equipmentGrowthPersistencePlayer;
    private string equipmentGrowthPersistenceError;
    private bool equipmentGrowthRecoveryRunning;
    private bool equipmentGrowthStateReadSucceeded;
    private bool equipmentGrowthPreservesBreakthrough;
    private GameObject equipmentBreakthroughPanel;
    private EquipmentUpgradePreviewData equipmentBreakthroughPlan;
    private EquipmentBreakthroughSelection equipmentBreakthroughSelection;
    private readonly List<EquipmentBreakthroughMaterial> equipmentBreakthroughMaterials = new List<EquipmentBreakthroughMaterial>();
    private EquipmentBreakthroughMaterial[] equipmentMaterialPage = new EquipmentBreakthroughMaterial[0];
    private readonly List<Button> equipmentMaterialSlots = new List<Button>();
    private int equipmentBreakthroughVersion;
    private int equipmentMaterialPageNumber = 1;
    private long equipmentMaterialTotal;
    private bool equipmentBreakthroughBusy;
    private string equipmentBreakthroughMessage;
    private Toggle equipmentIrreversibleToggle;
    private Toggle equipmentHighLevelToggle;

    private void OnEnable()
    {
        if (Application.isPlaying) StartCoroutine(ResumeEquipmentGrowthAfterEnable());
    }

    private IEnumerator ResumeEquipmentGrowthAfterEnable()
    {
        yield return null;
        if (HasAuthSession()) ResumePendingEquipmentGrowthRequest();
    }

    private static bool IsValidEquipmentSinglePreview(EquipmentUpgradePreviewData preview, long equipmentId, int level)
    {
        if (preview == null || preview.playerEquipmentId != equipmentId || level < 1
            || preview.currentLevel != level || preview.maxLevel < level || preview.goldCost < 0
            || preview.availableGold < 0) return false;
        if (preview.maxLevelReached) return level == preview.maxLevel;
        if (preview.targetLevel != level + 1 || preview.targetLevel > preview.maxLevel) return false;
        if (preview.resourceCosts != null && preview.resourceCosts.Any(cost => cost == null
            || string.IsNullOrEmpty(cost.itemKey) || cost.amount < 0 || cost.availableAmount < 0)) return false;
        return !preview.isBreakthrough || (preview.requiredMaterialCount > 0
            && preview.materialPolicy == EquipmentMaterialPolicy && preview.refundPolicy == "NONE"
            && IsValidUpgradePlanFingerprint(preview.planFingerprint));
    }

    private static bool HasEquipmentUpgradeFunds(EquipmentUpgradePreviewData preview)
    {
        return preview != null && !preview.maxLevelReached && preview.goldCost >= 0
            && preview.availableGold >= preview.goldCost
            && (preview.resourceCosts == null || preview.resourceCosts.All(cost => cost != null
                && cost.amount >= 0 && cost.availableAmount >= cost.amount));
    }

    private bool HasPendingEquipmentGrowthRequest()
    {
        return PendingEquipmentGrowth.Values.Any(operation => operation.playerId == GameSession.PlayerId);
    }

    private bool HasPendingEquipmentGrowthRequest(long playerEquipmentId)
    {
        return PendingEquipmentGrowth.Values.Any(operation => operation.playerId == GameSession.PlayerId
            && (operation.playerEquipmentId == playerEquipmentId
                || (operation.materialPlayerEquipmentIds != null && operation.materialPlayerEquipmentIds.Contains(playerEquipmentId))));
    }

    private static bool IsPendingEquipmentGrowthRequest(EquipmentGrowthRequest operation)
    {
        return operation != null
            && PendingEquipmentGrowth.TryGetValue(operation.playerEquipmentId, out EquipmentGrowthRequest pending)
            && ReferenceEquals(pending, operation);
    }

    private void ResumePendingEquipmentGrowthRequest()
    {
        LoadPendingEquipmentGrowthRequests();
        if (!string.IsNullOrEmpty(equipmentGrowthPersistenceError))
        {
            equipmentStatusMessage = equipmentGrowthPersistenceError;
            return;
        }
        foreach (EquipmentGrowthRequest pending in PendingEquipmentGrowth.Values)
        {
            if (pending.playerId != GameSession.PlayerId || !pending.inFlight) continue;
            if (pending.owner == null || !pending.owner.gameObject.activeInHierarchy)
            {
                pending.inFlight = false;
                pending.nextRecoveryTime = 0f;
                pending.recoveryAttempts = Math.Max(1, pending.recoveryAttempts);
            }
        }
        if (equipmentGrowthRecoveryRunning || !HasPendingEquipmentGrowthRequest() || !isActiveAndEnabled) return;
        equipmentGrowthRecoveryRunning = true;
        StartCoroutine(RecoverPendingEquipmentGrowthRequests());
    }

    private void LoadPendingEquipmentGrowthRequests()
    {
        long playerId = GameSession.PlayerId;
        if (playerId <= 0 || equipmentGrowthPersistencePlayer == playerId) return;
        equipmentGrowthPersistencePlayer = playerId;
        equipmentGrowthPersistenceError = string.Empty;
        string storageKey = EquipmentGrowthStoragePrefix + playerId;
        try
        {
            if (!PlayerPrefs.HasKey(storageKey)) return;
            string json = PlayerPrefs.GetString(storageKey);
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1048576) throw new FormatException("Invalid equipment recovery snapshot");
            EquipmentGrowthSnapshot snapshot = JsonUtility.FromJson<EquipmentGrowthSnapshot>(json);
            if (snapshot == null || snapshot.version != 1 || snapshot.playerId != playerId || snapshot.operations == null)
                throw new FormatException("Invalid equipment recovery snapshot");
            HashSet<long> equipmentIds = new HashSet<long>();
            foreach (EquipmentGrowthRequest operation in snapshot.operations)
            {
                if (!IsValidPersistedEquipmentGrowthRequest(operation, playerId) || !equipmentIds.Add(operation.playerEquipmentId))
                    throw new FormatException("Invalid equipment recovery operation");
            }
            foreach (EquipmentGrowthRequest operation in snapshot.operations)
            {
                if (PendingEquipmentGrowth.ContainsKey(operation.playerEquipmentId)) continue;
                operation.inFlight = false;
                operation.owner = null;
                operation.recoveryAttempts = 1;
                operation.nextRecoveryTime = 0f;
                PendingEquipmentGrowth.Add(operation.playerEquipmentId, operation);
            }
        }
        catch (Exception exception)
        {
            equipmentGrowthPersistenceError = "上次升级记录无法读取，为避免重复消耗已暂停升级与突破，请联系客服";
            Debug.LogWarning("Equipment recovery snapshot was preserved because it could not be loaded: " + exception.GetType().Name, this);
        }
    }

    private static bool IsValidPersistedEquipmentGrowthRequest(EquipmentGrowthRequest operation, long playerId)
    {
        if (operation == null || operation.playerId != playerId || operation.playerEquipmentId <= 0
            || operation.expectedLevel < 1 || operation.targetLevel <= operation.expectedLevel || operation.targetLevel > 100
            || string.IsNullOrWhiteSpace(operation.json) || operation.json.Length > 131072) return false;
        if (operation.path == "/equipment/upgrade")
        {
            EquipmentInstanceRequest payload = JsonUtility.FromJson<EquipmentInstanceRequest>(operation.json);
            return string.IsNullOrEmpty(operation.idempotencyKey) && operation.targetLevel == operation.expectedLevel + 1
                && payload != null && payload.playerEquipmentId == operation.playerEquipmentId;
        }
        if (!Guid.TryParseExact(operation.idempotencyKey, "D", out Guid requestId) || requestId == Guid.Empty) return false;
        if (operation.path == "/equipment/auto-upgrade")
        {
            EquipmentAutoUpgradeRequestPayload payload = JsonUtility.FromJson<EquipmentAutoUpgradeRequestPayload>(operation.json);
            return payload != null && payload.playerEquipmentId == operation.playerEquipmentId
                && payload.expectedCurrentLevel == operation.expectedLevel && payload.targetLevel == operation.targetLevel
                && IsValidUpgradePlanFingerprint(payload.planFingerprint);
        }
        if (operation.path == "/equipment/breakthrough")
        {
            EquipmentBreakthroughExecuteRequest payload = JsonUtility.FromJson<EquipmentBreakthroughExecuteRequest>(operation.json);
            if (payload == null || payload.playerEquipmentId != operation.playerEquipmentId
                || payload.expectedCurrentLevel != operation.expectedLevel || payload.targetLevel != operation.targetLevel
                || operation.targetLevel != operation.expectedLevel + 1 || !payload.confirmIrreversibleMaterialConsumption
                || !IsValidUpgradePlanFingerprint(payload.planFingerprint) || !IsValidUpgradePlanFingerprint(payload.materialSelectionFingerprint)
                || payload.materialPlayerEquipmentIds == null || payload.materialPlayerEquipmentIds.Length == 0
                || payload.materialPlayerEquipmentIds.Any(materialId => materialId <= 0 || materialId == operation.playerEquipmentId)
                || payload.materialPlayerEquipmentIds.Distinct().Count() != payload.materialPlayerEquipmentIds.Length) return false;
            operation.materialPlayerEquipmentIds = payload.materialPlayerEquipmentIds;
            return true;
        }
        return false;
    }

    private bool PersistPendingEquipmentGrowthRequests(long playerId)
    {
        try
        {
            EquipmentGrowthRequest[] operations = PendingEquipmentGrowth.Values.Where(operation => operation.playerId == playerId).ToArray();
            string storageKey = EquipmentGrowthStoragePrefix + playerId;
            if (operations.Length == 0) PlayerPrefs.DeleteKey(storageKey);
            else PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(new EquipmentGrowthSnapshot
            {
                version = 1,
                playerId = playerId,
                operations = operations
            }));
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception exception)
        {
            if (playerId == GameSession.PlayerId)
                equipmentGrowthPersistenceError = "无法保存升级恢复记录，升级与突破已暂停，请检查设备存储空间后重启游戏";
            Debug.LogWarning("Equipment recovery snapshot could not be saved: " + exception.GetType().Name, this);
            return false;
        }
    }

    private void CompletePendingEquipmentGrowthRequest(EquipmentGrowthRequest operation)
    {
        PendingEquipmentGrowth.Remove(operation.playerEquipmentId);
        if (!PersistPendingEquipmentGrowthRequests(operation.playerId))
            PendingEquipmentGrowth[operation.playerEquipmentId] = operation;
    }

    private IEnumerator RecoverPendingEquipmentGrowthRequests()
    {
        long playerId = GameSession.PlayerId;
        try
        {
            while (playerId == GameSession.PlayerId && HasAuthSession() && HasPendingEquipmentGrowthRequest()
                && string.IsNullOrEmpty(equipmentGrowthPersistenceError))
            {
                EquipmentGrowthRequest pending = PendingEquipmentGrowth.Values.FirstOrDefault(operation =>
                    operation.playerId == playerId && !operation.inFlight && Time.realtimeSinceStartup >= operation.nextRecoveryTime);
                if (pending == null || equipmentMutationRunning || equipmentRefreshRunning)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    continue;
                }
                if (pending.resultResolved)
                    yield return SynchronizeResolvedEquipmentGrowth(pending);
                else if (pending.responseCompleted || string.IsNullOrEmpty(pending.idempotencyKey))
                    yield return ReconcileSingleEquipmentUpgrade(pending);
                else
                    yield return SendEquipmentGrowthRequest(pending);
            }
        }
        finally
        {
            equipmentGrowthRecoveryRunning = false;
        }
    }

    private void ScheduleEquipmentGrowthRecovery(EquipmentGrowthRequest operation)
    {
        if (!IsPendingEquipmentGrowthRequest(operation)) return;
        operation.recoveryAttempts++;
        operation.nextRecoveryTime = Time.realtimeSinceStartup
            + (operation.recoveryAttempts <= 2 ? 1f : Mathf.Min(30f, 5f * Mathf.Pow(2f, Mathf.Min(operation.recoveryAttempts - 3, 3))));
        if (operation.playerId == GameSession.PlayerId)
        {
            equipmentStatusMessage = operation.recoveryAttempts <= 2
                ? "正在自动同步升级结果，请稍候..."
                : "网络暂未恢复，正在后台同步此装备；其他装备可继续操作";
            ResumePendingEquipmentGrowthRequest();
        }
    }

    private IEnumerator SynchronizeResolvedEquipmentGrowth(EquipmentGrowthRequest operation)
    {
        if (!IsPendingEquipmentGrowthRequest(operation) || operation.inFlight
            || equipmentMutationRunning || equipmentRefreshRunning) yield break;
        string token = GameSession.Token;
        operation.owner = this;
        operation.inFlight = true;
        equipmentMutationRunning = true;
        try
        {
            yield return RefreshEquipmentGrowthState(operation.playerId, token, operation.completionMessage, operation.playerEquipmentId);
            if (operation.playerId == GameSession.PlayerId && token == GameSession.Token && equipmentGrowthStateReadSucceeded)
                CompletePendingEquipmentGrowthRequest(operation);
        }
        finally
        {
            operation.inFlight = false;
            equipmentMutationRunning = false;
            equipmentRefreshRunning = false;
            ScheduleEquipmentGrowthRecovery(operation);
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }
    }

    private IEnumerator ReconcileSingleEquipmentUpgrade(EquipmentGrowthRequest pending)
    {
        if (!IsPendingEquipmentGrowthRequest(pending) || pending.inFlight
            || equipmentMutationRunning || equipmentRefreshRunning) yield break;
        string token = GameSession.Token;
        bool ownsMutation = false;
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{pending.playerEquipmentId}/upgrade-preview");
        if (request == null)
        {
            equipmentStatusMessage = requestCreationError;
            ScheduleEquipmentGrowthRecovery(pending);
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        pending.inFlight = true;
        pending.owner = this;
        equipmentStatusMessage = "正在自动同步装备升级结果...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        try
        {
            using (request)
            {
                yield return request.SendWebRequest();
                if (pending.playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
                if (HandleUnauthorizedResponse(request)) yield break;
                EquipmentUpgradePreviewApiResponse response = ParseJson<EquipmentUpgradePreviewApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200
                    || response.data == null || response.data.playerEquipmentId != pending.playerEquipmentId
                    || response.data.currentLevel < pending.expectedLevel || response.data.maxLevel < response.data.currentLevel)
                {
                    equipmentStatusMessage = "升级结果暂未同步，系统会自动重试";
                    yield break;
                }
                if (CanConfirmEquipmentGrowthSnapshot(pending, response.data.currentLevel))
                {
                    while (equipmentMutationRunning || equipmentRefreshRunning)
                        yield return new WaitForSecondsRealtime(0.1f);
                    if (pending.playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
                    equipmentMutationRunning = true;
                    ownsMutation = true;
                    string message = response.data.currentLevel > pending.expectedLevel
                        ? $"装备等级已确认：Lv.{response.data.currentLevel}"
                        : "本次操作已结束，装备等级未变化，消耗信息已刷新";
                    pending.resultResolved = true;
                    pending.completionMessage = message;
                    PersistPendingEquipmentGrowthRequests(pending.playerId);
                    yield return RefreshEquipmentGrowthState(pending.playerId, token, message, pending.playerEquipmentId);
                    if (pending.playerId == GameSession.PlayerId && token == GameSession.Token && equipmentGrowthStateReadSucceeded)
                        CompletePendingEquipmentGrowthRequest(pending);
                }
                else
                {
                    equipmentStatusMessage = "正在后台同步此装备的升级结果";
                }
            }
        }
        finally
        {
            pending.inFlight = false;
            if (ownsMutation)
            {
                equipmentMutationRunning = false;
                equipmentRefreshRunning = false;
            }
            ScheduleEquipmentGrowthRecovery(pending);
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }
    }

    private static bool CanConfirmEquipmentGrowthSnapshot(EquipmentGrowthRequest pending, int level)
    {
        return level >= pending.expectedLevel && (pending.responseCompleted
            || (string.IsNullOrEmpty(pending.idempotencyKey) && level >= pending.targetLevel));
    }

    private IEnumerator SendEquipmentGrowthRequest(EquipmentGrowthRequest operation)
    {
        LoadPendingEquipmentGrowthRequests();
        if (!string.IsNullOrEmpty(equipmentGrowthPersistenceError))
        {
            equipmentStatusMessage = equipmentGrowthPersistenceError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        if (operation == null || operation.playerId != GameSession.PlayerId || operation.playerId <= 0
            || equipmentMutationRunning || operation.inFlight) yield break;
        if (PendingEquipmentGrowth.TryGetValue(operation.playerEquipmentId, out EquipmentGrowthRequest pending)
            && !ReferenceEquals(pending, operation)) yield break;
        if (!TryCreateAuthorizedPost(operation.path, operation.json, out UnityWebRequest request, out string error))
        {
            equipmentStatusMessage = error;
            ScheduleEquipmentGrowthRecovery(operation);
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        if (!string.IsNullOrEmpty(operation.idempotencyKey)) ApiRequestUtility.SetIdempotencyKey(request, operation.idempotencyKey);
        PendingEquipmentGrowth[operation.playerEquipmentId] = operation;
        if (!PersistPendingEquipmentGrowthRequests(operation.playerId))
        {
            request.Dispose();
            equipmentStatusMessage = equipmentGrowthPersistenceError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        string token = GameSession.Token;
        bool ownsMutation = operation.recoveryAttempts == 0;
        if (ownsMutation) equipmentMutationRunning = true;
        operation.inFlight = true;
        operation.owner = this;
        if (ownsMutation) CloseEquipmentBreakthroughPanel();
        equipmentStatusMessage = "正在提交装备成长操作...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        try
        {
            string completion;
            using (request)
            {
                yield return request.SendWebRequest();
                if (operation.playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
                if (HandleUnauthorizedResponse(request)) yield break;
                string json = request.downloadHandler.text;
                BasicApiResponse response = ParseJson<BasicApiResponse>(json);
                bool success = request.result == UnityWebRequest.Result.Success && response != null && response.code == 200;
                if (!success)
                {
                    bool rejected = response != null && response.code > 0 && response.code != 200
                        && response.code != 408 && response.code != 429 && (response.code < 500 || response.code >= 600)
                        && request.responseCode > 0 && request.responseCode < 500
                        && request.responseCode != 408 && request.responseCode != 429;
                    if (!rejected)
                    {
                        Debug.LogWarning($"Equipment growth result unresolved: http={request.responseCode}, code={(response != null ? response.code : 0)}, result={request.result}, equipment={operation.playerEquipmentId}, from={operation.expectedLevel}, target={operation.targetLevel}. Original request retained for reconciliation.", this);
                        equipmentStatusMessage = "升级结果暂未同步，正在自动恢复，请稍候...";
                        yield break;
                    }
                    completion = EquipmentGrowthError(response.code, GetApiError(response));
                }
                else if (operation.path == "/equipment/auto-upgrade")
                {
                    operation.responseCompleted = true;
                    PersistPendingEquipmentGrowthRequests(operation.playerId);
                    AutoUpgradeApiResponse result = ParseJson<AutoUpgradeApiResponse>(json);
                    if (!IsValidEquipmentGrowthResult(result != null ? result.data : null, operation))
                    {
                        Debug.LogWarning("Equipment auto-upgrade returned success with an incompatible result; reconciling the authoritative equipment level.", this);
                        yield break;
                    }
                    completion = $"装备已升至 Lv.{result.data.reachedLevel}，{FormatAutoUpgradeOutcome(result.data.outcome)}";
                }
                else
                {
                    operation.responseCompleted = true;
                    PersistPendingEquipmentGrowthRequests(operation.playerId);
                    EquipmentUpgradeApiResponse result = ParseJson<EquipmentUpgradeApiResponse>(json);
                    if (result == null || result.data == null || result.data.playerEquipmentId != operation.playerEquipmentId
                        || result.data.fromLevel != operation.expectedLevel || result.data.level != operation.targetLevel)
                    {
                        yield break;
                    }
                    if (operation.path == "/equipment/breakthrough")
                    {
                        EquipmentBreakthroughResultResponse breakthrough = ParseJson<EquipmentBreakthroughResultResponse>(json);
                        EquipmentBreakthroughExecuteRequest payload = ParseJson<EquipmentBreakthroughExecuteRequest>(operation.json);
                        if (!IsValidEquipmentBreakthroughResult(breakthrough != null ? breakthrough.data : null, payload))
                        {
                            yield break;
                        }
                    }
                    SyncEquipmentUpgradeResourceBalances(result.data);
                    completion = (operation.path == "/equipment/breakthrough" ? "突破成功" : "升级成功")
                        + $"，装备 Lv.{result.data.level}，消耗金币 {result.data.goldCost}";
                }
            }
            operation.resultResolved = true;
            operation.completionMessage = completion;
            PersistPendingEquipmentGrowthRequests(operation.playerId);
            if (!ownsMutation)
            {
                while (equipmentMutationRunning || equipmentRefreshRunning)
                    yield return new WaitForSecondsRealtime(0.1f);
                if (operation.playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
                equipmentMutationRunning = true;
                ownsMutation = true;
            }
            yield return RefreshEquipmentGrowthState(operation.playerId, token, completion, operation.playerEquipmentId);
            if (operation.playerId == GameSession.PlayerId && token == GameSession.Token && equipmentGrowthStateReadSucceeded)
                CompletePendingEquipmentGrowthRequest(operation);
        }
        finally
        {
            operation.inFlight = false;
            if (ownsMutation)
            {
                equipmentMutationRunning = false;
                equipmentRefreshRunning = false;
            }
            ScheduleEquipmentGrowthRecovery(operation);
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }
    }

    private static bool IsValidEquipmentGrowthResult(AutoUpgradeData result, EquipmentGrowthRequest operation)
    {
        return result != null && result.playerEquipmentId == operation.playerEquipmentId
            && result.fromLevel == operation.expectedLevel && result.requestedTargetLevel == operation.targetLevel
            && result.reachedLevel >= result.fromLevel && result.reachedLevel <= operation.targetLevel
            && result.upgradedLevelCount == result.reachedLevel - result.fromLevel
            && result.targetReached == (result.reachedLevel == operation.targetLevel);
    }

    private IEnumerator RefreshEquipmentGrowthState(long playerId, string token, string message, long changedEquipmentId = 0)
    {
        equipmentGrowthStateReadSucceeded = false;
        equipmentGrowthPreservesBreakthrough = equipmentBreakthroughPanel != null && equipmentBreakthroughPanel.activeSelf
            && equipmentBreakthroughPlan != null && equipmentBreakthroughPlan.playerEquipmentId != changedEquipmentId;
        int preservedBreakthroughVersion = -1;
        if (equipmentGrowthPreservesBreakthrough)
        {
            equipmentBreakthroughVersion++;
            preservedBreakthroughVersion = equipmentBreakthroughVersion;
            equipmentBreakthroughBusy = true;
            InvalidateEquipmentMaterialSelection();
            equipmentBreakthroughMessage = "正在同步资源，已保留所选材料；同步后请重新预览";
            RenderEquipmentBreakthroughPanel();
        }
        try
        {
            ResetEquipmentUpgradePreview();
            ResetEquipmentAutoUpgradePreviews();
            equipmentRefreshRunning = true;
            string inventoryError = string.Empty;
            UnityWebRequest inventoryRequest = CreateAuthorizedGet($"/inventory/list/{playerId}");
            if (inventoryRequest == null) inventoryError = requestCreationError;
            else using (inventoryRequest)
            {
                yield return inventoryRequest.SendWebRequest();
                if (playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
                if (HandleUnauthorizedResponse(inventoryRequest)) yield break;
                InventoryListApiResponse inventory = ParseJson<InventoryListApiResponse>(inventoryRequest.downloadHandler.text);
                if (inventoryRequest.result == UnityWebRequest.Result.Success && inventory != null && inventory.code == 200)
                {
                    currentInventoryItems = inventory.data ?? new InventoryItemResponse[0];
                    inventoryServerBacked = true;
                    SyncInventoryResourceValues(currentInventoryItems);
                }
                else inventoryError = "余额正在后台重新同步";
            }
            yield return GetEquipmentInventoryList(true);
            if (playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
            if (equipmentGrowthPreservesBreakthrough)
                yield return RevalidateEquipmentBreakthroughAfterGrowth(playerId, token);
            if (playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
            equipmentRefreshRunning = false;
            equipmentGrowthStateReadSucceeded = string.IsNullOrEmpty(inventoryError) && _equipmentDataReadSucceeded;
            equipmentStatusMessage = message + (string.IsNullOrEmpty(inventoryError) ? string.Empty : "；" + inventoryError)
                + (_equipmentDataReadSucceeded ? string.Empty : "；装备数据正在后台重新同步");
            if (!_equipmentDataReadSucceeded)
            {
                equipmentUpgradePreview = null;
                equipmentUpgradePreviewRunning = false;
                equipmentUpgradePreviewError = "装备数据正在后台重新同步";
            }
            SetStatus(equipmentStatusMessage);
        }
        finally
        {
            equipmentGrowthPreservesBreakthrough = false;
            if (preservedBreakthroughVersion == equipmentBreakthroughVersion)
            {
                equipmentBreakthroughBusy = false;
                RenderEquipmentBreakthroughPanel();
            }
        }
    }

    private IEnumerator RevalidateEquipmentBreakthroughAfterGrowth(long playerId, string token)
    {
        EquipmentUpgradePreviewData previousPlan = equipmentBreakthroughPlan;
        int version = equipmentBreakthroughVersion;
        if (previousPlan == null || equipmentBreakthroughPanel == null || !equipmentBreakthroughPanel.activeSelf) yield break;
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{previousPlan.playerEquipmentId}/upgrade-preview");
        if (request == null)
        {
            equipmentBreakthroughMessage = "资源同步暂未完成，请稍后重新预览材料";
            RenderEquipmentBreakthroughPanel();
            yield break;
        }
        using (request)
        {
            yield return request.SendWebRequest();
            if (playerId != GameSession.PlayerId || token != GameSession.Token || version != equipmentBreakthroughVersion) yield break;
            EquipmentUpgradePreviewApiResponse response = ParseJson<EquipmentUpgradePreviewApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200
                && response.data != null && response.data.isBreakthrough
                && IsValidEquipmentSinglePreview(response.data, previousPlan.playerEquipmentId, previousPlan.currentLevel))
            {
                equipmentBreakthroughPlan = response.data;
                equipmentBreakthroughMessage = "资源已同步，已保留所选材料；请重新预览材料费用";
            }
            else
            {
                equipmentBreakthroughMessage = "此装备的费用或等级已变化，请重新打开突破界面";
            }
        }
        RenderEquipmentBreakthroughPanel();
    }

    private static string EquipmentGrowthError(int code, string message)
    {
        switch (code)
        {
            case 6204: return "资源不足，消耗与余额已刷新";
            case 6207: return "装备等级已变化，请重新预览";
            case 6208: return "升级请求已失效，费用已刷新，请重新升级";
            case 6209: return "升级条件已变化，请重新查看消耗";
            case 6210: return "需要先手动突破，请查看最新预览并选择突破材料";
            case 6211: return "突破材料数量不符，请重新选择";
            case 6212: return "材料状态已变化，请重新选择可用装备";
            case 6213: return "需要确认材料及培养投入永久消耗，请重新勾选确认";
            case 6214: return "材料状态已变化，请重新选择并查看消耗";
            default: return message;
        }
    }

    private void CloseEquipmentBreakthroughPanel()
    {
        equipmentBreakthroughVersion++;
        equipmentBreakthroughBusy = false;
        equipmentBreakthroughPlan = null;
        equipmentBreakthroughSelection = null;
        equipmentBreakthroughMaterials.Clear();
        if (equipmentBreakthroughPanel != null) equipmentBreakthroughPanel.SetActive(false);
    }

    private void ShowEquipmentBreakthroughPanel(InventoryItemResponse item)
    {
        EquipmentUpgradePreviewData plan = equipmentUpgradePreview;
        if (item == null || equipmentMutationRunning || equipmentRefreshRunning || HasPendingEquipmentGrowthRequest(item.playerEquipmentId)
            || !string.IsNullOrEmpty(equipmentGrowthPersistenceError)
            || !IsValidEquipmentSinglePreview(plan, item.playerEquipmentId, item.level)
            || !plan.isBreakthrough || !HasEquipmentUpgradeFunds(plan)) return;
        EnsureEquipmentBreakthroughPanel();
        if (equipmentBreakthroughPanel == null) return;
        CloseEquipmentBreakthroughPanel();
        equipmentBreakthroughPlan = plan;
        equipmentBreakthroughPanel.SetActive(true);
        equipmentBreakthroughPanel.transform.SetAsLastSibling();
        equipmentMaterialPageNumber = 1;
        equipmentMaterialPage = new EquipmentBreakthroughMaterial[0];
        equipmentMaterialTotal = 0;
        equipmentBreakthroughMessage = "请选择同款装备作为材料；每件占一个材料框";
        InvalidateEquipmentMaterialSelection();
        StartCoroutine(LoadEquipmentBreakthroughCandidates(1));
    }

    private void InvalidateEquipmentMaterialSelection()
    {
        equipmentBreakthroughSelection = null;
        equipmentIrreversibleToggle?.SetIsOnWithoutNotify(false);
        equipmentHighLevelToggle?.SetIsOnWithoutNotify(false);
    }

    private bool IsCurrentEquipmentBreakthrough(int version, int playerId, string token)
    {
        return version == equipmentBreakthroughVersion && playerId == GameSession.PlayerId && token == GameSession.Token
            && equipmentBreakthroughPlan != null && equipmentBreakthroughPanel != null && equipmentBreakthroughPanel.activeInHierarchy;
    }

    private IEnumerator LoadEquipmentBreakthroughCandidates(int page)
    {
        if (equipmentBreakthroughPlan == null || equipmentBreakthroughBusy) yield break;
        int version = equipmentBreakthroughVersion;
        int playerId = GameSession.PlayerId;
        string token = GameSession.Token;
        equipmentBreakthroughBusy = true;
        RenderEquipmentBreakthroughPanel();
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{equipmentBreakthroughPlan.playerEquipmentId}/breakthrough-materials?page={page}&pageSize={EquipmentMaterialPageSize}");
        if (request == null)
        {
            equipmentBreakthroughMessage = requestCreationError;
        }
        else using (request)
        {
            yield return request.SendWebRequest();
            if (!IsCurrentEquipmentBreakthrough(version, playerId, token)) yield break;
            if (HandleUnauthorizedResponse(request)) { CloseEquipmentBreakthroughPanel(); yield break; }
            EquipmentMaterialPageResponse response = ParseJson<EquipmentMaterialPageResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                equipmentBreakthroughMessage = response == null ? ApiRequestUtility.GetErrorMessage(request, "候选读取失败") : GetApiError(response);
            }
            else if (response.data.contractVersion != 2 || response.data.page != page
                || response.data.pageSize != EquipmentMaterialPageSize || response.data.total < 0
                || (response.data.items != null && response.data.items.Length > EquipmentMaterialPageSize))
            {
                equipmentBreakthroughMessage = "候选数据不完整，请刷新";
            }
            else
            {
                equipmentMaterialPage = response.data.items ?? new EquipmentBreakthroughMaterial[0];
                equipmentMaterialPageNumber = page;
                equipmentMaterialTotal = response.data.total;
                equipmentBreakthroughMessage = equipmentMaterialTotal == 0 ? "暂无可用的同款材料装备" : "请选择材料，再预览突破";
                InvalidateEquipmentMaterialSelection();
                foreach (EquipmentBreakthroughMaterial material in equipmentMaterialPage)
                {
                    if (material == null) continue;
                    int selectedIndex = equipmentBreakthroughMaterials.FindIndex(selected => selected.playerEquipmentId == material.playerEquipmentId);
                    if (selectedIndex < 0) continue;
                    if (IsEquipmentMaterialSelectable(material, equipmentBreakthroughPlan)) equipmentBreakthroughMaterials[selectedIndex] = material;
                    else equipmentBreakthroughMaterials.RemoveAt(selectedIndex);
                }
            }
        }
        equipmentBreakthroughBusy = false;
        RenderEquipmentBreakthroughPanel();
    }

    private static bool IsEquipmentMaterialSelectable(EquipmentBreakthroughMaterial material, EquipmentUpgradePreviewData plan)
    {
        return material != null && plan != null && material.selectable && material.playerEquipmentId > 0
            && material.playerEquipmentId != plan.playerEquipmentId && material.level >= 1
            && ((material.equipmentId > 0 && material.equipmentId == plan.equipmentId)
                || (material.equipmentId == 0 && !string.IsNullOrEmpty(plan.equipmentKey) && material.equipmentKey == plan.equipmentKey));
    }

    private void ToggleEquipmentBreakthroughMaterial(EquipmentBreakthroughMaterial material)
    {
        if (equipmentBreakthroughBusy || equipmentMutationRunning || equipmentBreakthroughPlan == null || material == null) return;
        int index = equipmentBreakthroughMaterials.FindIndex(selected => selected.playerEquipmentId == material.playerEquipmentId);
        if (index >= 0) equipmentBreakthroughMaterials.RemoveAt(index);
        else if (IsEquipmentMaterialSelectable(material, equipmentBreakthroughPlan)
            && equipmentBreakthroughMaterials.Count < equipmentBreakthroughPlan.requiredMaterialCount) equipmentBreakthroughMaterials.Add(material);
        else return;
        InvalidateEquipmentMaterialSelection();
        equipmentBreakthroughMessage = "材料选择已变化，请重新预览";
        RenderEquipmentBreakthroughPanel();
    }

    private IEnumerator PreviewEquipmentBreakthroughSelection()
    {
        EquipmentUpgradePreviewData plan = equipmentBreakthroughPlan;
        if (plan == null || equipmentBreakthroughBusy || equipmentBreakthroughMaterials.Count != plan.requiredMaterialCount) yield break;
        long[] ids = equipmentBreakthroughMaterials.Select(material => material.playerEquipmentId).OrderBy(id => id).ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0 || id == plan.playerEquipmentId)) yield break;
        int version = equipmentBreakthroughVersion;
        int playerId = GameSession.PlayerId;
        string token = GameSession.Token;
        EquipmentBreakthroughPreviewRequest payload = new EquipmentBreakthroughPreviewRequest
        {
            playerEquipmentId = plan.playerEquipmentId, expectedCurrentLevel = plan.currentLevel,
            targetLevel = plan.targetLevel, planFingerprint = plan.planFingerprint, materialPlayerEquipmentIds = ids
        };
        InvalidateEquipmentMaterialSelection();
        equipmentBreakthroughBusy = true;
        RenderEquipmentBreakthroughPanel();
        if (!TryCreateAuthorizedPost("/equipment/breakthrough-preview", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            equipmentBreakthroughMessage = error;
        }
        else using (request)
        {
            yield return request.SendWebRequest();
            if (!IsCurrentEquipmentBreakthrough(version, playerId, token)) yield break;
            if (HandleUnauthorizedResponse(request)) { CloseEquipmentBreakthroughPanel(); yield break; }
            EquipmentBreakthroughSelectionResponse response = ParseJson<EquipmentBreakthroughSelectionResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                if (response != null && response.code >= 6204 && response.code <= 6214)
                {
                    string message = EquipmentGrowthError(response.code, GetApiError(response));
                    CloseEquipmentBreakthroughPanel();
                    RetryEquipmentUpgradePreview(GetEquipmentPanelRoot());
                    SetStatus(message);
                    yield break;
                }
                equipmentBreakthroughMessage = response == null ? ApiRequestUtility.GetErrorMessage(request, "选择预览失败") : GetApiError(response);
            }
            else if (!IsValidEquipmentMaterialSelection(response.data, plan, ids))
            {
                equipmentBreakthroughMessage = "材料预览与当前选择不一致，请重新选择";
            }
            else
            {
                equipmentBreakthroughSelection = response.data;
                equipmentBreakthroughMaterials.Clear();
                equipmentBreakthroughMaterials.AddRange(response.data.selectedMaterials);
                equipmentBreakthroughMessage = "材料及其投入的金币、资源、突破材料永久消耗，不予返还。"
                    + (string.IsNullOrEmpty(response.data.warning) ? string.Empty : "\n" + response.data.warning);
            }
        }
        equipmentBreakthroughBusy = false;
        RenderEquipmentBreakthroughPanel();
    }

    private static bool IsValidEquipmentMaterialSelection(EquipmentBreakthroughSelection selection, EquipmentUpgradePreviewData plan, long[] ids)
    {
        if (selection == null || plan == null || ids == null || ids.Length != plan.requiredMaterialCount
            || ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0 || id == plan.playerEquipmentId)
            || selection.contractVersion != 2 || selection.playerEquipmentId != plan.playerEquipmentId
            || selection.currentLevel != plan.currentLevel || selection.targetLevel != plan.targetLevel
            || selection.requiredMaterialCount != plan.requiredMaterialCount || selection.planFingerprint != plan.planFingerprint
            || !IsValidUpgradePlanFingerprint(selection.materialSelectionFingerprint)
            || selection.materialPolicy != EquipmentMaterialPolicy || selection.refundPolicy != "NONE"
            || selection.selectedMaterials == null || selection.selectedMaterials.Length != ids.Length) return false;
        HashSet<long> remaining = new HashSet<long>(ids);
        int highLevelCount = 0;
        foreach (EquipmentBreakthroughMaterial material in selection.selectedMaterials)
        {
            if (material == null || !remaining.Remove(material.playerEquipmentId) || material.level < 1
                || material.equipmentId != plan.equipmentId || material.targetLevel != plan.targetLevel
                || material.hasCultivationInvestment != (material.level > 1)) return false;
            if (material.level > 1) highLevelCount++;
        }
        return remaining.Count == 0 && selection.highLevelMaterialCount == highLevelCount
            && selection.requiresHighLevelConfirmation == (highLevelCount > 0);
    }

    private void ConfirmEquipmentBreakthrough()
    {
        EquipmentUpgradePreviewData plan = equipmentBreakthroughPlan;
        EquipmentBreakthroughSelection selection = equipmentBreakthroughSelection;
        if (plan == null || selection == null || equipmentBreakthroughBusy || equipmentMutationRunning
            || equipmentRefreshRunning || HasPendingEquipmentGrowthRequest(plan.playerEquipmentId)
            || !string.IsNullOrEmpty(equipmentGrowthPersistenceError) || !HasEquipmentUpgradeFunds(plan)
            || equipmentIrreversibleToggle == null || !equipmentIrreversibleToggle.isOn
            || (selection.requiresHighLevelConfirmation && (equipmentHighLevelToggle == null || !equipmentHighLevelToggle.isOn))) return;
        long[] ids = equipmentBreakthroughMaterials.Select(material => material.playerEquipmentId).OrderBy(id => id).ToArray();
        if (!IsValidEquipmentMaterialSelection(selection, plan, ids)) return;
        if (ids.Any(HasPendingEquipmentGrowthRequest))
        {
            equipmentBreakthroughMessage = "所选材料正在同步升级结果，请选择其他装备或稍后再试";
            RenderEquipmentBreakthroughPanel();
            return;
        }
        EquipmentBreakthroughExecuteRequest payload = new EquipmentBreakthroughExecuteRequest
        {
            playerEquipmentId = plan.playerEquipmentId, expectedCurrentLevel = plan.currentLevel,
            targetLevel = plan.targetLevel, planFingerprint = selection.planFingerprint,
            materialPlayerEquipmentIds = ids, materialSelectionFingerprint = selection.materialSelectionFingerprint,
            confirmIrreversibleMaterialConsumption = true, confirmHighLevelMaterialLoss = equipmentHighLevelToggle != null && equipmentHighLevelToggle.isOn
        };
        StartCoroutine(SendEquipmentGrowthRequest(new EquipmentGrowthRequest
        {
            playerId = GameSession.PlayerId, playerEquipmentId = plan.playerEquipmentId,
            expectedLevel = plan.currentLevel, targetLevel = plan.targetLevel,
            path = "/equipment/breakthrough", json = JsonUtility.ToJson(payload), idempotencyKey = Guid.NewGuid().ToString("D"),
            materialPlayerEquipmentIds = ids
        }));
    }

    private void EnsureEquipmentBreakthroughPanel()
    {
        Transform root = GetEquipmentPanelRoot();
        if (root == null) return;
        if (equipmentBreakthroughPanel == null)
        {
            equipmentMaterialSlots.Clear();
            Transform existing = FindSceneTransform(root, "EquipmentBreakthroughPanel");
            if (existing != null) equipmentBreakthroughPanel = existing.gameObject;
        }
        if (equipmentBreakthroughPanel == null)
        {
            Image overlay = CreateImage("EquipmentBreakthroughPanel", root, new Color(0, 0, 0, 0.72f));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            equipmentBreakthroughPanel = overlay.gameObject;
            Image body = CreateImage("BreakthroughBody", overlay.transform, new Color(0.96f, 0.96f, 0.96f));
            SetEquipmentGrowthRect(body.rectTransform, 0.07f, 0.05f, 0.93f, 0.95f);
            TMP_Text title = CreateText("BreakthroughTitle", body.transform, "装备突破", 30, FontStyle.Bold, Color.black, TextAnchor.MiddleLeft);
            SetEquipmentGrowthRect(title.rectTransform, 0.03f, 0.90f, 0.76f, 0.98f);
            TMP_Text cost = CreateText("BreakthroughCost", body.transform, "", 22, FontStyle.Normal, Color.black, TextAnchor.MiddleLeft);
            SetEquipmentGrowthRect(cost.rectTransform, 0.03f, 0.81f, 0.97f, 0.90f);
            Image viewport = CreateImage("MaterialSlotsViewport", body.transform, new Color(0.85f, 0.88f, 0.88f));
            SetEquipmentGrowthRect(viewport.rectTransform, 0.03f, 0.64f, 0.97f, 0.80f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateLayoutObject("MaterialSlots", viewport.transform).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.sizeDelta = Vector2.zero;
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int index = 0; index < EquipmentMaterialPageSize; index++)
            {
                Button row = CreateButton(body.transform, "MaterialCandidate" + index, "", new Color(0.82f, 0.89f, 0.88f), Color.black);
                SetEquipmentGrowthRect(row.GetComponent<RectTransform>(), 0.03f, 0.55f - index * 0.073f, 0.97f, 0.614f - index * 0.073f);
                TMP_Text label = CreateText("Text", row.transform, "", 22, FontStyle.Normal, Color.black, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
            }
            Button previous = CreateButton(body.transform, "MaterialPrevious", "<", Color.gray, Color.white);
            SetEquipmentGrowthRect(previous.GetComponent<RectTransform>(), 0.03f, 0.26f, 0.12f, 0.32f);
            Button next = CreateButton(body.transform, "MaterialNext", ">", Color.gray, Color.white);
            SetEquipmentGrowthRect(next.GetComponent<RectTransform>(), 0.88f, 0.26f, 0.97f, 0.32f);
            Button reload = CreateButton(body.transform, "MaterialReload", "刷新候选", Color.gray, Color.white);
            SetEquipmentGrowthRect(reload.GetComponent<RectTransform>(), 0.65f, 0.26f, 0.86f, 0.32f);
            TMP_Text pages = CreateText("MaterialPage", body.transform, "", 20, FontStyle.Normal, Color.black, TextAnchor.MiddleCenter);
            SetEquipmentGrowthRect(pages.rectTransform, 0.14f, 0.26f, 0.63f, 0.32f);
            TMP_Text warning = CreateText("BreakthroughWarning", body.transform, "", 20, FontStyle.Normal, new Color(0.6f, 0.12f, 0.08f), TextAnchor.MiddleLeft);
            SetEquipmentGrowthRect(warning.rectTransform, 0.03f, 0.175f, 0.97f, 0.25f);
            CreateEquipmentGrowthToggle(body.transform, "ConfirmIrreversible", "确认材料及全部培养投入永久消耗，不予返还", 0.13f);
            CreateEquipmentGrowthToggle(body.transform, "ConfirmHighLevel", "再次确认消耗高等级装备及其培养投入", 0.085f);
            Button preview = CreateButton(body.transform, "BreakthroughPreview", "预览突破", Color.clear, Color.white);
            SetEquipmentGrowthRect(preview.GetComponent<RectTransform>(), 0.03f, 0.015f, 0.30f, 0.075f);
            Button confirm = CreateButton(body.transform, "BreakthroughConfirm", "确认突破", Color.clear, Color.white);
            SetEquipmentGrowthRect(confirm.GetComponent<RectTransform>(), 0.36f, 0.015f, 0.63f, 0.075f);
            Button cancel = CreateButton(body.transform, "BreakthroughCancel", "取消", Color.gray, Color.white);
            SetEquipmentGrowthRect(cancel.GetComponent<RectTransform>(), 0.70f, 0.015f, 0.97f, 0.075f);
        }
        Transform panel = equipmentBreakthroughPanel.transform;
        equipmentIrreversibleToggle = FindSceneComponent<Toggle>(panel, "ConfirmIrreversible");
        equipmentHighLevelToggle = FindSceneComponent<Toggle>(panel, "ConfirmHighLevel");
        if (equipmentIrreversibleToggle != null)
        {
            equipmentIrreversibleToggle.onValueChanged.RemoveListener(OnEquipmentMaterialConfirmationChanged);
            equipmentIrreversibleToggle.onValueChanged.AddListener(OnEquipmentMaterialConfirmationChanged);
        }
        if (equipmentHighLevelToggle != null)
        {
            equipmentHighLevelToggle.onValueChanged.RemoveListener(OnEquipmentMaterialConfirmationChanged);
            equipmentHighLevelToggle.onValueChanged.AddListener(OnEquipmentMaterialConfirmationChanged);
        }
        BindSceneButton(FindSceneComponent<Button>(panel, "BreakthroughCancel"), CloseEquipmentBreakthroughPanel);
        BindSceneButton(FindSceneComponent<Button>(panel, "BreakthroughPreview"), () => StartCoroutine(PreviewEquipmentBreakthroughSelection()));
        BindSceneButton(FindSceneComponent<Button>(panel, "BreakthroughConfirm"), ConfirmEquipmentBreakthrough);
        BindSceneButton(FindSceneComponent<Button>(panel, "MaterialPrevious"), () => StartCoroutine(LoadEquipmentBreakthroughCandidates(Math.Max(1, equipmentMaterialPageNumber - 1))));
        BindSceneButton(FindSceneComponent<Button>(panel, "MaterialNext"), () => StartCoroutine(LoadEquipmentBreakthroughCandidates(equipmentMaterialPageNumber + 1)));
        BindSceneButton(FindSceneComponent<Button>(panel, "MaterialReload"), () => StartCoroutine(LoadEquipmentBreakthroughCandidates(equipmentMaterialPageNumber)));
        ConfigureEquipmentBreakthroughPresentation(panel);
        BindSceneButton(FindSceneComponent<Button>(panel, "BreakthroughClose"), CloseEquipmentBreakthroughPanel);
    }


    private static void SetEquipmentGrowthRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void CreateEquipmentGrowthToggle(Transform parent, string name, string text, float bottom)
    {
        Image hitArea = CreateImage(name, parent, Color.clear);
        SetEquipmentGrowthRect(hitArea.rectTransform, 0.03f, bottom, 0.97f, bottom + 0.041f);
        Toggle toggle = hitArea.gameObject.AddComponent<Toggle>();
        Image box = CreateImage("Box", hitArea.transform, Color.gray);
        SetEquipmentGrowthRect(box.rectTransform, 0, 0.08f, 0.028f, 0.92f);
        Image check = CreateImage("Check", box.transform, new Color(0.2f, 0.7f, 0.35f));
        SetEquipmentGrowthRect(check.rectTransform, 0.15f, 0.15f, 0.85f, 0.85f);
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = false;
        TMP_Text label = CreateText("Text", hitArea.transform, text, 20, FontStyle.Normal, Color.black, TextAnchor.MiddleLeft);
        SetEquipmentGrowthRect(label.rectTransform, 0.04f, 0, 1, 1);
    }

    private void OnEquipmentMaterialConfirmationChanged(bool value)
    {
        if (equipmentBreakthroughPanel != null && equipmentBreakthroughPanel.activeSelf)
            RefreshEquipmentBreakthroughConfirmation(equipmentBreakthroughPanel.transform);
    }

    private void RefreshEquipmentBreakthroughConfirmation(Transform root)
    {
        bool previewed = equipmentBreakthroughSelection != null;
        bool highLevel = previewed && equipmentBreakthroughSelection.requiresHighLevelConfirmation;
        SetEquipmentGrowthButtonEnabled(root, "BreakthroughConfirm", !equipmentBreakthroughBusy && previewed && equipmentIrreversibleToggle != null && equipmentIrreversibleToggle.isOn
            && (!highLevel || (equipmentHighLevelToggle != null && equipmentHighLevelToggle.isOn)));
    }

    private void RenderEquipmentBreakthroughPanel()
    {
        EquipmentUpgradePreviewData plan = equipmentBreakthroughPlan;
        if (plan == null || equipmentBreakthroughPanel == null || !equipmentBreakthroughPanel.activeSelf) return;
        Transform root = equipmentBreakthroughPanel.transform;
        RenderEquipmentBreakthroughHeader(root, plan);
        SetSceneText(root, "MaterialPage", $"{equipmentMaterialPageNumber} / {Math.Max(1L, (equipmentMaterialTotal + EquipmentMaterialPageSize - 1) / EquipmentMaterialPageSize)}");
        Transform slotParent = FindSceneTransform(root, "MaterialSlots");
        if (slotParent != null)
        {
            for (int index = equipmentMaterialSlots.Count; index < plan.requiredMaterialCount; index++)
            {
                Button slot = FindSceneComponent<Button>(slotParent, "MaterialSlot" + index);
                if (slot == null)
                {
                    slot = CreateButton(slotParent, "MaterialSlot" + index, "材料", Color.clear, Color.black);
                    LayoutElement element = slot.gameObject.AddComponent<LayoutElement>();
                    element.preferredWidth = 228;
                    element.minWidth = 228;
                }
                equipmentMaterialSlots.Add(slot);
            }
            for (int index = 0; index < equipmentMaterialSlots.Count; index++)
            {
                Button slot = equipmentMaterialSlots[index];
                if (slot == null) continue;
                slot.gameObject.SetActive(index < plan.requiredMaterialCount);
                EquipmentBreakthroughMaterial material = index < equipmentBreakthroughMaterials.Count ? equipmentBreakthroughMaterials[index] : null;
                SetSceneButtonText(slot, material == null ? $"材料 {index + 1}\n未选择" : $"{(string.IsNullOrWhiteSpace(material.name) ? "同款装备" : material.name)}\nLv.{material.level} · 移除");
                ConfigureEquipmentMaterialPresentation(slot, material, material != null, true);
                slot.interactable = !equipmentBreakthroughBusy && material != null;
                BindSceneButton(slot, () => ToggleEquipmentBreakthroughMaterial(material));
            }
        }
        for (int index = 0; index < EquipmentMaterialPageSize; index++)
        {
            Button row = FindSceneComponent<Button>(root, "MaterialCandidate" + index);
            if (row == null) continue;
            EquipmentBreakthroughMaterial material = index < equipmentMaterialPage.Length ? equipmentMaterialPage[index] : null;
            row.gameObject.SetActive(material != null);
            if (material == null) continue;
            bool selected = equipmentBreakthroughMaterials.Any(entry => entry.playerEquipmentId == material.playerEquipmentId);
            bool selectable = IsEquipmentMaterialSelectable(material, plan);
            row.interactable = !equipmentBreakthroughBusy && (selected || (selectable && equipmentBreakthroughMaterials.Count < plan.requiredMaterialCount));
            SetSceneButtonText(row, $"{(string.IsNullOrWhiteSpace(material.name) ? "同款装备" : material.name)}   Lv.{material.level}"
                + "\n" + (selected ? "已选择" : !selectable ? material.unavailableReason
                    : equipmentBreakthroughMaterials.Count >= plan.requiredMaterialCount ? "材料已选满" : "点击选择")
                + (material.level > 1 ? " · 含培养投入" : ""));
            ConfigureEquipmentMaterialPresentation(row, material, selected, false);
            BindSceneButton(row, () => ToggleEquipmentBreakthroughMaterial(material));
        }
        bool previewed = equipmentBreakthroughSelection != null;
        bool highLevel = previewed && equipmentBreakthroughSelection.requiresHighLevelConfirmation;
        if (equipmentIrreversibleToggle != null)
        {
            equipmentIrreversibleToggle.gameObject.SetActive(previewed);
            equipmentIrreversibleToggle.interactable = !equipmentBreakthroughBusy;
        }
        if (equipmentHighLevelToggle != null)
        {
            equipmentHighLevelToggle.gameObject.SetActive(highLevel);
            equipmentHighLevelToggle.interactable = !equipmentBreakthroughBusy;
        }
        SetEquipmentGrowthButtonEnabled(root, "BreakthroughPreview", !equipmentBreakthroughBusy && equipmentBreakthroughMaterials.Count == plan.requiredMaterialCount);
        RefreshEquipmentBreakthroughConfirmation(root);
        SetEquipmentGrowthButtonEnabled(root, "MaterialPrevious", !equipmentBreakthroughBusy && equipmentMaterialPageNumber > 1);
        SetEquipmentGrowthButtonEnabled(root, "MaterialNext", !equipmentBreakthroughBusy && (long)equipmentMaterialPageNumber * EquipmentMaterialPageSize < equipmentMaterialTotal);
        SetEquipmentGrowthButtonEnabled(root, "MaterialReload", !equipmentBreakthroughBusy);
        RefreshEquipmentBreakthroughLayout(root, plan.requiredMaterialCount, previewed);
    }

    private void SetEquipmentGrowthButtonEnabled(Transform root, string name, bool enabled)
    {
        Button button = FindSceneComponent<Button>(root, name);
        if (button != null) button.interactable = enabled;
    }

    [Serializable]
    private sealed class EquipmentGrowthSnapshot
    {
        public int version;
        public long playerId;
        public EquipmentGrowthRequest[] operations;
    }

    [Serializable]
    private sealed class EquipmentGrowthRequest
    {
        public long playerId;
        public long playerEquipmentId;
        public int expectedLevel;
        public int targetLevel;
        public string path;
        public string json;
        public string idempotencyKey;
        [NonSerialized] public bool inFlight;
        public bool responseCompleted;
        [NonSerialized] public int recoveryAttempts;
        [NonSerialized] public float nextRecoveryTime;
        public long[] materialPlayerEquipmentIds;
        [NonSerialized] public HomePageUI owner;
        public bool resultResolved;
        public string completionMessage;
    }

    private static bool IsValidEquipmentBreakthroughResult(EquipmentBreakthroughResult result, EquipmentBreakthroughExecuteRequest payload)
    {
        if (result == null || payload == null || result.contractVersion != 2 || result.refundPolicy != "NONE"
            || result.playerEquipmentId != payload.playerEquipmentId || result.fromLevel != payload.expectedCurrentLevel
            || result.level != payload.targetLevel || payload.materialPlayerEquipmentIds == null
            || result.materialCount != payload.materialPlayerEquipmentIds.Length || result.consumedMaterials == null
            || result.consumedMaterials.Length != result.materialCount) return false;
        HashSet<long> remaining = new HashSet<long>(payload.materialPlayerEquipmentIds);
        int highLevelCount = 0;
        foreach (EquipmentBreakthroughMaterial material in result.consumedMaterials)
        {
            if (material == null || !remaining.Remove(material.playerEquipmentId) || material.level < 1) return false;
            if (material.level > 1) highLevelCount++;
        }
        return remaining.Count == 0 && highLevelCount == result.highLevelMaterialCount;
    }

#pragma warning disable 0649
    [Serializable]
    private class EquipmentBreakthroughResultResponse : BasicApiResponse { public EquipmentBreakthroughResult data; }
    [Serializable]
    private class EquipmentBreakthroughResult : EquipmentUpgradeData
    {
        public int contractVersion;
        public string requestNo;
        public int materialCount;
        public int highLevelMaterialCount;
        public string refundPolicy;
        public EquipmentBreakthroughMaterial[] consumedMaterials;
    }

    [Serializable]
    private class EquipmentBreakthroughMaterial
    {
        public long playerEquipmentId;
        public long equipmentId;
        public string equipmentKey;
        public string name;
        public string icon;
        public string quality;
        public int level;
        public string acquireTime;
        public bool selectable;
        public bool hasCultivationInvestment;
        public string unavailableReason;
        public int targetLevel;
        public int selectionOrder;
    }

    [Serializable]
    private class EquipmentMaterialPageResponse : BasicApiResponse { public EquipmentMaterialPage data; }
    [Serializable]
    private class EquipmentMaterialPage
    {
        public int contractVersion;
        public long total;
        public int page;
        public int pageSize;
        public EquipmentBreakthroughMaterial[] items;
    }

    [Serializable]
    private class EquipmentBreakthroughPreviewRequest
    {
        public long playerEquipmentId;
        public int expectedCurrentLevel;
        public int targetLevel;
        public string planFingerprint;
        public long[] materialPlayerEquipmentIds;
    }

    [Serializable]
    private class EquipmentBreakthroughExecuteRequest : EquipmentBreakthroughPreviewRequest
    {
        public string materialSelectionFingerprint;
        public bool confirmIrreversibleMaterialConsumption;
        public bool confirmHighLevelMaterialLoss;
    }

    [Serializable]
    private class EquipmentBreakthroughSelectionResponse : BasicApiResponse { public EquipmentBreakthroughSelection data; }
    [Serializable]
    private class EquipmentBreakthroughSelection
    {
        public int contractVersion;
        public long playerEquipmentId;
        public int currentLevel;
        public int targetLevel;
        public int requiredMaterialCount;
        public int highLevelMaterialCount;
        public bool requiresHighLevelConfirmation;
        public string materialPolicy;
        public string refundPolicy;
        public string warning;
        public string planFingerprint;
        public string materialSelectionFingerprint;
        public EquipmentBreakthroughMaterial[] selectedMaterials;
    }
#pragma warning restore 0649
}
