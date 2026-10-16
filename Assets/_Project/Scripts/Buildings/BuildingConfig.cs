using UnityEngine;

[CreateAssetMenu(fileName = "BuildingConfig", menuName = "Game/Building Config")]
public sealed class BuildingConfig : ScriptableObject
{
    [Header("Identity")]
    public int buildingId;
    public string buildingName = "Building";
    [TextArea] public string description;

    [Header("Visual")]
    public Sprite buildingSprite;
    public Color fallbackColor = new Color(0.92f, 0.62f, 0.25f, 1f);
    [Min(0.01f)] public float visualScale = 1f;
    public Vector2 visualOffset;

    [Header("Build")]
    [Min(0)] public int cost;
    [Min(0)] public long goldCost;
    [Min(0)] public long woodCost;
    [Min(0)] public long stoneCost;
    [Min(0)] public long ironCost;
    [Min(0)] public int buildSeconds;
    public string buildingType = "Default";
    [HideInInspector] public string requiredLandType;
    public bool canBuild = true;
    public string disabledReason;
    public string singletonGroupCode;
    [Min(0)] public int ownedCount;
    [Min(0)] public int maxOwnedCount;
    public bool hasOwnershipLimit;
    public bool ownershipLimitMet = true;

    [Header("Upgrade")]
    [Min(1)] public int maxLevel = 3;
    [Min(0)] public int upgradeCost = 50;
    [Min(0)] public int upgradeSeconds = 10;
    [Min(0)] public int resourceAmountPerLevel = 5;

    [Header("Resource Output")]
    public bool canProduceResource = true;
    public bool requireNpcForProduction;
    public bool showResourceBubbleImmediately = true;
    public string resourceName = "Gold";
    public Sprite resourceIcon;
    public Color resourceColor = new Color(1f, 0.78f, 0.2f, 1f);
    [Min(1)] public int resourceAmount = 10;
    [Min(0f)] public float productionSeconds = 10f;

    [Header("Resource Input")]
    public ProductionResourceCost[] productionCosts = new ProductionResourceCost[0];

    public bool HasProductionCosts
    {
        get
        {
            if (productionCosts == null) return false;
            for (int i = 0; i < productionCosts.Length; i++)
            {
                if (productionCosts[i] != null && productionCosts[i].IsValid) return true;
            }
            return false;
        }
    }
}
