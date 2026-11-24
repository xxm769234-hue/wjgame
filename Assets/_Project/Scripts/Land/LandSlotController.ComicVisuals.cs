using System.Collections.Generic;
using UnityEngine;

public sealed partial class LandSlotController
{
    private SpriteRenderer residentPortraitFrame;
    private static Sprite residentFrameSprite;
    private static Sprite unknownResidentSprite;
    private static readonly Dictionary<Sprite, Sprite> CircularPortraits = new Dictionary<Sprite, Sprite>();

    private bool UsesSimulationComicShape => simulationShapeRenderer != null
        && simulationShapeRenderer.enabled && simulationShapeRenderer.sprite != null;

    private Sprite BuildingScene()
    {
        int slotKey = HasGridPosition ? GridPosition.x * 31 + GridPosition.y : SlotId;
        return SimulationBuildingSceneCatalog.GetScene(currentBuilding, slotKey);
    }

    public Sprite ArchiveScene => HasBuilding ? BuildingScene()
        : landRenderer != null && landRenderer.sprite != null ? landRenderer.sprite
        : LandTerrainCatalog.GetTerrain(currentLand) ?? currentLand?.landSprite;

    private void ConfigureComicBuildingScene()
    {
        Sprite scene = BuildingScene();
        buildingRenderer.sprite = scene;
        LandSceneMotion.Apply(buildingRenderer, scene);
        buildingRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        buildingRenderer.sortingLayerName = landSortingLayer;
        buildingRenderer.sortingOrder = GetRuntimeSortingOrder() + 1;
        buildingRenderer.color = buildingStatus == BuildingRuntimeStatus.COMPLETED
            ? Color.white : new Color(0.86f, 0.88f, 0.89f, 1f);
        buildingRenderer.enabled = scene != null;
        if (scene == null) return;

        // Centre the illustration's subject inside the polygon, including triangular cells.
        // Keep the original scenic sprite as the mask and click geometry.
        Vector2 center = transform.InverseTransformPoint(CenterWorld);
        Vector2[] path = ShapeHelper.GetLargestPath(polygonCollider);
        Vector2 reach = Vector2.zero;
        foreach (Vector2 point in path)
        {
            Vector2 relative = point - center;
            reach = Vector2.Max(reach, new Vector2(Mathf.Abs(relative.x), Mathf.Abs(relative.y)));
        }
        Vector2 size = scene.bounds.size;
        float scale = Mathf.Max(reach.x * 2f / Mathf.Max(0.001f, size.x),
            reach.y * 2f / Mathf.Max(0.001f, size.y));
        buildingRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        Vector2 spriteCenter = scene.bounds.center;
        buildingRenderer.transform.localPosition = new Vector3(
            center.x - spriteCenter.x * scale, center.y - spriteCenter.y * scale, -0.05f);
    }

    private void ConfigureComicResidentPortrait()
    {
        CharacterAppearanceCatalog.Entry appearance = CharacterAppearanceCatalog.Find(
            currentNpc.description, currentNpc.npcName, currentNpc.npcId.ToString());
        Sprite portrait = appearance != null ? appearance.residentPortrait : null;
        if (portrait == null) portrait = currentNpc.npcSprite;
        if (portrait == null) portrait = GetUnknownResidentPortrait();
        if (portrait == null || !TryGetResidentCorner(out Vector2 center, out float radius))
        {
            npcRenderer.enabled = false;
            HideResidentPortraitFrame();
            return;
        }

        if (residentPortraitFrame == null)
        {
            Transform existing = transform.Find("Resident Portrait Frame");
            if (existing != null) residentPortraitFrame = existing.GetComponent<SpriteRenderer>();
            if (residentPortraitFrame == null)
            {
                GameObject frameObject = new GameObject("Resident Portrait Frame");
                frameObject.transform.SetParent(transform, false);
                residentPortraitFrame = frameObject.AddComponent<SpriteRenderer>();
            }
        }
        Sprite circle = GetCircularPortrait(portrait);
        SetResidentRenderer(residentPortraitFrame, GetResidentFrameSprite(), center, radius * 2f, 0);
        SetResidentRenderer(npcRenderer, circle, center, radius * 1.77f, 1);
    }

    private void SetResidentRenderer(SpriteRenderer renderer, Sprite sprite, Vector2 center, float diameter, int order)
    {
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.sortingLayerName = buildingSortingLayer;
        renderer.sortingOrder = GetRuntimeSortingOrder() + npcSortingOrderOffset + order;
        renderer.transform.localPosition = new Vector3(center.x, center.y, -0.08f - order * 0.001f);
        float scale = diameter / Mathf.Max(0.001f, sprite.bounds.size.x);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
        renderer.enabled = true;
    }

