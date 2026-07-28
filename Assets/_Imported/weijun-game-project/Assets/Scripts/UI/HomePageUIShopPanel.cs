using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int ShopPageSize = 6;

    private GameObject shopPanelObject;
    private ShopProduct[] shopProducts = new ShopProduct[0];
    private string activeShopCategory = "featured";
    private string selectedShopProductCode = string.Empty;
    private string shopStatusMessage = string.Empty;
    private string shopPurchaseSummary = string.Empty;
    private int shopPageIndex;

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
        HideGuestProfilePanel();
        HideFriendsPanel();
        HideSettingsPanel();
        HideShopPanel();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel == null)
        {
            SetLeftContent(
                "商城",
                "商城场景组件尚未生成。请在 Unity 中执行 Tools/WeiJun UI/Rebuild Home Scene Panels。",
                "商城场景组件未找到");
            return;
        }

        shopPanelObject = panel.gameObject;
        shopProducts = BuildShopProducts();
        activeShopCategory = "featured";
        selectedShopProductCode = shopProducts.Length > 0 ? shopProducts[0].code : string.Empty;
        shopPageIndex = 0;
        shopPurchaseSummary = "选择商品后，可使用金币或钻石购买";
        shopStatusMessage = HasAuthSession()
            ? "账号模式暂无资源商城交易接口，当前仅可预览商品"
            : "游客本地商城：购买的资源和道具将直接放入背包";
        RenderShopScenePanel();
    }

    private void RenderShopScenePanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel == null || shopPanelObject == null)
        {
            return;
        }

        Transform root = panel.PanelRoot;
        SetSceneText(root, "ShopGoldText", $"金币  {GetShopBalance(false)}");
        SetSceneText(root, "ShopCrystalText", $"钻石  {GetShopBalance(true)}");
        SetSceneText(root, "ShopStatusText", shopStatusMessage);

        BindSceneButton(FindSceneComponent<Button>(root, "ShopCloseButton"), HideShopPanel);
        Button refreshButton = FindSceneComponent<Button>(root, "ShopRefreshButton");
        SetSceneButtonText(refreshButton, "刷新");
        BindSceneButton(refreshButton, RefreshShopData);

        ConfigureShopCategory(root, "featured", "推荐");
        ConfigureShopCategory(root, "resource", "资源");
        ConfigureShopCategory(root, "item", "道具");
        ConfigureShopCategory(root, "growth", "成长");

        List<ShopProduct> visibleProducts = GetFilteredShopProducts();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleProducts.Count / (float)ShopPageSize));
        shopPageIndex = Mathf.Clamp(shopPageIndex, 0, pageCount - 1);
        EnsureSelectedShopProduct(visibleProducts);

        SetSceneText(root, "ShopResultCount", $"{GetShopCategoryLabel(activeShopCategory)}  {visibleProducts.Count} 件商品");
        SetSceneText(root, "ShopPageText", $"{shopPageIndex + 1}/{pageCount}");

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
                ConfigureShopProductCard(card, visibleProducts[productIndex]);
            }
        }

        SetSceneObjectActive(root, "ShopEmptyState", visibleProducts.Count == 0);
        ConfigureShopDetails(root, FindSelectedShopProduct(visibleProducts));
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
            text.text = label;
            text.color = selected ? Color.white : new Color(0.34f, 0.18f, 0.08f, 1f);
        }

        SetSceneObjectActive(button.transform, "SelectedMarker", selected);
        BindSceneButton(button, () => SelectShopCategory(category));
    }

    private void ConfigureShopProductCard(Transform card, ShopProduct product)
    {
        bool selected = product.code == selectedShopProductCode;
        SetSceneText(card, "ShopProductTag", GetShopProductTag(product));
        SetSceneText(card, "ShopProductIconText", product.iconText);
        SetSceneText(card, "ShopProductName", product.name);
        SetSceneText(card, "ShopProductReward", $"{product.name} x{product.amount}");
        SetSceneText(card, "ShopProductPrice", $"金币{product.goldPrice}  钻石{product.crystalPrice}");
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
            icon.color = product.iconColor;
        }

        Button button = card.GetComponent<Button>();
        string productCode = product.code;
        BindSceneButton(button, () => SelectShopProduct(productCode));
    }

    private void ConfigureShopDetails(Transform root, ShopProduct product)
    {
        Button goldButton = FindSceneComponent<Button>(root, "ShopBuyGoldButton");
        Button crystalButton = FindSceneComponent<Button>(root, "ShopBuyCrystalButton");
        Button openBagButton = FindSceneComponent<Button>(root, "ShopOpenBagButton");
        BindSceneButton(openBagButton, OpenLocalInventoryFromShop);

        if (product == null)
        {
            SetSceneText(root, "ShopDetailIconText", "商");
            SetSceneText(root, "ShopDetailName", "暂无商品");
            SetSceneText(root, "ShopDetailDescription", "当前分类没有可购买的商品");
            SetSceneText(root, "ShopDetailReward", "购买内容  --");
            SetSceneText(root, "ShopDetailPrice", "价格  --");
            SetSceneText(root, "ShopPurchaseSummary", "请选择其他商品分类");
            SetSceneText(root, "ShopOwnedCount", "背包已拥有  --");
            ConfigureShopBuyButton(goldButton, "金币购买", false, null, false);
            ConfigureShopBuyButton(crystalButton, "钻石购买", false, null, true);
            return;
        }

        int owned = GetLocalQuantity(product.itemKey, product.defaultOwned);
        SetSceneText(root, "ShopDetailIconText", product.iconText);
        SetSceneText(root, "ShopDetailName", product.name);
        SetSceneText(root, "ShopDetailDescription", product.description);
        SetSceneText(root, "ShopDetailReward", $"购买内容  {product.name} x{product.amount}");
        SetSceneText(root, "ShopDetailPrice", $"金币 {product.goldPrice}    钻石 {product.crystalPrice}");
        SetSceneText(root, "ShopPurchaseSummary", shopPurchaseSummary);
        SetSceneText(root, "ShopOwnedCount", $"背包已拥有  {owned}");

        Image detailIcon = FindSceneComponent<Image>(root, "ShopDetailIcon");
        if (detailIcon != null)
        {
            detailIcon.color = product.iconColor;
        }

        ConfigureShopBuyButton(goldButton, $"金币 {product.goldPrice}", true, product, false);
        ConfigureShopBuyButton(crystalButton, $"钻石 {product.crystalPrice}", true, product, true);
    }

    private void ConfigureShopBuyButton(Button button, string label, bool interactable, ShopProduct product, bool useCrystal)
    {
        interactable = interactable && !HasAuthSession();
        if (button != null)
        {
            button.interactable = interactable;
        }
        SetSceneButtonText(button, label);
        if (interactable)
        {
            BindSceneButton(button, () => PurchaseShopProduct(product, useCrystal));
        }
        else
        {
            BindSceneButton(button, null);
        }
    }

    private void SelectShopCategory(string category)
    {
        activeShopCategory = category;
        shopPageIndex = 0;
        selectedShopProductCode = string.Empty;
        shopPurchaseSummary = "选择商品后，可使用金币或钻石购买";
        shopStatusMessage = $"已切换到{GetShopCategoryLabel(category)}分类";
        RenderShopScenePanel();
    }

    private void SelectShopProduct(string productCode)
    {
        selectedShopProductCode = productCode ?? string.Empty;
        ShopProduct product = FindShopProduct(selectedShopProductCode);
        shopPurchaseSummary = product == null
            ? "请选择商品"
            : $"可购买 {product.name} x{product.amount}";
        shopStatusMessage = product == null ? "请选择商品" : $"已选择 {product.name}";
        RenderShopScenePanel();
    }

    private void ChangeShopPage(int offset)
    {
        List<ShopProduct> products = GetFilteredShopProducts();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(products.Count / (float)ShopPageSize));
        shopPageIndex = Mathf.Clamp(shopPageIndex + offset, 0, pageCount - 1);
        selectedShopProductCode = string.Empty;
        shopPurchaseSummary = "选择商品后，可使用金币或钻石购买";
        RenderShopScenePanel();
    }

    private void RefreshShopData()
    {
        shopProducts = BuildShopProducts();
        if (!HasAuthSession())
        {
            currentInventoryItems = BuildLocalInventoryItems();
        }
        shopStatusMessage = HasAuthSession()
            ? "账号资源来自服务器；资源商城交易接口尚未提供"
            : "金币、钻石和本地背包数量已刷新";
        RenderShopScenePanel();
    }

    private void PurchaseShopProduct(ShopProduct product, bool useCrystal)
    {
        if (HasAuthSession())
        {
            shopPurchaseSummary = "账号模式不能在本地扣款或增加背包物品";
            shopStatusMessage = "购买已阻止：接口文档没有资源商城交易接口";
            RenderShopScenePanel();
            return;
        }

        if (product == null)
        {
            shopStatusMessage = "商品信息无效，请重新选择";
            RenderShopScenePanel();
            return;
        }

        string currencyKey = useCrystal ? "Crystal" : "Gold";
        string currencyName = useCrystal ? "钻石" : "金币";
        int price = useCrystal ? product.crystalPrice : product.goldPrice;
        int balance = GetShopBalance(useCrystal);
        if (balance < price)
        {
            shopPurchaseSummary = $"{currencyName}不足，还差 {price - balance}";
            shopStatusMessage = $"购买失败：{currencyName}余额不足";
            RenderShopScenePanel();
            return;
        }

        int owned = GetLocalQuantity(product.itemKey, product.defaultOwned);
        int nextOwned = owned > int.MaxValue - product.amount
            ? int.MaxValue
            : owned + product.amount;

        PlayerPrefs.SetInt(currencyKey, balance - price);
        SetLocalQuantity(product.itemKey, nextOwned);
        PlayerPrefs.Save();

        currentInventoryItems = BuildLocalInventoryItems();
        inventoryServerBacked = false;
        inventoryStatusMessage = "商城购买内容已放入本地背包";
        shopPurchaseSummary = $"购买成功：{product.name} x{product.amount}";
        shopStatusMessage = $"已使用 {price} {currencyName}，背包现有 {product.name} x{nextOwned}";
        RenderShopScenePanel();
    }

    private void OpenLocalInventoryFromShop()
    {
        if (HasAuthSession())
        {
            HideShopPanel();
            ShowBagContent();
            return;
        }

        HideShopPanel();
        activeInventoryCategory = "all";
        selectedInventoryItemKey = string.Empty;
        ShowInventoryPanel(
            BuildLocalInventoryItems(),
            false,
            "商城本地背包：购买的资源和道具已同步，可使用、出售和整理");
    }

    private int GetShopBalance(bool useCrystal)
    {
        return useCrystal
            ? PlayerPrefs.GetInt("Crystal", 427)
            : PlayerPrefs.GetInt("Gold", 1000);
    }

    private List<ShopProduct> GetFilteredShopProducts()
    {
        List<ShopProduct> result = new List<ShopProduct>();
        if (shopProducts == null)
        {
            return result;
        }

        for (int i = 0; i < shopProducts.Length; i++)
        {
            ShopProduct product = shopProducts[i];
            if (product != null && (activeShopCategory == "featured" || product.category == activeShopCategory))
            {
                result.Add(product);
            }
        }
        return result;
    }

    private void EnsureSelectedShopProduct(List<ShopProduct> products)
    {
        ShopProduct selected = FindSelectedShopProduct(products);
        if (selected != null && products.IndexOf(selected) / ShopPageSize == shopPageIndex)
        {
            return;
        }

        int pageStart = shopPageIndex * ShopPageSize;
        selectedShopProductCode = pageStart < products.Count ? products[pageStart].code : string.Empty;
    }

    private ShopProduct FindSelectedShopProduct(List<ShopProduct> products)
    {
        for (int i = 0; i < products.Count; i++)
        {
            if (products[i].code == selectedShopProductCode)
            {
                return products[i];
            }
        }
        return null;
    }

    private ShopProduct FindShopProduct(string productCode)
    {
        if (shopProducts == null)
        {
            return null;
        }

        for (int i = 0; i < shopProducts.Length; i++)
        {
            if (shopProducts[i] != null && shopProducts[i].code == productCode)
            {
                return shopProducts[i];
            }
        }
        return null;
    }

    private static string GetShopCategoryLabel(string category)
    {
        if (category == "resource") return "资源";
        if (category == "item") return "道具";
        if (category == "growth") return "成长";
        return "推荐";
    }

    private static string GetShopProductTag(ShopProduct product)
    {
        return product == null ? "商品" : GetShopCategoryLabel(product.category);
    }

    private static int GetShopRewardAmount(PaymentProductResponse product)
    {
        if (product == null)
        {
            return 0;
        }

        return Mathf.Max(0, product.rewardAmount > 0 ? product.rewardAmount : product.rewardQuantity);
    }

    private static ShopProduct[] BuildShopProducts()
    {
        return new[]
        {
            CreateShopProduct("shop_wood", "Wood", "木材", "木", "resource", 100, 120, 12, 500, "领地建造与升级所需的基础材料。", new Color(0.34f, 0.68f, 0.31f, 1f)),
            CreateShopProduct("shop_stone", "Stone", "石材", "石", "resource", 100, 150, 15, 500, "坚固的建造材料，可用于设施升级。", new Color(0.48f, 0.58f, 0.65f, 1f)),
            CreateShopProduct("shop_iron", "Iron", "铁矿", "铁", "resource", 80, 180, 18, 300, "锻造装备和高级建筑使用的矿产。", new Color(0.43f, 0.51f, 0.58f, 1f)),
            CreateShopProduct("shop_food", "Food", "食物", "粮", "resource", 5, 80, 8, 8, "补充领地日常经营所需的食物。", new Color(0.77f, 0.52f, 0.20f, 1f)),
            CreateShopProduct("shop_potion", "HealingPotion", "恢复药水", "药", "item", 3, 120, 12, 3, "使用后恢复状态的常备消耗品。", new Color(0.76f, 0.28f, 0.32f, 1f)),
            CreateShopProduct("shop_speed", "SpeedItem", "加速卷轴", "速", "item", 1, 200, 20, 2, "用于缩短经营或建造等待时间。", new Color(0.25f, 0.64f, 0.76f, 1f)),
            CreateShopProduct("shop_sword", "BeginnerSword", "新手短剑", "剑", "growth", 1, 600, 60, 1, "适合新手领主使用的基础武器。", new Color(0.58f, 0.63f, 0.69f, 1f)),
            CreateShopProduct("shop_armor", "ClothArmor", "布甲", "甲", "growth", 1, 600, 60, 1, "提供基础防护的轻便装备。", new Color(0.55f, 0.43f, 0.73f, 1f)),
            CreateShopProduct("shop_pet_egg", "PetEgg", "灵宠蛋", "宠", "growth", 1, 800, 80, 1, "可在背包中查看和使用的灵宠道具。", new Color(0.70f, 0.42f, 0.72f, 1f)),
            CreateShopProduct("shop_equipment_chest", "EquipmentChest", "装备宝箱", "装", "growth", 1, 500, 50, 0, "开启后可获得成长所需的装备。", new Color(0.78f, 0.39f, 0.18f, 1f)),
            CreateShopProduct("shop_hero_chest", "HeroChest", "英雄宝箱", "英", "growth", 1, 800, 80, 0, "用于获取英雄成长内容的宝箱。", new Color(0.88f, 0.55f, 0.18f, 1f)),
            CreateShopProduct("shop_advanced_hero_chest", "AdvancedHeroChest", "高级英雄箱", "高", "growth", 1, 1500, 150, 0, "包含更高品质英雄成长内容。", new Color(0.73f, 0.32f, 0.30f, 1f)),
            CreateShopProduct("shop_lottery_ticket", "LotteryTicket", "高级抽奖券", "券", "item", 1, 300, 30, 0, "可在抽奖界面使用的高级抽奖券。", new Color(0.22f, 0.62f, 0.50f, 1f))
        };
    }

    private static ShopProduct CreateShopProduct(
        string code,
        string itemKey,
        string name,
        string iconText,
        string category,
        int amount,
        int goldPrice,
        int crystalPrice,
        int defaultOwned,
        string description,
        Color iconColor)
    {
        return new ShopProduct
        {
            code = code,
            itemKey = itemKey,
            name = name,
            iconText = iconText,
            category = category,
            amount = amount,
            goldPrice = goldPrice,
            crystalPrice = crystalPrice,
            defaultOwned = defaultOwned,
            description = description,
            iconColor = iconColor
        };
    }

    private void HideShopPanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Shop);
        if (panel != null)
        {
            panel.SetVisible(false);
        }
        shopPanelObject = null;
    }

    private sealed class ShopProduct
    {
        public string code;
        public string itemKey;
        public string name;
        public string iconText;
        public string category;
        public string description;
        public int amount;
        public int goldPrice;
        public int crystalPrice;
        public int defaultOwned;
        public Color iconColor;
    }
}
