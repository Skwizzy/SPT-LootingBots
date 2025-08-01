using SPTarkov.Server.Core.Models.Spt.Mod;

namespace LootingBotsServerMod
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string Name { get; set; } = "LootingBots-ServerMod";
        public override string Author { get; set; } = "Skwizzy";
        public override List<string>? Contributors { get; set; }
        public override string Version { get; set; } = "1.6.0";
        public override string SptVersion { get; set; } = "4.0.0";
        public override List<string>? LoadBefore { get; set; }
        public override List<string>? LoadAfter { get; set; }
        public override List<string>? Incompatibilities { get; set; }
        public override Dictionary<string, string>? ModDependencies { get; set; }
        public override string? Url { get; set; }
        public override bool? IsBundleMod { get; set; }
        public override string? Licence { get; set; } = "MIT";
    }
}
