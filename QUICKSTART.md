# Quick Start Guide

## For Users

### Installation

1. **Download** the latest release from [Releases](https://github.com/Skwizzy/SPT-LootingBots/releases)

2. **Extract** the archive to your SPT root directory:
   ```
   SPT/
   ├── BepInEx/
   │   └── plugins/
   │       └── skwizzy.LootingBots.dll    ← Client mod
   └── user/
       └── mods/
           └── LootingBotsServerMod-2.0.0/  ← Server mod
               ├── LootingBotsServerMod.dll
               └── Config/
                   └── config.json
   ```

3. **Configure** (optional):
   - Edit `user/mods/LootingBotsServerMod-2.0.0/Config/config.json`
   - Use F12 in-game to adjust client settings

4. **Start SPT** and enjoy enhanced bot looting!

### Configuration

#### Server Settings (`Config/config.json`)
```json
{
  "PmcSpawnWithLoot": false,  // PMCs spawn without loot
  "ScavSpawnWithLoot": true   // Scavs spawn with loot
}
```

#### Client Settings (F12 in-game)
- **Loot Finder**: Enable/disable looting for different bot types
- **Detection Distances**: How far bots can detect loot
- **Timing**: Delays and scan intervals
- **Value Thresholds**: Minimum/maximum item values to loot

---

## For Developers

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 or Rider
- SPT 4.0 installation

### Quick Build
```bash
# Clone repository
git clone https://github.com/Skwizzy/SPT-LootingBots.git
cd SPT-LootingBots

# Copy references (see DEVELOPMENT.md for details)
# Place SPT DLLs in References/ folder

# Restore packages
dotnet restore

# Build
npm run build
# or
dotnet build -c Release
```

### Output
- **Client**: `LootingBots/bin/Release/netstandard2.1/skwizzy.LootingBots.dll`
- **Server**: `LootingBotsServerMod/bin/Release/20LootingBotsServerMod/`

---

## Upgrading from SPT 3.x

See [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md) for detailed instructions.

**TL;DR**:
1. Remove old version completely
2. Install new version 2.0.0
3. Update config property names (camelCase → PascalCase)
4. Done!

---

## Troubleshooting

### "Mod not loading"
- ✅ Check you're using SPT 4.0+
- ✅ Verify BigBrain is installed and up-to-date
- ✅ Check server logs: `SPT_Data/Server/user/logs/`

### "Config changes not applying"
- ✅ Ensure correct config path: `user/mods/LootingBotsServerMod-2.0.0/Config/config.json`
- ✅ Use PascalCase property names (`PmcSpawnWithLoot`, not `pmcSpawnWithLoot`)
- ✅ Restart SPT server after config changes

### "Bots not looting"
- ✅ Enable looting in F12 menu for the bot type
- ✅ Check detection distances are reasonable (10-50m)
- ✅ Verify BigBrain is working (check other BigBrain mods)
- ✅ Check console for error messages

### "Build errors"
- ✅ Install .NET 9 SDK
- ✅ Copy all required DLLs to `References/` folder
- ✅ Run `dotnet restore`

---

## Support

- **Issues**: [GitHub Issues](https://github.com/Skwizzy/SPT-LootingBots/issues)
- **Discord**: [SPT Discord](https://discord.gg/spt-tarkov)
- **Hub**: [SPT Hub Forums](https://hub.sp-tarkov.com/)

When reporting issues, include:
- SPT version
- LootingBots version
- Error messages from logs
- Steps to reproduce

---

## Quick Links

- 📖 [Full README](README.md)
- 🔄 [Migration Guide](MIGRATION_GUIDE.md)
- 📝 [Changelog](CHANGELOG.md)
- 💻 [Development Guide](DEVELOPMENT.md)
- 🐛 [Report Issue](https://github.com/Skwizzy/SPT-LootingBots/issues)

---

**Enjoy more immersive bot AI!** 🎮
