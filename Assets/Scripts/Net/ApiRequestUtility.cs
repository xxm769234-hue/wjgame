using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class ApiRequestUtility
{
    public const int DefaultTimeoutSeconds = 10;

    public static bool TryCreateGet(string path, bool authorized, out UnityWebRequest request, out string error)
    {
        request = null;
        if (!ApiRuntimeConfig.TryBuildUrl(path, out string url, out error))
        {
            return false;
        }

        request = UnityWebRequest.Get(url);
        Configure(request, authorized);
        return true;
    }

    public static bool TryCreateJsonRequest(
        string path,
        string method,
        string json,
        bool authorized,
        out UnityWebRequest request,
        out string error)
    {
        request = null;
        if (!ApiRuntimeConfig.TryBuildUrl(path, out string url, out error))
        {
            return false;
        }

        request = new UnityWebRequest(url, method)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json ?? string.Empty)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        Configure(request, authorized);
        return true;
    }

    public static bool IsUnauthorized(UnityWebRequest request)
    {
        if (request == null)
        {
            return false;
        }

        if (request.responseCode == 401)
        {
            return true;
        }

        ApiEnvelope envelope = ParseEnvelope(request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
        return envelope != null && envelope.code == 401;
    }

    public static string GetErrorMessage(UnityWebRequest request, string fallback)
    {
        if (request == null)
        {
            return string.IsNullOrEmpty(fallback) ? "请求未创建。" : fallback;
        }

        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        ApiEnvelope envelope = ParseEnvelope(body);
        if (envelope != null && !string.IsNullOrWhiteSpace(envelope.message))
        {
            return envelope.message.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.error))
        {
            return request.error;
        }

        if (request.responseCode >= 400)
        {
            return $"服务器返回 HTTP {request.responseCode}";
        }

        return string.IsNullOrEmpty(fallback) ? "请求失败。" : fallback;
    }

    private static void Configure(UnityWebRequest request, bool authorized)
    {
        request.downloadHandler = request.downloadHandler ?? new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
        request.timeout = DefaultTimeoutSeconds;
        if (authorized)
        {
            request.SetRequestHeader("Authorization", "Bearer " + GameSession.Token);
        }
    }

    private static ApiEnvelope ParseEnvelope(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<ApiEnvelope>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #pragma warning disable 0649
    [Serializable]
    private class ApiEnvelope
    {
        public int code;
        public string message;
    }
    #pragma warning restore 0649
}
