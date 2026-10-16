using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;

public partial class HomePageUI
{
    private const int ShopPageSize = 8;
    private const int CoinShopHistoryPageSize = 4;
    private const string GoldShopCategory = "gold";
    private const string DiamondShopCategory = "diamond";

    private GameObject shopPanelObject;
    private readonly Dictionary<int, Sprite> shopCategoryNormalSprites = new Dictionary<int, Sprite>();
    private CoinShopCatalogData coinShopCatalog;
    private CoinShopProductResponse[] coinShopProducts = new CoinShopProductResponse[0];
    private string activeShopCategory = GoldShopCategory;
    private long selectedCoinShopProductId;
    private long selectedCoinShopLotCount;
    private bool shopDetailVisible;
    private string shopStatusMessage = string.Empty;
    private string shopPurchaseSummary = string.Empty;
    private int shopPageIndex;
    private bool coinShopCatalogLoading;
    private bool coinShopPurchaseRunning;

    private GameObject coinShopHistoryPanelObject;
    private GameObject coinShopHistoryBlockerObject;
    private CoinShopPurchaseHistoryItem[] coinShopHistoryItems = new CoinShopPurchaseHistoryItem[0];
    private readonly List<long> coinShopHistoryCursors = new List<long>();
    private int coinShopHistoryPageIndex;
    private long coinShopHistoryNextCursor;
    private bool coinShopHistoryHasMore;
    private bool coinShopHistoryLoading;
    private readonly HashSet<string> coinShopSyncedOrderIds = new HashSet<string>();
    private readonly HashSet<string> coinShopRealtimeEventIds = new HashSet<string>();

    private void ShowShopContent()
    {
        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideAnnouncementPanel();
        HideAccountProfilePanel();
        HideFriendsPanel();
        HideSettingsPanel();
        HideShopPanel();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel == null)
        {
            SetLeftContent("商城", "商城场景组件尚未生成，请在 Unity 中执行 Tools/WeiJun UI/Rebuild Home Scene Panels。", "商城场景组件未找到");
            return;
        }

        shopPanelObject = panel.gameObject;
        activeShopCategory = GoldShopCategory;
        shopPageIndex = 0;
        selectedCoinShopProductId = 0;
        selectedCoinShopLotCount = 0;
        shopDetailVisible = false;

        if (!HasAuthSession())
        {
            coinShopCatalog = null;
            coinShopProducts = new CoinShopProductResponse[0];
            shopPurchaseSummary = "商城仅对已登录玩家开放";
            shopStatusMessage = "请登录账号后读取服务器商城";
            RenderShopScenePanel();
            return;
        }

