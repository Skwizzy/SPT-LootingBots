namespace LootingBotsServerMod.Models
{
    public record ConfigModel
    {
        public bool PmcSpawnWithLoot { get; set; } = true;
        public bool ScavSpawnWithLoot { get; set; } = true;
    }
}
