using System.Collections.Generic;
using UnityEngine;

public static class LandSceneMotion
{
    private static readonly Dictionary<Sprite, Material> worldMaterials = new Dictionary<Sprite, Material>();
    private static readonly Dictionary<Sprite, Material> imageMaterials = new Dictionary<Sprite, Material>();
    private static Material defaultSpriteMaterial;

    public static Material ForImage(Sprite scene) => Resolve(scene, imageMaterials, "LandImage");

    public static void Apply(SpriteRenderer target, Sprite scene)
    {
        if (defaultSpriteMaterial == null) defaultSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
        target.sharedMaterial = Resolve(scene, worldMaterials, "LandSprite") ?? defaultSpriteMaterial;
    }

    private static Material Resolve(Sprite scene, Dictionary<Sprite, Material> cache, string shaderName)
    {
        if (scene == null) return null;
        if (cache.TryGetValue(scene, out var material)) return material;
        var mask = Resources.Load<Texture2D>("LandMotion/Masks/" + scene.name);
        if (mask != null)
        {
            Shader shader = Resources.Load<Shader>("LandMotion/" + shaderName);
            if (shader != null && shader.isSupported)
            {
                material = new Material(shader) { name = scene.name + " · " + shaderName };
                material.SetTexture("_MotionMask", mask);
            }
        }
        cache[scene] = material;
        return material;
    }
}
