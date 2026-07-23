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

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_WeChatLogin(string receiverName);

    [DllImport("__Internal")]
    private static extern void WJ_WeChatRequestPayment(string receiverName, string paymentParametersJson);
#endif
}
