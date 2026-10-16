using System.Globalization;
using UnityEngine;

public static class HomePageUINotificationDeepLink
{
    private const string PendingTargetTypeKey = "Notification.DeepLink.Type";
    private const string PendingSceneIdKey = "Notification.DeepLink.SceneId";
    private const string PendingBuildingIdKey = "Notification.DeepLink.PlayerBuildingId";
    private const string PendingLandIdKey = "Notification.DeepLink.PlayerLandId";

    public static void QueueScene(int sceneId)
    {
        SetSceneId(sceneId);
        PlayerPrefs.SetString(PendingTargetTypeKey, "SCENE");
        PlayerPrefs.DeleteKey(PendingBuildingIdKey);
        PlayerPrefs.DeleteKey(PendingLandIdKey);
        PlayerPrefs.Save();
    }

    public static void QueueBuilding(int sceneId, long playerBuildingId, long playerLandId)
    {
        SetSceneId(sceneId);
        PlayerPrefs.SetString(PendingTargetTypeKey, "BUILDING");
        SetLong(PendingBuildingIdKey, playerBuildingId);
        SetLong(PendingLandIdKey, playerLandId);
        PlayerPrefs.Save();
    }

    private static void SetSceneId(int sceneId)
    {
        if (sceneId > 0)
        {
            PlayerPrefs.SetInt(PendingSceneIdKey, sceneId);
        }
        else
        {
            PlayerPrefs.DeleteKey(PendingSceneIdKey);
        }
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
}
