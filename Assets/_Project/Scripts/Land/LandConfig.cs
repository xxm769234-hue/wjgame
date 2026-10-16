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
    [HideInInspector] public string backendLandType;
    [Min(0)] public int cost;
    [Min(0)] public long goldCost;
    [Min(0)] public long woodCost;
    [Min(0)] public long stoneCost;
    [Min(0)] public long ironCost;
    [Min(1)] public int unlockLevel = 1;
    public bool canPlace = true;
    public string disabledReason;

    [Header("Building System")]
    public string[] allowBuildingType;

    public string LandTypeKey => string.IsNullOrWhiteSpace(backendLandType)
        ? landType.ToString().ToUpperInvariant()
        : backendLandType.Trim();
}

public enum LandType
{
    Grassland,
    Forest,
    Mountain,
    Water,
    Desert
}