        shopPurchaseSummary = "正在读取服务器商品目录";
        shopStatusMessage = "正在读取金币商城...";
        RenderShopScenePanel();
        StartCoroutine(GetCoinShopCatalog("金币商城目录已同步"));
    }

    private void RenderShopScenePanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel == null || shopPanelObject == null)
        {
            return;
        }

        Transform root = panel.PanelRoot;
        string currencyLabel = GetActiveShopCurrencyLabel();
        SetSceneText(root, "ShopSubtitle", "使用" + currencyLabel + "购买服务器商品");
        SetSceneText(root, "ShopSidebarNoteTitle", "商城规则");
        SetSceneText(root, "ShopSidebarNoteText", "商品、价格和限额\n均由服务器结算\n\n金币与钻石独立结算");
        long goldBalance = IsDiamondShopActive()
            ? PlayerPrefs.GetInt("Gold", 0)
            : (coinShopCatalog == null ? PlayerPrefs.GetInt("Gold", 0) : coinShopCatalog.goldBalance);
        long diamondBalance = IsDiamondShopActive()
            ? (coinShopCatalog == null ? PlayerPrefs.GetInt("Crystal", 0) : coinShopCatalog.diamondBalance)
            : PlayerPrefs.GetInt("Crystal", 0);
        SetSceneText(root, "ShopGoldText", "金币  " + goldBalance);
        SetSceneText(root, "ShopCrystalText", "钻石  " + diamondBalance);
        SetSceneText(root, "ShopStatusText", shopStatusMessage);

        BindSceneButton(FindSceneComponent<Button>(root, "ShopCloseButton"), HideShopPanel);
        BindSceneButton(FindSceneComponent<Button>(root, "ShopWarehouseButton"), OpenServerInventoryFromShop);
        Button refreshButton = FindSceneComponent<Button>(root, "ShopRefreshButton");
        if (refreshButton != null)
        {
            refreshButton.interactable = HasAuthSession() && !coinShopCatalogLoading && !coinShopPurchaseRunning;
            SetSceneButtonText(refreshButton, coinShopCatalogLoading ? "读取中..." : "刷新");
            BindSceneButton(refreshButton, RefreshShopData);
        }
        ConfigureCoinShopHistoryButton(FindSceneComponent<Button>(root, "ShopBuyCrystalButton"));
        EnsureCoinShopHistoryPanel(root);
        if (coinShopHistoryPanelObject != null)
        {
            BindSceneButton(
                FindSceneComponent<Button>(coinShopHistoryPanelObject.transform, "CoinShopHistoryCloseButton"),
                HideCoinShopHistory);
        }

        ConfigureShopCategory(root, GoldShopCategory, "金币");
        ConfigureShopCategory(root, DiamondShopCategory, "钻石");

        List<CoinShopProductResponse> visibleProducts = GetFilteredCoinShopProducts();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleProducts.Count / (float)ShopPageSize));
        shopPageIndex = Mathf.Clamp(shopPageIndex, 0, pageCount - 1);
        EnsureSelectedCoinShopProduct(visibleProducts);

        SetSceneText(root, "ShopResultCount", GetShopCategoryLabel(activeShopCategory) + "  " + visibleProducts.Count + " 件商品");
        SetSceneText(root, "ShopPageText", (shopPageIndex + 1) + "/" + pageCount);

        Button previousButton = FindSceneComponent<Button>(root, "ShopPreviousButton");
        Button nextButton = FindSceneComponent<Button>(root, "ShopNextButton");
        SetSceneObjectActive(root, "ShopPreviousButton", true);
        SetSceneObjectActive(root, "ShopNextButton", true);
        SetSceneObjectActive(root, "ShopPageText", true);
        if (previousButton != null)
        {
            previousButton.interactable = shopPageIndex > 0;
        }
        if (nextButton != null)
        {
            nextButton.interactable = shopPageIndex + 1 < pageCount;
        }
        BindSceneButton(previousButton, () => ChangeShopPage(-1));
        BindSceneButton(nextButton, () => ChangeShopPage(1));

        int pageStart = shopPageIndex * ShopPageSize;
        List<Transform> cards = GetShopPageCards(root);
        for (int index = 0; index < cards.Count; index++)
        {
            Transform card = cards[index];
            int productIndex = pageStart + index;
            bool visible = productIndex < visibleProducts.Count;
            card.gameObject.SetActive(visible);
            if (visible)
            {
                ConfigureCoinShopProductCard(card, visibleProducts[productIndex]);
            }
        }

        CoinShopProductResponse selectedProduct = FindSelectedCoinShopProduct(visibleProducts);
        SetSceneObjectActive(root, "ShopEmptyState", !coinShopCatalogLoading && visibleProducts.Count == 0);
        SetSceneObjectActive(root, "ShopDetailBlocker", (shopDetailVisible && selectedProduct != null) || IsCoinShopHistoryVisible());
        SetSceneObjectActive(root, "ShopDetail", shopDetailVisible && selectedProduct != null);
        if (shopDetailVisible && selectedProduct != null)
        {
            Transform detail = FindSceneTransform(root, "ShopDetail");
            if (detail != null)
            {
                detail.SetAsLastSibling();
            }
            ConfigureCoinShopDetails(root, selectedProduct);
        }
        SetStatus(shopStatusMessage);
    }

    private void ConfigureShopCategory(Transform root, string category, string label)
    {
        Button button = FindSceneComponent<Button>(root, "ShopCategory_" + category);
        if (button == null)
        {
            return;
        }

        bool selected = activeShopCategory == category;
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
        }

        Image categoryImage = button.targetGraphic as Image;
        Sprite selectedSprite = button.spriteState.selectedSprite;
        if (categoryImage != null)
        {
            int buttonId = button.GetInstanceID();
            if (!shopCategoryNormalSprites.ContainsKey(buttonId))
            {
                shopCategoryNormalSprites.Add(buttonId, categoryImage.sprite);
            }

            Sprite normalSprite = shopCategoryNormalSprites[buttonId];
            categoryImage.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
        }

        button.interactable = !coinShopCatalogLoading && !coinShopPurchaseRunning;
        BindSceneButton(button, button.interactable ? () => SelectShopCategory(category) : null);
    }

    private List<Transform> GetShopPageCards(Transform root)
    {
        List<Transform> cards = new List<Transform>(ShopPageSize);
        for (int index = 0; index < ShopPageSize; index++)
        {
            Transform card = FindSceneTransform(root, "ShopProductCard" + index);
            if (card != null)
            {
                cards.Add(card);
            }
        }

        cards.Sort((left, right) =>
        {
            Vector3 leftPosition = root.InverseTransformPoint(left.position);
            Vector3 rightPosition = root.InverseTransformPoint(right.position);
            return Mathf.Abs(leftPosition.y - rightPosition.y) < 1f
                ? leftPosition.x.CompareTo(rightPosition.x)
                : rightPosition.y.CompareTo(leftPosition.y);
        });
        return cards;
    }

    private void ConfigureCoinShopProductCard(Transform card, CoinShopProductResponse product)
    {
        bool selected = product.productId == selectedCoinShopProductId;
        ConfigureShopProductIcon(card, product, "ShopProductIcon", "ShopProductIconText");
        SetSceneText(card, "ShopProductTag", GetShopProductTag(product));
        SetSceneText(card, "ShopProductName", GetCoinShopProductName(product));
        SetSceneText(card, "ShopProductReward", GetCoinShopRewardName(product) + " x" + product.rewardQuantityPerLot + "/批");
        SetSceneText(card, "ShopProductPrice", GetActiveShopCurrencyLabel() + " " + GetActiveShopPricePerLot(product) + "/批");
        SetSceneObjectActive(card, "ShopSelectionMark", selected);

        Button button = card.GetComponent<Button>();
        long productId = product.productId;
        if (button != null)
        {
            button.interactable = !coinShopCatalogLoading && !coinShopPurchaseRunning;
        }
        BindSceneButton(button, () => SelectCoinShopProduct(productId));
    }

    private void ConfigureCoinShopDetails(Transform root, CoinShopProductResponse product)
    {
        Button buyButton = FindSceneComponent<Button>(root, "ShopBuyGoldButton");
        Button openBagButton = FindSceneComponent<Button>(root, "ShopOpenBagButton");
        Button closeButton = FindSceneComponent<Button>(root, "ShopDetailCloseButton");
        BindSceneButton(openBagButton, OpenServerInventoryFromShop);
        BindSceneButton(closeButton, HideCoinShopDetail);
        SetSceneButtonText(openBagButton, "打开背包");
        if (openBagButton != null)
        {
            openBagButton.interactable = HasAuthSession();
        }

        if (product == null)
        {
            SetSceneText(root, "ShopDetailIconText", "商");
            SetSceneText(root, "ShopDetailName", coinShopCatalogLoading ? "正在读取商品" : "暂无商品");
            SetSceneText(root, "ShopDetailDescription", coinShopCatalogLoading ? "请稍候..." : "当前分类没有可购买的服务器商品");
            SetSceneText(root, "ShopDetailReward", "购买内容  --");
            SetSceneText(root, "ShopDetailPrice", GetActiveShopCurrencyLabel() + "价格  --");
            SetSceneText(root, "ShopPurchaseSummary", shopPurchaseSummary);
            SetSceneText(root, "ShopOwnedCount", "背包数量  --");
            SetSceneText(root, "ShopStorageModeText", HasAuthSession() ? "商品和限购以服务器为准" : "登录后可购买");
            ConfigureCoinShopLotControls(root, null);
            ConfigureCoinShopBuyButton(buyButton, false);
            return;
        }

        string productName = GetCoinShopProductName(product);
        long totalReward = GetCoinShopTotal(product.rewardQuantityPerLot, selectedCoinShopLotCount);
        long pricePerLot = GetActiveShopPricePerLot(product);
        long totalCost = GetCoinShopTotal(pricePerLot, selectedCoinShopLotCount);
        bool canPurchase = HasAuthSession()
            && coinShopCatalog != null
            && coinShopCatalog.enabled
            && product.maxPurchasableLots > 0
            && selectedCoinShopLotCount >= product.minLotsPerOrder
            && selectedCoinShopLotCount <= product.maxPurchasableLots
            && !coinShopPurchaseRunning
            && !coinShopCatalogLoading;

        SetSceneText(root, "ShopDetailIconText", GetCoinShopIconText(product));
        ConfigureShopProductIcon(root, product, "ShopDetailIcon", "ShopDetailIconText");
        SetSceneText(root, "ShopDetailName", productName);
        SetSceneText(root, "ShopDetailDescription", string.IsNullOrEmpty(product.description) ? "服务器正式发布商品" : product.description);
        SetSceneText(root, "ShopDetailReward", "购买内容  " + GetCoinShopRewardName(product) + " x" + totalReward);
        SetSceneText(root, "ShopDetailPrice", GetActiveShopCurrencyLabel() + " " + totalCost + "  (" + pricePerLot + "/批)");
        SetSceneText(root, "ShopPurchaseSummary", shopPurchaseSummary);
        SetSceneText(root, "ShopOwnedCount", GetCoinShopOwnedText(product));
        SetSceneText(root, "ShopStorageModeText", BuildCoinShopLimitText(product));

        ConfigureCoinShopLotControls(root, product);
        ConfigureCoinShopBuyButton(buyButton, canPurchase);
    }

    private void ConfigureCoinShopBuyButton(Button button, bool interactable)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = interactable;
        SetSceneButtonText(button, "购买");
        BindSceneButton(button, interactable ? BeginCoinShopPurchase : null);
    }

    private void ConfigureCoinShopHistoryButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(true);
        button.interactable = HasAuthSession() && !coinShopHistoryLoading;
        SetSceneButtonText(button, "购买记录");
        BindSceneButton(button, ShowCoinShopHistory);
    }

    private void ConfigureCoinShopLotControls(Transform root, CoinShopProductResponse product)
    {
        Button decrease = FindSceneComponent<Button>(root, "ShopLotDecreaseButton");
        Button increase = FindSceneComponent<Button>(root, "ShopLotIncreaseButton");
        Text count = FindSceneComponent<Text>(root, "ShopLotCountText");
        bool enabled = product != null && HasAuthSession() && !coinShopPurchaseRunning && !coinShopCatalogLoading;
        long min = product == null ? 0 : product.minLotsPerOrder;
        long max = product == null ? 0 : product.maxPurchasableLots;
        enabled = enabled && max >= Math.Max(1L, min);
        if (product != null)
        {
            selectedCoinShopLotCount = ClampCoinShopLots(product, selectedCoinShopLotCount);
        }

        if (count != null)
        {
            count.text = product == null ? "批次 --" : "批次 " + selectedCoinShopLotCount;
        }
        if (decrease != null)
        {
            decrease.interactable = enabled && selectedCoinShopLotCount > min;
            BindSceneButton(decrease, decrease.interactable ? () => ChangeCoinShopLotCount(-1) : null);
        }
        if (increase != null)
        {
            increase.interactable = enabled && selectedCoinShopLotCount < max;
            BindSceneButton(increase, increase.interactable ? () => ChangeCoinShopLotCount(1) : null);
        }
    }

    private bool IsDiamondShopActive()
    {
        return activeShopCategory == DiamondShopCategory;
    }

    private string GetActiveShopCurrencyLabel()
    {
        return IsDiamondShopActive() ? "钻石" : "金币";
    }

    private string GetActiveShopApiPrefix()
    {
        return IsDiamondShopActive() ? "/diamond-shop" : "/coin-shop";
    }

    private long GetActiveShopPricePerLot(CoinShopProductResponse product)
    {
        if (product == null)
        {
            return 0;
        }
        return IsDiamondShopActive() ? product.diamondPricePerLot : product.goldPricePerLot;
    }

    private void SelectShopCategory(string category)
    {
        if (category != GoldShopCategory && category != DiamondShopCategory)
        {
            return;
        }

        activeShopCategory = category;
        shopPageIndex = 0;
        selectedCoinShopProductId = 0;
        selectedCoinShopLotCount = 0;
        shopDetailVisible = false;
        coinShopCatalog = null;
        coinShopProducts = new CoinShopProductResponse[0];
        shopPurchaseSummary = "正在读取" + GetActiveShopCurrencyLabel() + "商城商品";
        shopStatusMessage = "已切换到" + GetShopCategoryLabel(category) + "分类";
        RenderShopScenePanel();
        if (HasAuthSession())
        {
            StartCoroutine(GetCoinShopCatalog(GetActiveShopCurrencyLabel() + "商城目录已同步"));
        }
    }

    private void SelectCoinShopProduct(long productId)
    {
        selectedCoinShopProductId = productId;
        CoinShopProductResponse product = FindCoinShopProduct(productId);
        selectedCoinShopLotCount = product == null ? 0 : ClampCoinShopLots(product, product.minLotsPerOrder);
        shopDetailVisible = product != null;
        shopPurchaseSummary = product == null
            ? "请选择商品"
            : "可购买 " + GetCoinShopRewardName(product) + "，每批 " + product.rewardQuantityPerLot + " 个";
        shopStatusMessage = product == null ? "请选择商品" : "已选择 " + GetCoinShopProductName(product);
        RenderShopScenePanel();
    }

    private void ChangeShopPage(int offset)
    {
        List<CoinShopProductResponse> products = GetFilteredCoinShopProducts();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(products.Count / (float)ShopPageSize));
        shopPageIndex = Mathf.Clamp(shopPageIndex + offset, 0, pageCount - 1);
        selectedCoinShopProductId = 0;
        selectedCoinShopLotCount = 0;
        shopDetailVisible = false;
        shopPurchaseSummary = "选择商品后可按服务器规则调整购买批次";
        RenderShopScenePanel();
    }

    private void ChangeCoinShopLotCount(int offset)
    {
        CoinShopProductResponse product = FindCoinShopProduct(selectedCoinShopProductId);
        if (product == null)
        {
            return;
        }

        selectedCoinShopLotCount = ClampCoinShopLots(product, selectedCoinShopLotCount + offset);
        long totalCost = GetCoinShopTotal(GetActiveShopPricePerLot(product), selectedCoinShopLotCount);
        shopPurchaseSummary = "本次获得 " + GetCoinShopRewardName(product) + " x"
            + GetCoinShopTotal(product.rewardQuantityPerLot, selectedCoinShopLotCount) + "，消耗" + GetActiveShopCurrencyLabel() + " " + totalCost;
        RenderShopScenePanel();
    }

    private void RefreshShopData()
    {
        if (!HasAuthSession())
        {
            shopStatusMessage = "请登录账号后读取服务器商城";
            shopPurchaseSummary = "商城仅对已登录玩家开放";
            RenderShopScenePanel();
            return;
        }

        if (!coinShopCatalogLoading && !coinShopPurchaseRunning)
        {
            StartCoroutine(GetCoinShopCatalog(GetActiveShopCurrencyLabel() + "商城目录已刷新"));
        }
    }

    private IEnumerator GetCoinShopCatalog(string completionMessage)
    {
        if (coinShopCatalogLoading || !HasAuthSession())
        {
            yield break;
        }

        coinShopCatalogLoading = true;
        string currencyLabel = GetActiveShopCurrencyLabel();
        string apiPrefix = GetActiveShopApiPrefix();
        shopStatusMessage = "正在读取" + currencyLabel + "商城目录...";
        RenderShopScenePanel();
        UnityWebRequest request = CreateAuthorizedGet(apiPrefix + "/catalog");
        if (request == null)
        {
            coinShopCatalogLoading = false;
            shopStatusMessage = "商城目录读取失败：" + requestCreationError;
            RenderShopScenePanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                coinShopCatalogLoading = false;
                yield break;
            }

            CoinShopCatalogApiResponse response = ParseJson<CoinShopCatalogApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                coinShopCatalogLoading = false;
                string catalogError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                if (apiPrefix == "/diamond-shop" && request.responseCode == 404)
                {
                    catalogError = "钻石商城 V41 后端接口尚未部署";
                }
                shopStatusMessage = "商城目录读取失败：" + catalogError;
                RenderShopScenePanel();
                yield break;
            }

            coinShopCatalog = response.data;
            coinShopProducts = response.data.products ?? new CoinShopProductResponse[0];
        }

        coinShopCatalogLoading = false;
        CoinShopProductResponse selected = FindCoinShopProduct(selectedCoinShopProductId);
        if (selected == null)
        {
            List<CoinShopProductResponse> visibleProducts = GetFilteredCoinShopProducts();
            selectedCoinShopProductId = visibleProducts.Count > 0 ? visibleProducts[0].productId : 0;
            selected = FindCoinShopProduct(selectedCoinShopProductId);
            selectedCoinShopLotCount = selected == null ? 0 : selected.minLotsPerOrder;
        }
        else
        {
            selectedCoinShopLotCount = ClampCoinShopLots(selected, selectedCoinShopLotCount);
        }

        shopPurchaseSummary = coinShopCatalog.enabled
            ? "商品价格、每日额度和可购买批次均以服务器目录为准"
            : currencyLabel + "商城当前未开放";
        shopStatusMessage = string.IsNullOrEmpty(completionMessage)
            ? (coinShopCatalog.enabled ? currencyLabel + "商城目录已同步" : currencyLabel + "商城当前未开放")
            : completionMessage;
        RenderShopScenePanel();
    }

    private void BeginCoinShopPurchase()
    {
        if (coinShopPurchaseRunning || coinShopCatalog == null || !coinShopCatalog.enabled)
        {
            return;
        }

        CoinShopProductResponse product = FindCoinShopProduct(selectedCoinShopProductId);
        if (product == null || product.maxPurchasableLots <= 0)
        {
            shopStatusMessage = "当前商品不可购买，请刷新商城目录";
            RenderShopScenePanel();
            return;
        }

        long lotCount = ClampCoinShopLots(product, selectedCoinShopLotCount);
        if (lotCount < product.minLotsPerOrder || lotCount > product.maxPurchasableLots)
        {
            shopStatusMessage = "购买批次数量不符合当前规则";
            RenderShopScenePanel();
            return;
        }

        CoinShopPendingPurchase pending = LoadPendingCoinShopPurchase();
        if (pending != null && !IsSameCoinShopPurchase(pending, product.productId, lotCount, coinShopCatalog.configRevision))
        {
            shopPurchaseSummary = "上次购买结果未知，请先对同一商品和批次重试，不能覆盖幂等请求";
            shopStatusMessage = "存在待确认的购买请求";
            RenderShopScenePanel();
            return;
        }

        if (pending == null)
        {
            pending = new CoinShopPendingPurchase
            {
                idempotencyKey = Guid.NewGuid().ToString("D"),
                productId = product.productId,
                lotCount = lotCount,
                expectedConfigRevision = coinShopCatalog.configRevision
            };
            SavePendingCoinShopPurchase(pending);
        }

        StartCoroutine(PurchaseCoinShopProduct(product, pending));
    }

    private IEnumerator PurchaseCoinShopProduct(CoinShopProductResponse product, CoinShopPendingPurchase pending)
    {
        coinShopPurchaseRunning = true;
        string currencyLabel = GetActiveShopCurrencyLabel();
        string apiPrefix = GetActiveShopApiPrefix();
        shopPurchaseSummary = "正在提交" + currencyLabel + "购买请求...";
        shopStatusMessage = "购买结算中，请勿重复提交";
        RenderShopScenePanel();

        CoinShopPurchaseRequest payload = new CoinShopPurchaseRequest
        {
            productId = pending.productId,
            lotCount = pending.lotCount,
            expectedConfigRevision = pending.expectedConfigRevision
        };
        if (!TryCreateAuthorizedPost(apiPrefix + "/purchases", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            coinShopPurchaseRunning = false;
            shopStatusMessage = "无法提交购买：" + error;
            shopPurchaseSummary = "请求尚未发送，可使用同一商品和批次重试";
            RenderShopScenePanel();
            yield break;
        }

        request.SetRequestHeader("Idempotency-Key", pending.idempotencyKey);
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                coinShopPurchaseRunning = false;
                yield break;
            }

            CoinShopPurchaseApiResponse response = ParseJson<CoinShopPurchaseApiResponse>(request.downloadHandler.text);
            bool succeeded = request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null;
            if (!succeeded)
            {
                int code = response == null ? 0 : response.code;
                bool refreshRequired = code == 7401 || code == 7402 || code == 7403 || code == 7405
                    || code == 7406 || code == 7409 || code == 7410 || code == 7501 || code == 7502
                    || code == 7503 || code == 7505 || code == 7506 || code == 7509 || code == 7510;
                bool outcomeUncertain = request.result == UnityWebRequest.Result.ConnectionError
                    || request.result == UnityWebRequest.Result.DataProcessingError
                    || request.responseCode >= 500
                    || (response == null && request.responseCode < 400);
                if (!outcomeUncertain)
                {
                    ClearPendingCoinShopPurchase();
                }

                coinShopPurchaseRunning = false;
                string failure = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                shopStatusMessage = "购买失败：" + failure;
                shopPurchaseSummary = outcomeUncertain
                    ? "网络结果未知，请使用同一商品和批次重试；系统会复用原幂等键"
                    : "请根据最新商城规则重新确认购买";
                RenderShopScenePanel();
                if (refreshRequired)
                {
                    yield return GetCoinShopCatalog("商城状态已刷新，请重新确认购买");
                }
                yield break;
            }

            CoinShopPurchaseResult result = response.data;
            result.paymentCurrency = IsDiamondShopActive() ? DiamondShopCategory : GoldShopCategory;
            ClearPendingCoinShopPurchase();
            ApplyActiveShopBalance(result);
            coinShopPurchaseRunning = false;
            shopPurchaseSummary = "购买成功：" + GetCoinShopPurchaseRewardText(result)
                + "，消耗" + currencyLabel + " " + GetActiveShopChargedAmount(result);
            shopStatusMessage = result.replayed ? "已确认之前的购买结果" : currencyLabel + "购买成功";
            QueueCoinShopPurchaseRewardRefresh(result);
            RenderShopScenePanel();
            yield return GetCoinShopCatalog(shopStatusMessage);
        }
    }

    private void QueueCoinShopPurchaseRewardRefresh(CoinShopPurchaseResult result)
    {
        string orderKey = result == null ? string.Empty : result.paymentCurrency + ":" + result.orderId;
        if (result == null || result.orderId <= 0 || !coinShopSyncedOrderIds.Add(orderKey))
        {
            return;
        }

        StartCoroutine(RefreshCoinShopPurchaseRewardState(result));
    }

    private IEnumerator RefreshCoinShopPurchaseRewardState(CoinShopPurchaseResult result)
    {
        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        if (string.Equals(result.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase))
        {
            yield return RefreshCardDrawEquipmentState(syncedCategories, failedCategories);
        }
        else if (string.Equals(result.rewardType, "ITEM", StringComparison.OrdinalIgnoreCase))
        {
            yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
        }
        else if (string.Equals(result.rewardType, "STAMINA", StringComparison.OrdinalIgnoreCase))
        {
            RefreshStaminaAfterShopPurchase();
        }

        if (inventoryPanelObject != null && inventoryPanelObject.activeInHierarchy
            && string.Equals(result.rewardType, "ITEM", StringComparison.OrdinalIgnoreCase))
        {
            inventoryStatusMessage = GetShopCurrencyLabel(result.paymentCurrency) + "商城奖励已同步";
            RenderInventoryPanel();
        }
        if (equipmentPanelObject != null && equipmentPanelObject.activeInHierarchy
            && string.Equals(result.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase))
        {
            RebuildServerEquipmentItems();
            equipmentStatusMessage = GetShopCurrencyLabel(result.paymentCurrency) + "商城奖励已同步";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }

        if (failedCategories.Count > 0)
        {
            SetStatus(GetShopCurrencyLabel(result.paymentCurrency) + "商城奖励同步失败：" + string.Join("、", failedCategories.ToArray()));
        }
    }

    private void ApplyActiveShopBalance(CoinShopPurchaseResult result)
    {
        if (result == null)
        {
            return;
        }

        if (string.Equals(result.paymentCurrency, DiamondShopCategory, StringComparison.OrdinalIgnoreCase))
        {
            PlayerPrefs.SetInt("Crystal", ClampCoinShopPlayerPrefsValue(result.diamondBalanceAfter));
        }
        else
        {
            PlayerPrefs.SetInt("Gold", ClampCoinShopPlayerPrefsValue(result.goldBalanceAfter));
        }
        PlayerPrefs.Save();
        RefreshHomeResourceMetrics();
    }

    private long GetActiveShopChargedAmount(CoinShopPurchaseResult result)
    {
        if (result == null)
        {
            return 0;
        }
        return string.Equals(result.paymentCurrency, DiamondShopCategory, StringComparison.OrdinalIgnoreCase)
            ? result.chargedDiamond
            : result.chargedGold;
    }

    private static string GetShopCurrencyLabel(string currency)
    {
        return string.Equals(currency, DiamondShopCategory, StringComparison.OrdinalIgnoreCase) ? "钻石" : "金币";
    }

    private void OpenServerInventoryFromShop()
    {
        HideShopPanel();
        ShowBagContent();
    }

    private List<CoinShopProductResponse> GetFilteredCoinShopProducts()
    {
        List<CoinShopProductResponse> result = new List<CoinShopProductResponse>();
        if (coinShopProducts == null)
        {
            return result;
        }

        for (int i = 0; i < coinShopProducts.Length; i++)
        {
            CoinShopProductResponse product = coinShopProducts[i];
            if (product != null)
            {
                result.Add(product);
            }
        }
        return result;
    }

    private void EnsureSelectedCoinShopProduct(List<CoinShopProductResponse> products)
    {
        CoinShopProductResponse selected = FindSelectedCoinShopProduct(products);
        if (selected != null && products.IndexOf(selected) / ShopPageSize == shopPageIndex)
        {
            selectedCoinShopLotCount = ClampCoinShopLots(selected, selectedCoinShopLotCount);
            return;
        }

        int pageStart = shopPageIndex * ShopPageSize;
        CoinShopProductResponse next = pageStart < products.Count ? products[pageStart] : null;
        selectedCoinShopProductId = next == null ? 0 : next.productId;
        selectedCoinShopLotCount = next == null ? 0 : next.minLotsPerOrder;
    }

    private CoinShopProductResponse FindSelectedCoinShopProduct(List<CoinShopProductResponse> products)
    {
        for (int i = 0; i < products.Count; i++)
        {
            if (products[i].productId == selectedCoinShopProductId)
            {
                return products[i];
            }
        }
        return null;
    }

    private CoinShopProductResponse FindCoinShopProduct(long productId)
    {
        if (coinShopProducts == null || productId <= 0)
        {
            return null;
        }

        for (int i = 0; i < coinShopProducts.Length; i++)
        {
            if (coinShopProducts[i] != null && coinShopProducts[i].productId == productId)
            {
                return coinShopProducts[i];
            }
        }
        return null;
    }

    private static string GetShopCategoryLabel(string category)
    {
        if (category == GoldShopCategory) return "金币";
        if (category == DiamondShopCategory) return "钻石";
        if (category == "resource") return "资源";
        if (category == "item") return "道具";
        if (category == "growth") return "成长";
        return "推荐";
    }

    private static string GetCoinShopCategory(CoinShopProductResponse product)
    {
        if (product == null)
        {
            return "item";
        }

        if (string.Equals(product.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase))
        {
            return "growth";
        }
        if (string.Equals(product.rewardType, "STAMINA", StringComparison.OrdinalIgnoreCase))
        {
            return "resource";
        }
        if (string.Equals(product.rewardCategory, "RESOURCE", StringComparison.OrdinalIgnoreCase))
        {
            return "resource";
        }
        return "item";
    }

    private static string GetShopProductTag(CoinShopProductResponse product)
    {
        return GetShopCategoryLabel(GetCoinShopCategory(product));
    }

    private static int GetShopRewardAmount(PaymentProductResponse product)
    {
        if (product == null)
        {
            return 0;
        }

        return Mathf.Max(0, product.rewardAmount > 0 ? product.rewardAmount : product.rewardQuantity);
    }

    private static string GetCoinShopProductName(CoinShopProductResponse product)
    {
        if (product == null)
        {
            return "商品";
        }
        return string.IsNullOrEmpty(product.displayName)
            ? GetCoinShopRewardName(product)
            : product.displayName;
    }

    private static string GetCoinShopRewardName(CoinShopProductResponse product)
    {
        if (product == null)
        {
            return "奖励";
        }
        if (!string.IsNullOrEmpty(product.rewardName))
        {
            return product.rewardName;
        }
        if (string.Equals(product.rewardType, "STAMINA", StringComparison.OrdinalIgnoreCase))
        {
            return "体力";
        }
        if (!string.IsNullOrEmpty(product.rewardKey))
        {
            return product.rewardKey;
        }
        if (string.Equals(product.rewardType, "STAMINA", StringComparison.OrdinalIgnoreCase))
        {
            return "体力";
        }
        return string.Equals(product.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase) ? "装备" : "物品";
    }

    private static string GetCoinShopIconText(CoinShopProductResponse product)
    {
        string label = product == null ? string.Empty : GetCoinShopRewardName(product);
        return string.IsNullOrEmpty(label) ? "商" : label.Substring(0, 1);
    }

    private void ConfigureShopProductIcon(
        Transform root,
        CoinShopProductResponse product,
        string imageObjectName,
        string fallbackTextObjectName)
    {
        Image image = null;
        if (imageObjectName == "ShopProductIcon")
        {
            Transform iconFrame = FindSceneTransform(root, "ShopProductIcon");
            image = FindSceneComponent<Image>(iconFrame, "Image");
        }
        else
        {
            image = FindSceneComponent<Image>(root, imageObjectName);
        }

        if (image == null)
        {
            if (imageObjectName != "ShopProductIcon")
            {
                image = FindSceneComponent<Image>(root, "ShopDetailIcon");
            }
        }

        TMP_Text fallback = FindSceneComponent<TMP_Text>(root, fallbackTextObjectName);
        if (fallback == null)
        {
            fallback = FindSceneComponent<TMP_Text>(root, fallbackTextObjectName == "ProductIconText" ? "ShopProductIconText" : "ShopDetailIconText");
        }
        Sprite sprite = ResolveShopProductSprite(product);

        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        if (fallback != null)
        {
            fallback.gameObject.SetActive(sprite == null);
            fallback.text = GetCoinShopIconText(product);
        }
    }

    private Sprite ResolveShopProductSprite(CoinShopProductResponse product)
    {
        if (product == null)
        {
            return null;
        }

        if (string.Equals(product.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase))
        {
            string[] equipmentIconKeys =
            {
                product.productIcon,
                product.rewardIcon,
                product.rewardKey,
                product.rewardName,
                product.displayName,
                product.productCode
            };
            for (int candidateIndex = 0; candidateIndex < equipmentIconKeys.Length; candidateIndex++)
            {
                Sprite equipmentSprite = ResolveEquipmentSprite(equipmentIconKeys[candidateIndex]);
                if (equipmentSprite != null)
                {
                    return equipmentSprite;
                }
            }
            return null;
        }

        string[] candidates =
        {
            product.rewardKey,
            product.rewardName,
            product.productIcon,
            product.productCode,
            product.rewardIcon
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            string assetPath = GetShopIconAssetPath(candidates[i]);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            Sprite sprite = LoadSpriteFromAsset(assetPath);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static string GetShopIconAssetPath(string value)
    {
        string key = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (key.IndexOf("/") >= 0)
        {
            string normalizedPath = key.Replace("\\", "/");
            if (!normalizedPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && !normalizedPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                && !normalizedPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath += ".png";
            }
            return normalizedPath;
        }

        string normalized = key.ToLowerInvariant();
        if (normalized.Contains("gold") || key.Contains("金币")) return "UI/资源图标/金币.png";
        if (normalized.Contains("diamond") || normalized.Contains("crystal") || key.Contains("钻石")) return "UI/资源图标/万能彩墨.png";
        if (normalized.Contains("wood") || key.Contains("木材")) return "UI/资源图标/木材.png";
        if (normalized.Contains("stone") || key.Contains("石材")) return "UI/资源图标/石材.png";
        if (normalized.Contains("iron") || normalized.Contains("ore") || key.Contains("矿")) return "UI/资源图标/矿石.png";
        if (normalized.Contains("wheat") || key.Contains("小麦")) return "UI/资源图标/小麦.png";
        if (normalized.Contains("fish") || key.Contains("鱼")) return "UI/资源图标/鱼.png";
        if (normalized.Contains("meat") || key.Contains("肉")) return "UI/资源图标/肉.png";
        if (normalized.Contains("bread") || normalized.Contains("food") || key.Contains("面包") || key.Contains("食物")) return "UI/资源图标/面包.png";
        if (normalized.Contains("metal") || key.Contains("金属")) return "UI/资源图标/金属.png";
        if (normalized.Contains("purification") || key.Contains("净化剂")) return "UI/资源图标/净化剂.png";
        if (normalized.Contains("stamina") || key.Contains("体力")) return "UI/资源图标/体力.png";
        if (normalized.Contains("chest") || key.Contains("宝箱")) return "UI/资源图标/宝箱（未打开）.png";
        if (normalized.Contains("sword") || normalized.Contains("weapon") || normalized.Contains("equipment")
            || key.Contains("短剑") || key.Contains("装备")) return "UI/图标/主图标/装备.png";
        return string.Empty;
    }

    private string GetCoinShopOwnedText(CoinShopProductResponse product)
    {
        if (string.Equals(product.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase))
        {
            return "装备将存入装备仓库";
        }

        if (currentInventoryItems != null)
        {
            for (int i = 0; i < currentInventoryItems.Length; i++)
            {
                InventoryItemResponse item = currentInventoryItems[i];
                long rewardItemId = product.rewardItemId;
                if (item != null && rewardItemId > 0 && item.itemId == rewardItemId)
                {
                    return "背包已拥有 " + item.quantity;
                }
            }
        }
        return "背包数量将在购买后同步";
    }

    private string BuildCoinShopLimitText(CoinShopProductResponse product)
    {
        if (coinShopCatalog == null || !coinShopCatalog.enabled)
        {
            return "商城当前未开放";
        }

        return "可购 " + product.maxPurchasableLots + " 批，今日剩余 " + product.remainingDailyRewardQuantity;
    }

    private static long ClampCoinShopLots(CoinShopProductResponse product, long value)
    {
        if (product == null || product.maxPurchasableLots <= 0)
        {
            return 0;
        }

        long min = Math.Max(1L, product.minLotsPerOrder);
        long max = Math.Max(0L, product.maxPurchasableLots);
        if (max < min)
        {
            return 0;
        }
        return Math.Max(min, Math.Min(max, value <= 0 ? min : value));
    }

    private static long GetCoinShopTotal(long perLot, long lotCount)
    {
        if (perLot <= 0 || lotCount <= 0)
        {
            return 0;
        }
        return perLot > long.MaxValue / lotCount ? long.MaxValue : perLot * lotCount;
    }

    private static int ClampCoinShopPlayerPrefsValue(long value)
    {
        return (int)Math.Max(0L, Math.Min(int.MaxValue, value));
    }

    private static string GetCoinShopPurchaseRewardText(CoinShopPurchaseResult result)
    {
        if (result == null)
        {
            return "奖励已发放";
        }
        string name = !string.IsNullOrEmpty(result.rewardName)
            ? result.rewardName
            : (!string.IsNullOrEmpty(result.rewardKey) ? result.rewardKey : "奖励");
        if (string.Equals(result.rewardType, "STAMINA", StringComparison.OrdinalIgnoreCase)) name = "体力";
        return name + " x" + result.grantedQuantity;
    }

    private string GetPendingCoinShopPurchaseKey()
    {
        return (IsDiamondShopActive() ? "DiamondShopPendingPurchase_" : "CoinShopPendingPurchase_") + GameSession.PlayerId;
    }

    private CoinShopPendingPurchase LoadPendingCoinShopPurchase()
    {
        string json = PlayerPrefs.GetString(GetPendingCoinShopPurchaseKey(), string.Empty);
        CoinShopPendingPurchase pending = ParseJson<CoinShopPendingPurchase>(json);
        return pending == null || string.IsNullOrEmpty(pending.idempotencyKey) || pending.productId <= 0 || pending.lotCount <= 0
            ? null
            : pending;
    }

    private void SavePendingCoinShopPurchase(CoinShopPendingPurchase pending)
    {
        PlayerPrefs.SetString(GetPendingCoinShopPurchaseKey(), JsonUtility.ToJson(pending));
        PlayerPrefs.Save();
    }

    private void ClearPendingCoinShopPurchase()
    {
        PlayerPrefs.DeleteKey(GetPendingCoinShopPurchaseKey());
        PlayerPrefs.Save();
    }

    private static bool IsSameCoinShopPurchase(CoinShopPendingPurchase pending, long productId, long lotCount, long configRevision)
    {
        return pending != null
            && pending.productId == productId
            && pending.lotCount == lotCount
            && pending.expectedConfigRevision == configRevision;
    }

    private void ShowCoinShopHistory()
    {
        if (!HasAuthSession() || shopPanelObject == null)
        {
            return;
        }

        EnsureCoinShopHistoryPanel(GetRegisteredScenePanel(HomePageScenePanelType.Shop).PanelRoot);
        if (coinShopHistoryPanelObject == null || coinShopHistoryBlockerObject == null)
        {
            Debug.LogWarning("CoinShopHistoryPanel or ShopDetailBlocker is missing from the ShopPanel scene hierarchy.", this);
            return;
        }
        PlaceCoinShopHistoryBlockerBehindPanel();
        coinShopHistoryBlockerObject.SetActive(true);
        coinShopHistoryPanelObject.SetActive(true);
        coinShopHistoryCursors.Clear();
        coinShopHistoryCursors.Add(0);
        coinShopHistoryItems = new CoinShopPurchaseHistoryItem[0];
        coinShopHistoryPageIndex = 0;
        coinShopHistoryNextCursor = 0;
        coinShopHistoryHasMore = false;
        RenderCoinShopHistory("正在读取购买记录...");
        StartCoroutine(GetCoinShopHistoryPage());
    }

    private void EnsureCoinShopHistoryPanel(Transform root)
    {
        if (root == null)
        {
            return;
        }

        if (coinShopHistoryPanelObject == null)
        {
            Transform historyPanel = FindSceneTransform(root, "CoinShopHistoryPanel");
            if (historyPanel != null)
            {
                coinShopHistoryPanelObject = historyPanel.gameObject;
            }
        }

        if (coinShopHistoryBlockerObject == null)
        {
            Transform historyBlocker = FindSceneTransform(root, "ShopDetailBlocker");
            if (historyBlocker != null)
            {
                coinShopHistoryBlockerObject = historyBlocker.gameObject;
            }
        }
    }

    private void RenderCoinShopHistory(string status)
    {
        if (coinShopHistoryPanelObject == null || !coinShopHistoryPanelObject.activeInHierarchy)
        {
            return;
        }

        Transform root = coinShopHistoryPanelObject.transform;
        string currencyLabel = GetActiveShopCurrencyLabel();
        SetSceneText(root, "CoinShopHistoryTitle", currencyLabel + "商城购买记录");
        SetSceneText(root, "CoinShopHistoryStatus", status);
        SetSceneText(root, "CoinShopHistoryPageText", "第 " + (coinShopHistoryPageIndex + 1) + " 页");
        Button close = FindSceneComponent<Button>(root, "CoinShopHistoryCloseButton");
        Button previous = FindSceneComponent<Button>(root, "CoinShopHistoryPreviousButton");
        Button next = FindSceneComponent<Button>(root, "CoinShopHistoryNextButton");
        Button refresh = FindSceneComponent<Button>(root, "CoinShopHistoryRefreshButton");
        BindSceneButton(close, HideCoinShopHistory);
        if (previous != null)
        {
            previous.interactable = !coinShopHistoryLoading && coinShopHistoryPageIndex > 0;
        }
        if (next != null)
        {
            next.interactable = !coinShopHistoryLoading && coinShopHistoryHasMore && coinShopHistoryNextCursor > 0;
        }
        if (refresh != null)
        {
            refresh.interactable = !coinShopHistoryLoading;
        }
        BindSceneButton(previous, previous != null && previous.interactable ? () => ChangeCoinShopHistoryPage(-1) : null);
        BindSceneButton(next, next != null && next.interactable ? () => ChangeCoinShopHistoryPage(1) : null);
        BindSceneButton(refresh, refresh != null && refresh.interactable ? RefreshCoinShopHistory : null);

        StringBuilder historyText = new StringBuilder();
        if (coinShopHistoryItems == null || coinShopHistoryItems.Length == 0)
        {
            historyText.Append("暂无").Append(currencyLabel).Append("商城购买记录");
        }
        else
        {
            for (int i = 0; i < coinShopHistoryItems.Length; i++)
            {
                CoinShopPurchaseHistoryItem item = coinShopHistoryItems[i];
                if (item == null)
                {
                    continue;
                }
                string rewardName = string.IsNullOrEmpty(item.rewardName) ? item.rewardKey : item.rewardName;
                historyText.Append('#').Append(item.orderId).Append("  ")
                    .Append(string.IsNullOrEmpty(item.productName) ? item.productCode : item.productName)
                    .Append("  批次 ").Append(item.lotCount).Append('\n')
                    .Append("获得 ").Append(rewardName).Append(" x").Append(item.grantedQuantity)
                    .Append("，消耗").Append(currencyLabel).Append(' ').Append(IsDiamondShopActive() ? item.chargedDiamond : item.chargedGold)
                    .Append("，余额 ").Append(IsDiamondShopActive() ? item.diamondBalanceAfter : item.goldBalanceAfter).Append('\n');
                if (i + 1 < coinShopHistoryItems.Length)
                {
                    historyText.Append('\n');
                }
            }
        }
        SetSceneText(root, "CoinShopHistoryList", historyText.ToString());
    }

    private void HideCoinShopHistory()
    {
        if (coinShopHistoryPanelObject != null)
        {
            coinShopHistoryPanelObject.SetActive(false);
        }
        if (coinShopHistoryBlockerObject != null)
        {
            coinShopHistoryBlockerObject.SetActive(shopDetailVisible);
        }
    }

    private bool IsCoinShopHistoryVisible()
    {
        return coinShopHistoryPanelObject != null && coinShopHistoryPanelObject.activeSelf;
    }

    private void PlaceCoinShopHistoryBlockerBehindPanel()
    {
        if (coinShopHistoryPanelObject == null || coinShopHistoryBlockerObject == null)
        {
            return;
        }

        Transform panelTransform = coinShopHistoryPanelObject.transform;
        Transform blockerTransform = coinShopHistoryBlockerObject.transform;
        if (panelTransform.parent == null || blockerTransform.parent != panelTransform.parent)
        {
            return;
        }

        int panelIndex = panelTransform.GetSiblingIndex();
        int blockerIndex = blockerTransform.GetSiblingIndex();
        int targetIndex = blockerIndex < panelIndex ? panelIndex - 1 : panelIndex;
        blockerTransform.SetSiblingIndex(Mathf.Max(0, targetIndex));
    }

    private void RefreshCoinShopHistory()
    {
        if (coinShopHistoryLoading)
        {
            return;
        }
        coinShopHistoryCursors.Clear();
        coinShopHistoryCursors.Add(0);
        coinShopHistoryPageIndex = 0;
        StartCoroutine(GetCoinShopHistoryPage());
    }

    private void ChangeCoinShopHistoryPage(int offset)
    {
        if (coinShopHistoryLoading)
        {
            return;
        }
        if (offset > 0 && coinShopHistoryHasMore && coinShopHistoryNextCursor > 0)
        {
            if (coinShopHistoryPageIndex + 1 >= coinShopHistoryCursors.Count)
            {
                coinShopHistoryCursors.Add(coinShopHistoryNextCursor);
            }
            coinShopHistoryPageIndex++;
        }
        else if (offset < 0 && coinShopHistoryPageIndex > 0)
        {
            coinShopHistoryPageIndex--;
        }
        else
        {
            return;
        }
        StartCoroutine(GetCoinShopHistoryPage());
    }

    private IEnumerator GetCoinShopHistoryPage()
    {
        if (coinShopHistoryLoading || !HasAuthSession())
        {
            yield break;
        }

        coinShopHistoryLoading = true;
        RenderCoinShopHistory("正在读取购买记录...");
        long cursor = coinShopHistoryPageIndex < coinShopHistoryCursors.Count ? coinShopHistoryCursors[coinShopHistoryPageIndex] : 0;
        string apiPrefix = GetActiveShopApiPrefix();
        string path = cursor > 0
            ? apiPrefix + "/purchases/history?limit=" + CoinShopHistoryPageSize + "&beforeId=" + cursor
            : apiPrefix + "/purchases/history?limit=" + CoinShopHistoryPageSize;
        UnityWebRequest request = CreateAuthorizedGet(path);
        if (request == null)
        {
            coinShopHistoryLoading = false;
            RenderCoinShopHistory("购买记录读取失败：" + requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                coinShopHistoryLoading = false;
                yield break;
            }

            CoinShopHistoryApiResponse response = ParseJson<CoinShopHistoryApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                coinShopHistoryLoading = false;
                RenderCoinShopHistory("购买记录读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            coinShopHistoryItems = response.data.items ?? new CoinShopPurchaseHistoryItem[0];
            coinShopHistoryNextCursor = response.data.nextCursor;
            coinShopHistoryHasMore = response.data.hasMore && coinShopHistoryNextCursor > 0;
            if (coinShopHistoryHasMore && coinShopHistoryPageIndex + 1 < coinShopHistoryCursors.Count)
            {
                coinShopHistoryCursors[coinShopHistoryPageIndex + 1] = coinShopHistoryNextCursor;
            }
        }

        coinShopHistoryLoading = false;
        RenderCoinShopHistory("已读取 " + coinShopHistoryItems.Length + " 条购买记录");
    }

    private void HandleCoinShopPurchaseRealtime(CoinShopPurchaseResult result, string eventId)
    {
        HandleShopPurchaseRealtime(result, eventId, GoldShopCategory);
    }

    private void HandleDiamondShopPurchaseRealtime(CoinShopPurchaseResult result, string eventId)
    {
        HandleShopPurchaseRealtime(result, eventId, DiamondShopCategory);
    }

    private void HandleShopPurchaseRealtime(CoinShopPurchaseResult result, string eventId, string currency)
    {
        string realtimeKey = currency + ":" + eventId;
        if (!string.IsNullOrEmpty(eventId) && !coinShopRealtimeEventIds.Add(realtimeKey))
        {
            return;
        }
        if (result == null)
        {
            return;
        }

        result.paymentCurrency = currency;
        ApplyActiveShopBalance(result);
        QueueCoinShopPurchaseRewardRefresh(result);
        if (shopPanelObject != null && shopPanelObject.activeInHierarchy && activeShopCategory == currency
            && !coinShopCatalogLoading && !coinShopPurchaseRunning)
        {
            StartCoroutine(GetCoinShopCatalog(GetShopCurrencyLabel(currency) + "商城已同步最新购买结果"));
        }
        if (coinShopHistoryPanelObject != null && coinShopHistoryPanelObject.activeInHierarchy
            && activeShopCategory == currency && !coinShopHistoryLoading)
        {
            RefreshCoinShopHistory();
        }
    }

    private void HideShopPanel()
    {
        HideCoinShopHistory();
        HideCoinShopDetail();
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel != null)
        {
            panel.SetVisible(false);
        }
        shopPanelObject = null;
    }

    private void HideCoinShopDetail()
    {
        shopDetailVisible = false;
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel != null)
        {
            SetSceneObjectActive(panel.PanelRoot, "ShopDetailBlocker", IsCoinShopHistoryVisible());
            SetSceneObjectActive(panel.PanelRoot, "ShopDetail", false);
        }
    }

#pragma warning disable 0649
    [Serializable]
    private class CoinShopCatalogApiResponse : BasicApiResponse
    {
        public CoinShopCatalogData data;
    }

    [Serializable]
    private class CoinShopCatalogData
    {
        public bool enabled;
        public long configRevision;
        public string usageDate;
        public long goldItemId;
        public string goldItemKey;
        public string goldItemName;
        public string goldItemIcon;
        public long goldBalance;
        public long diamondItemId;
        public string diamondItemKey;
        public string diamondItemName;
        public string diamondItemIcon;
        public long diamondBalance;
        public CoinShopProductResponse[] products;
    }

    [Serializable]
    private class CoinShopProductResponse
    {
        public long productId;
        public string productCode;
        public string displayName;
        public string description;
        public string productIcon;
        public string rewardType;
        public long rewardItemId;
        public long rewardEquipmentId;
        public string rewardKey;
        public string rewardName;
        public string rewardCategory;
        public string rewardIcon;
        public long rewardQuantityPerLot;
        public long goldPricePerLot;
        public long diamondPricePerLot;
        public long minLotsPerOrder;
        public long maxLotsPerOrder;
        public long dailyRewardQuantityLimit;
        public long usedRewardQuantity;
        public long remainingDailyRewardQuantity;
        public long maxPurchasableLots;
    }

    [Serializable]
    private class CoinShopPurchaseRequest
    {
        public long productId;
        public long lotCount;
        public long expectedConfigRevision;
    }

    [Serializable]
    private class CoinShopPurchaseApiResponse : BasicApiResponse
    {
        public CoinShopPurchaseResult data;
    }

    [Serializable]
    private class CoinShopPurchaseResult
    {
        public long orderId;
        public string orderNo;
        public string usageDate;
        public long productId;
        public string productCode;
        public string productName;
        public string productIcon;
        public string rewardType;
        public long rewardItemId;
        public long rewardEquipmentId;
        public string rewardKey;
        public string rewardName;
        public string rewardCategory;
        public string rewardIcon;
        public long rewardQuantityPerLot;
        public long goldPricePerLot;
        public long diamondPricePerLot;
        public long lotCount;
        public long chargedGold;
        public long chargedDiamond;
        public long grantedQuantity;
        public long goldItemId;
        public string goldItemKey;
        public long goldBalanceBefore;
        public long goldBalanceAfter;
        public long diamondItemId;
        public string diamondItemKey;
        public long diamondBalanceBefore;
        public long diamondBalanceAfter;
        public long rewardItemBalanceBefore;
        public long rewardItemBalanceAfter;
        public long configRevision;
        public string createTime;
        public bool replayed;
        public string paymentCurrency;
    }

    [Serializable]
    private class CoinShopHistoryApiResponse : BasicApiResponse
    {
        public CoinShopHistoryData data;
    }

    [Serializable]
    private class CoinShopHistoryData
    {
        public CoinShopPurchaseHistoryItem[] items;
        public long nextCursor;
        public bool hasMore;
    }

    [Serializable]
    private class CoinShopPurchaseHistoryItem
    {
        public long orderId;
        public string orderNo;
        public string usageDate;
        public long productId;
        public string productCode;
        public string productName;
        public string productIcon;
        public string rewardType;
        public long rewardItemId;
        public long rewardEquipmentId;
        public string rewardKey;
        public string rewardName;
        public string rewardCategory;
        public string rewardIcon;
        public long lotCount;
        public long chargedGold;
        public long chargedDiamond;
        public long grantedQuantity;
        public long goldBalanceAfter;
        public long diamondBalanceAfter;
        public long rewardItemBalanceAfter;
        public long configRevision;
        public string createTime;
    }

    [Serializable]
    private class CoinShopPendingPurchase
    {
        public string idempotencyKey;
        public long productId;
        public long lotCount;
        public long expectedConfigRevision;
    }
#pragma warning restore 0649
}
