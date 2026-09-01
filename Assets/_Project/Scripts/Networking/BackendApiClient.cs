using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class BackendApiClient
{
    public static IEnumerator Get<T>(string baseUrl, string token, string path, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbGET, path, null, null, onComplete);
    }

    public static IEnumerator Post<T>(string baseUrl, string token, string path, object body, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbPOST, path, body, Guid.NewGuid().ToString("D"), onComplete);
    }

    public static IEnumerator PostWithoutIdempotencyKey<T>(
        string baseUrl,
        string token,
        string path,
        object body,
        Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbPOST, path, body, null, onComplete);
    }

    public static IEnumerator Post<T>(string baseUrl, string token, string path, object body, string idempotencyKey, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbPOST, path, body, idempotencyKey, onComplete);
    }

    public static IEnumerator Put<T>(string baseUrl, string token, string path, object body, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbPUT, path, body, Guid.NewGuid().ToString("D"), onComplete);
    }

    public static IEnumerator Put<T>(string baseUrl, string token, string path, object body, string idempotencyKey, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbPUT, path, body, idempotencyKey, onComplete);
    }

    private static IEnumerator Send<T>(string baseUrl, string token, string method, string path, object body, string idempotencyKey, Action<BackendResult<T>> onComplete)
    {
        string url = BuildUrl(baseUrl, path);
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            request.SetRequestHeader("X-Client-Channel", BackendClientMetadata.GetClientChannel());
            request.SetRequestHeader(
                "X-Client-Version-Code",
                BackendClientMetadata.GetClientVersionCode().ToString(CultureInfo.InvariantCulture));
            request.timeout = BackendClientMetadata.DefaultTimeoutSeconds;
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", $"Bearer {token}");
            if (!string.IsNullOrEmpty(idempotencyKey)) request.SetRequestHeader("Idempotency-Key", idempotencyKey);

            if (body != null)
            {
                string json = body is string rawJson ? rawJson : JsonUtility.ToJson(body);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(payload);
            }

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            int status = (int)request.responseCode;
            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(new BackendResult<T>(false, status, ExtractResponseCode(responseText), BuildTransportMessage(request, responseText), default));
                yield break;
            }

            try
            {
                ApiResponse<T> response = JsonUtility.FromJson<ApiResponse<T>>(responseText);
                if (response == null)
                {
                    onComplete?.Invoke(new BackendResult<T>(false, status, 0, "Backend returned an empty response.", default));
                    yield break;
                }

                bool success = response.code == 200;
                onComplete?.Invoke(new BackendResult<T>(success, status, response.code, response.message, response.data));
            }
            catch (Exception exception)
            {
                onComplete?.Invoke(new BackendResult<T>(false, status, 0, $"Failed to parse backend response: {exception.Message}", default));
            }
        }
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        string safeBase = string.IsNullOrEmpty(baseUrl) ? "http://localhost:8080/api" : baseUrl.TrimEnd('/');
        string safePath = string.IsNullOrEmpty(path) ? string.Empty : path.TrimStart('/');
        return $"{safeBase}/{safePath}";
    }

    private static string BuildTransportMessage(UnityWebRequest request, string responseText)
    {
        if (!string.IsNullOrEmpty(responseText))
        {
            try
            {
                ApiResponse<EmptyResponse> response = JsonUtility.FromJson<ApiResponse<EmptyResponse>>(responseText);
                if (response != null && !string.IsNullOrEmpty(response.message)) return response.message;
            }
            catch
            {
                // Fall through to Unity's transport error below.
            }
        }

        return string.IsNullOrEmpty(request.error) ? $"HTTP {(int)request.responseCode}" : request.error;
    }

    private static int ExtractResponseCode(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return 0;

        try
        {
            ApiResponse<EmptyResponse> response = JsonUtility.FromJson<ApiResponse<EmptyResponse>>(responseText);
            return response == null ? 0 : response.code;
        }
        catch
        {
            return 0;
        }
    }
}

internal static class BackendClientMetadata
{
    public const int DefaultTimeoutSeconds = 10;

    public static string GetClientChannel()
    {
        BackendClientConfig config = LoadConfig();
        return string.IsNullOrWhiteSpace(config.clientChannel) ? "DEFAULT" : config.clientChannel.Trim().ToUpperInvariant();
    }

    public static long GetClientVersionCode()
    {
        BackendClientConfig config = LoadConfig();
        return config.clientVersionCode > 0L ? config.clientVersionCode : 10001L;
    }

    private static BackendClientConfig LoadConfig()
    {
        TextAsset asset = Resources.Load<TextAsset>("ApiConfig");
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return new BackendClientConfig();

        try
        {
            BackendClientConfig parsed = JsonUtility.FromJson<BackendClientConfig>(asset.text);
            return parsed ?? new BackendClientConfig();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to read ApiConfig client metadata: " + exception.Message);
            return new BackendClientConfig();
        }
    }

    [Serializable]
    private sealed class BackendClientConfig
    {
        public string clientChannel;
        public long clientVersionCode;
    }
}
