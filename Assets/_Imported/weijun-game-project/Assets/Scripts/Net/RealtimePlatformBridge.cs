using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

public static class RealtimePlatformBridge
{
    public static bool IsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#elif UNITY_EDITOR || UNITY_STANDALONE
            return true;
#else
            return false;
#endif
        }
    }

    public static void Connect(MonoBehaviour receiver, string url, string authorization)
    {
        if (receiver == null || string.IsNullOrEmpty(url) || string.IsNullOrEmpty(authorization))
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_RealtimeConnect(receiver.gameObject.name, url, authorization);
#elif UNITY_EDITOR || UNITY_STANDALONE
        ConnectDesktop(receiver, url, authorization);
#endif
    }

    public static void Send(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_RealtimeSend(message ?? string.Empty);
#elif UNITY_EDITOR || UNITY_STANDALONE
        SendDesktop(message ?? string.Empty);
#endif
    }

    public static void Close()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_RealtimeClose();
#elif UNITY_EDITOR || UNITY_STANDALONE
        CloseDesktop();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_RealtimeConnect(string receiverName, string url, string authorization);

    [DllImport("__Internal")]
    private static extern void WJ_RealtimeSend(string message);

    [DllImport("__Internal")]
    private static extern void WJ_RealtimeClose();
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
    private const int MaxClientMessageBytes = 8192;
    private const int MaxServerMessageBytes = 65536;
    private static readonly object DesktopSocketLock = new object();
    private static readonly SemaphoreSlim DesktopSendLock = new SemaphoreSlim(1, 1);
    private static ClientWebSocket desktopSocket;
    private static CancellationTokenSource desktopCancellation;
    private static int desktopGeneration;

    private static void ConnectDesktop(MonoBehaviour receiver, string url, string authorization)
    {
        SynchronizationContext context = SynchronizationContext.Current;
        if (context == null)
        {
            Debug.LogWarning("Realtime WebSocket requires the Unity synchronization context.");
            return;
        }

        CloseDesktop();

        ClientWebSocket socket = new ClientWebSocket();
        CancellationTokenSource cancellation = new CancellationTokenSource();
        int generation;
        lock (DesktopSocketLock)
        {
            generation = ++desktopGeneration;
            desktopSocket = socket;
            desktopCancellation = cancellation;
        }

        socket.Options.SetRequestHeader("Authorization", authorization);
        _ = RunDesktopSocketAsync(receiver, context, socket, cancellation, generation, url);
    }

    private static async Task RunDesktopSocketAsync(
        MonoBehaviour receiver,
        SynchronizationContext context,
        ClientWebSocket socket,
        CancellationTokenSource cancellation,
        int generation,
        string url)
    {
        bool notifyClosed = false;
        try
        {
            await socket.ConnectAsync(new Uri(url), cancellation.Token);
            if (!IsCurrentDesktopGeneration(generation))
            {
                return;
            }

            NotifyDesktop(receiver, context, generation, "OnRealtimeOpened", "connected");
            await ReceiveDesktopMessagesAsync(receiver, context, socket, cancellation.Token, generation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrentDesktopGeneration(generation))
            {
                NotifyDesktop(receiver, context, generation, "OnRealtimeError", exception.Message);
            }
        }
        finally
        {
            lock (DesktopSocketLock)
            {
                if (desktopGeneration == generation && ReferenceEquals(desktopSocket, socket))
                {
                    desktopSocket = null;
                    desktopCancellation = null;
                    notifyClosed = true;
                }
            }

            socket.Dispose();
            cancellation.Dispose();
            if (notifyClosed)
            {
                NotifyDesktop(receiver, context, generation, "OnRealtimeClosed", "closed");
            }
        }
    }

    private static async Task ReceiveDesktopMessagesAsync(
        MonoBehaviour receiver,
        SynchronizationContext context,
        ClientWebSocket socket,
        CancellationToken cancellation,
        int generation)
    {
        byte[] buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && IsCurrentDesktopGeneration(generation))
        {
            using (MemoryStream messageStream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                WebSocketMessageType messageType = WebSocketMessageType.Binary;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
                        return;
                    }

                    messageType = result.MessageType;
                    messageStream.Write(buffer, 0, result.Count);
                    if (messageStream.Length > MaxServerMessageBytes)
                    {
                        throw new InvalidOperationException("Realtime server message exceeds the supported size.");
                    }
                }
                while (!result.EndOfMessage);

                if (messageType != WebSocketMessageType.Text || !IsCurrentDesktopGeneration(generation))
                {
                    continue;
                }

                string message = Encoding.UTF8.GetString(messageStream.ToArray());
                NotifyDesktop(receiver, context, generation, "OnRealtimeMessage", message);
            }
        }
    }

    private static void SendDesktop(string message)
    {
        if (Encoding.UTF8.GetByteCount(message) > MaxClientMessageBytes)
        {
            Debug.LogWarning("Realtime client message exceeds the protocol limit.");
            return;
        }

        ClientWebSocket socket;
        int generation;
        lock (DesktopSocketLock)
        {
            socket = desktopSocket;
            generation = desktopGeneration;
        }

        if (socket == null || socket.State != WebSocketState.Open)
        {
            return;
        }

        _ = SendDesktopMessageAsync(socket, message, generation);
    }

    private static async Task SendDesktopMessageAsync(ClientWebSocket socket, string message, int generation)
    {
        bool lockTaken = false;
        try
        {
            await DesktopSendLock.WaitAsync();
            lockTaken = true;
            if (!IsCurrentDesktopSocket(socket, generation) || socket.State != WebSocketState.Open)
            {
                return;
            }

            byte[] payload = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(
                new ArraySegment<byte>(payload),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Realtime WebSocket send failed: " + exception.Message);
        }
        finally
        {
            if (lockTaken)
            {
                DesktopSendLock.Release();
            }
        }
    }

    private static void CloseDesktop()
    {
        ClientWebSocket socket;
        CancellationTokenSource cancellation;
        lock (DesktopSocketLock)
        {
            desktopGeneration++;
            socket = desktopSocket;
            cancellation = desktopCancellation;
            desktopSocket = null;
            desktopCancellation = null;
        }

        if (cancellation != null)
        {
            cancellation.Cancel();
        }

        if (socket != null)
        {
            socket.Abort();
        }
    }

    private static bool IsCurrentDesktopGeneration(int generation)
    {
        lock (DesktopSocketLock)
        {
            return desktopGeneration == generation;
        }
    }

    private static bool IsCurrentDesktopSocket(ClientWebSocket socket, int generation)
    {
        lock (DesktopSocketLock)
        {
            return desktopGeneration == generation && ReferenceEquals(desktopSocket, socket);
        }
    }

    private static void NotifyDesktop(
        MonoBehaviour receiver,
        SynchronizationContext context,
        int generation,
        string method,
        string message)
    {
        context.Post(_ =>
        {
            if (!IsCurrentDesktopGeneration(generation) || receiver == null)
            {
                return;
            }

            receiver.SendMessage(method, message ?? string.Empty, SendMessageOptions.DontRequireReceiver);
        }, null);
    }
#endif
}
