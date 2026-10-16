using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class IdleAdventureStageProgressEvent : UnityEvent<int>
{
}

public sealed class IdleAdventureStageProgress : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private string defaultPrefsKey = "weijun.idleAdventure.highestClearedStage";
    [SerializeField] private string playerPrefsPrefix = "weijun.idleAdventure.highestClearedStage.";
    [SerializeField] private bool scopeByBackendPlayer = true;

    [Header("Runtime State")]
    [SerializeField, Min(0)] private int highestClearedStage;
    [SerializeField, Min(1)] private int nextUnclearedStage = 1;

    [Header("Events")]
    [SerializeField] private IdleAdventureStageProgressEvent progressLoaded = new IdleAdventureStageProgressEvent();
    [SerializeField] private IdleAdventureStageProgressEvent stageCleared = new IdleAdventureStageProgressEvent();

    public int HighestClearedStage => highestClearedStage;
    public int NextUnclearedStage => nextUnclearedStage;
    public string CurrentPrefsKey => ResolvePrefsKey();
    public IdleAdventureStageProgressEvent ProgressLoaded => progressLoaded;
    public IdleAdventureStageProgressEvent StageCleared => stageCleared;

    private void OnValidate()
    {
        highestClearedStage = Mathf.Max(0, highestClearedStage);
        RefreshNextStage();
    }

    public int LoadProgress()
    {
        highestClearedStage = Mathf.Max(0, PlayerPrefs.GetInt(ResolvePrefsKey(), 0));
        RefreshNextStage();
        progressLoaded.Invoke(highestClearedStage);
        return highestClearedStage;
    }

    public int RecordStageCleared(int clearedStage)
    {
        int stage = Mathf.Max(1, clearedStage);
        if (stage <= highestClearedStage) return highestClearedStage;

        highestClearedStage = stage;
        SaveProgress();
        RefreshNextStage();
        stageCleared.Invoke(highestClearedStage);
        return highestClearedStage;
    }

    public void SetProgressFromServer(int serverHighestClearedStage)
    {
        highestClearedStage = Mathf.Max(0, serverHighestClearedStage);
        SaveProgress();
        RefreshNextStage();
        progressLoaded.Invoke(highestClearedStage);
    }

    public void ClearLocalProgress()
    {
        highestClearedStage = 0;
        RefreshNextStage();
        PlayerPrefs.DeleteKey(ResolvePrefsKey());
        PlayerPrefs.Save();
        progressLoaded.Invoke(highestClearedStage);
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(ResolvePrefsKey(), highestClearedStage);
        PlayerPrefs.Save();
    }

    private void RefreshNextStage()
    {
        nextUnclearedStage = Mathf.Max(1, highestClearedStage + 1);
    }

    private string ResolvePrefsKey()
    {
        if (scopeByBackendPlayer)
        {
            BackendGameService service = BackendGameService.Instance;
            if (service != null && service.PlayerId > 0) return $"{playerPrefsPrefix}{service.PlayerId}";
        }

        return string.IsNullOrEmpty(defaultPrefsKey) ? "weijun.idleAdventure.highestClearedStage" : defaultPrefsKey;
    }
}
