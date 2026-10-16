using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Consumes one-shot notification targets after a scene transition.</summary>
public sealed class NotificationDeepLinkRouter : MonoBehaviour
{
    private const string SimulationSceneName = "Simulation management mode";
    private const float ResolveTimeoutSeconds = 35f;
    private const float SceneSwitchRetrySeconds = 2f;
    private const int MaxSceneSwitchAttempts = 3;
    private const string PendingTargetTypeKey = "Notification.DeepLink.Type";
    private const string PendingSceneIdKey = "Notification.DeepLink.SceneId";
    private const string PendingBuildingIdKey = "Notification.DeepLink.PlayerBuildingId";
    private const string PendingLandIdKey = "Notification.DeepLink.PlayerLandId";

    private static NotificationDeepLinkRouter instance;
    private bool resolving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    private static NotificationDeepLinkRouter EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject existingObject = GameObject.Find("NotificationDeepLinkRouter");
        if (existingObject == null)
        {
            existingObject = new GameObject("NotificationDeepLinkRouter");
        }

        instance = existingObject.GetComponent<NotificationDeepLinkRouter>();
        if (instance == null)
        {
            instance = existingObject.AddComponent<NotificationDeepLinkRouter>();
        }

        DontDestroyOnLoad(existingObject);
        return instance;
    }

    public static void QueueScene(int sceneId)
    {
        if (sceneId > 0)
        {
            PlayerPrefs.SetInt(PendingSceneIdKey, sceneId);
        }
        else
        {
            PlayerPrefs.DeleteKey(PendingSceneIdKey);
        }
        PlayerPrefs.SetString(PendingTargetTypeKey, "SCENE");
        PlayerPrefs.DeleteKey(PendingBuildingIdKey);
        PlayerPrefs.DeleteKey(PendingLandIdKey);
        PlayerPrefs.Save();
        EnsureInstance();
    }

    public static void QueueBuilding(int sceneId, long playerBuildingId, long playerLandId)
    {
        if (sceneId > 0)
        {
            PlayerPrefs.SetInt(PendingSceneIdKey, sceneId);
        }
        else
        {
            PlayerPrefs.DeleteKey(PendingSceneIdKey);
        }

        PlayerPrefs.SetString(PendingTargetTypeKey, "BUILDING");
        SetLong(PendingBuildingIdKey, playerBuildingId);
        SetLong(PendingLandIdKey, playerLandId);
        PlayerPrefs.Save();
        EnsureInstance();
    }

    private static void SetLong(string key, long value)
    {
        if (value > 0L)
        {
            PlayerPrefs.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    private void Update()
    {
        if (resolving || !HasPendingTarget() || SceneManager.GetActiveScene().name != SimulationSceneName)
        {
            return;
        }

        resolving = true;
        StartCoroutine(ResolvePendingTarget());
    }

    private IEnumerator ResolvePendingTarget()
    {
        string targetType = PlayerPrefs.GetString(PendingTargetTypeKey, string.Empty);
        int targetSceneId = PlayerPrefs.GetInt(PendingSceneIdKey, 0);
        long targetBuildingId = ReadLong(PendingBuildingIdKey);
        long targetLandId = ReadLong(PendingLandIdKey);
        float deadline = Time.unscaledTime + ResolveTimeoutSeconds;
        float nextSceneSwitchAttemptAt = 0f;
        int sceneSwitchAttempts = 0;
        bool sceneSwitchInFlight = false;
        bool sceneSwitchSucceeded = false;

        while (Time.unscaledTime < deadline)
        {
            if (SceneManager.GetActiveScene().name != SimulationSceneName)
            {
                resolving = false;
                yield break;
            }

            BackendGameService service = BackendGameService.Instance;
            if (service != null && service.IsAuthenticated)
            {
                bool sceneMatches = targetSceneId <= 0 || service.CurrentSceneId == targetSceneId;
                if (targetSceneId > 0 && !sceneMatches && !sceneSwitchInFlight
                    && !sceneSwitchSucceeded && sceneSwitchAttempts < MaxSceneSwitchAttempts
                    && Time.unscaledTime >= nextSceneSwitchAttemptAt)
                {
                    sceneSwitchAttempts++;
                    sceneSwitchInFlight = true;
                    int attempt = sceneSwitchAttempts;
                    service.SwitchLandScene(targetSceneId, result =>
                    {
                        if (attempt != sceneSwitchAttempts) return;
                        sceneSwitchInFlight = false;
                        sceneSwitchSucceeded = result != null && result.Success && result.Data != null;
                        if (!sceneSwitchSucceeded)
                        {
                            nextSceneSwitchAttemptAt = Time.unscaledTime + SceneSwitchRetrySeconds;
                        }
                    });
                }
                else if (targetSceneId <= 0 || sceneMatches)
                {
                    if (string.Equals(targetType, "SCENE", StringComparison.OrdinalIgnoreCase))
                    {
                        ClearPendingTarget();
                        resolving = false;
                        yield break;
                    }

                    LandSlotController slot = FindTargetSlot(targetBuildingId, targetLandId);
                    if (slot != null && slot.CurrentLand != null && LandInfoPanel.Instance != null)
                    {
                        LandInfoPanel.Instance.Show(slot);
                        ClearPendingTarget();
                        resolving = false;
                        yield break;
                    }
                }
            }

            yield return null;
        }

        string failureReason = targetSceneId > 0 && sceneSwitchAttempts >= MaxSceneSwitchAttempts
            ? "scene switch retry limit reached"
            : "timeout";
        Debug.LogWarning("Notification deep link target could not be resolved (" + failureReason + ").");
        ClearPendingTarget();
        resolving = false;
    }

    private static LandSlotController FindTargetSlot(long playerBuildingId, long playerLandId)
    {
        LandSlotController[] slots = FindObjectsByType<LandSlotController>(FindObjectsSortMode.None);
        LandSlotController landMatch = null;
        for (int i = 0; i < slots.Length; i++)
        {
            LandSlotController slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (playerBuildingId > 0L && slot.PlayerBuildingId == playerBuildingId)
            {
                return slot;
            }

            if (landMatch == null && playerLandId > 0L && slot.PlayerLandId == playerLandId)
            {
                landMatch = slot;
            }
        }

        return landMatch;
    }

    private static long ReadLong(string key)
    {
        string value = PlayerPrefs.GetString(key, string.Empty);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : 0L;
    }

    private static bool HasPendingTarget()
    {
        return !string.IsNullOrEmpty(PlayerPrefs.GetString(PendingTargetTypeKey, string.Empty));
    }

    private static void ClearPendingTarget()
    {
        PlayerPrefs.DeleteKey(PendingTargetTypeKey);
        PlayerPrefs.DeleteKey(PendingSceneIdKey);
        PlayerPrefs.DeleteKey(PendingBuildingIdKey);
        PlayerPrefs.DeleteKey(PendingLandIdKey);
        PlayerPrefs.Save();
    }
}
