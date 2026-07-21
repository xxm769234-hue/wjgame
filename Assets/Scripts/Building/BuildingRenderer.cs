using UnityEngine;
using TMPro;

public class BuildingRenderer : MonoBehaviour
{
    public SpriteRenderer spr;
    public TMP_Text levelText;
    public BuildingData data;

    public void RefreshView(BuildingData buildData, BuildingConfig cfg)
    {
        data = buildData;
        spr.sprite = cfg.buildSprite;
        levelText.text = $"Lv.{data.level}";
    }

    public void SetMoveTransparent(bool isMove)
    {
        Color c = spr.color;
        c.a = isMove ? 0.4f : 1f;
        spr.color = c;
    }
}