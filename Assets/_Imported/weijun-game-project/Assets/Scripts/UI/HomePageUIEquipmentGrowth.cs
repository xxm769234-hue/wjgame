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
    private static readonly Dictionary<long, EquipmentGrowthRequest> PendingEquipmentGrowth = new Dictionary<long, EquipmentGrowthRequest>();
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
        return PendingEquipmentGrowth.ContainsKey(GameSession.PlayerId);
    }

    private void ResumePendingEquipmentGrowthRequest()
    {
        if (equipmentMutationRunning || equipmentRefreshRunning
            || !PendingEquipmentGrowth.TryGetValue(GameSession.PlayerId, out EquipmentGrowthRequest pending)
            || pending.inFlight) return;
        StartCoroutine(string.IsNullOrEmpty(pending.idempotencyKey)
            ? ReconcileSingleEquipmentUpgrade(pending) : SendEquipmentGrowthRequest(pending));
    }

    private IEnumerator ReconcileSingleEquipmentUpgrade(EquipmentGrowthRequest pending)
    {
        string token = GameSession.Token;
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{pending.playerEquipmentId}/upgrade-preview");
        if (request == null)
        {
            equipmentStatusMessage = requestCreationError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        equipmentMutationRunning = true;
        pending.inFlight = true;
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
                    || !IsValidEquipmentSinglePreview(response.data, pending.playerEquipmentId, response.data.currentLevel)
                    || response.data.currentLevel < pending.expectedLevel)
                {
                    equipmentStatusMessage = "暂时无法核对升级结果，请稍后再次核对；不会自动重复升级";
                    yield break;
                }
                PendingEquipmentGrowth.Remove(pending.playerId);
                string message = response.data.currentLevel > pending.expectedLevel
                    ? $"服务器装备等级为 Lv.{response.data.currentLevel}，已同步升级结果"
                    : "服务器装备等级未变化，已重新读取费用；如需升级请重新确认";
                yield return RefreshEquipmentGrowthState(pending.playerId, token, message);
            }
        }
        finally
        {
            pending.inFlight = false;
            equipmentMutationRunning = false;
            equipmentRefreshRunning = false;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }
    }

    private IEnumerator SendEquipmentGrowthRequest(EquipmentGrowthRequest operation)
    {
        if (operation == null || operation.playerId != GameSession.PlayerId || operation.playerId <= 0
            || equipmentMutationRunning || operation.inFlight) yield break;
        if (PendingEquipmentGrowth.TryGetValue(operation.playerId, out EquipmentGrowthRequest pending)
            && !ReferenceEquals(pending, operation)) yield break;
        if (!TryCreateAuthorizedPost(operation.path, operation.json, out UnityWebRequest request, out string error))
        {
            equipmentStatusMessage = error;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        if (!string.IsNullOrEmpty(operation.idempotencyKey)) ApiRequestUtility.SetIdempotencyKey(request, operation.idempotencyKey);
        string token = GameSession.Token;
        equipmentMutationRunning = true;
        operation.inFlight = true;
        PendingEquipmentGrowth[operation.playerId] = operation;
        CloseEquipmentBreakthroughPanel();
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
                    bool rejected = response != null && response.code != 200
                        && request.responseCode > 0 && request.responseCode < 500;
                    if (!rejected)
                    {
                        equipmentStatusMessage = "提交结果尚未确认，请点击“核对上次结果”；不要重复发起新升级";
                        yield break;
                    }
                    PendingEquipmentGrowth.Remove(operation.playerId);
                    completion = EquipmentGrowthError(response.code, GetApiError(response));
                }
                else if (operation.path == "/equipment/auto-upgrade")
                {
                    AutoUpgradeApiResponse result = ParseJson<AutoUpgradeApiResponse>(json);
                    if (!IsValidEquipmentGrowthResult(result != null ? result.data : null, operation))
                    {
                        equipmentStatusMessage = "升级响应不完整，请核对上次结果";
                        yield break;
                    }
                    PendingEquipmentGrowth.Remove(operation.playerId);
                    completion = $"装备已升至 Lv.{result.data.reachedLevel}，{FormatAutoUpgradeOutcome(result.data.outcome)}";
                }
                else
                {
                    EquipmentUpgradeApiResponse result = ParseJson<EquipmentUpgradeApiResponse>(json);
                    if (result == null || result.data == null || result.data.playerEquipmentId != operation.playerEquipmentId
                        || result.data.fromLevel != operation.expectedLevel || result.data.level != operation.targetLevel)
                    {
                        equipmentStatusMessage = "升级响应不完整，请核对上次结果";
                        yield break;
                    }
                    if (operation.path == "/equipment/breakthrough")
                    {
                        EquipmentBreakthroughResultResponse breakthrough = ParseJson<EquipmentBreakthroughResultResponse>(json);
                        EquipmentBreakthroughExecuteRequest payload = ParseJson<EquipmentBreakthroughExecuteRequest>(operation.json);
                        if (!IsValidEquipmentBreakthroughResult(breakthrough != null ? breakthrough.data : null, payload))
                        {
                            equipmentStatusMessage = "突破响应不完整，请核对上次结果";
                            yield break;
                        }
                    }
                    PendingEquipmentGrowth.Remove(operation.playerId);
                    SyncEquipmentUpgradeResourceBalances(result.data);
                    completion = (operation.path == "/equipment/breakthrough" ? "突破成功" : "升级成功")
                        + $"，装备 Lv.{result.data.level}，消耗金币 {result.data.goldCost}";
                }
            }
            yield return RefreshEquipmentGrowthState(operation.playerId, token, completion);
        }
        finally
        {
            operation.inFlight = false;
            equipmentMutationRunning = false;
            equipmentRefreshRunning = false;
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

    private IEnumerator RefreshEquipmentGrowthState(long playerId, string token, string message)
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
            else inventoryError = "余额同步失败，请刷新";
        }
        yield return GetEquipmentInventoryList(true);
        if (playerId != GameSession.PlayerId || token != GameSession.Token) yield break;
        equipmentRefreshRunning = false;
        equipmentStatusMessage = message + (string.IsNullOrEmpty(inventoryError) ? string.Empty : "；" + inventoryError)
            + (_equipmentDataReadSucceeded ? string.Empty : "；装备或穿戴属性同步失败，请刷新后再操作");
        if (!_equipmentDataReadSucceeded)
        {
            equipmentUpgradePreview = null;
            equipmentUpgradePreviewRunning = false;
            equipmentUpgradePreviewError = "装备同步失败，请重新同步后预览";
        }
        SetStatus(equipmentStatusMessage);
    }

    private static string EquipmentGrowthError(int code, string message)
    {
        switch (code)
        {
            case 6204: return "资源不足，已重新读取服务器费用与余额";
            case 6207: return "装备等级已变化，请重新预览";
            case 6208: return "请求号已失效，已清除旧请求，请重新预览并确认";
            case 6209: return "升级配置已变化，请重新预览";
            case 6210: return "需要先手动突破，请查看最新预览并选择突破材料";
            case 6211: return "突破材料数量不符，请重新选择";
            case 6212: return "材料状态已变化，请重新选择服务器提供的候选装备";
            case 6213: return "需要确认材料及培养投入永久消耗，请重新勾选确认";
            case 6214: return "材料快照已变化，请重新选择并预览";
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
        if (item == null || equipmentMutationRunning || equipmentRefreshRunning || HasPendingEquipmentGrowthRequest()
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
            || equipmentRefreshRunning || HasPendingEquipmentGrowthRequest() || !HasEquipmentUpgradeFunds(plan)
            || equipmentIrreversibleToggle == null || !equipmentIrreversibleToggle.isOn
            || (selection.requiresHighLevelConfirmation && (equipmentHighLevelToggle == null || !equipmentHighLevelToggle.isOn))) return;
        long[] ids = equipmentBreakthroughMaterials.Select(material => material.playerEquipmentId).OrderBy(id => id).ToArray();
        if (!IsValidEquipmentMaterialSelection(selection, plan, ids)) return;
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
            path = "/equipment/breakthrough", json = JsonUtility.ToJson(payload), idempotencyKey = Guid.NewGuid().ToString("D")
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
            Button preview = CreateButton(body.transform, "BreakthroughPreview", "预览突破", new Color(0.17f, 0.42f, 0.36f), Color.white);
            SetEquipmentGrowthRect(preview.GetComponent<RectTransform>(), 0.03f, 0.015f, 0.30f, 0.075f);
            Button confirm = CreateButton(body.transform, "BreakthroughConfirm", "确认突破", new Color(0.17f, 0.42f, 0.36f), Color.white);
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
        RenderEquipmentBreakthroughPanel();
    }

    private void RenderEquipmentBreakthroughPanel()
    {
        EquipmentUpgradePreviewData plan = equipmentBreakthroughPlan;
        if (plan == null || equipmentBreakthroughPanel == null || !equipmentBreakthroughPanel.activeSelf) return;
        Transform root = equipmentBreakthroughPanel.transform;
        SetSceneText(root, "BreakthroughTitle", $"{plan.name}  Lv.{plan.currentLevel} → Lv.{plan.targetLevel}");
        string costs = $"金币 {plan.goldCost}（持有 {plan.availableGold}）";
        if (plan.resourceCosts != null) foreach (EquipmentUpgradeResourceCostPreviewData cost in plan.resourceCosts)
            if (cost != null) costs += $"  {cost.itemName ?? cost.itemKey} {cost.amount}（持有 {cost.availableAmount}）";
        SetSceneText(root, "BreakthroughCost", costs + $"\n材料 {equipmentBreakthroughMaterials.Count}/{plan.requiredMaterialCount}");
        SetSceneText(root, "BreakthroughWarning", equipmentBreakthroughBusy ? "正在读取服务器数据..." : equipmentBreakthroughMessage);
        SetSceneText(root, "MaterialPage", $"{equipmentMaterialPageNumber}/{Math.Max(1L, (equipmentMaterialTotal + EquipmentMaterialPageSize - 1) / EquipmentMaterialPageSize)}  共 {equipmentMaterialTotal} 件");
        Transform slotParent = FindSceneTransform(root, "MaterialSlots");
        if (slotParent != null)
        {
            for (int index = equipmentMaterialSlots.Count; index < plan.requiredMaterialCount; index++)
            {
                Button slot = FindSceneComponent<Button>(slotParent, "MaterialSlot" + index);
                if (slot == null)
                {
                    slot = CreateButton(slotParent, "MaterialSlot" + index, "材料", new Color(0.75f, 0.82f, 0.81f), Color.black);
                    LayoutElement element = slot.gameObject.AddComponent<LayoutElement>();
                    element.preferredWidth = 205;
                    element.minWidth = 160;
                }
                equipmentMaterialSlots.Add(slot);
            }
            for (int index = 0; index < equipmentMaterialSlots.Count; index++)
            {
                Button slot = equipmentMaterialSlots[index];
                if (slot == null) continue;
                slot.gameObject.SetActive(index < plan.requiredMaterialCount);
                EquipmentBreakthroughMaterial material = index < equipmentBreakthroughMaterials.Count ? equipmentBreakthroughMaterials[index] : null;
                SetSceneButtonText(slot, material == null ? $"材料 {index + 1}\n未选择" : $"{material.name ?? material.equipmentKey}\nLv.{material.level}  ID {material.playerEquipmentId}\n移除");
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
            SetSceneButtonText(row, (selected ? "[已选择] " : "") + $"{material.name ?? material.equipmentKey}   Lv.{material.level}   ID {material.playerEquipmentId}"
                + (material.level > 1 ? "   含培养投入" : "") + (!selectable ? "   " + material.unavailableReason : ""));
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
        SetEquipmentGrowthButtonEnabled(root, "BreakthroughConfirm", !equipmentBreakthroughBusy && previewed && equipmentIrreversibleToggle != null && equipmentIrreversibleToggle.isOn
            && (!highLevel || (equipmentHighLevelToggle != null && equipmentHighLevelToggle.isOn)));
        SetEquipmentGrowthButtonEnabled(root, "MaterialPrevious", !equipmentBreakthroughBusy && equipmentMaterialPageNumber > 1);
        SetEquipmentGrowthButtonEnabled(root, "MaterialNext", !equipmentBreakthroughBusy && (long)equipmentMaterialPageNumber * EquipmentMaterialPageSize < equipmentMaterialTotal);
        SetEquipmentGrowthButtonEnabled(root, "MaterialReload", !equipmentBreakthroughBusy);
    }

    private void SetEquipmentGrowthButtonEnabled(Transform root, string name, bool enabled)
    {
        Button button = FindSceneComponent<Button>(root, name);
        if (button != null) button.interactable = enabled;
    }

    private sealed class EquipmentGrowthRequest
    {
        public long playerId;
        public long playerEquipmentId;
        public int expectedLevel;
        public int targetLevel;
        public string path;
        public string json;
        public string idempotencyKey;
        public bool inFlight;
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
