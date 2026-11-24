using System;
using UnityEngine;

public static class RoleFragmentArt
{
    private const string Prefix = "npc_fragment_";
    private const int Cell = 192;
    private static readonly string[] Codes = { "", "field_steward","workshop_foreman","master_manager","chief_manager","yanheng","shuoying","lingke" };
    private static readonly string[] Names = { "", "漫画家","沧祈","岑岳","绮萝","砚衡","朔影","铃珂" };
    private static readonly string[] Qualities = { "", "LEGENDARY","LEGENDARY","EPIC","LEGENDARY","EPIC","EPIC","LEGENDARY" };
    private static readonly string[] catalogQualities = new string[8];
    private static readonly Sprite[,] sprites = new Sprite[8,5];
    private static Texture2D sheet;

    public static void SetRoleQuality(string code,string quality)
    {
        int index = Array.FindIndex(Codes, entry => string.Equals(entry,code,StringComparison.OrdinalIgnoreCase));
        if (index > 0 && !string.IsNullOrWhiteSpace(quality)) catalogQualities[index] = quality;
    }

    public static Sprite Resolve(string key,string name,string icon,string quality = null,string category = null,string roleCode = null,string roleQuality = null)
    {
        string fragment = (key ?? "").Trim();
        if (!fragment.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase) && (icon ?? "").StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)) fragment = icon;
        bool prefixed = fragment.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase);
        string label = (name ?? "").Trim();
        bool named = label.EndsWith("碎片",StringComparison.Ordinal);
        if (!prefixed && !string.Equals(category,"NPC_FRAGMENT",StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(roleCode) && !named) return null;
        string code = prefixed ? fragment.Substring(Prefix.Length) : roleCode;
        string character = named ? label.Substring(0,label.Length-2).TrimEnd('专','属','·',' ') : "";
        if (character == "玲珂") character = "铃珂";
        int index = 0;
        for (int slot = 1; slot < Codes.Length; slot++)
        {
            if (string.Equals(code,Codes[slot],StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleCode,Codes[slot],StringComparison.OrdinalIgnoreCase) || character == Names[slot])
            { index = slot; break; }
        }
        if (index == 0 && !prefixed && !string.Equals(category,"NPC_FRAGMENT",StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(roleCode)) return null;
        string selected = !string.IsNullOrWhiteSpace(roleQuality) ? roleQuality : catalogQualities[index];
        if (string.IsNullOrWhiteSpace(selected)) selected = index > 0 ? Qualities[index] : quality;
        int tier;
        switch ((selected ?? "").Trim().ToUpperInvariant())
        {
            case "FINE": case "GREEN": case "UNCOMMON": case "优秀": tier=1; break;
            case "EXCELLENT": case "BLUE": case "RARE": case "稀有": tier=2; break;
            case "EPIC": case "PURPLE": case "史诗": tier=3; break;
            case "LEGENDARY": case "GOLD": case "SSR": case "MYTHIC": case "LUXURY": case "传说": tier=4; break;
            default: tier=0; break;
        }
        if (sprites[index,tier] != null) return sprites[index,tier];
        if (sheet == null) sheet = Resources.Load<Texture2D>("UI/SummonBook/RoleFragmentIcons");
        if (sheet == null) return null;
        var rect = new Rect(index*Cell,(4-tier)*Cell,Cell,Cell);
        var sprite = Sprite.Create(sheet,rect,new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
        sprite.name = "RoleFragment_" + index + "_" + tier;
        sprites[index,tier] = sprite;
        return sprite;
    }
}
