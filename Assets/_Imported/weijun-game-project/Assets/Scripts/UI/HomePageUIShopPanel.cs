using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int ShopPageSize = 6;
    private const int CoinShopHistoryPageSize = 10;
    private const string GoldShopCategory = "gold";
    private const string DiamondShopCategory = "diamond";

    private GameObject shopPanelObject;
    private CoinShopCatalogData coinShopCatalog;
    private CoinShopProductResponse[] coinShopProducts = new CoinShopProductResponse[0];
    private string activeShopCategory = GoldShopCategory;
    private long selectedCoinShopProductId;
    private long selectedCoinShopLotCount;
    private string shopStatusMessage = string.Empty;
    private string shopPurchaseSummary = string.Empty;
    private int shopPageIndex;
    private bool coinShopCatalogLoading;
    private bool coinShopPurchaseRunning;

    private GameObject coinShopHistoryPanelObject;
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
            SetLeftContent("商城", "商城场景组件尚未生成，请重新生成首页面板。", "商城场景组件未找到");
            return;
        }

        shopPanelObject = panel.gameObject;
        activeShopCategory = GoldShopCategory;
        shopPageIndex = 0;
        selectedCoinShopProductId = 0;
        selectedCoinShopLotCount = 0;

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
        EnsureShopCurrencyCategoryControls(root);
        EnsureShopCurrencyBalanceBadges(root);
        EnsureCoinShopLotControls(root);
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
        Button refreshButton = FindSceneComponent<Button>(root, "ShopRefreshButton");
        refreshButton.interactable = HasAuthSession() && !coinShopCatalogLoading && !coinShopPurchaseRunning;
        SetSceneButtonText(refreshButton, coinShopCatalogLoading ? "读取中..." : "刷新");
        BindSceneButton(refreshButton, RefreshShopData);

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
        for (int i = 0; i < ShopPageSize; i++)
        {
            Transform card = FindSceneTransform(root, "ShopProductCard" + i);
            if (card == null)
            {
                continue;
            }

            int productIndex = pageStart + i;
            bool visible = productIndex < visibleProducts.Count;
            card.gameObject.SetActive(visible);
            if (visible)
            {
                ConfigureCoinShopProductCard(card, visibleProducts[productIndex]);
            }
        }

        SetSceneObjectActive(root, "ShopEmptyState", !coinShopCatalogLoading && visibleProducts.Count == 0);
        ConfigureCoinShopDetails(root, FindSelectedCoinShopProduct(visibleProducts));
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
        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = selected
                ? new Color(0.22f, 0.58f, 0.48f, 1f)
                : new Color(0.95f, 0.78f, 0.48f, 1f);
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = FrontendTextLocalization.Normalize(label);
            text.color = selected ? Color.white : new Color(0.34f, 0.18f, 0.08f, 1f);
        }

        button.interactable = !coinShopCatalogLoading && !coinShopPurchaseRunning;
        SetSceneObjectActive(button.transform, "SelectedMarker", selected);
        BindSceneButton(button, button.interactable ? () => SelectShopCategory(category) : null);
    }

    private void EnsureShopCurrencyCategoryControls(Transform root)
    {
        Transform sidebar = FindSceneTransform(root, "ShopSidebar");
        if (sidebar == null)
        {
            return;
        }

        EnsureShopCurrencyCategoryButton(sidebar, GoldShopCategory, "featured", new Vector2(12f, -76f));
        EnsureShopCurrencyCategoryButton(sidebar, DiamondShopCategory, "resource", new Vector2(12f, -148f));
        SetSceneObjectActive(sidebar, "ShopCategory_item", false);
        SetSceneObjectActive(sidebar, "ShopCategory_growth", false);
        Transform note = FindSceneTransform(sidebar, "ShopSidebarNote");
        if (note != null)
        {
            SetRect(note.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(12f, -250f), new Vector2(158f, 176f));
        }
    }

    private void EnsureShopCurrencyCategoryButton(Transform sidebar, string category, string legacyCategory, Vector2 position)
    {
        string objectName = "ShopCategory_" + category;
        Button button = FindSceneComponent<Button>(sidebar, objectName);
        if (button == null)
        {
            button = FindSceneComponent<Button>(sidebar, "ShopCategory_" + legacyCategory);
            if (button != null)
            {
                button.gameObject.name = objectName;
            }
        }
        if (button == null)
        {
            button = CreateButton(sidebar, objectName, GetShopCategoryLabel(category), new Color(0.95f, 0.78f, 0.48f, 1f), new Color(0.34f, 0.18f, 0.08f, 1f));
        }

        button.gameObject.SetActive(true);
        SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, new Vector2(158f, 58f));
        if (FindSceneTransform(button.transform, "SelectedMarker") == null)
        {
            Image selected = CreateImage("SelectedMarker", button.transform, new Color(1f, 0.87f, 0.48f, 1f));
            SetRect(selected.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(7f, 58f));
        }
    }

    private void EnsureShopCurrencyBalanceBadges(Transform root)
    {
        Transform header = FindSceneTransform(root, "ShopHeader");
        if (header == null)
        {
            return;
        }

        Text gold = FindSceneComponent<Text>(header, "ShopGoldText");
        if (gold != null && gold.transform.parent != null)
        {
            gold.transform.parent.gameObject.SetActive(true);
            SetRect(gold.transform.parent.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(650f, -16f), new Vector2(188f, 44f));
        }

        Text diamond = FindSceneComponent<Text>(header, "ShopCrystalText");
        if (diamond == null)
        {
            Image badge = CreateImage("ShopCrystalTextBadge", header, new Color(0.08f, 0.22f, 0.19f, 0.92f));
            SetRect(badge.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(850f, -16f), new Vector2(188f, 44f));
            Image mark = CreateImage("ShopCrystalTextMark", badge.transform, new Color(0.30f, 0.78f, 0.95f, 1f));
            SetRect(mark.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(10f, -12f), new Vector2(20f, 20f));
            diamond = CreateText("ShopCrystalText", badge.transform, "钻石  --", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(diamond.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(40f, -5f), new Vector2(138f, 34f));
        }
        else if (diamond.transform.parent != null)
        {
            diamond.transform.parent.gameObject.SetActive(true);
            SetRect(diamond.transform.parent.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(850f, -16f), new Vector2(188f, 44f));
        }
    }

    private void ConfigureCoinShopProductCard(Transform card, CoinShopProductResponse product)
    {
        bool selected = product.productId == selectedCoinShopProductId;
        SetSceneText(card, "ShopProductTag", GetShopProductTag(product));
        SetSceneText(card, "ShopProductIconText", GetCoinShopIconText(product));
        SetSceneText(card, "ShopProductName", GetCoinShopProductName(product));
        SetSceneText(card, "ShopProductReward", GetCoinShopRewardName(product) + " ×" + product.rewardQuantityPerLot + "/批");
        SetSceneText(card, "ShopProductPrice", GetActiveShopCurrencyLabel() + " " + GetActiveShopPricePerLot(product) + "/批");
        SetSceneObjectActive(card, "ShopSelectionMark", selected);

        Image background = card.GetComponent<Image>();
        if (background != null)
        {
            background.color = selected
                ? new Color(1f, 0.88f, 0.55f, 1f)
                : new Color(1f, 0.96f, 0.82f, 1f);
        }

        Image icon = FindSceneComponent<Image>(card, "ShopProductIcon");
        if (icon != null)
        {
            icon.color = GetCoinShopIconColor(product);
        }

        Button button = card.GetComponent<Button>();
        long productId = product.productId;
        BindSceneButton(button, () => SelectCoinShopProduct(productId));
    }

    private void ConfigureCoinShopDetails(Transform root, CoinShopProductResponse product)
    {
        Button buyButton = FindSceneComponent<Button>(root, "ShopBuyGoldButton");
        Button historyButton = FindSceneComponent<Button>(root, "ShopBuyCrystalButton");
        Button openBagButton = FindSceneComponent<Button>(root, "ShopOpenBagButton");
        ConfigureCoinShopHistoryButton(historyButton);
        BindSceneButton(openBagButton, OpenServerInventoryFromShop);
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
            ConfigureCoinShopBuyButton(buyButton, GetActiveShopCurrencyLabel() + "购买", false);
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
        SetSceneText(root, "ShopDetailName", productName);
        SetSceneText(root, "ShopDetailDescription", string.IsNullOrEmpty(product.description) ? "服务器正式发布商品" : product.description);
        SetSceneText(root, "ShopDetailReward", "购买内容  " + GetCoinShopRewardName(product) + " ×" + totalReward);
        SetSceneText(root, "ShopDetailPrice", GetActiveShopCurrencyLabel() + " " + totalCost + "  (" + pricePerLot + "/批)");
        SetSceneText(root, "ShopPurchaseSummary", shopPurchaseSummary);
        SetSceneText(root, "ShopOwnedCount", GetCoinShopOwnedText(product));
        SetSceneText(root, "ShopStorageModeText", BuildCoinShopLimitText(product));

        Image detailIcon = FindSceneComponent<Image>(root, "ShopDetailIcon");
        if (detailIcon != null)
        {
            detailIcon.color = GetCoinShopIconColor(product);
        }

        ConfigureCoinShopLotControls(root, product);
        ConfigureCoinShopBuyButton(buyButton, coinShopPurchaseRunning ? "购买中..." : GetActiveShopCurrencyLabel() + " " + totalCost + " 购买", canPurchase);
    }

    private void ConfigureCoinShopBuyButton(Button button, string label, bool interactable)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = interactable;
        SetSceneButtonText(button, label);
        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = IsDiamondShopActive()
                ? new Color(0.28f, 0.66f, 0.82f, 1f)
                : new Color(0.88f, 0.57f, 0.16f, 1f);
        }
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
        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.30f, 0.57f, 0.52f, 1f);
        }
        BindSceneButton(button, ShowCoinShopHistory);
    }

    private void EnsureCoinShopLotControls(Transform root)
    {
        Transform purchase = FindSceneTransform(root, "ShopPurchaseBox");
        if (purchase == null)
        {
            return;
        }

        Text storageText = FindSceneComponent<Text>(purchase, "ShopStorageModeText");
        if (storageText != null)
        {
            SetRect(storageText.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(14f, -104f), new Vector2(132f, 30f));
        }

        Button openBagButton = FindSceneComponent<Button>(purchase, "ShopOpenBagButton");
        if (openBagButton != null)
        {
            SetRect(openBagButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(214f, -142f), new Vector2(144f, 34f));
        }

        if (FindSceneComponent<Button>(purchase, "ShopLotDecreaseButton") != null)
        {
            return;
        }

        Button decrease = CreateButton(purchase, "ShopLotDecreaseButton", "-", new Color(0.67f, 0.46f, 0.22f, 1f), Color.white);
        SetRect(decrease.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(152f, -101f), new Vector2(38f, 34f));
        Text count = CreateText("ShopLotCountText", purchase, "批次 --", 14, FontStyle.Bold, new Color(0.27f, 0.16f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetRect(count.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(192f, -101f), new Vector2(74f, 34f));
        Button increase = CreateButton(purchase, "ShopLotIncreaseButton", "+", new Color(0.31f, 0.62f, 0.45f, 1f), Color.white);
        SetRect(increase.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(268f, -101f), new Vector2(38f, 34f));
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
        shopPurchaseSummary = "本次获得 " + GetCoinShopRewardName(product) + " ×"
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

    private static Color GetCoinShopIconColor(CoinShopProductResponse product)
    {
        string category = GetCoinShopCategory(product);
        if (category == "resource") return new Color(0.34f, 0.68f, 0.31f, 1f);
        if (category == "growth") return new Color(0.58f, 0.47f, 0.77f, 1f);
        return new Color(0.28f, 0.62f, 0.76f, 1f);
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
        return name + " ×" + result.grantedQuantity;
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
        coinShopHistoryPanelObject.SetActive(true);
        coinShopHistoryPanelObject.transform.SetAsLastSibling();
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
        if (coinShopHistoryPanelObject != null || root == null)
        {
            return;
        }

        Image panel = CreateImage("CoinShopHistoryPanel", root, new Color(0.98f, 0.90f, 0.68f, 1f));
        coinShopHistoryPanelObject = panel.gameObject;
        SetRect(panel.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(316f, -152f), new Vector2(800f, 500f));

        Image header = CreateImage("CoinShopHistoryHeader", panel.transform, new Color(0.17f, 0.42f, 0.36f, 1f));
        SetRect(header.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), Vector2.zero, new Vector2(800f, 58f));
        Text title = CreateText("CoinShopHistoryTitle", header.transform, "商城购买记录", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -8f), new Vector2(400f, 38f));
        Button close = CreateButton(header.transform, "CoinShopHistoryCloseButton", "关闭", new Color(0.84f, 0.30f, 0.24f, 1f), Color.white);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(740f, -8f), new Vector2(42f, 42f));

        Text status = CreateText("CoinShopHistoryStatus", panel.transform, string.Empty, 15, FontStyle.Normal, new Color(0.36f, 0.22f, 0.11f, 1f), TextAnchor.MiddleLeft);
        SetRect(status.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -72f), new Vector2(740f, 28f));
        Text list = CreateText("CoinShopHistoryList", panel.transform, string.Empty, 16, FontStyle.Normal, new Color(0.31f, 0.18f, 0.09f, 1f), TextAnchor.UpperLeft);
        SetRect(list.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(24f, -108f), new Vector2(752f, 288f));
        Button previous = CreateButton(panel.transform, "CoinShopHistoryPreviousButton", "<", new Color(0.38f, 0.60f, 0.54f, 1f), Color.white);
        SetRect(previous.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(490f, -436f), new Vector2(46f, 40f));
        Text page = CreateText("CoinShopHistoryPageText", panel.transform, "1", 16, FontStyle.Bold, new Color(0.30f, 0.17f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetRect(page.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(542f, -438f), new Vector2(100f, 36f));
        Button next = CreateButton(panel.transform, "CoinShopHistoryNextButton", ">", new Color(0.38f, 0.60f, 0.54f, 1f), Color.white);
        SetRect(next.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(648f, -436f), new Vector2(46f, 40f));
        Button refresh = CreateButton(panel.transform, "CoinShopHistoryRefreshButton", "刷新", new Color(0.84f, 0.59f, 0.22f, 1f), new Color(0.28f, 0.16f, 0.06f, 1f));
        SetRect(refresh.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(704f, -436f), new Vector2(72f, 40f));
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
        .Append("获得 ").Append(rewardName).Append(" ×").Append(item.grantedQuantity)
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
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel != null)
        {
            panel.SetVisible(false);
        }
        shopPanelObject = null;
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
