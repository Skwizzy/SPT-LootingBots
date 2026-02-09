using SPTarkov.Server.Core.Models.Spt.Mod;
using Version = SemanticVersioning.Version;
using Range = SemanticVersioning.Range;

namespace LootingBotsServerMod
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "me.skwizzy.lootingbots_servermod";
        public override string Name { get; init; } = "LootingBots-ServerMod";
        public override string Author { get; init; } = "Skwizzy";
        public override List<string>? Contributors { get; init; }
        public override Version Version { get; init; } = new("1.6.2");
        public override Range SptVersion { get; init; } = new("~4.0.0");
        public override List<string>? Incompatibilities { get; init; }
        public override Dictionary<string, Range>? ModDependencies { get; init; }
        public override string? Url { get; init; }
        public override bool? IsBundleMod { get; init; }
        public override string License { get; init; } = "MIT";
    }
}
