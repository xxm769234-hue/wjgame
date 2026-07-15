using UnityEngine;

[CreateAssetMenu(fileName = "LandConfig", menuName = "Game/Land Config")]
public sealed class LandConfig : ScriptableObject
{
    [Header("Base Info")]
    public int landId;
    public string landName;
    [TextArea] public string description;
    public Sprite landSprite;
    public Sprite maskSprite;
    public LandType landType;
    [Min(0)] public int cost;
    [Min(1)] public int unlockLevel = 1;

    [Header("Building System")]
    public string[] allowBuildingType;
}

public enum LandType
{
    Grassland,
    Forest,
    Mountain,
    Water,
    Desert
}
