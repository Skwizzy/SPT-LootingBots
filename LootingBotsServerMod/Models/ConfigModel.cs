namespace LootingBotsServerMod.Models;

/// <summary>
/// Configuration model for LootingBots server mod
/// </summary>
public record ConfigModel
{
    /// <summary>
    /// When false, PMCs will spawn without loot in their backpacks/pockets
    /// </summary>
    public bool PmcSpawnWithLoot { get; set; } = false;
    
    /// <summary>
    /// When false, Scavs will spawn without loot in their backpacks/pockets
    /// </summary>
    public bool ScavSpawnWithLoot { get; set; } = true;
}
