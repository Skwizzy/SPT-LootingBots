# Migration Guide: SPT 3.x to SPT 4.0

## What Changed in Version 2.0.0

### Major Changes

1. **Server Mod Architecture**
   - **Old (SPT 3.x)**: TypeScript server mod using Node.js
   - **New (SPT 4.0)**: C# server mod using .NET 9
   - The server mod is now a compiled C# assembly instead of TypeScript files

2. **Dependency Injection**
   - SPT 4.0 adopts full dependency injection architecture
   - Services are injected via constructor parameters
   - Uses `[Injectable]` attribute with `TypePriority` for load ordering

3. **API Changes**
   - `DatabaseServer` → `DatabaseService`
   - `ConfigServer` → `ConfigService`
   - New semantic versioning with `SemanticVersioning.Version` and `SemanticVersioning.Range`
   - Config types now use enums: `ConfigTypes.PMC`, `ConfigTypes.BOT`

4. **Module Metadata**
   - Replaced `package.json` with C# `ModMetadata` class
   - Must use `ModGuid` (unique identifier) instead of simple mod name
   - Version must be `SemanticVersioning.Version` format

### File Structure Changes

#### Old Structure (SPT 3.x)
```
LootingBots-ServerMod/
├── package.json          # Mod metadata
├── src/
│   ├── mod.ts           # Main TypeScript file
│   └── enums.ts
├── config/
│   └── config.json
└── types/               # TypeScript definitions
```

#### New Structure (SPT 4.0)
```
LootingBotsServerMod/
├── LootingBotsServerMod.csproj    # C# project file
├── ModMetadata.cs                  # Mod metadata (replaces package.json)
├── PostDBLoad.cs                   # Main logic
├── Models/
│   └── ConfigModel.cs
└── Config/
    └── config.json
```

### Configuration Changes

**Config Property Names**: Now use PascalCase (C# convention)
- `pmcSpawnWithLoot` → `PmcSpawnWithLoot`
- `scavSpawnWithLoot` → `ScavSpawnWithLoot`

### Build Process Changes

#### Old Build (SPT 3.x)
```bash
npm run build  # Compiled TypeScript to JavaScript
```

#### New Build (SPT 4.0)
```bash
npm run build  # Now builds both C# client and server mods
# Or individually:
npm run build:client  # dotnet build for client
npm run build:server  # dotnet build for server
```

### Client Mod Changes

- **Version**: Updated to 2.0.0
- **Target Framework**: Still uses netstandard2.1 (Unity compatibility)
- **Dependencies**: No breaking changes to BigBrain dependency
- All client-side logic remains compatible

### Installation Changes

**Old Installation**:
1. Extract `BepInEx/plugins/` folder
2. Extract `user/mods/Skwizzy-LootingBots-ServerMod/` (TypeScript files)

**New Installation**:
1. Extract `BepInEx/plugins/` folder (same as before)
2. Extract `user/mods/LootingBotsServerMod-2.0.0/` (compiled DLL)

## Breaking Changes

### For Mod Users
- **Must update to SPT 4.0** - Version 2.0.0 is NOT compatible with SPT 3.x
- Config file location: `user/mods/LootingBotsServerMod-2.0.0/Config/config.json`
- Property names in config.json now use PascalCase

### For Mod Developers (using LootingBotsInterop)
- No breaking changes to interop interface
- Same methods: `TryForceBotToScanLoot()`, etc.
- Client mod API remains stable

## Upgrade Steps

1. **Uninstall Old Version**
   - Remove `BepInEx/plugins/skwizzy.LootingBots.dll`
   - Remove `user/mods/Skwizzy-LootingBots-ServerMod/`

2. **Install New Version**
   - Extract new package to SPT root directory
   - Update config file with new property names if you had custom settings

3. **Update Configuration**
   ```json
   // Old (3.x)
   {
     "pmcSpawnWithLoot": false,
     "scavSpawnWithLoot": true
   }
   
   // New (4.0)
   {
     "PmcSpawnWithLoot": false,
     "ScavSpawnWithLoot": true
   }
   ```

## Building from Source

### Prerequisites
- **.NET 9 SDK**: https://dotnet.microsoft.com/download/dotnet/9.0
- **Visual Studio 2022** or **Rider** (recommended)
- SPT 4.0 reference assemblies

### Build Steps
```bash
# Install dependencies
npm install

# Build everything
npm run build

# Or build individually
npm run build:client  # Builds LootingBots client mod
npm run build:server  # Builds LootingBotsServerMod
```

## FAQ

**Q: Will my old config work?**
A: You need to update property names to PascalCase, but functionality remains the same.

**Q: Can I use version 2.0.0 with SPT 3.x?**
A: No, version 2.0.0 requires SPT 4.0+. Use version 1.x for SPT 3.x.

**Q: Do I need to reinstall BigBrain?**
A: Make sure you have the SPT 4.0 compatible version of BigBrain.

**Q: Where did the TypeScript code go?**
A: The server mod is now written in C# for better performance and to match SPT 4.0's new architecture. The old TypeScript code is preserved in the `LootingBots-ServerMod/` folder for reference.

## Support

For issues, please check:
- [GitHub Issues](https://github.com/Skwizzy/SPT-LootingBots/issues)
- [SPT Hub Forums](https://hub.sp-tarkov.com/)

Make sure to specify you're using version 2.0.0 and SPT 4.0 when reporting issues.
