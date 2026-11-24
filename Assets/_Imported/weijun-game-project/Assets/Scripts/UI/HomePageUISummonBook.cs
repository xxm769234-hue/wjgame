using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private static readonly Color SummonInk = new Color32(65, 37, 31, 255);
    private SummonRevealPlayer summonReveal;
    private long? summonTicketBalance;
    private InventoryItemResponse[] summonTicketItems;
    private int summonTicketReadVersion;

    private void PlaySummonReveal(string clip, CardDrawData draw)
    {
        if (lotteryPanelObject == null || !lotteryPanelObject.activeInHierarchy || summonReveal != null) return;
        HomePageScenePanel panel = lotteryPanelObject.GetComponent<HomePageScenePanel>();
        Transform parent = panel != null ? panel.PanelRoot : lotteryPanelObject.transform;
        summonReveal = SummonRevealPlayer.Show(parent, clip, () => ShowLotteryRewardConfirm(draw));
    }

    private string ResolveSummonReveal(CardDrawData draw)
    {
        bool epic = false;
        CardDrawCardResponse[] previousCards = serverCardDrawPool != null
            && string.Equals(serverCardDrawPool.poolCode, draw.poolCode, StringComparison.OrdinalIgnoreCase)
            && serverCardDrawPool.version == draw.poolVersion ? serverCardDrawPool.cards : null;
        foreach (CardDrawResultResponse result in draw.results ?? Array.Empty<CardDrawResultResponse>())
        {
            if (result == null || result.duplicateNpcConverted) continue;
            string type = (result.grantedRewardType ?? "").Trim().ToUpperInvariant();
            if (type != "NPC" && type != "EQUIPMENT") continue;
            string quality = GrantedSummonQuality(result, draw.cards, previousCards);
            switch ((quality ?? "").Trim().ToUpperInvariant())
            {
                case "LEGENDARY": case "SSR": case "GOLD": case "LUXURY": case "传说": case "金色": return "Legendary";
                case "EPIC": case "PURPLE": case "史诗": case "紫色": epic = true; break;
            }
        }
        return epic ? "Epic" : "Blue";
    }

    private string GrantedSummonQuality(CardDrawResultResponse result, CardDrawCardResponse[] cards, CardDrawCardResponse[] previousCards)
    {
        if (!string.IsNullOrWhiteSpace(result.grantedRewardQuality)) return result.grantedRewardQuality;
        bool sameReward = !result.duplicateNpcConverted
            && (string.IsNullOrWhiteSpace(result.configuredRewardType) || string.Equals(result.configuredRewardType,result.grantedRewardType,StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(result.configuredRewardKey) || string.IsNullOrWhiteSpace(result.grantedRewardKey) || string.Equals(result.configuredRewardKey,result.grantedRewardKey,StringComparison.OrdinalIgnoreCase))
            && (result.configuredAssetId <= 0 || result.grantedAssetId <= 0 || result.configuredAssetId == result.grantedAssetId);
        if (sameReward)
        {
            if (!string.IsNullOrWhiteSpace(result.rewardQuality)) return result.rewardQuality;
            if (!string.IsNullOrWhiteSpace(result.configuredRewardQuality)) return result.configuredRewardQuality;
        }
        return WinningCardQuality(result,cards) ?? WinningCardQuality(result,previousCards) ?? OwnedSummonQuality(result);
    }

    private static string WinningCardQuality(CardDrawResultResponse result, CardDrawCardResponse[] cards)
    {
        if (cards == null || result.winningSlot <= 0) return null;
        foreach (var card in cards)
        {
            if (card == null || card.slotNo != result.winningSlot) continue;
            if (!string.Equals(result.grantedRewardType,card.rewardType,StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(result.grantedRewardKey) && !string.IsNullOrEmpty(card.rewardKey)
                && !string.Equals(result.grantedRewardKey, card.rewardKey, StringComparison.OrdinalIgnoreCase)) continue;
            if (result.grantedAssetId > 0 && card.rewardAssetId > 0 && result.grantedAssetId != card.rewardAssetId) continue;
            if (!string.IsNullOrWhiteSpace(card.rewardQuality)) return card.rewardQuality;
        }
        return null;
    }

    private string OwnedSummonQuality(CardDrawResultResponse result)
    {
        string type = (result.grantedRewardType ?? "").Trim().ToUpperInvariant();
        string key = result.grantedRewardKey;
        long id = result.grantedAssetId;
        if (type == "NPC")
        {
            foreach (var npc in serverPlayerNpcs)
                if (npc != null && (id > 0 && npc.npcId == id || !string.IsNullOrEmpty(key) && key == npc.npcCode))
                    return npc.quality;
            return null;
        }
        if (type != "ITEM" && type != "EQUIPMENT") return null;
        var items = type == "ITEM" ? currentInventoryItems : serverEquipmentWarehouse;
        foreach (var item in items)
        {
            if (item == null) continue;
            string itemKey = type == "ITEM" ? item.itemKey : item.equipmentKey;
            long assetId = type == "ITEM" ? item.itemId : item.equipmentId;
            if (id > 0 && id == assetId || !string.IsNullOrEmpty(key) && key == itemKey) return item.quality;
        }
        return null;
    }

    private static string SummonPoolPath(string code) => "/card-draw/pools/" + Uri.EscapeDataString(code);

    private static bool IsSummonDrawPath(string path)
    {
        if (path == "/card-draw/draw") return true;
        if (string.IsNullOrEmpty(path)) return false;
        string[] parts = path.Split('/');
        return parts.Length == 5 && parts[0] == "" && parts[1] == "card-draw" && parts[2] == "pools"
            && parts[3].Length > 0 && parts[3] != "." && parts[3] != ".." && parts[4] == "draw"
            && path == SummonPoolPath(Uri.UnescapeDataString(parts[3])) + "/draw";
    }

    private static void BookArea(Transform node, float left, float bottom, float right, float top)
    {
        if (!(node is RectTransform rect)) return;
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void BookSkin(Image image, string asset)
    {
        if (image == null) return;
        image.overrideSprite = null;
        image.sprite = Resources.Load<Sprite>("UI/SummonBook/" + asset);
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    private static void BookLabel(TMP_Text label, float size, Color ink)
    {
        if (label == null) return;
        label.color = ink;
        label.outlineWidth = 0f;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(16f, size);
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private Image BookIllustration(Transform parent, string name, string asset, float left, float bottom, float right, float top)
    {
        Transform existing = parent.Find(name);
        Image image = existing != null ? existing.GetComponent<Image>() : CreateImage(name, parent, Color.white);
        BookArea(image.transform, left, bottom, right, top);
        if (asset != null) BookSkin(image, asset);
        image.raycastTarget = false;
        return image;
    }

    private void BookButton(Button button, string asset, float size)
    {
        if (button == null) return;
        ((RectTransform)button.transform).pivot = new Vector2(.5f, .5f);
        BookSkin(button.targetGraphic as Image, asset);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.disabledColor = new Color(.62f,.59f,.58f,.8f);
        button.colors = colors;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        BookArea(label.transform,.07f,.09f,.93f,.91f);
        BookLabel(label, size, SummonInk);
        label.margin = Vector4.zero;
        label.enableWordWrapping = false;
        label.alignment = TextAlignmentOptions.Midline;
    }

    private Button SummonTab(Transform parent, string name, string caption, bool selected, float left, float right)
    {
        Transform found = parent.Find(name);
        Button button = found == null ? CreateButton(parent, name, caption, Color.white, SummonInk) : found.GetComponent<Button>();
        BookArea(button.transform,left,.075f,right,.935f);
        Transform shine = button.transform.Find("ButtonShine");
        if (shine != null) shine.gameObject.SetActive(false);
        BookButton(button, selected ? "SelectedTab" : "Tab", 25);
        return button;
    }

    private void ConfigureSummonCost(Button button, CardDrawOptionResponse option)
    {
        Transform existing = button.transform.Find("Cost");
        if (option == null)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        RectTransform cost = existing as RectTransform;
        if (cost == null)
        {
            cost = new GameObject("Cost", typeof(RectTransform)).GetComponent<RectTransform>();
            cost.SetParent(button.transform, false);
        }
        cost.gameObject.SetActive(true);
        BookArea(cost, .10f, .24f, .90f, .48f);
        TMP_Text caption = button.transform.Find("Text")?.GetComponent<TMP_Text>();
        if (caption != null) BookArea(caption.transform,.09f,.49f,.91f,.78f);
        bool ink = string.Equals(option.costItemKey,"diamond",StringComparison.OrdinalIgnoreCase);
        Sprite sprite = ink ? Resources.Load<Sprite>("IdleAdventureUI/FormationBook/InkIcon")
            : ResolveMailInventorySprite(option.costItemKey) ?? ResolveMailInventorySprite(option.costItemName);
        if (sprite == null && viewingLimitedSummon && !ink)
            sprite = ResolveSummonTicketIcon(option) ?? Resources.Load<Sprite>("UI/SummonBook/Ticket");
        if (sprite == null && currentInventoryItems != null)
        {
            InventoryItemResponse item = Array.Find(currentInventoryItems, entry => entry != null
                && string.Equals(entry.itemKey,option.costItemKey,StringComparison.OrdinalIgnoreCase));
            if (item != null) sprite = ResolveMailInventorySprite(item.icon);
        }
        Image icon = BookIllustration(cost,"Icon",null,0,0,0,0);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.enabled = sprite != null;
        string amount = option.costAmount.ToString("N0");
        string currency = ink ? "彩墨" : option.costItemName;
        TMP_Text label = EnsurePresentationCaption(cost,"Amount",sprite != null ? "×" + amount : currency + " ×" + amount);
        BookLabel(label,22f,SummonInk);
        BookArea(label.transform,0,0,1,1);
        if (sprite == null) return;
        const float iconSize = 30f;
        float amountWidth = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 4f;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f,.5f);
        iconRect.sizeDelta = new Vector2(iconSize,iconSize);
        iconRect.anchoredPosition = new Vector2(-(amountWidth + 4f) * .5f,0);
        label.rectTransform.anchorMin = new Vector2(.5f,0f);
        label.rectTransform.anchorMax = new Vector2(.5f,1f);
        label.rectTransform.offsetMin = new Vector2((iconSize + 4f - amountWidth) * .5f,0f);
        label.rectTransform.offsetMax = new Vector2((iconSize + 4f + amountWidth) * .5f,0f);
    }

    private void ConfigureSummonBook(Transform root, CardDrawCardResponse[] cards, bool busy,
        CardDrawOptionResponse singleCost = null, CardDrawOptionResponse multiCost = null)
    {
        Transform panel = FindSceneTransform(root, "LotteryPanel");
        if (panel == null) return;
        bool limited = HasAuthSession() && viewingLimitedSummon;
        bool locked = busy || serverCardDrawRunning || serverCardDrawPoolRunning || !string.IsNullOrEmpty(pendingCardDrawKey) || cardDrawPersistenceBlocked;
        Image background = FindSceneComponent<Image>(root,"LotteryBackground");
        BookSkin(background,"Backdrop");
        if (background != null) { background.preserveAspect = false; background.transform.SetAsFirstSibling(); }
        foreach (string surface in new[]{"LotteryPanel","LotteryActionBar","LotteryHeader","LotteryPrizeBounds","LotteryPrizeArea"})
        {
            Image image = FindSceneComponent<Image>(root,surface);
            if (image != null) image.color = Color.clear;
        }
        Transform header = FindSceneTransform(root,"LotteryHeader");
        BookArea(header,0f,.885f,1f,1f);
        Button back = FindSceneComponent<Button>(root,"LotteryCloseButton");
        BookArea(back.transform,.018f,.12f,.081f,.88f);
        BookButton(back,"Back",20);
        TMP_Text title = FindSceneComponent<TMP_Text>(root,"LotteryTitle");
        if (title.transform.parent != panel) title.transform.SetParent(panel,false);
        BookArea(title.transform,.156f,.73f,.478f,.856f);
        title.text = "绘本召唤";
        BookLabel(title,46f,new Color32(255,239,195,255));
        title.fontStyle = FontStyles.Bold;
        title.outlineColor = new Color32(65,34,25,255);
        title.outlineWidth = .24f;
        title.alignment = TextAlignmentOptions.Midline;
        title.enabled = !limited;
        BookIllustration(panel,"Limited Title","LimitedTitle",.154f,.727f,.478f,.855f).gameObject.SetActive(limited);
        Button detail = FindSceneComponent<Button>(root,"LotteryPoolButton");
        BookArea(detail.transform,.551f,.245f,.663f,.755f);
        BookButton(detail,"Tab",22);
        Image wallet = FindSceneComponent<Image>(root,"LotteryDiamondBadge");
        ConfigureSummonWallet(wallet,.680f,FindSceneComponent<TMP_Text>(wallet.transform,"LotteryDiamondText"),
            Resources.Load<Sprite>("IdleAdventureUI/FormationBook/InkIcon"),"Ink Icon",locked,() => { HideLotteryPanel(); ShowRechargeContent(); });
        Image tickets = BookIllustration(header,"LotteryTicketBadge",null,0,0,1,1);
        TMP_Text ticketCount = EnsurePresentationCaption(tickets.transform,"LotteryTicketText",SummonTicketCountText());
        ticketCount.text = SummonTicketCountText();
        ConfigureSummonWallet(tickets,.833f,ticketCount,SummonWalletTicketIcon(),
            "Ticket Icon",locked,() => OpenShopCategory(DiamondShopCategory));

        Button normal = SummonTab(header,"Standard Pool Tab","常驻召唤",!limited,.369f,.533f);
        bool hasLimited = !string.IsNullOrWhiteSpace(limitedSummonPoolCode);
        Button eventTab = SummonTab(header,"Limited Pool Tab",hasLimited ? "限定召唤" : "限定未开放",limited,.215f,.367f);
        SetSceneButtonText(eventTab, hasLimited ? "限定召唤" : "限定未开放");
        normal.interactable = limited && !locked;
        eventTab.interactable = HasAuthSession() && !limited && hasLimited && !locked;
        Button selectedTab = limited ? eventTab : normal;
        ColorBlock selectedColors = selectedTab.colors;
        selectedColors.disabledColor = Color.white;
        selectedTab.colors = selectedColors;
        BindSceneButton(normal, () => OpenServerLottery());
        BindSceneButton(eventTab, () => OpenServerLottery(limitedSummonPoolCode,0));

        RectTransform showcaseBounds = SummonLayoutArea(panel,"Featured Bounds",.579f,.012f,.905f,.856f);
        RectTransform showcase = SummonLayoutArea(showcaseBounds,"Featured Display",0,0,1,1);
        FitSummonArt(showcase,.66f);
        Image stage = BookIllustration(showcase,"Featured Stand","Stage",0,0,1,.246f);
        RectTransform standingArea = SummonLayoutArea(showcase,"Standing Area",.014f,.171f,.986f,.988f);
        Image featured = BookIllustration(standingArea,"Featured Reward",null,0,0,1,1);
        CardDrawCardResponse choice = null;
        foreach (CardDrawCardResponse card in cards)
        {
            if (card == null) continue;
            if (pendingCardDrawHighlightNpcId > 0 && card.rewardAssetId == pendingCardDrawHighlightNpcId && card.rewardType == "NPC") { choice = card; break; }
            if (choice == null || (choice.rewardType != "NPC" && card.rewardType == "NPC")) choice = card;
        }
        featured.sprite = choice == null ? null : choice.rewardType == "NPC"
            ? ResolveSummonStandingArt(choice) : ResolveLotteryPrizeSprite(choice);
        featured.enabled = featured.sprite != null;
        if (featured.enabled) FitSummonArt(featured.rectTransform,featured.sprite.rect.width / featured.sprite.rect.height);
        featured.preserveAspect = true;
        stage.enabled = featured.enabled;
        TMP_Text featuredName = EnsurePresentationCaption(showcase,"Featured Name", choice != null ? choice.rewardName : "");
        featuredName.text = choice != null ? choice.rewardName : "";
        Image nameplate = BookIllustration(showcase,"Featured Nameplate","Tab",.342f,.053f,.658f,.112f);
        nameplate.gameObject.SetActive(choice != null);
        BookArea(featuredName.transform,.349f,.055f,.651f,.11f);
        featuredName.transform.SetAsLastSibling();
        BookLabel(featuredName,28,SummonInk);
        featuredName.fontStyle = FontStyles.Bold;
        Transform bounds = FindSceneTransform(root,"LotteryPrizeBounds");
        BookArea(bounds,.157f,.263f,.516f,.713f);
        Transform area = FindSceneTransform(root,"LotteryPrizeArea");
        AspectRatioFitter legacyAspect = area.GetComponent<AspectRatioFitter>();
        if (legacyAspect != null) legacyAspect.enabled = false;
        BookArea(area,0,0,1,1);
        SetSceneObjectActive(root, "LotteryPrizeHint", false);
        TMP_Text railTitle = FindSceneComponent<TMP_Text>(root,"LotteryPrizeTitle");
        railTitle.text = "奖池预览";
        BookArea(railTitle.transform,.70f,.801f,.998f,1f);
        BookLabel(railTitle,30,SummonInk);
        railTitle.fontStyle = FontStyles.Bold;
        RectTransform viewport = FindSceneTransform(root,"LotteryPrizeViewport") as RectTransform;
        BookArea(viewport,.047f,.238f,.975f,.772f);
        RectTransform content = FindSceneTransform(root,"LotteryPrizeContent") as RectTransform;
        LayoutSummonPreviewRail(content, cards);
        BookLabel(FindSceneComponent<TMP_Text>(root,"LotteryPrizeEmptyText"),23,SummonInk);
        ScrollRect scroll = viewport.GetComponentInParent<ScrollRect>();
        if (scroll != null)
        {
            Image track = BookIllustration(area,"Reward Scroll","ScrollTrack",0,.018f,1,.125f);
            track.raycastTarget = true;
            Scrollbar bar = track.GetComponent<Scrollbar>();
            if (bar == null) bar = track.gameObject.AddComponent<Scrollbar>();
            RectTransform slider = track.transform.Find("Slider") as RectTransform;
            if (slider == null)
            {
                slider = new GameObject("Slider", typeof(RectTransform)).GetComponent<RectTransform>();
                slider.SetParent(track.transform,false);
            }
            bar.handleRect = slider;
            Image handle = BookIllustration(slider,"Handle","ScrollThumb",0f,0f,1f,1f);
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = handleRect.anchorMax = handleRect.pivot = new Vector2(.5f,.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(24,24);
            bar.targetGraphic = handle;
            scroll.horizontalScrollbar = bar;
            track.gameObject.SetActive(cards.Length>1);
        }
        Transform actions = FindSceneTransform(root,"LotteryActionBar");
        BookArea(actions,.111f,.063f,.542f,.209f);
        Button single = FindSceneComponent<Button>(root,"LotterySingleButton");
        Button multi = FindSceneComponent<Button>(root,"LotteryTenButton");
        BookArea(single.transform,0,0,.462f,1);
        BookArea(multi.transform,.544f,0,1,1);
        BookButton(single,"Single",26); BookButton(multi,"Multiple",26);
        ConfigureSummonCost(single,singleCost);
        ConfigureSummonCost(multi,multiCost);
        TMP_Text status = FindSceneComponent<TMP_Text>(root,"LotteryStatus");
        BookArea(status.transform,.109f,.210f,.539f,.263f);
        BookLabel(status,21,new Color32(255,244,221,255));
        status.outlineColor = new Color32(67,36,24,255);
        status.outlineWidth = .2f;
        Transform broadcast = FindSceneTransform(root,"LotteryRareBroadcast");
        if (broadcast != null)
        {
            if (broadcast.parent != panel) broadcast.SetParent(panel,false);
            BookArea(broadcast,.181f,.631f,.407f,.713f);
            Image banner = broadcast.GetComponent<Image>() ?? broadcast.gameObject.AddComponent<Image>();
            BookSkin(banner,"Banner");
            banner.raycastTarget = false;
            if (rareCardDrawBroadcastText != null) rareCardDrawBroadcastText.color = SummonInk;
        }
    }

    private static RectTransform SummonLayoutArea(Transform parent,string name,float left,float bottom,float right,float top)
    {
        var area = parent.Find(name) as RectTransform;
        if (area == null)
        {
            area = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(parent,false);
        }
        BookArea(area,left,bottom,right,top);
        return area;
    }

    private static void FitSummonArt(RectTransform area,float ratio)
    {
        area.pivot = new Vector2(.5f,0f);
        var fit = area.GetComponent<AspectRatioFitter>() ?? area.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = ratio;
        LayoutRebuilder.MarkLayoutForRebuild(area);
    }

    private Sprite ResolveSummonStandingArt(CardDrawCardResponse card)
    {
        string key = SummonStandingKey(card.rewardKey) ?? SummonStandingKey(card.icon) ?? SummonStandingKey(card.rewardName);
        if (key == null)
        {
            foreach (var role in roleCatalogEntries)
            {
                if (role == null || !(card.rewardAssetId > 0 && card.rewardAssetId == role.npcId ||
                    !string.IsNullOrEmpty(card.rewardKey) && card.rewardKey == role.npcCode ||
                    !string.IsNullOrEmpty(card.rewardName) && card.rewardName == role.name)) continue;
                key = SummonStandingKey(role.npcCode) ?? SummonStandingKey(role.modelKey) ?? SummonStandingKey(role.name);
                break;
            }
        }
        if (key == "field_steward" && starterGenderSnapshot?.gender == "MALE") key = "field_steward_male";
        return key == null ? null : Resources.Load<Sprite>("UI/SummonBook/Standing/" + key);
    }

    private static string SummonStandingKey(string value)
    {
        switch (NormalizeRoleAssetKey(value).ToLowerInvariant())
        {
            case "npc_1": case "漫画家": case "田园管家": case "field_steward": return "field_steward";
            case "npc_2": case "沧祈": case "workshop_foreman": return "workshop_foreman";
            case "npc_3": case "岑岳": case "master_manager": return "master_manager";
            case "npc_4": case "绮萝": case "chief_manager": return "chief_manager";
            case "npc_5": case "砚衡": case "yanheng": return "yanheng";
            case "npc_6": return "shuoying";
            case "npc_7": return "lingke";
            default: return null;
        }
    }

    private static void LayoutSummonPreviewRail(RectTransform content, CardDrawCardResponse[] cards)
    {
        const int visibleSlots = 3;
        const float gap = 50f;
        int count = 0;
        foreach (var card in cards) if (card != null) count++;
        float pages = Mathf.Max(1, count) / (float)visibleSlots;
        var layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset();
        layout.spacing = gap;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        content.pivot = new Vector2(0f,.5f);
        content.anchorMin = Vector2.zero;
        content.anchorMax = new Vector2(pages,1f);
        content.sizeDelta = new Vector2((pages - 1f) * gap,0f);
        content.anchoredPosition = new Vector2(Mathf.Min(0f,content.anchoredPosition.x),0f);
    }

    private void ConfigureSummonWallet(Image frame, float left, TMP_Text amount, Sprite sprite, string iconName, bool locked, Action open)
    {
        frame.rectTransform.pivot = new Vector2(.5f,.5f);
        BookArea(frame.transform,left,.26f,left + .142f,.74f);
        BookSkin(frame,"Wallet");
        frame.preserveAspect = false;
        frame.raycastTarget = false;
        Image icon = BookIllustration(frame.transform,iconName,null,.052f,.12f,.25f,.88f);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        BookArea(amount.transform,.255f,.10f,.765f,.90f);
        BookLabel(amount,23,Color.white);
        amount.margin = Vector4.zero;
        amount.enableWordWrapping = false;
        amount.alignment = TextAlignmentOptions.Midline;
        Image plus = BookIllustration(frame.transform,"Wallet Plus","Plus",.765f,.09f,.962f,.91f);
        plus.raycastTarget = true;
        Button button = plus.GetComponent<Button>() ?? plus.gameObject.AddComponent<Button>();
        button.targetGraphic = plus;
        button.interactable = HasAuthSession() && !locked;
        BindSceneButton(button,open);
    }

    private string SummonTicketCountText()
    {
        if (!HasAuthSession()) return GetLocalQuantity("LotteryTicket",0).ToString("N0");
        return summonTicketBalance.HasValue ? summonTicketBalance.Value.ToString("N0") : "—";
    }

    private Sprite SummonWalletTicketIcon()
    {
        var options = viewingLimitedSummon ? serverCardDrawPool?.options : null;
        var ticketOption = options == null ? null : Array.Find(options, option => option != null &&
            !string.IsNullOrEmpty(option.costItemKey) && !string.Equals(option.costItemKey,"diamond",StringComparison.OrdinalIgnoreCase));
        return ResolveSummonTicketIcon(ticketOption) ?? Resources.Load<Sprite>("UI/SummonBook/Ticket");
    }

    private Sprite ResolveSummonTicketIcon(CardDrawOptionResponse option)
    {
        if (option == null || string.IsNullOrEmpty(option.costItemKey)
            || string.Equals(option.costItemKey,"diamond",StringComparison.OrdinalIgnoreCase)) return null;
        var items = summonTicketItems ?? currentInventoryItems;
        var ticket = items == null ? null : Array.Find(items, item => item != null &&
            string.Equals(item.itemKey,option.costItemKey,StringComparison.OrdinalIgnoreCase));
        return ResolveMailInventorySprite(ticket?.icon) ?? ResolveMailInventorySprite(option.costItemKey)
            ?? ResolveMailInventorySprite(option.costItemName);
    }

    private void SetSummonTicketBalance(InventoryItemResponse[] items)
    {
        summonTicketItems = items;
        summonTicketBalance = null;
        if (items != null)
        {
            long count = 0;
            foreach (var item in items)
                if (item != null && string.Equals(item.itemType,"CARD_DRAW_TICKET",StringComparison.OrdinalIgnoreCase))
                    count += Math.Max(0,item.quantity);
            summonTicketBalance = count;
        }
        if (lotteryPanelObject != null && lotteryPanelObject.activeInHierarchy)
        {
            SetSceneText(lotteryPanelObject.transform,"LotteryTicketText",SummonTicketCountText());
            Image ticket = FindSceneComponent<Image>(lotteryPanelObject.transform,"Ticket Icon");
            if (ticket != null) ticket.sprite = SummonWalletTicketIcon();
        }
    }

    private Sprite ResolveSummonRoleArt(CardDrawCardResponse card, bool full)
    {
        string id = card.rewardAssetId > 0 ? card.rewardAssetId.ToString() : null;
        Sprite art = ResolveRoleSprite(new[]{card.icon,card.rewardKey,id,card.rewardName},full);
        if (art != null) return art;
        foreach (var role in roleCatalogEntries)
        {
            if (role == null || !(card.rewardAssetId > 0 && role.npcId == card.rewardAssetId || !string.IsNullOrEmpty(card.rewardName) && role.name == card.rewardName
                || !string.IsNullOrEmpty(card.rewardKey) && role.npcCode == card.rewardKey)) continue;
            art = ResolveRoleSprite(new[]{role.modelKey,role.icon,role.npcId.ToString()},full);
            if (art != null) return art;
        }
        return null;
    }

    private static string SummonFrame(string quality)
    {
        switch (FormatRoleQualityForDisplay(quality))
        {
            case "传说": return "PrizeGold";
            case "史诗": return "PrizeCard";
            case "稀有": return "PrizeBlue";
            case "优秀": return "PrizeGreen";
            default: return "PrizePaper";
        }
    }

    private static void StyleSummonPaper(Transform overlay, string cardName)
    {
        Image shade = overlay.GetComponent<Image>();
        shade.color = new Color32(40, 27, 36, 173);
        shade.raycastTarget = true;
        Image sheet = overlay.Find(cardName).GetComponent<Image>();
        BookArea(sheet.transform, .105f, .035f, .895f, .965f);
        sheet.overrideSprite = null;
        sheet.sprite = Resources.Load<Sprite>("UI/ApprovedUi/Panels/PaperPanel");
        sheet.color = Color.white;
        sheet.type = Image.Type.Simple;
        sheet.preserveAspect = false;
        foreach (string name in new[] { "FrameTop", "FrameBottom", "FrameLeft", "FrameRight" })
            SetSceneObjectActive(sheet.transform, name, false);
    }

    private static TMP_Text SummonText(Transform root, string name, float size, float left, float bottom,
        float right, float top, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        TMP_Text label = FindSceneComponent<TMP_Text>(root, name);
        BookArea(label.transform, left, bottom, right, top);
        BookLabel(label, size, SummonInk);
        label.alignment = alignment;
        label.margin = Vector4.zero;
        return label;
    }

    private void StyleSummonPoolList(Transform overlay, string poolName)
    {
        StyleSummonPaper(overlay, "LotteryPoolCard");
        Transform page = overlay.Find("LotteryPoolCard");
        TMP_Text title = SummonText(page,"LotteryPoolTitle",36,.10f,.775f,.70f,.885f,TextAlignmentOptions.MidlineLeft);
        title.text = string.IsNullOrWhiteSpace(poolName) ? "奖池详情" : poolName;
        title.fontStyle = FontStyles.Bold;
        Button close = FindSceneComponent<Button>(page,"LotteryPoolCloseButton");
        BookArea(close.transform,.763f,.795f,.877f,.867f);
        BookButton(close,"Tab",22);
        Image headings = BookIllustration(page,"Pool Columns",null,.09f,.675f,.91f,.745f);
        headings.color = Color.clear;
        TMP_Text note = FindSceneComponent<TMP_Text>(page,"LotteryPoolNote");
        note.transform.SetParent(headings.transform,false);
        SummonText(headings.transform,"LotteryPoolNote",22,.085f,0,.585f,1,TextAlignmentOptions.MidlineLeft);
        note.text = "奖励";
        EnsurePresentationCaption(headings.transform,"Count Column","数量");
        EnsurePresentationCaption(headings.transform,"Odds Column","获得概率");
        SummonText(headings.transform,"Count Column",22,.60f,0,.755f,1,TextAlignmentOptions.MidlineRight);
        SummonText(headings.transform,"Odds Column",22,.76f,0,.9634f,1,TextAlignmentOptions.MidlineRight);
        ScrollRect scroll = FindSceneComponent<ScrollRect>(page,"LotteryPoolViewport");
        BookArea(scroll.viewport,.09f,.12f,.91f,.67f);
        scroll.viewport.GetComponent<Image>().color = Color.clear;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = scroll.content;
        content.anchorMin = new Vector2(0,1);
        content.anchorMax = content.pivot = Vector2.one;
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0,content.sizeDelta.y);
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset();
        layout.spacing = 4;
        layout.childControlWidth = layout.childForceExpandWidth = true;
    }

    private void StyleSummonPoolRow(Transform row, CardDrawCardResponse card, int index)
    {
        row.GetComponent<Image>().color = new Color32(145,98,68,(byte)(index % 2 == 0 ? 32 : 12));
        SetSceneObjectActive(row,"LotteryPoolRowSlot",false);
        Image art = BookIllustration(row,"Reward Art",null,.008f,.10f,.078f,.90f);
        art.sprite = ResolveLotteryPrizeSprite(card);
        art.enabled = art.sprite != null;
        art.preserveAspect = true;
        TMP_Text name = SummonText(row,"LotteryPoolRowName",24,.085f,.08f,.585f,.92f,TextAlignmentOptions.MidlineLeft);
        name.text = string.IsNullOrWhiteSpace(card.rewardName) ? GetRewardCategoryLabel(card.rewardType) : card.rewardName;
        SummonText(row,"LotteryPoolRowAmount",22,.60f,.08f,.755f,.92f,TextAlignmentOptions.MidlineRight).text =
            "×" + card.rewardAmount.ToString("N0");
        float chance = card.probability * 100f;
        SummonText(row,"LotteryPoolRowProbability",23,.76f,.08f,.9634f,.92f,TextAlignmentOptions.MidlineRight).text =
            chance > 0 && chance < .0001f ? "<0.0001%" : chance.ToString("0.####") + "%";
    }

    private Transform PrepareSummonRewards(Transform overlay, Transform original, int count)
    {
        StyleSummonPaper(overlay,"LotteryRewardConfirmCard");
        Transform page = overlay.Find("LotteryRewardConfirmCard");
        SummonText(page,"LotteryRewardConfirmTitle",40,.16f,.77f,.84f,.89f).fontStyle = FontStyles.Bold;
        SetSceneObjectActive(page,"LotteryRewardConfirmSummary",false);
        Button confirm = FindSceneComponent<Button>(page,"LotteryRewardConfirmButton");
        BookArea(confirm.transform,.417f,.082f,.583f,.173f);
        BookButton(confirm,"Single",24);
        ScrollRect scroll = FindSceneComponent<ScrollRect>(page,"LotteryRewardViewport");
        BookArea(scroll.viewport,.075f,.196f,.925f,count <= 5 ? .69f : .755f);
        scroll.viewport.GetComponent<Image>().color = Color.clear;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        original.gameObject.SetActive(false);
        RectTransform grid = scroll.viewport.Find("Reward Cards") as RectTransform;
        if (grid == null)
        {
            grid = new GameObject("Reward Cards", typeof(RectTransform), typeof(SummonRewardGrid), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            grid.gameObject.layer = page.gameObject.layer;
            grid.SetParent(scroll.viewport,false);
            grid.anchorMin = new Vector2(0,1);
            grid.anchorMax = new Vector2(1,1);
            grid.pivot = new Vector2(.5f,1);
            grid.sizeDelta = grid.anchoredPosition = Vector2.zero;
            var layout = grid.GetComponent<SummonRewardGrid>();
            layout.spacing = new Vector2(10,8);
            layout.childAlignment = TextAnchor.UpperCenter;
            grid.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        scroll.content = grid;
        return grid;
    }

    private static void ResetSummonScroll(ScrollRect scroll)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void StyleSummonResult(Transform row, CardDrawResultResponse result, Image icon)
    {
        string quality = result.grantedRewardQuality;
        if (string.IsNullOrWhiteSpace(quality) && !result.duplicateNpcConverted)
            quality = string.IsNullOrWhiteSpace(result.rewardQuality) ? result.configuredRewardQuality : result.rewardQuality;
        Image frame = row.GetComponent<Image>();
        BookSkin(frame,SummonFrame(quality));
        frame.preserveAspect = false;
        BookArea(icon.transform,.20f,.43f,.80f,.78f);
        TMP_Text fallback = SummonText(row,"LotteryRewardRowSequence",32,.21f,.43f,.79f,.78f);
        fallback.text = GetLotteryIconLabel(GetCardDrawResultName(result),result.grantedRewardType);
        fallback.gameObject.SetActive(!icon.gameObject.activeSelf);
        TMP_Text name = SummonText(row,"LotteryRewardRowName",25,.10f,.245f,.90f,.43f);
        name.fontStyle = FontStyles.Bold;
        SummonText(row,"LotteryRewardRowAmount",22,.08f,.10f,.92f,.245f);
        TMP_Text note = SummonText(row,"LotteryRewardRowNote",18,.09f,.765f,.91f,.90f);
        if (!result.duplicateNpcConverted && !string.IsNullOrWhiteSpace(quality))
            note.text = FormatRoleQualityForDisplay(quality) + "·" + note.text;
    }

    private void ConfigureSummonCard(Transform row, CardDrawCardResponse card)
    {
        Image frame = row.GetComponent<Image>();
        BookSkin(frame,SummonFrame(card.rewardQuality));
        if (frame != null) frame.preserveAspect = false;
        Transform icon = FindSceneTransform(row,"LotteryPrizeIcon");
        Image well = icon.GetComponent<Image>();
        if (well != null) well.color = Color.clear;
        BookArea(icon,.20f,.45f,.80f,.82f);
        Transform reward = FindSceneTransform(row,"LotteryPrizeRewardImage");
        BookArea(reward,.04f,.04f,.96f,.96f);
        if (reward != null) reward.GetComponent<Image>().preserveAspect = true;
        TMP_Text name = FindSceneComponent<TMP_Text>(row,"LotteryPrizeCardName");
        TMP_Text amount = FindSceneComponent<TMP_Text>(row,"LotteryPrizeCardAmount");
        TMP_Text odds = FindSceneComponent<TMP_Text>(row,"LotteryPrizeCardProbability");
        BookArea(name.transform,.08f,.265f,.92f,.46f);
        BookArea(amount.transform,.07f,.13f,.93f,.27f);
        odds.gameObject.SetActive(false);
        BookLabel(name,24,SummonInk);
        name.fontStyle = FontStyles.Bold;
        BookLabel(amount,20,SummonInk);
        if (!string.IsNullOrEmpty(card.rewardQuality))
        {
            Image plate = BookIllustration(row,"Reward Quality","Tab",.16f,.80f,.84f,.96f);
            TMP_Text quality = EnsurePresentationCaption(plate.transform,"Quality Label",FormatRoleQualityForDisplay(card.rewardQuality));
            BookArea(quality.transform,.08f,.08f,.92f,.92f);
            BookLabel(quality,16,SummonInk);
        }
    }
}
