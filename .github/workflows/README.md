# GitHub Actions Workflows

This directory contains automated CI/CD workflows for LootingBots.

## Available Workflows

### 1. Build (`build.yml`)
**Triggers**: Push to master/main/develop, tags, pull requests, manual

**Purpose**: Build both client and server mods, create release packages

**Actions**:
- ✅ Checkout code
- ✅ Setup .NET 9 SDK
- ✅ Restore NuGet packages
- ✅ Build client mod (LootingBots)
- ✅ Build server mod (LootingBotsServerMod)
- ✅ Upload build artifacts
- ✅ Create GitHub release (on tag push)

**Artifacts**:
- `LootingBots-Client` - Client mod DLL
- `LootingBots-Server` - Server mod files

**Release Creation**:
Push a tag starting with `v` to automatically create a release:
```bash
git tag v2.0.0
git push origin v2.0.0
```

### 2. PR Check (`pr-check.yml`)
**Triggers**: Pull requests to master/main

**Purpose**: Validate code quality and buildability on pull requests

**Actions**:
- ✅ Code formatting check
- ✅ Build validation
- ✅ Run tests (if available)
- ✅ Security scan
- ✅ Comment on PR with results

### 3. Code Quality (`code-quality.yml`)
**Triggers**: Push to main branches, weekly schedule, manual

**Purpose**: Analyze code quality and dependency health

**Actions**:
- ✅ Run code analysis
- ✅ Check formatting
- ✅ List outdated packages
- ✅ Check for vulnerable dependencies

**Schedule**: Runs weekly on Mondays at 00:00 UTC

### 4. Pre-Release (`prerelease.yml`)
**Triggers**: Push to develop/beta, manual with version input

**Purpose**: Create pre-release builds for testing

**Actions**:
- ✅ Build with custom version
- ✅ Create pre-release package
- ✅ Upload artifacts
- ✅ Create GitHub pre-release (manual only)

**Manual Trigger**:
1. Go to Actions tab
2. Select "Pre-Release Build"
3. Click "Run workflow"
4. Enter version (e.g., `2.0.0-beta.1`)

## Requirements

### Secrets
No secrets required for public repositories. For private repos, ensure `GITHUB_TOKEN` has appropriate permissions.

### Permissions
Workflows require the following permissions:
- `contents: write` - For creating releases
- `pull-requests: write` - For commenting on PRs
- `packages: read` - For NuGet packages

## Build Artifacts

### Client Mod Structure
```
BepInEx/
└── plugins/
    └── skwizzy.LootingBots.dll
```

### Server Mod Structure
```
user/
└── mods/
    └── LootingBotsServerMod-2.0.0/
        ├── LootingBotsServerMod.dll
        └── Config/
            └── config.json
```

### Release Package Contents
```
LootingBots-v2.0.0.zip
├── BepInEx/
│   └── plugins/
│       └── skwizzy.LootingBots.dll
├── user/
│   └── mods/
│       └── LootingBotsServerMod-2.0.0/
├── README.md
├── CHANGELOG.md
├── MIGRATION_GUIDE.md
└── QUICKSTART.md
```

## Usage Examples

### Creating a Release
```bash
# Create and push a tag
git tag v2.0.0 -m "Release version 2.0.0"
git push origin v2.0.0

# GitHub Actions will automatically:
# 1. Build both mods
# 2. Create release package
# 3. Upload to GitHub Releases
```

### Manual Workflow Dispatch
```bash
# Using GitHub CLI
gh workflow run build.yml

# Or via web interface:
# 1. Go to Actions tab
# 2. Select workflow
# 3. Click "Run workflow"
```

### Testing PR Changes
Pull requests automatically trigger build validation:
1. Create PR
2. Wait for checks to complete
3. Review build results in PR comments
4. Fix any issues and push updates

## Troubleshooting

### Build Fails: Missing References
**Problem**: Client mod build fails due to missing SPT/EFT DLLs

**Solution**: 
- Client mod builds may fail on GitHub Actions (requires game DLLs)
- Server mod should build successfully
- Mark client build as `continue-on-error: true`

### NuGet Package Restore Fails
**Problem**: Cannot restore SPTarkov.* packages

**Solution**:
- Ensure NuGet source is configured correctly
- Check package versions are available
- Verify .NET SDK version matches project requirements

### Release Creation Fails
**Problem**: GitHub release not created on tag push

**Solution**:
- Verify tag format starts with `v` (e.g., `v2.0.0`)
- Check `GITHUB_TOKEN` has write permissions
- Review workflow logs for specific errors

### Artifact Upload Size Limit
**Problem**: Artifacts exceed GitHub's size limits

**Solution**:
- Optimize output directories
- Exclude unnecessary files
- Use compression for large binaries

## Local Testing

Test workflows locally using [act](https://github.com/nektos/act):

```bash
# Install act
choco install act-cli

# Test build workflow
act -j build

# Test PR check
act pull_request -j validate

# List available workflows
act -l
```

## Maintenance

### Updating .NET Version
When updating to a new .NET version:
1. Update `dotnet-version` in all workflow files
2. Update project files (.csproj)
3. Test locally before committing

### Updating Dependencies
When updating SPTarkov packages:
1. Update version in .csproj files
2. Test build locally
3. Update workflows if breaking changes
4. Document in CHANGELOG.md

### Adding New Workflows
1. Create new .yml file in `.github/workflows/`
2. Follow existing naming conventions
3. Test with minimal permissions first
4. Document in this README

## Best Practices

### Commit Messages
Use conventional commits for better changelogs:
- `feat:` New features
- `fix:` Bug fixes
- `docs:` Documentation changes
- `ci:` CI/CD changes
- `chore:` Maintenance tasks

### Versioning
Follow semantic versioning:
- `MAJOR.MINOR.PATCH` (e.g., 2.0.0)
- `-beta.N` for pre-releases (e.g., 2.0.0-beta.1)
- `-dev.N` for development builds

### Branch Strategy
- `master/main` - Stable releases
- `develop` - Development work
- `feature/*` - New features
- `bugfix/*` - Bug fixes
- `hotfix/*` - Urgent fixes

## Monitoring

### Build Status
Check build status at: `https://github.com/[owner]/SPT-LootingBots/actions`

### Notifications
Configure GitHub notifications for:
- ❌ Failed builds
- ✅ Successful releases
- 💬 PR comments

### Metrics
Monitor in GitHub Insights:
- Build success rate
- Average build time
- Artifact sizes
- Download statistics

## Support

For workflow issues:
1. Check workflow logs in Actions tab
2. Review this documentation
3. Open issue with `ci:` label
4. Include workflow run URL

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET Build Actions](https://github.com/actions/setup-dotnet)
- [Creating Releases](https://docs.github.com/en/repositories/releasing-projects-on-github)
- [Workflow Syntax](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions)
