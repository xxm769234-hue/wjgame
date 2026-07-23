using System.Runtime.InteropServices;
using UnityEngine;

public static class RealtimePlatformBridge
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

    public static void Connect(MonoBehaviour receiver, string url)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (receiver != null && !string.IsNullOrEmpty(url))
        {
            WJ_RealtimeConnect(receiver.gameObject.name, url);
        }
#endif
    }

    public static void Send(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_RealtimeSend(message ?? string.Empty);
#endif
    }

    public static void Close()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_RealtimeClose();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_RealtimeConnect(string receiverName, string url);

    [DllImport("__Internal")]
    private static extern void WJ_RealtimeSend(string message);

    [DllImport("__Internal")]
    private static extern void WJ_RealtimeClose();
#endif
}
