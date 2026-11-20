using System;
using UnityEngine;

public partial class HomePageUI
{
    private int pendingCardDrawPlayerId;
    private string pendingCardDrawJson;
    private bool cardDrawPersistenceBlocked;

    private void RestoreCardDrawIntent()
    {
        if (pendingCardDrawPlayerId == GameSession.PlayerId && !string.IsNullOrEmpty(pendingCardDrawKey)) return;
        pendingCardDrawPlayerId = GameSession.PlayerId;
        pendingCardDrawKey = string.Empty;
        pendingCardDrawOption = string.Empty;
        pendingCardDrawJson = string.Empty;
        cardDrawPersistenceBlocked = false;
        string stored = PlayerPrefs.GetString(CardDrawStorageKey(pendingCardDrawPlayerId), string.Empty);
        if (string.IsNullOrEmpty(stored)) return;
        CardDrawIntent intent = ParseJson<CardDrawIntent>(stored);
        CardDrawRequest body = intent != null ? ParseJson<CardDrawRequest>(intent.json) : null;
        if (intent == null || intent.playerId != pendingCardDrawPlayerId || !Guid.TryParse(intent.key, out _)
            || intent.path != "/card-draw/draw" || body == null || body.optionCode != intent.option
            || (intent.option != "SINGLE" && intent.option != "FIVE" && intent.option != "TEN"))
        {
            cardDrawPersistenceBlocked = true;
            serverLotteryStatus = "上次召唤记录暂时无法读取，请稍后重试或联系客服";
            return;
        }
        pendingCardDrawKey = intent.key;
        pendingCardDrawOption = intent.option;
        pendingCardDrawJson = intent.json;
    }

    private bool SaveCardDrawIntent(string option)
    {
        string key = Guid.NewGuid().ToString();
        string json = JsonUtility.ToJson(new CardDrawRequest { optionCode = option });
        try
        {
            PlayerPrefs.SetString(CardDrawStorageKey(GameSession.PlayerId), JsonUtility.ToJson(new CardDrawIntent
            {
                playerId = GameSession.PlayerId, key = key, path = "/card-draw/draw", option = option, json = json
            }));
            PlayerPrefs.Save();
        }
        catch (Exception)
        {
            serverLotteryStatus = "无法保存本次召唤记录，请检查存储空间后重试";
            cardDrawPersistenceBlocked = true;
            return false;
        }
        pendingCardDrawPlayerId = GameSession.PlayerId;
        pendingCardDrawKey = key;
        pendingCardDrawOption = option;
        pendingCardDrawJson = json;
        return true;
    }

    private void ClearCardDrawIntent(int playerId)
    {
        try
        {
            PlayerPrefs.DeleteKey(CardDrawStorageKey(playerId));
            PlayerPrefs.Save();
        }
        catch (Exception)
        {
            cardDrawPersistenceBlocked = true;
            serverLotteryStatus = "召唤已结算，记录同步中，请稍后再试";
            return;
        }
        if (pendingCardDrawPlayerId != playerId) return;
        pendingCardDrawKey = string.Empty;
        pendingCardDrawOption = string.Empty;
        pendingCardDrawJson = string.Empty;
    }

    private static string CardDrawStorageKey(int playerId)
    {
        return "weijun.cardDraw.pending." + playerId;
    }

    [Serializable]
    private sealed class CardDrawIntent
    {
        public int playerId;
        public string key;
        public string path;
        public string option;
        public string json;
    }
}
