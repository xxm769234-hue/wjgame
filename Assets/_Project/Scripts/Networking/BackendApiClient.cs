using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class BackendApiClient
{
    public static IEnumerator Get<T>(string baseUrl, string token, string path, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbGET, path, null, onComplete);
    }

    public static IEnumerator Post<T>(string baseUrl, string token, string path, object body, Action<BackendResult<T>> onComplete)
    {
        return Send<T>(baseUrl, token, UnityWebRequest.kHttpVerbPOST, path, body, onComplete);
    }

    private static IEnumerator Send<T>(string baseUrl, string token, string method, string path, object body, Action<BackendResult<T>> onComplete)
    {
        string url = BuildUrl(baseUrl, path);
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", $"Bearer {token}");

            if (body != null)
            {
                string json = JsonUtility.ToJson(body);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(payload);
            }

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            int status = (int)request.responseCode;
            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(new BackendResult<T>(false, status, BuildTransportMessage(request, responseText), default));
                yield break;
            }

            try
            {
                ApiResponse<T> response = JsonUtility.FromJson<ApiResponse<T>>(responseText);
                if (response == null)
                {
                    onComplete?.Invoke(new BackendResult<T>(false, status, "Backend returned an empty response.", default));
                    yield break;
                }

                bool success = response.code == 200;
                onComplete?.Invoke(new BackendResult<T>(success, status, response.message, response.data));
            }
            catch (Exception exception)
            {
                onComplete?.Invoke(new BackendResult<T>(false, status, $"Failed to parse backend response: {exception.Message}", default));
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
}
