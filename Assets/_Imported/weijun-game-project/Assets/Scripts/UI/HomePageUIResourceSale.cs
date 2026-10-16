using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int ResourceSaleHistoryPageSize = 8;
    private const string ResourceSalePendingPlayerKey = "ResourceSale.Pending.PlayerId";
    private const string ResourceSalePendingSourceKey = "ResourceSale.Pending.SourceItemId";
    private const string ResourceSalePendingLotsKey = "ResourceSale.Pending.LotCount";
    private const string ResourceSalePendingRevisionKey = "ResourceSale.Pending.ConfigRevision";
    private const string ResourceSalePendingIdempotencyKey = "ResourceSale.Pending.IdempotencyKey";

    private ResourceSaleOptionsData resourceSaleOptions;
    private ResourceSaleOption activeResourceSaleOption;
    private string activeResourceSaleItemKey = string.Empty;
    private long activeResourceSaleLotCount;
    private string resourceSaleQuoteStatus = string.Empty;
    private bool resourceSaleOptionsLoading;
    private bool resourceSaleSubmitting;
    private bool resourceSaleHistoryLoading;
    private bool resourceSaleSubmissionBlocked;
    private bool activeResourceSalePendingRetry;
    private bool resourceSaleHistoryVisible;
    private bool resourceSaleDialogVisible;
    private int resourceSaleContextVersion;
    private ResourceSaleDialogView resourceSaleDialogView;
    private readonly List<ResourceSaleHistoryItem> resourceSaleHistoryItems = new List<ResourceSaleHistoryItem>();
    private long resourceSaleHistoryNextCursor;
    private bool resourceSaleHistoryHasMore;
    private string resourceSaleHistoryStatus = string.Empty;

    private void ResetResourceSaleForInventory()
    {
        resourceSaleContextVersion++;
        resourceSaleOptions = null;
        activeResourceSaleOption = null;
        activeResourceSaleItemKey = string.Empty;
        activeResourceSaleLotCount = 0;
        resourceSaleQuoteStatus = string.Empty;
        resourceSaleOptionsLoading = false;
        resourceSaleSubmissionBlocked = false;
        activeResourceSalePendingRetry = false;
        resourceSaleHistoryVisible = false;
        resourceSaleDialogVisible = false;
        resourceSaleHistoryItems.Clear();
        resourceSaleHistoryNextCursor = 0;
        resourceSaleHistoryHasMore = false;
        resourceSaleHistoryStatus = string.Empty;
        HideResourceSaleDialog();
    }

    private string GetInventorySidebarHint()
    {
        return inventoryServerBacked
            ? "服务器库存\n按正式报价出售"
            : "本地奖励库存\n出售功能需账号数据";
    }

    private string GetInventorySaleContextHint(InventoryItemResponse item)
    {
        if (!inventoryServerBacked || !HasAuthSession())
        {
            return "登录后可出售资源";
        }

        if (resourceSaleOptionsLoading)
        {
            return "正在读取正式报价";
        }

        if (resourceSaleOptions == null)
        {
            return "点击读取正式报价";
        }

        if (!resourceSaleOptions.enabled)
        {
            return "资源出售暂未开放";
        }

        ResourceSaleOption option = FindResourceSaleOption(item);
        if (option == null)
        {
            return "该物品暂无出售规则";
        }

        if (GetResourceSaleMaximumLots(option) < GetResourceSaleMinimumLots(option))
        {
            return "余额或今日额度不足";
        }

        return $"每批{FormatResourceSaleQuantity(option.sourceQuantityPerLot)}换{FormatResourceSaleQuantity(option.targetQuantityPerLot)}金币";
    }

    private void BeginSelectedInventorySale()
    {
        InventoryItemResponse item = FindCurrentInventoryItem(selectedInventoryItemKey);
        if (item == null)
        {
            return;
        }

        if (!inventoryServerBacked || !HasAuthSession())
        {
            inventoryStatusMessage = "玩家资源出售需要登录账号，并以服务器正式报价和库存为准";
            RenderInventoryPanel();
            return;
        }

        if (resourceSaleSubmitting)
        {
            inventoryStatusMessage = "资源出售请求正在处理中，请勿重复提交";
            RenderInventoryPanel();
            return;
        }

        if (resourceSaleOptionsLoading)
        {
            inventoryStatusMessage = "正在读取服务器正式报价，请稍候";
            RenderInventoryPanel();
            return;
        }

        activeResourceSaleItemKey = GetInventoryItemKey(item);
        inventoryStatusMessage = $"正在读取{item.name}的正式出售报价...";
        RenderInventoryPanel();
        int contextVersion = resourceSaleContextVersion;
        StartCoroutine(LoadResourceSaleOptions(activeResourceSaleItemKey, true, false, string.Empty, contextVersion));
    }

    private IEnumerator LoadResourceSaleOptions(
        string itemKey,
        bool openDialog,
        bool preserveDialogOnFailure,
        string successStatus,
        int contextVersion)
    {
        resourceSaleOptionsLoading = true;
        if (preserveDialogOnFailure && activeResourceSaleOption != null)
        {
            resourceSaleQuoteStatus = "正在刷新服务器正式报价...";
            RenderResourceSaleQuote(true);
        }

        UnityWebRequest request = CreateAuthorizedGet("/resource-sales/options");
        if (request == null)
        {
            resourceSaleOptionsLoading = false;
            HandleResourceSaleOptionsFailure(requestCreationError, preserveDialogOnFailure, contextVersion);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (contextVersion != resourceSaleContextVersion)
            {
                yield break;
            }

            resourceSaleOptionsLoading = false;

            ResourceSaleOptionsApiResponse response = ParseJson<ResourceSaleOptionsApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                string message = response != null && !string.IsNullOrWhiteSpace(response.message)
                    ? response.message
                    : ApiRequestUtility.GetErrorMessage(request, "正式报价读取失败");
                if (response != null && (response.code == 7201 || response.code == 7209))
                {
                    resourceSaleOptions = null;
                    activeResourceSaleOption = null;
                    HideResourceSaleDialog();
                    SetResourceSaleInventoryStatus(message);
                    yield break;
                }

                HandleResourceSaleOptionsFailure(message, preserveDialogOnFailure, contextVersion);
                yield break;
            }

            long previousRevision = resourceSaleOptions == null ? 0 : resourceSaleOptions.configRevision;
            resourceSaleOptions = response.data;
            if (!preserveDialogOnFailure
                && !string.Equals(selectedInventoryItemKey, itemKey, StringComparison.Ordinal))
            {
                yield break;
            }

            if (!resourceSaleOptions.enabled)
            {
                activeResourceSaleOption = null;
                HideResourceSaleDialog();
                SetResourceSaleInventoryStatus("玩家资源出售当前未开放，请等待管理端发布并开启兑换规则");
                yield break;
            }

            InventoryItemResponse item = FindCurrentInventoryItem(itemKey);
            ResourceSaleOption option = FindResourceSaleOption(item);
            if (item == null || option == null)
            {
                activeResourceSaleOption = null;
                HideResourceSaleDialog();
                SetResourceSaleInventoryStatus("该物品不在服务器当前允许出售的资源列表中");
                yield break;
            }

            activeResourceSaleItemKey = GetInventoryItemKey(item);
            activeResourceSaleOption = option;
            resourceSaleSubmissionBlocked = false;

            if (previousRevision != 0 && previousRevision != resourceSaleOptions.configRevision)
            {
                ClearPendingResourceSaleIntent();
            }

            long minimumLots = GetResourceSaleMinimumLots(option);
            long maximumLots = GetResourceSaleMaximumLots(option);
            if (!TryRestorePendingResourceSaleLot(option, resourceSaleOptions.configRevision, minimumLots, out long restoredLots))
            {
                activeResourceSalePendingRetry = false;
                restoredLots = activeResourceSaleLotCount > 0
                    ? ClampResourceSaleLots(activeResourceSaleLotCount, minimumLots, maximumLots)
                    : minimumLots;
            }
            else
            {
                activeResourceSalePendingRetry = true;
            }

            activeResourceSaleLotCount = restoredLots;
            resourceSaleQuoteStatus = activeResourceSalePendingRetry
                ? "检测到上次成交结果未确认，可使用原请求编号安全重试"
                : string.IsNullOrEmpty(successStatus)
                    ? $"报价版本 {resourceSaleOptions.configRevision}，额度日期 {resourceSaleOptions.usageDate}"
                    : successStatus;

            bool selectedItemStillActive = string.Equals(
                selectedInventoryItemKey,
                activeResourceSaleItemKey,
                StringComparison.Ordinal);
            if (openDialog
                && IsInventoryPanelVisible()
                && selectedItemStillActive
                && (!preserveDialogOnFailure || resourceSaleDialogVisible))
            {
                ShowResourceSaleDialog();
            }
        }
    }

    private void HandleResourceSaleOptionsFailure(string message, bool preserveDialog, int contextVersion)
    {
        if (contextVersion != resourceSaleContextVersion)
        {
            return;
        }

        string finalMessage = string.IsNullOrWhiteSpace(message) ? "正式报价读取失败，请稍后重试" : message.Trim();
        if (preserveDialog && activeResourceSaleOption != null && IsInventoryPanelVisible())
        {
            resourceSaleQuoteStatus = finalMessage;
            RenderResourceSaleQuote(false);
            return;
        }

        SetResourceSaleInventoryStatus(finalMessage);
    }

    private void SetResourceSaleInventoryStatus(string message)
    {
        inventoryStatusMessage = message ?? string.Empty;
        if (IsInventoryPanelVisible())
        {
            RenderInventoryPanel();
        }
        else
        {
            SetStatus(inventoryStatusMessage);
        }
    }

    private bool IsInventoryPanelVisible()
    {
        return inventoryPanelObject != null && inventoryPanelObject.activeInHierarchy;
    }

    private ResourceSaleOption FindResourceSaleOption(InventoryItemResponse item)
    {
        if (item == null || resourceSaleOptions == null || resourceSaleOptions.options == null)
        {
            return null;
        }

        for (int i = 0; i < resourceSaleOptions.options.Length; i++)
        {
            ResourceSaleOption option = resourceSaleOptions.options[i];
            if (option == null)
            {
                continue;
            }

            if (item.itemId > 0 && option.sourceItemId == item.itemId)
            {
                return option;
            }

            if (!string.IsNullOrWhiteSpace(item.itemKey)
                && string.Equals(item.itemKey.Trim(), option.sourceItemKey, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }

    private ResourceSaleOption FindResourceSaleOption(int itemId, string itemKey)
    {
        if (resourceSaleOptions == null || resourceSaleOptions.options == null)
        {
            return null;
        }

        for (int i = 0; i < resourceSaleOptions.options.Length; i++)
        {
            ResourceSaleOption option = resourceSaleOptions.options[i];
            if (option == null)
            {
                continue;
            }

            if (itemId > 0 && option.sourceItemId == itemId)
            {
                return option;
            }

            if (!string.IsNullOrWhiteSpace(itemKey)
                && string.Equals(itemKey.Trim(), option.sourceItemKey, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }

    private void ShowResourceSaleDialog()
    {
        resourceSaleDialogVisible = true;
        resourceSaleHistoryVisible = false;
        EnsureResourceSaleDialogView();
        RenderResourceSaleQuote(false);
    }

    private void EnsureResourceSaleDialogView()
    {
        Transform parent = inventoryPanelView != null
            ? inventoryPanelView.transform
            : inventoryPanelObject != null ? inventoryPanelObject.transform : null;
        if (parent == null)
        {
            return;
        }

        if (resourceSaleDialogView == null)
        {
            resourceSaleDialogView = parent.GetComponentInChildren<ResourceSaleDialogView>(true);
            if (resourceSaleDialogView == null)
            {
                Debug.LogError("Inventory scene is missing ResourceSaleDialog; sale UI will not be created at runtime.", this);
                return;
            }
        }

        if (!resourceSaleDialogView.EnsureBuilt(defaultFont))
        {
            Debug.LogError("ResourceSaleDialog is missing authored sale controls; sale UI will not be created at runtime.", resourceSaleDialogView);
            resourceSaleDialogView = null;
        }
    }

    private void RenderResourceSaleQuote(bool busy)
    {
        if (activeResourceSaleOption == null || !resourceSaleDialogVisible || !IsInventoryPanelVisible())
        {
            return;
        }

        EnsureResourceSaleDialogView();
        if (resourceSaleDialogView == null)
        {
            return;
        }

        ResourceSaleOption option = activeResourceSaleOption;
        long minimumLots = GetResourceSaleMinimumLots(option);
        long maximumLots = GetResourceSaleMaximumLots(option);
        bool hasAvailableLots = maximumLots >= minimumLots;
        if (!activeResourceSalePendingRetry)
        {
            activeResourceSaleLotCount = ClampResourceSaleLots(activeResourceSaleLotCount, minimumLots, maximumLots);
        }

        long soldQuantity = SaturatingMultiply(option.sourceQuantityPerLot, activeResourceSaleLotCount);
        long grantedQuantity = SaturatingMultiply(option.targetQuantityPerLot, activeResourceSaleLotCount);
        string sourceName = GetResourceSaleName(option.sourceItemName, option.sourceItemKey, "资源");
        string targetName = GetResourceSaleName(option.targetItemName, option.targetItemKey, "金币");
        string limits =
            $"持有 {FormatResourceSaleQuantity(option.sourceBalance)}  |  " +
            $"今日剩余 {FormatResourceSaleQuantity(option.remainingDailySourceQuantity)}  |  " +
            $"最多 {FormatResourceSaleQuantity(maximumLots)} 批";
        string summary = activeResourceSalePendingRetry
            ? $"重试上次请求：出售 {FormatResourceSaleQuantity(soldQuantity)} {sourceName}，获得 {FormatResourceSaleQuantity(grantedQuantity)} {targetName}"
            : hasAvailableLots
            ? $"将出售 {FormatResourceSaleQuantity(soldQuantity)} {sourceName}，获得 {FormatResourceSaleQuantity(grantedQuantity)} {targetName}"
            : "当前余额或今日额度不足，暂时无法完成出售";

        bool matchesLiveLimits = hasAvailableLots
            && activeResourceSaleLotCount >= minimumLots
            && activeResourceSaleLotCount <= maximumLots;
        bool canSubmit = (activeResourceSalePendingRetry || matchesLiveLimits)
            && !resourceSaleSubmissionBlocked;

        resourceSaleDialogView.ShowQuote(
            sourceName,
            $"每批消耗 {FormatResourceSaleQuantity(option.sourceQuantityPerLot)} {sourceName}，兑换 {FormatResourceSaleQuantity(option.targetQuantityPerLot)} {targetName}",
            limits,
            FormatResourceSaleQuantity(activeResourceSaleLotCount),
            summary,
            resourceSaleQuoteStatus,
            activeResourceSalePendingRetry ? "重试原请求" : "确认出售",
            activeResourceSaleLotCount > minimumLots,
            hasAvailableLots && activeResourceSaleLotCount < maximumLots,
            canSubmit,
            busy || resourceSaleOptionsLoading || resourceSaleSubmitting,
            ShowActiveResourceSaleQuote,
            ShowResourceSaleHistory,
            DecreaseResourceSaleLots,
            IncreaseResourceSaleLots,
            SelectMaximumResourceSaleLots,
            RefreshActiveResourceSaleQuote,
            ConfirmActiveResourceSale,
            HideResourceSaleDialog,
            option.sourceItemIcon);
    }

    private void ShowActiveResourceSaleQuote()
    {
        resourceSaleDialogVisible = true;
        resourceSaleHistoryVisible = false;
        if (activeResourceSaleOption != null)
        {
            RenderResourceSaleQuote(resourceSaleOptionsLoading || resourceSaleSubmitting);
        }
    }

    private void DecreaseResourceSaleLots()
    {
        ChangeResourceSaleLots(activeResourceSaleLotCount - 1);
    }

    private void IncreaseResourceSaleLots()
    {
        ChangeResourceSaleLots(activeResourceSaleLotCount + 1);
    }

    private void SelectMaximumResourceSaleLots()
    {
        if (activeResourceSaleOption != null)
        {
            ChangeResourceSaleLots(GetResourceSaleMaximumLots(activeResourceSaleOption));
        }
    }

    private void ChangeResourceSaleLots(long requestedLots)
    {
        if (activeResourceSaleOption == null || resourceSaleSubmitting || resourceSaleOptionsLoading)
        {
            return;
        }

        long minimumLots = GetResourceSaleMinimumLots(activeResourceSaleOption);
        long maximumLots = GetResourceSaleMaximumLots(activeResourceSaleOption);
        long nextLots = ClampResourceSaleLots(requestedLots, minimumLots, maximumLots);
        if (nextLots == activeResourceSaleLotCount)
        {
            return;
        }

        activeResourceSaleLotCount = nextLots;
        activeResourceSalePendingRetry = false;
        resourceSaleQuoteStatus = $"报价版本 {resourceSaleOptions.configRevision}，请确认本次出售数量";
        ClearPendingResourceSaleIntent();
        RenderResourceSaleQuote(false);
    }

    private void RefreshActiveResourceSaleQuote()
    {
        if (activeResourceSaleOption == null || resourceSaleOptionsLoading || resourceSaleSubmitting)
        {
            return;
        }

        int contextVersion = resourceSaleContextVersion;
        StartCoroutine(LoadResourceSaleOptions(
            activeResourceSaleItemKey,
            true,
            true,
            "报价已刷新，请重新确认出售数量",
            contextVersion));
    }

    private void ConfirmActiveResourceSale()
    {
        if (activeResourceSaleOption == null || resourceSaleOptions == null || resourceSaleSubmitting || resourceSaleOptionsLoading)
        {
            return;
        }

        long minimumLots = GetResourceSaleMinimumLots(activeResourceSaleOption);
        long maximumLots = GetResourceSaleMaximumLots(activeResourceSaleOption);
        bool matchesLiveLimits = maximumLots >= minimumLots
            && activeResourceSaleLotCount >= minimumLots
            && activeResourceSaleLotCount <= maximumLots;
        if (resourceSaleSubmissionBlocked || (!activeResourceSalePendingRetry && !matchesLiveLimits))
        {
            resourceSaleQuoteStatus = "当前出售数量不符合服务器规则，请刷新报价后重试";
            RenderResourceSaleQuote(false);
            return;
        }

        ResourceSaleSubmitRequest payload = new ResourceSaleSubmitRequest
        {
            sourceItemId = activeResourceSaleOption.sourceItemId,
            lotCount = activeResourceSaleLotCount,
            expectedConfigRevision = resourceSaleOptions.configRevision
        };
        string idempotencyKey = GetOrCreateResourceSaleIdempotencyKey(payload);
        if (!TryCreateAuthorizedPost(
                "/resource-sales",
                JsonUtility.ToJson(payload),
                out UnityWebRequest request,
                out string error))
        {
            resourceSaleQuoteStatus = error;
            RenderResourceSaleQuote(false);
            return;
        }

        request.SetRequestHeader("Idempotency-Key", idempotencyKey);
        StartCoroutine(SubmitResourceSale(request));
    }

    private IEnumerator SubmitResourceSale(UnityWebRequest request)
    {
        resourceSaleSubmitting = true;
        resourceSaleQuoteStatus = "正在提交出售请求，请勿重复操作...";
        RenderResourceSaleQuote(true);

        using (request)
        {
            yield return request.SendWebRequest();
            resourceSaleSubmitting = false;

            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            ResourceSaleSubmitApiResponse response = ParseJson<ResourceSaleSubmitApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null)
            {
                CompleteResourceSale(response.data, response.message);
                yield break;
            }

            HandleResourceSaleSubmitFailure(request, response);
        }
    }

    private void HandleResourceSaleSubmitFailure(UnityWebRequest request, ResourceSaleSubmitApiResponse response)
    {
        int code = response == null ? 0 : response.code;
        string message = response != null && !string.IsNullOrWhiteSpace(response.message)
            ? response.message.Trim()
            : ApiRequestUtility.GetErrorMessage(request, "资源出售失败");

        bool hasDefinitiveClientResponse = request.responseCode >= 400 && request.responseCode < 500;
        bool knownBusinessError = code == 6401 || (code >= 7201 && code <= 7210);
        bool serverOutcomeUncertain = request.result == UnityWebRequest.Result.ConnectionError
            || request.result == UnityWebRequest.Result.DataProcessingError
            || request.responseCode >= 500
            || (!hasDefinitiveClientResponse && response == null);
        if (knownBusinessError || !serverOutcomeUncertain)
        {
            ClearPendingResourceSaleIntent();
        }

        switch (code)
        {
            case 7201:
            case 7209:
                resourceSaleSubmissionBlocked = true;
                resourceSaleOptions = null;
                activeResourceSaleOption = null;
                HideResourceSaleDialog();
                SetResourceSaleInventoryStatus(message);
                return;
            case 7202:
            case 7203:
            case 7205:
            case 7206:
            case 7210:
                resourceSaleQuoteStatus = message + "，正在刷新报价...";
                RenderResourceSaleQuote(true);
                RefreshActiveResourceSaleQuote();
                return;
            case 7204:
            case 7208:
                resourceSaleSubmissionBlocked = false;
                resourceSaleQuoteStatus = message + "，请重新确认后提交";
                RenderResourceSaleQuote(false);
                return;
            case 7207:
            case 6401:
                resourceSaleSubmissionBlocked = true;
                resourceSaleQuoteStatus = message;
                RenderResourceSaleQuote(false);
                return;
        }

        resourceSaleQuoteStatus = serverOutcomeUncertain
            ? message + "。成交结果尚未确认，再次点击确认会使用同一请求编号安全重试"
            : message;
        activeResourceSalePendingRetry = serverOutcomeUncertain;
        RenderResourceSaleQuote(false);
    }

    private void CompleteResourceSale(ResourceSaleResult result, string message)
    {
        bool inventoryWasVisible = IsInventoryPanelVisible();
        string sourceName = activeResourceSaleOption == null
            ? GetResourceSaleName(result.sourceItemKey, result.sourceItemKey, "资源")
            : GetResourceSaleName(activeResourceSaleOption.sourceItemName, activeResourceSaleOption.sourceItemKey, "资源");
        string targetName = activeResourceSaleOption == null
            ? GetResourceSaleName(result.targetItemKey, result.targetItemKey, "金币")
            : GetResourceSaleName(activeResourceSaleOption.targetItemName, activeResourceSaleOption.targetItemKey, "金币");

        ClearPendingResourceSaleIntent();
        ApplyResourceSaleBalances(result);
        HideResourceSaleDialog();

        InventoryItemResponse remaining = FindCurrentInventoryItem(activeResourceSaleItemKey);
        if (remaining == null || remaining.quantity <= 0)
        {
            selectedInventoryItemKey = string.Empty;
        }

        string replayText = result.replayed ? "（服务器返回原成交记录）" : string.Empty;
        inventoryStatusMessage =
            $"{(string.IsNullOrWhiteSpace(message) ? "资源出售成功" : message)}：" +
            $"出售 {FormatResourceSaleQuantity(result.soldQuantity)} {sourceName}，" +
            $"获得 {FormatResourceSaleQuantity(result.grantedQuantity)} {targetName}{replayText}";

        resourceSaleOptions = null;
        activeResourceSaleOption = null;
        activeResourceSaleLotCount = 0;
        resourceSaleQuoteStatus = string.Empty;
        resourceSaleSubmissionBlocked = false;
        activeResourceSalePendingRetry = false;
        if (inventoryWasVisible)
        {
            RenderInventoryPanel();
        }
        else
        {
            SetStatus(inventoryStatusMessage);
        }
    }

    private void ApplyResourceSaleBalances(ResourceSaleResult result)
    {
        if (currentInventoryItems != null)
        {
            for (int i = 0; i < currentInventoryItems.Length; i++)
            {
                InventoryItemResponse item = currentInventoryItems[i];
                if (item == null)
                {
                    continue;
                }

                if (MatchesResourceSaleItem(item, result.sourceItemId, result.sourceItemKey))
                {
                    item.quantity = ResourceSaleQuantityToInt(result.sourceBalanceAfter);
                }

                if (MatchesResourceSaleItem(item, result.targetItemId, result.targetItemKey))
                {
                    item.quantity = ResourceSaleQuantityToInt(result.targetBalanceAfter);
                }
            }
        }

        if (string.Equals(result.targetItemKey, "gold", StringComparison.OrdinalIgnoreCase))
        {
            PlayerPrefs.SetInt("Gold", ResourceSaleQuantityToInt(result.targetBalanceAfter));
        }

        PlayerPrefs.Save();
        SyncInventoryResourceValues(currentInventoryItems);
    }

    private bool MatchesResourceSaleItem(InventoryItemResponse item, int itemId, string itemKey)
    {
        if (itemId > 0 && item.itemId == itemId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(itemKey)
            && string.Equals(item.itemKey, itemKey, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowResourceSaleHistory()
    {
        if (resourceSaleHistoryLoading)
        {
            return;
        }

        resourceSaleDialogVisible = true;
        resourceSaleHistoryVisible = true;
        resourceSaleHistoryItems.Clear();
        resourceSaleHistoryNextCursor = 0;
        resourceSaleHistoryHasMore = false;
        resourceSaleHistoryStatus = "正在读取服务器出售记录...";
        RenderResourceSaleHistory(true);
        StartCoroutine(GetResourceSaleHistory(true));
    }

    private void LoadMoreResourceSaleHistory()
    {
        if (!resourceSaleHistoryLoading && resourceSaleHistoryHasMore && resourceSaleHistoryNextCursor > 0)
        {
            resourceSaleHistoryStatus = "正在读取更多出售记录...";
            RenderResourceSaleHistory(true);
            StartCoroutine(GetResourceSaleHistory(false));
        }
    }

    private IEnumerator GetResourceSaleHistory(bool reset)
    {
        resourceSaleHistoryLoading = true;
        string path = $"/resource-sales/history?limit={ResourceSaleHistoryPageSize}";
        if (!reset && resourceSaleHistoryNextCursor > 0)
        {
            path += "&beforeId=" + resourceSaleHistoryNextCursor.ToString(CultureInfo.InvariantCulture);
        }

        UnityWebRequest request = CreateAuthorizedGet(path);
        if (request == null)
        {
            resourceSaleHistoryLoading = false;
            resourceSaleHistoryStatus = requestCreationError;
            RenderResourceSaleHistory(false);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            resourceSaleHistoryLoading = false;

            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            ResourceSaleHistoryApiResponse response = ParseJson<ResourceSaleHistoryApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                resourceSaleHistoryStatus = response != null && !string.IsNullOrWhiteSpace(response.message)
                    ? response.message
                    : ApiRequestUtility.GetErrorMessage(request, "出售记录读取失败");
                RenderResourceSaleHistory(false);
                yield break;
            }

            if (reset)
            {
                resourceSaleHistoryItems.Clear();
            }

            AppendResourceSaleHistoryItems(response.data.items);
            resourceSaleHistoryNextCursor = response.data.nextCursor;
            resourceSaleHistoryHasMore = response.data.hasMore;
            resourceSaleHistoryStatus = resourceSaleHistoryItems.Count == 0
                ? "暂无出售记录"
                : $"已加载 {resourceSaleHistoryItems.Count} 条出售记录";
            RenderResourceSaleHistory(false);
        }
    }

    private void AppendResourceSaleHistoryItems(ResourceSaleHistoryItem[] items)
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            ResourceSaleHistoryItem item = items[i];
            if (item == null)
            {
                continue;
            }

            bool exists = false;
            for (int j = 0; j < resourceSaleHistoryItems.Count; j++)
            {
                if (resourceSaleHistoryItems[j].orderId == item.orderId)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                resourceSaleHistoryItems.Add(item);
            }
        }
    }

    private void RenderResourceSaleHistory(bool busy)
    {
        if (!resourceSaleDialogVisible || !resourceSaleHistoryVisible || !IsInventoryPanelVisible())
        {
            return;
        }

        EnsureResourceSaleDialogView();
        if (resourceSaleDialogView == null)
        {
            return;
        }

        resourceSaleDialogView.ShowHistory(
            BuildResourceSaleHistoryText(),
            resourceSaleHistoryStatus,
            resourceSaleHistoryHasMore,
            busy || resourceSaleHistoryLoading,
            ShowActiveResourceSaleQuote,
            ShowResourceSaleHistory,
            ShowResourceSaleHistory,
            LoadMoreResourceSaleHistory,
            HideResourceSaleDialog);
    }

    private string BuildResourceSaleHistoryText()
    {
        if (resourceSaleHistoryItems.Count == 0)
        {
            return resourceSaleHistoryLoading ? "正在读取..." : "暂无出售记录";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < resourceSaleHistoryItems.Count; i++)
        {
            ResourceSaleHistoryItem item = resourceSaleHistoryItems[i];
            ResourceSaleOption sourceOption = FindResourceSaleOption(item.sourceItemId, item.sourceItemKey);
            string sourceName = GetResourceSaleName(
                item.sourceItemName,
                item.sourceItemKey,
                sourceOption == null
                    ? "资源"
                    : GetResourceSaleName(sourceOption.sourceItemName, sourceOption.sourceItemKey, "资源"));
            string targetName = GetResourceSaleName(
                item.targetItemName,
                item.targetItemKey,
                sourceOption == null
                    ? "金币"
                    : GetResourceSaleName(sourceOption.targetItemName, sourceOption.targetItemKey, "金币"));
            string time = FormatResourceSaleTime(item.createTime);

            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.Append(time)
                .Append("  ")
                .Append(sourceName)
                .Append("  #")
                .Append(item.orderId)
                .AppendLine();
            builder.Append("出售 ")
                .Append(FormatResourceSaleQuantity(item.soldQuantity))
                .Append("，获得 ")
                .Append(FormatResourceSaleQuantity(item.grantedQuantity))
                .Append(' ')
                .Append(targetName);
        }

        return builder.ToString();
    }

    private void HideResourceSaleDialog()
    {
        resourceSaleDialogVisible = false;
        resourceSaleHistoryVisible = false;
        activeResourceSaleLotCount = 0;
        resourceSaleQuoteStatus = string.Empty;
        resourceSaleSubmissionBlocked = false;
        ClearPendingResourceSaleIntent();
        if (resourceSaleDialogView != null)
        {
            resourceSaleDialogView.Hide();
        }
    }

    private long GetResourceSaleMinimumLots(ResourceSaleOption option)
    {
        return option == null ? 1 : Math.Max(1L, option.minLotsPerOrder);
    }

    private long GetResourceSaleMaximumLots(ResourceSaleOption option)
    {
        if (option == null)
        {
            return 0;
        }

        long configuredMaximum = Math.Max(0L, option.maxLotsPerOrder);
        long sellableMaximum = Math.Max(0L, option.maxSellableLots);
        return Math.Min(configuredMaximum, sellableMaximum);
    }

    private long ClampResourceSaleLots(long value, long minimum, long maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        return Math.Max(minimum, Math.Min(maximum, value));
    }

    private long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private string GetResourceSaleName(string name, string key, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            return key.Trim();
        }

        return fallback;
    }

    private string FormatResourceSaleQuantity(long value)
    {
        return Math.Max(0L, value).ToString("N0", CultureInfo.InvariantCulture);
    }

    private string FormatResourceSaleTime(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime time))
        {
            return time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        return string.IsNullOrWhiteSpace(value) ? "时间未知" : value.Trim();
    }

    private int ResourceSaleQuantityToInt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private string GetOrCreateResourceSaleIdempotencyKey(ResourceSaleSubmitRequest payload)
    {
        int playerId = GameSession.PlayerId;
        string existingKey = PlayerPrefs.GetString(ResourceSalePendingIdempotencyKey, string.Empty);
        bool sameIntent = PlayerPrefs.GetInt(ResourceSalePendingPlayerKey, 0) == playerId
            && PlayerPrefs.GetInt(ResourceSalePendingSourceKey, 0) == payload.sourceItemId
            && ReadResourceSaleLong(ResourceSalePendingLotsKey) == payload.lotCount
            && ReadResourceSaleLong(ResourceSalePendingRevisionKey) == payload.expectedConfigRevision
            && Guid.TryParse(existingKey, out _);
        if (sameIntent)
        {
            return existingKey;
        }

        string idempotencyKey = Guid.NewGuid().ToString("D");
        PlayerPrefs.SetInt(ResourceSalePendingPlayerKey, playerId);
        PlayerPrefs.SetInt(ResourceSalePendingSourceKey, payload.sourceItemId);
        PlayerPrefs.SetString(ResourceSalePendingLotsKey, payload.lotCount.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(ResourceSalePendingRevisionKey, payload.expectedConfigRevision.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(ResourceSalePendingIdempotencyKey, idempotencyKey);
        PlayerPrefs.Save();
        return idempotencyKey;
    }

    private bool TryRestorePendingResourceSaleLot(
        ResourceSaleOption option,
        long configRevision,
        long minimumLots,
        out long lotCount)
    {
        lotCount = 0;
        if (option == null
            || PlayerPrefs.GetInt(ResourceSalePendingPlayerKey, 0) != GameSession.PlayerId
            || PlayerPrefs.GetInt(ResourceSalePendingSourceKey, 0) != option.sourceItemId
            || ReadResourceSaleLong(ResourceSalePendingRevisionKey) != configRevision
            || !Guid.TryParse(PlayerPrefs.GetString(ResourceSalePendingIdempotencyKey, string.Empty), out _))
        {
            return false;
        }

        long pendingLots = ReadResourceSaleLong(ResourceSalePendingLotsKey);
        long configuredMaximum = Math.Max(minimumLots, option.maxLotsPerOrder);
        if (pendingLots < minimumLots || pendingLots > configuredMaximum)
        {
            return false;
        }

        lotCount = pendingLots;
        return true;
    }

    private long ReadResourceSaleLong(string key)
    {
        string value = PlayerPrefs.GetString(key, string.Empty);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0L;
    }

    private void ClearPendingResourceSaleIntent()
    {
        activeResourceSalePendingRetry = false;
        if (!PlayerPrefs.HasKey(ResourceSalePendingIdempotencyKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(ResourceSalePendingPlayerKey);
        PlayerPrefs.DeleteKey(ResourceSalePendingSourceKey);
        PlayerPrefs.DeleteKey(ResourceSalePendingLotsKey);
        PlayerPrefs.DeleteKey(ResourceSalePendingRevisionKey);
        PlayerPrefs.DeleteKey(ResourceSalePendingIdempotencyKey);
        PlayerPrefs.Save();
    }

#pragma warning disable 0649
    [Serializable]
    private sealed class ResourceSaleOptionsApiResponse : BasicApiResponse
    {
        public ResourceSaleOptionsData data;
    }

    [Serializable]
    private sealed class ResourceSaleOptionsData
    {
        public bool enabled;
        public long configRevision;
        public string usageDate;
        public ResourceSaleOption[] options;
    }

    [Serializable]
    private sealed class ResourceSaleOption
    {
        public int sourceItemId;
        public string sourceItemKey;
        public string sourceItemName;
        public string sourceItemType;
        public string sourceItemIcon;
        public int targetItemId;
        public string targetItemKey;
        public string targetItemName;
        public string targetItemIcon;
        public long sourceQuantityPerLot;
        public long targetQuantityPerLot;
        public long minLotsPerOrder;
        public long maxLotsPerOrder;
        public long dailySourceQuantityLimit;
        public long usedSourceQuantity;
        public long remainingDailySourceQuantity;
        public long sourceBalance;
        public long maxSellableLots;
    }

    [Serializable]
    private sealed class ResourceSaleSubmitRequest
    {
        public int sourceItemId;
        public long lotCount;
        public long expectedConfigRevision;
    }

    [Serializable]
    private sealed class ResourceSaleSubmitApiResponse : BasicApiResponse
    {
        public ResourceSaleResult data;
    }

    [Serializable]
    private sealed class ResourceSaleResult
    {
        public long orderId;
        public string orderNo;
        public string usageDate;
        public int sourceItemId;
        public string sourceItemKey;
        public string sourceItemName;
        public string sourceItemType;
        public string sourceItemIcon;
        public int targetItemId;
        public string targetItemKey;
        public string targetItemName;
        public string targetItemType;
        public string targetItemIcon;
        public long sourceQuantityPerLot;
        public long targetQuantityPerLot;
        public long lotCount;
        public long soldQuantity;
        public long grantedQuantity;
        public long sourceBalanceBefore;
        public long sourceBalanceAfter;
        public long targetBalanceBefore;
        public long targetBalanceAfter;
        public long configRevision;
        public string createTime;
        public bool replayed;
        public long latestRevision;
        public bool refreshRequired;
    }

    [Serializable]
    private sealed class ResourceSaleHistoryApiResponse : BasicApiResponse
    {
        public ResourceSaleHistoryData data;
    }

    [Serializable]
    private sealed class ResourceSaleHistoryData
    {
        public ResourceSaleHistoryItem[] items;
        public long nextCursor;
        public bool hasMore;
    }

    [Serializable]
    private sealed class ResourceSaleHistoryItem
    {
        public long orderId;
        public string orderNo;
        public string usageDate;
        public int sourceItemId;
        public string sourceItemKey;
        public string sourceItemName;
        public string sourceItemType;
        public string sourceItemIcon;
        public int targetItemId;
        public string targetItemKey;
        public string targetItemName;
        public string targetItemType;
        public string targetItemIcon;
        public long lotCount;
        public long soldQuantity;
        public long grantedQuantity;
        public long sourceBalanceAfter;
        public long targetBalanceAfter;
        public long configRevision;
        public string createTime;
    }
#pragma warning restore 0649
}
