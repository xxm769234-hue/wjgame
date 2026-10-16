using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WeChatPlatformBridge
{
    public static bool IsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static void RequestLogin(MonoBehaviour receiver)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatLogin(receiver.gameObject.name);
#else
        receiver.SendMessage(
            "OnWeChatLoginFailed",
            "微信登录只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void RequestPrivacyAuthorization(MonoBehaviour receiver)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatRequestPrivacyAuthorization(receiver.gameObject.name);
#else
        receiver.SendMessage(
            "OnWeChatPrivacyAuthorized",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void OpenPrivacyContract(MonoBehaviour receiver)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatOpenPrivacyContract(receiver.gameObject.name);
#else
        receiver.SendMessage(
            "OnWeChatPrivacyContractOpened",
            string.Empty,
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void RequestPayment(MonoBehaviour receiver, string paymentParametersJson)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatRequestPayment(receiver.gameObject.name, paymentParametersJson ?? "{}");
#else
        receiver.SendMessage(
            "OnWeChatPaymentFailed",
            "微信支付只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void RequestRewardedVideo(MonoBehaviour receiver, string watchId, string adUnitId)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatShowRewardedVideo(receiver.gameObject.name, watchId ?? string.Empty, adUnitId ?? string.Empty);
#else
        receiver.SendMessage(
            "OnWeChatRewardedVideoFailed",
            "激励视频只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void RequestSubscribeMessages(MonoBehaviour receiver, string requestJson)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatRequestSubscribeMessages(receiver.gameObject.name, requestJson ?? "{}");
#else
        receiver.SendMessage(
            "OnWeChatSubscribeFailed",
            "微信订阅消息只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void RequestAvatarSelection(MonoBehaviour receiver)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatChooseAvatar(receiver.gameObject.name);
#else
        receiver.SendMessage(
            "OnAvatarSelectionFailed",
            "系统图片选择只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void SyncFriendLevel(int level)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatSyncFriendLevel(Mathf.Max(0, level));
#endif
    }

    public static void GetFriendCloudStorage(MonoBehaviour receiver, string[] keyList)
    {
        if (receiver == null)
        {
            return;
        }

        string requestJson = JsonUtility.ToJson(new FriendCloudStorageRequest
        {
            keyList = keyList ?? new string[0]
        });

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatGetFriendCloudStorage(receiver.gameObject.name, requestJson);
#else
        SendBridgeFailure(
            receiver,
            "OnWeChatFriendCloudStorageFailed",
            "微信好友云存储只能在微信开放数据域中调用。");
#endif
    }

    public static void GetFriendSendGiftStatus(
        MonoBehaviour receiver,
        string giftId,
        string[] openIdList)
    {
        if (receiver == null)
        {
            return;
        }

        string requestJson = JsonUtility.ToJson(new FriendSendGiftStatusRequest
        {
            giftId = giftId ?? string.Empty,
            openidList = openIdList ?? new string[0]
        });

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatGetFriendSendGiftStatus(receiver.gameObject.name, requestJson);
#else
        SendBridgeFailure(
            receiver,
            "OnWeChatFriendSendGiftStatusFailed",
            "微信好友送礼状态只能在微信开放数据域中调用。");
#endif
    }

    public static void GetFriendSendGiftStatus(
        MonoBehaviour receiver,
        string giftId,
        string openId)
    {
        GetFriendSendGiftStatus(
            receiver,
            giftId,
            string.IsNullOrEmpty(openId) ? new string[0] : new[] { openId });
    }

    public static void SendGiftToFriend(
        MonoBehaviour receiver,
        string giftId,
        string openId)
    {
        if (receiver == null)
        {
            return;
        }

        string requestJson = JsonUtility.ToJson(new SendGiftToFriendRequest
        {
            giftId = giftId ?? string.Empty,
            openid = openId ?? string.Empty
        });

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatSendGiftToFriend(receiver.gameObject.name, requestJson);
#else
        SendBridgeFailure(
            receiver,
            "OnWeChatSendGiftToFriendFailed",
            "微信好友赠礼只能在微信开放数据域中调用。");
#endif
    }

    public static void PostMessageToOpenDataContext(MonoBehaviour receiver, string messageJson)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatPostMessageToOpenDataContext(
            receiver.gameObject.name,
            messageJson ?? string.Empty);
#else
        SendBridgeFailure(
            receiver,
            "OnWeChatOpenDataMessageFailed",
            "微信开放数据域消息只能在微信小游戏运行环境中发送。");
#endif
    }

    public static void PostOpenDataContextMessage(MonoBehaviour receiver, string messageJson)
    {
        PostMessageToOpenDataContext(receiver, messageJson);
    }

    public static void PostFriendGiftRenderMessage(MonoBehaviour receiver, string giftId)
    {
        PostMessageToOpenDataContext(
            receiver,
            JsonUtility.ToJson(new FriendGiftRenderMessage
            {
                type = "WECHAT_FRIEND_GIFT_RENDER",
                giftId = giftId ?? string.Empty
            }));
    }

    public static void OpenFriendGiftPage(MonoBehaviour receiver, string openlink, string minimumBaseLibraryVersion)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatOpenFriendGiftPage(
            receiver.gameObject.name,
            openlink ?? string.Empty,
            minimumBaseLibraryVersion ?? string.Empty);
#else
        receiver.SendMessage(
            "OnWeChatFriendGiftPageFailed",
            "微信好友赠礼只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    public static void DestroyFriendGiftPage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatDestroyFriendGiftPage();
#endif
    }

    public static void OpenCustomerServiceConversation(
        MonoBehaviour receiver,
        string sessionFrom,
        bool showMessageCard,
        string sendMessageTitle,
        string sendMessagePath,
        string sendMessageImg,
        string minimumBaseLibraryVersion)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatOpenCustomerServiceConversation(
            receiver.gameObject.name,
            sessionFrom ?? string.Empty,
            showMessageCard ? 1 : 0,
            sendMessageTitle ?? string.Empty,
            sendMessagePath ?? string.Empty,
            sendMessageImg ?? string.Empty,
            minimumBaseLibraryVersion ?? string.Empty);
#else
        receiver.SendMessage(
            "OnWeChatCustomerServiceFailed",
            "客服会话只能在微信小游戏运行环境中调用。",
            SendMessageOptions.DontRequireReceiver);
#endif
    }

    private static void SendBridgeFailure(MonoBehaviour receiver, string method, string message)
    {
        receiver.SendMessage(method, message ?? string.Empty, SendMessageOptions.DontRequireReceiver);
    }

    [Serializable]
    private sealed class FriendCloudStorageRequest
    {
        public string[] keyList;
    }

    [Serializable]
    private sealed class FriendSendGiftStatusRequest
    {
        public string giftId;
        public string[] openidList;
    }

    [Serializable]
    private sealed class SendGiftToFriendRequest
    {
        public string giftId;
        public string openid;
    }

    [Serializable]
    private sealed class FriendGiftRenderMessage
    {
        public string type;
        public string giftId;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_WeChatLogin(string receiverName);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatRequestPrivacyAuthorization(string receiverName);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatOpenPrivacyContract(string receiverName);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatRequestPayment(string receiverName, string paymentParametersJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatShowRewardedVideo(string receiverName, string watchId, string adUnitId);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatRequestSubscribeMessages(string receiverName, string requestJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatChooseAvatar(string receiverName);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatSyncFriendLevel(int level);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatGetFriendCloudStorage(
        string receiverName,
        string requestJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatGetFriendSendGiftStatus(
        string receiverName,
        string requestJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatSendGiftToFriend(
        string receiverName,
        string requestJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatPostMessageToOpenDataContext(
        string receiverName,
        string messageJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatOpenFriendGiftPage(
        string receiverName,
        string openlink,
        string minimumBaseLibraryVersion);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatDestroyFriendGiftPage();

    [DllImport("__Internal")]
    private static extern void WJ_WeChatOpenCustomerServiceConversation(
        string receiverName,
        string sessionFrom,
        int showMessageCard,
        string sendMessageTitle,
        string sendMessagePath,
        string sendMessageImg,
        string minimumBaseLibraryVersion);
#endif
}
