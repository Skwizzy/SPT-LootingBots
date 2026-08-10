using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace LootingBotsServerMod;

// SPT 4.1: AbstractModMetadata was replaced by the IModMetadata interface, so these are plain
// property implementations rather than overrides. IsBundleMod is gone; HasPrepatcher is new.
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "me.skwizzy.lootingbotsservermod";
    public string Name { get; init; } = "LootingBots-ServerMod";
    public string Author { get; init; } = "Skwizzy";
    public List<string>? Contributors { get; init; }
    public Version Version { get; init; } = new("1.8.0");
    public Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public bool HasPrepatcher { get; init; }
    public string License { get; init; } = "MIT";
}
