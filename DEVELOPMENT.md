# Development Guide for LootingBots 2.0

This guide covers development setup, architecture, and contribution guidelines for LootingBots 2.0 (SPT 4.0).

## Table of Contents
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Development Setup](#development-setup)
- [Architecture Overview](#architecture-overview)
- [Building the Mod](#building-the-mod)
- [Debugging](#debugging)
- [Contributing](#contributing)

## Prerequisites

### Required Software
1. **[.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)** - Required for building both client and server mods
2. **IDE** (choose one):
   - [Visual Studio 2022](https://visualstudio.microsoft.com/vs/community/) (Recommended for Windows)
   - [JetBrains Rider](https://www.jetbrains.com/rider/) (Cross-platform)
   - [VS Code](https://code.visualstudio.com/) with C# Dev Kit extension

3. **Node.js** (optional) - Only needed for npm build scripts

### SPT Reference Assemblies
You'll need DLL files from your SPT installation. Place these in the `/References` folder:

#### For Client Mod (LootingBots)
- `hollowed.dll` - Main game assembly (from `EscapeFromTarkov_Data/Managed/`)
- `spt-common.dll` - SPT common library
- `spt-reflection.dll` - SPT reflection utilities
- `DrakiaXYZ-BigBrain.dll` - BigBrain dependency
- `Comfort.dll`, `DissonanceVoip.dll`, `ItemComponent.Types.dll`
- `Newtonsoft.Json.dll`

These files are referenced in `LootingBots/LootingBots.csproj`:
```xml
<Reference Include="Assembly-CSharp" HintPath="..\References\hollowed.dll" />
<Reference Include="spt-common" HintPath="..\References\spt-common.dll" />
<Reference Include="DrakiaXYZ-BigBrain" HintPath="..\References\DrakiaXYZ-BigBrain.dll" />
```

#### For Server Mod (LootingBotsServerMod)
Server mod uses NuGet packages - no manual references needed:
- `SPTarkov.Common` (4.0.5)
- `SPTarkov.DI` (4.0.5)
- `SPTarkov.Server.Core` (4.0.5)

## Project Structure

```
SPT-LootingBots/
├── LootingBots/                    # Client-side BepInEx plugin (.NET Standard 2.1)
│   ├── LootingBots.csproj
│   ├── LootingBots.cs              # Main plugin class
│   ├── LootingBotsInterop.cs       # Interop API for other mods
│   ├── External.cs                 # External method hooks
│   ├── LootingLayer.cs             # BigBrain custom layer
│   ├── Actions/                    # Bot actions (equip, move, swap)
│   ├── Components/                 # Core components (brain, inventory, etc.)
│   ├── Logic/                      # Looting logic implementations
│   ├── Patches/                    # Harmony patches
│   └── Utilities/                  # Helper classes
│
├── LootingBotsServerMod/          # Server-side mod (.NET 9)
│   ├── LootingBotsServerMod.csproj
│   ├── ModMetadata.cs             # Mod metadata (replaces package.json)
│   ├── PostDBLoad.cs              # Main server logic
│   ├── Models/
│   │   └── ConfigModel.cs         # Configuration data model
│   └── Config/
│       └── config.json            # Mod configuration
│
├── References/                     # SPT/EFT DLL files (not in git)
├── LootingBots.sln                # Visual Studio solution
├── package.json                   # Build scripts
├── README.md
├── MIGRATION_GUIDE.md
├── CHANGELOG.md
└── DEVELOPMENT.md                 # This file
```

## Development Setup

### 1. Clone Repository
```bash
git clone https://github.com/Skwizzy/SPT-LootingBots.git
cd SPT-LootingBots
```

### 2. Create References Folder
```bash
mkdir References
```

### 3. Copy Required DLLs
Copy the required assemblies from your SPT installation to the `References/` folder.

**Client mod references** (from SPT):
- `EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll` → `References/hollowed.dll`
- `BepInEx/plugins/spt-common.dll` → `References/spt-common.dll`
- `BepInEx/plugins/spt-reflection.dll` → `References/spt-reflection.dll`
- `BepInEx/plugins/DrakiaXYZ-BigBrain.dll` → `References/DrakiaXYZ-BigBrain.dll`

### 4. Restore NuGet Packages
```bash
dotnet restore
```

### 5. Build Solution
```bash
# Using npm scripts
npm run build

# Or using dotnet CLI
dotnet build -c Release

# Or open LootingBots.sln in your IDE and build
```

## Architecture Overview

### SPT 4.0 Dependency Injection Pattern

LootingBots 2.0 follows SPT 4.0's dependency injection architecture:

```csharp
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class PostDBLoad(
    DatabaseService databaseService,     // Injected
    ConfigService configService,         // Injected
    ModHelper modHelper,                 // Injected
    ISptLogger<PostDBLoad> logger        // Injected
) : IOnLoad
{
    public Task OnLoad()
    {
        // Your mod logic here
        return Task.CompletedTask;
    }
}
```

**Key Concepts**:
- `[Injectable]` - Marks class for dependency injection
- `TypePriority` - Controls load order (higher = later)
- Constructor injection - Dependencies passed via constructor
- `IOnLoad` - Interface for executing code during server startup

### Load Order

```
OnLoadOrder.PreSptModLoader = 5000
  ↓
OnLoadOrder.PostDBModLoader = 10000
  ↓
PostDBLoad (TypePriority = 10001) ← LootingBots server mod runs here
  ↓
OnLoadOrder.PostSptModLoader = 15000
```

### Client Mod Architecture

The client mod uses BepInEx and integrates with DrakiaXYZ's BigBrain:

```csharp
[BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
[BepInDependency("xyz.drakia.bigbrain", "1.3.2")]
public class LootingBots : BaseUnityPlugin
{
    void Awake()
    {
        // Initialize settings
        // Add custom brain layers
    }
}
```

**Components**:
- **LootingBrain** - Manages bot looting state
- **LootFinder** - Scans for lootable objects
- **ItemAppraiser** - Calculates item values
- **LootingInventoryController** - Handles inventory operations
- **Custom BigBrain Layers** - Integrates with bot AI

## Building the Mod

### Option 1: Using npm Scripts (Recommended)
```bash
# Build everything
npm run build

# Build only client
npm run build:client

# Build only server
npm run build:server

# Clean build artifacts
npm run clean
```

### Option 2: Using dotnet CLI
```bash
# Build everything in Release mode
dotnet build -c Release

# Build specific project
dotnet build LootingBots/LootingBots.csproj -c Release
dotnet build LootingBotsServerMod/LootingBotsServerMod.csproj -c Release

# Clean
dotnet clean
```

### Option 3: Using IDE
- Open `LootingBots.sln` in Visual Studio or Rider
- Select "Release" configuration
- Build > Build Solution (Ctrl+Shift+B)

### Build Output Locations

**Client Mod**:
```
LootingBots/bin/Release/netstandard2.1/skwizzy.LootingBots.dll
```

**Server Mod**:
```
LootingBotsServerMod/bin/Release/20LootingBotsServerMod/LootingBotsServerMod.dll
LootingBotsServerMod/bin/Release/20LootingBotsServerMod/Config/config.json
```

## Debugging

### Debugging Client Mod

1. **Attach to Process**:
   - Launch SPT and start a raid
   - In Visual Studio: Debug > Attach to Process
   - Select `EscapeFromTarkov.exe`
   - Set breakpoints in client code

2. **Enable Debug Logging**:
   ```csharp
   // In LootingBots.cs
   public static ConfigEntry<LogLevel> LootingLogLevels;
   LootingLogLevels = Config.Bind("Loot Finder", "Log Levels", 
       LogLevel.Error | LogLevel.Warning | LogLevel.Info | LogLevel.Debug);
   ```

3. **Use BepInEx Console**:
   - Enable console in `BepInEx/config/BepInEx.cfg`:
     ```ini
     [Logging.Console]
     Enabled = true
     ```

### Debugging Server Mod

1. **Attach to SPT Server**:
   - Launch SPT server
   - In Visual Studio: Debug > Attach to Process
   - Select `SPT.Server.exe` or the dotnet process
   - Set breakpoints in server code

2. **Console Logging**:
   ```csharp
   logger.Info("Debug message");
   logger.Warning("Warning message");
   logger.Error("Error message");
   logger.Debug("Debug message (file only)");
   ```

3. **Check Server Logs**:
   - Location: `SPT_Data/Server/user/logs/`
   - Enable verbose logging in SPT server settings

### Common Issues

**Issue**: "Could not load file or assembly"
- **Solution**: Ensure all reference DLLs are in the `References/` folder

**Issue**: "Type or namespace not found"
- **Solution**: Run `dotnet restore` to restore NuGet packages

**Issue**: Server mod not loading
- **Solution**: Check that `ModMetadata.cs` is correctly configured and dll is in the right output path

## Contributing

### Code Style
- Follow C# naming conventions (PascalCase for public members)
- Use modern C# features (records, init properties, file-scoped namespaces)
- Add XML documentation comments for public APIs
- Keep methods focused and single-responsibility

### Commit Guidelines
- Use descriptive commit messages
- Reference issue numbers when applicable
- Keep commits atomic and focused

### Pull Request Process
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Test thoroughly
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

### Testing
- Test both client and server mods together
- Verify configuration changes work correctly
- Test with various bot types (PMC, Scav, Raiders, etc.)
- Check for performance impacts
- Test compatibility with other popular mods

## Resources

### SPT 4.0 Documentation
- [Server Mod Examples](https://github.com/sp-tarkov/server-mod-examples)
- [SPT Development Discord](https://discord.gg/spt-tarkov)

### Dependency Documentation
- [BigBrain](https://github.com/DrakiaXYZ/SPT-BigBrain) - Custom AI layers
- [BepInEx](https://docs.bepinex.dev/) - Plugin framework
- [Harmony](https://harmony.pardeike.net/) - Runtime patching

### C# Resources
- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Contact

- **GitHub Issues**: https://github.com/Skwizzy/SPT-LootingBots/issues
- **SPT Hub**: https://hub.sp-tarkov.com/

---

Happy modding! 🎮
