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

    public static void SyncFriendLevel(int level)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatSyncFriendLevel(Mathf.Max(0, level));
#endif
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

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_WeChatLogin(string receiverName);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatRequestPayment(string receiverName, string paymentParametersJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatShowRewardedVideo(string receiverName, string watchId, string adUnitId);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatRequestSubscribeMessages(string receiverName, string requestJson);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatSyncFriendLevel(int level);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatOpenFriendGiftPage(
        string receiverName,
        string openlink,
        string minimumBaseLibraryVersion);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatDestroyFriendGiftPage();
#endif
}
