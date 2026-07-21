using UnityEngine;

public class LandRenderer : MonoBehaviour
{
    public SpriteRenderer spr;
    private LandTile tileData;

    private void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
    }

    public void RefreshTileView(LandTile tile, LandConfig cfg)
    {
        tileData = tile;
        if (tile.isLocked)
            spr.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        else
            spr.color = Color.white;

        if (cfg != null) spr.sprite = cfg.tileSprite;
        else spr.sprite = null;
    }
    //判断是否能建造
    public void SetPreviewColor(bool canPlace)
    {
        Color color = canPlace ? new Color(0, 1, 0, 0.6f) : new Color(1, 0, 0, 0.6f);
        spr.color = color;
    }
}
