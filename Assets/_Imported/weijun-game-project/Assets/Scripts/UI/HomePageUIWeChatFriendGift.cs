using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private bool weChatFriendGiftCatalogRunning;
    private bool weChatFriendGiftCatalogLoaded;
    private WeChatFriendGiftCatalog currentWeChatFriendGiftCatalog;
    private WeChatFriendGiftPackage currentWeChatFriendGiftPackage;

    private void ResetWeChatFriendGiftCatalog()
    {
        weChatFriendGiftCatalogRunning = false;
        weChatFriendGiftCatalogLoaded = false;
        currentWeChatFriendGiftCatalog = null;
        currentWeChatFriendGiftPackage = null;
    }

    private void ConfigureWeChatFriendGiftButton(Transform root)
    {
        // The project only exposes game-friend stamina gifts.
        SetSceneObjectActive(root, "FriendsWeChatGiftButton", false);
    }

    private void BeginWeChatFriendGift()
    {
        if (weChatFriendGiftCatalogRunning || !HasAuthSession())
        {
            return;
        }

        if (currentWeChatFriendGiftPackage != null && currentWeChatFriendGiftCatalog != null)
        {
            OpenWeChatFriendGiftPage();
            return;
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        StartCoroutine(GetWeChatFriendGiftCatalog(panel != null ? panel.PanelRoot : null));
    }

    private IEnumerator GetWeChatFriendGiftCatalog(Transform root)
    {
        weChatFriendGiftCatalogRunning = true;
        ConfigureWeChatFriendGiftButton(root);
        SetSceneText(root, "FriendsStatus", "正在读取微信官方好友赠礼配置...");

        UnityWebRequest request = CreateAuthorizedGet("/wechat/friend-gifts/catalog");
        if (request == null)
        {
            weChatFriendGiftCatalogRunning = false;
            ConfigureWeChatFriendGiftButton(root);
            SetSceneText(root, "FriendsStatus", requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                weChatFriendGiftCatalogRunning = false;
                yield break;
            }

            WeChatFriendGiftCatalogApiResponse response = ParseJson<WeChatFriendGiftCatalogApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                weChatFriendGiftCatalogRunning = false;
                ConfigureWeChatFriendGiftButton(root);
                SetSceneText(
                    root,
                    "FriendsStatus",
                    "微信好友赠礼配置读取失败：" + (response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                        : GetApiError(response)));
                yield break;
            }

            currentWeChatFriendGiftCatalog = response.data;
            currentWeChatFriendGiftPackage = response.data.giftPackages != null && response.data.giftPackages.Length > 0
                ? response.data.giftPackages[0]
                : null;
            weChatFriendGiftCatalogLoaded = true;
        }

        weChatFriendGiftCatalogRunning = false;
        ConfigureWeChatFriendGiftButton(root);
        if (currentWeChatFriendGiftPackage == null)
        {
            SetSceneText(root, "FriendsStatus", "微信官方好友赠礼当前未开放");
            yield break;
        }

        OpenWeChatFriendGiftPage();
    }

    private void OpenWeChatFriendGiftPage()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        Transform root = panel != null ? panel.PanelRoot : null;
        if (currentWeChatFriendGiftCatalog == null
            || currentWeChatFriendGiftPackage == null
            || string.IsNullOrEmpty(currentWeChatFriendGiftCatalog.pageManagerOpenlink))
        {
            SetSceneText(root, "FriendsStatus", "服务器未返回可用的微信好友赠礼页面");
            return;
        }

        weChatFriendGiftCatalogRunning = true;
        ConfigureWeChatFriendGiftButton(root);
        SetSceneText(root, "FriendsStatus", $"正在打开微信好友赠礼：{currentWeChatFriendGiftPackage.giftName}");
        WeChatPlatformBridge.SyncFriendLevel(PlayerPrefs.GetInt("PlayerLv", 1));
        WeChatPlatformBridge.OpenFriendGiftPage(
            this,
            currentWeChatFriendGiftCatalog.pageManagerOpenlink,
            currentWeChatFriendGiftCatalog.minimumBaseLibraryVersion);
    }

    public void OnWeChatFriendGiftPageOpened(string message)
    {
        weChatFriendGiftCatalogRunning = false;
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        Transform root = panel != null ? panel.PanelRoot : null;
        ConfigureWeChatFriendGiftButton(root);
        SetSceneText(root, "FriendsStatus", "微信好友赠礼页面已打开");
        SetStatus("微信好友赠礼页面已打开");
    }

    public void OnWeChatFriendGiftPageFailed(string message)
    {
        weChatFriendGiftCatalogRunning = false;
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        Transform root = panel != null ? panel.PanelRoot : null;
        ConfigureWeChatFriendGiftButton(root);
        string failure = string.IsNullOrEmpty(message) ? "微信好友赠礼页面打开失败" : message;
        SetSceneText(root, "FriendsStatus", "微信好友赠礼不可用：" + failure);
        SetStatus("微信好友赠礼不可用：" + failure);
    }

#pragma warning disable 0649
    [Serializable]
    private class WeChatFriendGiftCatalogApiResponse : BasicApiResponse
    {
        public WeChatFriendGiftCatalog data;
    }

    [Serializable]
    private class WeChatFriendGiftCatalog
    {
        public string provider;
        public string friendListSource;
        public string minimumBaseLibraryVersion;
        public string pageManagerOpenlink;
        public int giftValidityHours;
        public WeChatFriendGiftPackage[] giftPackages;
    }

    [Serializable]
    private class WeChatFriendGiftPackage
    {
        public string giftId;
        public string giftName;
        public int dailyReceiveLimit;
        public long configVersion;
        public WeChatFriendGiftReward[] rewards;
    }

    [Serializable]
    private class WeChatFriendGiftReward
    {
        public long itemId;
        public string itemKey;
        public long quantity;
    }
#pragma warning restore 0649
}
