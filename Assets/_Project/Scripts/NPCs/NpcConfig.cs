using UnityEngine;

[CreateAssetMenu(fileName = "NpcConfig", menuName = "Game/NPC Config")]
public sealed class NpcConfig : ScriptableObject
{
    [Header("Identity")]
    public int npcId;
    public string npcName = "NPC";
    public string role = "Resident";
    [TextArea] public string description;

    [Header("Visual")]
    public Sprite npcSprite;
    public Color fallbackColor = new Color(0.34f, 0.66f, 0.96f, 1f);
    [Min(0.01f)] public float visualScale = 0.85f;
    public Vector2 visualOffset = new Vector2(0.85f, 0.15f);

    [Header("Residence")]
    public string requiredBuildingType;
    public bool canAssign = true;
    public string disabledReason;
}
