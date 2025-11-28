# Contributing to LootingBots

First off, thank you for considering contributing to LootingBots! It's people like you that make this mod better for everyone.

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Commit Messages](#commit-messages)

## Code of Conduct

This project and everyone participating in it is governed by common sense and respect. By participating, you are expected to uphold this standard. Please report unacceptable behavior to the repository maintainers.

### Our Standards

**Positive behavior includes:**
- Being respectful of differing viewpoints and experiences
- Gracefully accepting constructive criticism
- Focusing on what is best for the community
- Showing empathy towards other community members

**Unacceptable behavior includes:**
- Trolling, insulting/derogatory comments, and personal attacks
- Public or private harassment
- Publishing others' private information without permission
- Other conduct which could reasonably be considered inappropriate

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues as you might find that you don't need to create one. When you create a bug report, include as many details as possible using the bug report template.

**Good bug reports include:**
- A clear and descriptive title
- Exact steps to reproduce the problem
- Expected behavior vs. actual behavior
- Screenshots or logs if applicable
- Your SPT version and LootingBots version
- List of other installed mods

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:
- Use a clear and descriptive title
- Provide a detailed description of the proposed feature
- Explain why this enhancement would be useful
- List any alternative solutions you've considered

### Your First Code Contribution

Unsure where to begin? You can start by looking through issues labeled:
- `good first issue` - Simple issues that are good for newcomers
- `help wanted` - Issues that need assistance

### Pull Requests

1. **Fork the repository** and create your branch from `master`
2. **Make your changes** following our coding standards
3. **Test your changes** thoroughly
4. **Update documentation** if needed
5. **Submit a pull request** with a clear description

## Development Setup

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022, Rider, or VS Code with C# extension
- SPT 4.0 installation for testing

### Setup Steps

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/SPT-LootingBots.git
   cd SPT-LootingBots
   ```

2. **Copy Reference DLLs**
   - Create `References/` folder
   - Copy required DLLs from your SPT installation (see DEVELOPMENT.md)

3. **Restore Packages**
   ```bash
   dotnet restore
   ```

4. **Build**
   ```bash
   dotnet build -c Debug
   ```

5. **Test**
   - Copy built DLLs to your SPT installation
   - Launch SPT and test your changes
   - Check logs for errors

For detailed setup instructions, see [DEVELOPMENT.md](DEVELOPMENT.md).

## Pull Request Process

### Before Submitting

1. **Update your fork** with the latest changes from `master`
   ```bash
   git checkout master
   git pull upstream master
   ```

2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes** and commit them with clear messages

4. **Test thoroughly**
   - Build succeeds without errors
   - All existing functionality still works
   - New features work as expected
   - No new warnings or errors in logs

5. **Update documentation** if needed
   - Update README.md for user-facing changes
   - Update DEVELOPMENT.md for dev-facing changes
   - Add to CHANGELOG.md if significant

### Submitting the PR

1. **Push your changes**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create Pull Request** on GitHub
   - Use the PR template
   - Link related issues
   - Describe your changes clearly
   - Add screenshots/videos if applicable

3. **Wait for review**
   - Address any feedback
   - Keep the PR updated with master
   - Be patient and respectful

### PR Review Process

- Maintainers will review your PR within a few days
- They may request changes or ask questions
- Once approved, your PR will be merged
- Your contribution will be credited in the release notes

## Coding Standards

### C# Style Guide

Follow standard C# conventions and the existing codebase style:

**Naming Conventions:**
```csharp
// PascalCase for public members
public class LootingBrain { }
public void ProcessLoot() { }

// camelCase for private fields with underscore prefix
private readonly BotOwner _botOwner;

// PascalCase for properties
public bool IsLooting { get; set; }

// UPPER_CASE for constants
private const string MOD_GUID = "com.skwizzy.lootingbots";
```

**Code Organization:**
```csharp
// Use file-scoped namespaces (C# 10+)
namespace LootingBots.Components;

// Use modern C# features
public record ConfigModel
{
    public bool PmcSpawnWithLoot { get; init; } = false;
}
```

**Comments:**
```csharp
/// <summary>
/// XML documentation for public APIs
/// </summary>
/// <param name="botOwner">The bot to process</param>
public void ProcessBot(BotOwner botOwner)
{
    // Inline comments for complex logic
    // Keep them concise and meaningful
}
```

### Project Structure

- **Client Mod** (`LootingBots/`): BepInEx plugin code
  - `Actions/` - Bot actions
  - `Components/` - Core components
  - `Logic/` - Looting logic
  - `Patches/` - Harmony patches
  - `Utilities/` - Helper classes

- **Server Mod** (`LootingBotsServerMod/`): Server-side code
  - `Models/` - Data models
  - `Config/` - Configuration files

### Testing

While we don't have automated tests yet, please manually test:
- [ ] Build succeeds without errors
- [ ] Server starts without errors
- [ ] Client mod loads in-game
- [ ] Configuration changes work
- [ ] Bot looting behavior functions correctly
- [ ] No errors in logs

**Test with:**
- Different bot types (PMC, Scav, Raiders)
- Different maps
- Different configurations
- Other popular mods installed

## Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Types:**
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `style:` Code style changes (formatting, etc.)
- `refactor:` Code refactoring
- `perf:` Performance improvements
- `test:` Adding or updating tests
- `chore:` Maintenance tasks
- `ci:` CI/CD changes

**Examples:**
```bash
feat(server): add configuration for loot value thresholds
fix(client): prevent bots from looting through walls
docs: update migration guide for SPT 4.0
refactor(looting): simplify item value calculation
```

**Scopes:**
- `client` - Client mod changes
- `server` - Server mod changes
- `config` - Configuration changes
- `docs` - Documentation
- `build` - Build system
- `ci` - CI/CD

## Branch Naming

Use descriptive branch names:
- `feature/add-loot-priority-system`
- `bugfix/fix-inventory-overflow`
- `hotfix/critical-crash-fix`
- `docs/update-readme`
- `refactor/simplify-looting-logic`

## Questions?

- Check [DEVELOPMENT.md](DEVELOPMENT.md) for technical details
- Check [README.md](README.md) for user documentation
- Open a [Question issue](https://github.com/Skwizzy/SPT-LootingBots/issues/new?template=question.yml)
- Join the SPT Discord for community help

## Recognition

Contributors will be:
- Listed in release notes
- Mentioned in CHANGELOG.md
- Added to the Contributors section (if significant contribution)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

**Thank you for contributing to LootingBots!** 🎉

Your contributions help make the mod better for everyone in the SPT community.
