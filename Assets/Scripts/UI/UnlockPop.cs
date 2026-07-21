using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UnlockPop : MonoBehaviour
{
    public TMP_Text tipText;
    public Button unlockBtn;
    public Button closeBtn;
    private MapAreaConfig targetArea;

    private void Awake()
    {
        closeBtn.onClick.AddListener(() => gameObject.SetActive(false));
        unlockBtn.onClick.AddListener(OnClickUnlock);
    }

    public void SetAreaData(MapAreaConfig areaCfg)
    {
        targetArea = areaCfg;
        tipText.text = MapAreaGenerator.Instance.GetUnlockTip(areaCfg);
        bool canUnlock = MapAreaGenerator.Instance.CheckUnlockCondition(areaCfg);
        unlockBtn.interactable = canUnlock;
    }

    void OnClickUnlock()
    {
        MapAreaGenerator.Instance.UnlockWholeArea(targetArea);
        gameObject.SetActive(false);
    }
}