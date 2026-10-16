using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TipTool : MonoBehaviour
{
    public static TipTool Instance;
    public GameObject tipPrefab;
    public Transform uiRoot;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
    }

    /// <summary>
    /// 弹出飘字提示
    /// </summary>
    public static void ShowTip(string text, Color color)
    {
        if (Instance == null) return;
        GameObject tipObj = Instantiate(Instance.tipPrefab, Instance.uiRoot);
        TMP_Text tmp = tipObj.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.color = color;
        // 自动销毁
        Destroy(tipObj, 1.2f);
    }
}
