# Changelog

All notable changes to LootingBots will be documented in this file.

## [2.0.0] - 2025-11-28

### ⚠️ Breaking Changes
- **Complete rewrite for SPT 4.0** - NOT backwards compatible with SPT 3.x
- Server mod migrated from TypeScript to C# .NET 9
- Config property names now use PascalCase (C# convention)
- Requires .NET 9 SDK for building from source

### Added
- C# server mod implementation using SPT 4.0 architecture
- Full dependency injection support
- Improved error handling and logging
- Migration guide for upgrading from SPT 3.x
- Support for SPT 4.0.5 NuGet packages

### Changed
- **Server Mod**: Complete rewrite from TypeScript to C#
  - Now uses `DatabaseService` instead of `DatabaseServer`
  - Now uses `ConfigService` with `ConfigTypes` enum
  - Implements `IOnLoad` interface with `[Injectable]` attribute
  - Uses `ModHelper` for file operations
  - Improved type safety with C# strong typing

- **Mod Metadata**: Replaced package.json with C# ModMetadata class
  - Uses `ModGuid` for unique identification
  - Uses `SemanticVersioning.Version` and `SemanticVersioning.Range`
  - All metadata properties use `init` accessors

- **Configuration**:
  - `pmcSpawnWithLoot` → `PmcSpawnWithLoot`
  - `scavSpawnWithLoot` → `ScavSpawnWithLoot`
  - Config file path: `Config/config.json` (capital C)

- **Build Process**:
  - Server mod now builds with `dotnet build` instead of TypeScript compiler
  - Output path follows SPT 4.0 conventions
  - Unified build scripts for both client and server mods

- **Project Structure**:
  - Uses .NET 9 Web SDK for server mod
  - Proper output path configuration for mod loading
  - Config file automatically copied to output directory

### Technical Details

#### API Changes
```csharp
// Old (SPT 3.x TypeScript)
databaseServer.getTables()
configServer.getConfig()

// New (SPT 4.0 C#)
databaseService.GetTables()
configService.GetConfig(ConfigTypes.PMC)
```

#### Dependency Injection Pattern
```csharp
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class PostDBLoad(
    DatabaseService databaseService,
    ConfigService configService,
    ModHelper modHelper,
    ISptLogger<PostDBLoad> logger
) : IOnLoad
```

### Removed
- TypeScript server mod implementation (preserved in `LootingBots-ServerMod/` for reference)
- Node.js dependencies and package build scripts
- TypeScript type definitions

### Fixed
- Improved null safety with C# nullable reference types
- Better error handling during config loading
- More reliable weight clearing for bot inventory

### Migration
See [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md) for detailed upgrade instructions.

---

## [1.6.1] - Previous Version (SPT 3.x)

Last version supporting SPT 3.10 and earlier. See previous releases for changelog.

### Client Mod Changes
- No breaking changes
- All looting logic remains unchanged
- Compatible with BigBrain 1.3.2+

### Server Mod Changes
- TypeScript implementation
- Node.js based
- Compatible with SPT 3.x API

---

## Version Compatibility Matrix

| LootingBots Version | SPT Version | Server Mod Type | .NET Version Required |
|---------------------|-------------|-----------------|----------------------|
| 2.0.0+              | 4.0.x       | C# (.NET 9)     | .NET 9 SDK           |
| 1.6.1               | 3.10.x      | TypeScript      | N/A                  |
| 1.6.0               | 3.9.x       | TypeScript      | N/A                  |
| 1.5.x               | 3.8.x       | TypeScript      | N/A                  |

---

## Development Notes

### For Modders
- The client mod (BepInEx plugin) remains largely unchanged
- LootingBotsInterop interface is preserved for compatibility
- All existing mod integrations should continue to work

### For Contributors
- Development now requires .NET 9 SDK
- Recommended IDEs: Visual Studio 2022, Rider, or VS Code with C# extension
- Server mod debugging is easier with C# tooling
- Type safety improvements reduce runtime errors

### Performance
- C# compilation provides better runtime performance
- Reduced memory overhead vs. Node.js TypeScript
- Faster server startup times

---

## Feedback and Support

If you encounter issues with version 2.0.0:
1. Check the [Migration Guide](MIGRATION_GUIDE.md)
2. Verify you're running SPT 4.0+
3. Ensure .NET 9 runtime is installed (if building from source)
4. Report issues on [GitHub](https://github.com/Skwizzy/SPT-LootingBots/issues)

---

## Credits

- **Original Author**: Skwizzy
- **SPT Team**: For the excellent SPT 4.0 architecture and examples
- **Community**: For testing and feedback

Thank you to everyone who contributed to making this migration possible!
