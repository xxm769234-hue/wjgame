using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public partial class HomePageUI
{
    private bool serverStaminaLoaded;
    private bool serverStaminaRequestRunning;
    private long serverStaminaCurrent;
    private long serverStaminaMaximum;
    private int serverStaminaSecondsToNextRecovery;

    private IEnumerator RefreshServerStamina()
    {
        if (serverStaminaRequestRunning || !HasAuthSession()) yield break;

        serverStaminaRequestRunning = true;
        UnityWebRequest request = CreateAuthorizedGet($"/idle-adventure/players/{GameSession.PlayerId}/stamina");
        if (request == null)
        {
            serverStaminaRequestRunning = false;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverStaminaRequestRunning = false;
                yield break;
            }

            StaminaApiResponse response = ParseJson<StaminaApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null)
            {
                serverStaminaCurrent = Math.Max(0L, response.data.current_stamina);
                serverStaminaMaximum = Math.Max(0L, response.data.max_stamina);
                serverStaminaSecondsToNextRecovery = Math.Max(0, response.data.seconds_to_next_recovery);
                serverStaminaLoaded = true;
                PlayerPrefs.SetInt("PlayerStamina", ClampLongToInt(serverStaminaCurrent));
                PlayerPrefs.Save();
                RefreshHomeResourceMetrics();
                RefreshOpenProfileResourceCards();
            }
            else
            {
                Debug.LogWarning("Authoritative stamina refresh failed: " + ApiRequestUtility.GetErrorMessage(request, "request failed"), this);
            }
        }

        serverStaminaRequestRunning = false;
    }

    private int GetAuthoritativeHomeStamina()
    {
        return serverStaminaLoaded ? ClampLongToInt(serverStaminaCurrent) : 0;
    }

    private static int ClampLongToInt(long value)
    {
        return (int)Math.Max(0L, Math.Min(int.MaxValue, value));
    }

    private static string FormatStaminaValue(long current, long maximum)
    {
        return maximum > 0L ? current + "/" + maximum : current.ToString();
    }

    private void RefreshStaminaAfterShopPurchase()
    {
        if (HasAuthSession()) StartCoroutine(RefreshServerStamina());
    }

    [Serializable]
    private sealed class StaminaApiResponse : BasicApiResponse
    {
        public StaminaData data;
    }

    [Serializable]
    private sealed class StaminaData
    {
        public bool enabled;
        public long current_stamina;
        public long max_stamina;
        public bool over_cap;
        public long overflow_amount;
        public int recovery_interval_seconds;
        public string next_recovery_at;
        public int seconds_to_next_recovery;
        public int seconds_to_full;
        public string server_time;
        public long config_version;
    }
}