    private void HideResidentPortraitFrame()
    {
        if (residentPortraitFrame != null) residentPortraitFrame.enabled = false;
    }

    private bool TryGetResidentCorner(out Vector2 center, out float radius)
    {
        center = Vector2.zero;
        radius = 0f;
        if (polygonCollider == null) return false;
        Vector2[] path = ShapeHelper.GetLargestPath(polygonCollider);
        if (path.Length < 3) return false;
        Vector2 min = path[0], max = path[0];
        for (int i = 1; i < path.Length; i++)
        {
            min = Vector2.Min(min, path[i]);
            max = Vector2.Max(max, path[i]);
        }
        Vector2 size = max - min;
        float unit = Mathf.Min(size.x, size.y);
        float inset = unit * 0.035f;
        radius = unit * 0.14f;
        // Search inside the polygon instead of using its bounding-box corner, which can
        // lie outside a slanted panel. The whole badge retains a small gutter inset.
        for (int attempt = 0; attempt < 6; attempt++, radius *= 0.8f)
        {
            float best = float.PositiveInfinity;
            bool found = false;
            for (int y = 0; y <= 32; y++)
            for (int x = 0; x <= 32; x++)
            {
                Vector2 point = min + Vector2.Scale(size, new Vector2(x / 32f, y / 32f));
                if (!CircleFitsPath(point, radius + inset, path)) continue;
                float right = (max.x - point.x) / Mathf.Max(0.001f, size.x);
                float bottom = (point.y - min.y) / Mathf.Max(0.001f, size.y);
                float score = right * right + bottom * bottom;
                if (score >= best) continue;
                best = score;
                center = point;
                found = true;
            }
            if (found) return true;
        }
        return false;
    }

    private static bool CircleFitsPath(Vector2 center, float radius, Vector2[] path)
    {
        bool inside = false;
        for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
        {
            Vector2 a = path[j], b = path[i];
            if ((a.y > center.y) != (b.y > center.y)
                && center.x < (b.x - a.x) * (center.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
            Vector2 edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(center - a, edge) / Mathf.Max(0.000001f, edge.sqrMagnitude));
            if ((center - (a + t * edge)).sqrMagnitude < radius * radius) return false;
        }
        return inside;
    }

    private static Sprite GetCircularPortrait(Sprite source)
    {
        if (CircularPortraits.TryGetValue(source, out Sprite cached) && cached != null) return cached;
        Rect rect = source.rect;
        float side = Mathf.Min(rect.width, rect.height);
        rect = new Rect(rect.center.x - side * 0.5f, rect.center.y - side * 0.5f, side, side);
        // Existing headshots are imported without Read/Write. Read a small GPU copy,
        // leaving their import settings, original pixels and other UI uses untouched.
        const int size = 256;
        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = source.name + " Circular Resident Portrait";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        try
        {
            Graphics.Blit(source.texture, target,
                new Vector2(rect.width / source.texture.width, rect.height / source.texture.height),
                new Vector2(rect.x / source.texture.width, rect.y / source.texture.height));
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
        Color32[] pixels = texture.GetPixels32();
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f));
            int index = y * size + x;
            pixels[index].a = (byte)(pixels[index].a * Mathf.Clamp01(size * 0.5f - radius));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size,
            0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        CircularPortraits[source] = sprite;
        return sprite;
    }

    private static Sprite GetResidentFrameSprite()
    {
        if (residentFrameSprite != null) return residentFrameSprite;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Comic Resident Portrait Rim";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 64f);
            Color color = r > 60f ? new Color(0.28f, 0.22f, 0.17f)
                : r > 57f ? new Color(0.72f, 0.54f, 0.30f) : new Color(1f, 0.95f, 0.82f);
            color.a = Mathf.Clamp01(64f - r);
            pixels[y * size + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        residentFrameSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        residentFrameSprite.hideFlags = HideFlags.HideAndDontSave;
        return residentFrameSprite;
    }

    private static Sprite GetUnknownResidentPortrait()
    {
        if (unknownResidentSprite != null) return unknownResidentSprite;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool head = (new Vector2(x, y) - new Vector2(64, 82)).sqrMagnitude < 21f * 21f;
            bool body = (x - 64f) * (x - 64f) / (43f * 43f) + (y - 21f) * (y - 21f) / (39f * 39f) < 1f;
            pixels[y * size + x] = head || body ? new Color(0.49f, 0.52f, 0.49f) : new Color(0.92f, 0.90f, 0.83f);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        unknownResidentSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        unknownResidentSprite.name = "Unassigned Resident Portrait Placeholder";
        unknownResidentSprite.hideFlags = HideFlags.HideAndDontSave;
        return unknownResidentSprite;
    }
}
