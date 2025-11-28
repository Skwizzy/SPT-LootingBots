using SPTarkov.Server.Core.Models.Spt.Mod;

namespace LootingBotsServerMod;

/// <summary>
/// Metadata for the LootingBots server-side mod
/// Configures bot spawn loot settings and disables discard limits for proper gear swapping
/// </summary>
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.skwizzy.lootingbots.servermod";
    public override string Name { get; init; } = "LootingBots-ServerMod";
    public override string Author { get; init; } = "Skwizzy";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/Skwizzy/SPT-LootingBots";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}
