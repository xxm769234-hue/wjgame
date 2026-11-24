public static class IdleAdventureFormationPlacement
{
    public static bool TryPlace(int[] formation, int slot, int character, int availableCount)
    {
        if (formation == null || slot < 0 || slot >= formation.Length || character < 0 || character >= availableCount
            || formation[slot] == character) return false;
        int source = System.Array.IndexOf(formation, character);
        if (source >= 0) formation[source] = formation[slot];
        formation[slot] = character;
        return true;
    }
}
