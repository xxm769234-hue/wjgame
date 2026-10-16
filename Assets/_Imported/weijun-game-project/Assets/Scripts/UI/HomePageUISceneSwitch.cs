using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class HomePageUI
{
    private IEnumerator LoadLandSceneAndEnter(string landType)
    {
        mapEntryRunning = true;
        SetStatus("正在读取服务器区域...");

        if (!HasAuthSession() || GameSession.PlayerId <= 0)
        {
            mapEntryRunning = false;
            SetStatus("请先登录账号");
            yield break;
        }

        int playerId = GameSession.PlayerId;
        UnityWebRequest listRequest = CreateAuthorizedGet($"/land/scenes/list/{playerId}");
        if (listRequest == null)
        {
            mapEntryRunning = false;
            SetStatus("区域列表读取失败：" + requestCreationError);
            yield break;
        }

        SceneListApiResponse scenesResponse;
        using (listRequest)
        {
            yield return listRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(listRequest))
            {
                mapEntryRunning = false;
                yield break;
            }

            if (listRequest.result != UnityWebRequest.Result.Success)
            {
                mapEntryRunning = false;
                SetStatus("区域列表读取失败：" + ApiRequestUtility.GetErrorMessage(listRequest, "请求失败"));
                yield break;
            }

            scenesResponse = ParseJson<SceneListApiResponse>(listRequest.downloadHandler.text);
        }

        if (scenesResponse == null || scenesResponse.code != 200 || scenesResponse.data == null)
        {
            mapEntryRunning = false;
            SetStatus("区域列表读取失败：" + (scenesResponse == null
                ? "返回数据格式错误"
                : GetApiError(scenesResponse)));
            yield break;
        }

        SceneEntry target = FindLandSceneForArea(scenesResponse.data, landType);
        if (target == null || target.ResolvedSceneId <= 0L)
        {
            mapEntryRunning = false;
            SetStatus("服务器未配置该区域，暂时无法进入");
            yield break;
        }

        if (!target.IsEnterable && !target.current)
        {
            mapEntryRunning = false;
            string sceneName = string.IsNullOrEmpty(target.ResolvedSceneName) ? landType : target.ResolvedSceneName;
            SetStatus(sceneName + "尚未解锁或当前不可进入");
            yield break;
        }

        HomeSceneSwitchRequest switchPayload = new HomeSceneSwitchRequest
        {
            playerId = playerId,
            sceneId = target.ResolvedSceneId
        };
        if (!TryCreateAuthorizedPost(
                "/land/scenes/switch",
                JsonUtility.ToJson(switchPayload),
                out UnityWebRequest switchRequest,
                out string switchRequestError))
        {
            mapEntryRunning = false;
            SetStatus("区域切换失败：" + switchRequestError);
            yield break;
        }

        SceneSwitchApiResponse switchResponse;
        using (switchRequest)
        {
            yield return switchRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(switchRequest))
            {
                mapEntryRunning = false;
                yield break;
            }

            if (switchRequest.result != UnityWebRequest.Result.Success)
            {
                mapEntryRunning = false;
                SetStatus("区域切换失败：" + ApiRequestUtility.GetErrorMessage(switchRequest, "请求失败"));
                yield break;
            }

            switchResponse = ParseJson<SceneSwitchApiResponse>(switchRequest.downloadHandler.text);
        }

        if (switchResponse == null || switchResponse.code != 200 || switchResponse.data == null)
        {
            mapEntryRunning = false;
            SetStatus("区域切换失败：" + (switchResponse == null
                ? "返回数据格式错误"
                : GetApiError(switchResponse)));
            yield break;
        }

        long switchedSceneId = switchResponse.data.ResolvedSceneId > 0L
            ? switchResponse.data.ResolvedSceneId
            : target.ResolvedSceneId;
        if (switchedSceneId > int.MaxValue)
        {
            mapEntryRunning = false;
            SetStatus("区域切换失败：场景编号超出客户端支持范围");
            yield break;
        }

        PlayerPrefs.SetInt("weijun.backend.sceneId", (int)switchedSceneId);
        PlayerPrefs.Save();
        string targetLandType = ResolveLocalLandType(
            target.ResolvedSceneKey,
            target.ResolvedSceneName,
            "grass");
        string resolvedLandType = ResolveLocalLandType(
            switchResponse.data.ResolvedSceneKey,
            switchResponse.data.ResolvedSceneName,
            targetLandType);
        mapEntryRunning = false;
        yield return GetMapDataAndEnter(resolvedLandType);
    }

    private static SceneEntry FindLandSceneForArea(SceneEntry[] scenes, string landType)
    {
        if (scenes == null || scenes.Length == 0) return null;

        string requested = NormalizeSceneToken(landType);
        string alias = GetSceneAlias(requested);
        SceneEntry currentFallback = null;
        SceneEntry enterableFallback = null;
        for (int i = 0; i < scenes.Length; i++)
        {
            SceneEntry scene = scenes[i];
            if (scene == null || scene.ResolvedSceneId <= 0L) continue;
            if (scene.current && currentFallback == null) currentFallback = scene;
            if (scene.IsEnterable && enterableFallback == null) enterableFallback = scene;

            string key = NormalizeSceneToken(scene.ResolvedSceneKey);
            string name = NormalizeSceneToken(scene.ResolvedSceneName);
            if ((scene.current || scene.IsEnterable)
                && (MatchesSceneToken(key, requested, alias) || MatchesSceneToken(name, requested, alias)))
            {
                return scene;
            }
        }

        return currentFallback ?? enterableFallback;
    }

    private static string ResolveLocalLandType(string sceneKey, string sceneName, string fallbackLandType)
    {
        string key = GetSceneAlias(sceneKey);
        if (key != "HOME" && key != "FOREST" && key != "SEA" && key != "VOLCANO" && key != "SNOW")
        {
            key = GetSceneAlias(sceneName);
        }

        if (key == "HOME") return "grass";
        if (key == "FOREST") return "forest";
        if (key == "SEA") return "sea";
        if (key == "VOLCANO") return "volcano";
        if (key == "SNOW") return "snow";
        return string.IsNullOrEmpty(fallbackLandType) ? "grass" : fallbackLandType;
    }

    private static string GetSceneAlias(string token)
    {
        token = NormalizeSceneToken(token);
        switch (token)
        {
            case "GRASS":
            case "GRASSLAND":
            case "GRASS_LAND":
            case "PLAINS":
            case "MEADOW":
            case "草原":
            case "草地":
            case "HOME":
            case "家园":
                return "HOME";
            case "FOREST":
            case "WOODLAND":
            case "JUNGLE":
            case "森林":
                return "FOREST";
            case "SEA":
            case "OCEAN":
            case "COAST":
            case "海":
            case "海洋":
                return "SEA";
            case "VOLCANO":
            case "LAVA":
            case "火山":
                return "VOLCANO";
            case "SNOW":
            case "SNOWFIELD":
            case "SNOW_FIELD":
            case "TUNDRA":
            case "ICE":
            case "ICEFIELD":
            case "ICE_FIELD":
            case "GLACIER":
            case "雪地":
            case "雪原":
            case "冰原":
                return "SNOW";
            default:
                return token;
        }
    }

    private static bool MatchesSceneToken(string candidate, string requested, string requestedAlias)
    {
        if (string.IsNullOrEmpty(candidate)) return false;

        string normalizedCandidate = NormalizeSceneToken(candidate);
        return normalizedCandidate == requested
            || normalizedCandidate == requestedAlias
            || GetSceneAlias(normalizedCandidate) == requestedAlias;
    }

    private static string NormalizeSceneToken(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
    }

    [Serializable]
    private sealed class SceneListApiResponse : BasicApiResponse
    {
        public SceneEntry[] data;
    }

    [Serializable]
    private sealed class SceneSwitchApiResponse : BasicApiResponse
    {
        public SceneEntry data;
    }

    [Serializable]
    private sealed class HomeSceneSwitchRequest
    {
        public int playerId;
        public long sceneId;
    }

    [Serializable]
    private sealed class SceneEntry
    {
        public long sceneId;
        public long scene_id;
        public string sceneKey;
        public string scene_key;
        public string sceneName;
        public string scene_name;
        public bool current;
        public bool canEnter;
        public bool can_enter;

        public long ResolvedSceneId => sceneId > 0L ? sceneId : scene_id;
        public string ResolvedSceneKey => string.IsNullOrEmpty(sceneKey) ? scene_key : sceneKey;
        public string ResolvedSceneName => string.IsNullOrEmpty(sceneName) ? scene_name : sceneName;
        public bool IsEnterable => canEnter || can_enter;
    }
}
