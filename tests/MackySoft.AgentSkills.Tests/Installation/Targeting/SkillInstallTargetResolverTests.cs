using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Inventory;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Targeting;

public sealed class SkillCatalogTargetRootSelectorTests
{
    private static readonly SkillCatalogId CatalogId = new("com.mackysoft.agent-skills.tests");

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(HostKind.Codex, ".agents/skills/com.mackysoft.agent-skills.tests")]
    [InlineData(HostKind.ClaudeCode, ".claude/skills")]
    [InlineData(HostKind.GitHubCopilot, ".github/skills/com.mackysoft.agent-skills.tests")]
    public async Task SelectTargetAsync_ProjectScope_AppliesHostCatalogLayout (
        HostKind host,
        string expectedRelativePath)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-project-default");
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(host, SkillScopeKind.Project, scope.FullPath),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(scope.FullPath, expectedRelativePath)), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_UserScope_UsesCodexHomeWhenPresent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-codex-home");
        var codexHome = scope.GetPath("codex-home");
        var selector = CreateSelector(scope.GetPath("home"), codexHome);

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.User, null),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(codexHome, "skills", CatalogId.Value)), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_UserScope_FallsBackToCodexDirectoryUnderHome ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-home-fallback");
        var home = scope.GetPath("home");
        var selector = CreateSelector(home);

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.User, null),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(home, ".codex", "skills", CatalogId.Value)), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_UserScope_UsesClaudeHomeDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-claude-home");
        var home = scope.GetPath("home");
        var selector = CreateSelector(home);

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.ClaudeCode, SkillScopeKind.User, null),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(home, ".claude", "skills")), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_UserScope_UsesCopilotHomeDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-copilot-home");
        var home = scope.GetPath("home");
        var selector = CreateSelector(home);

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.GitHubCopilot, SkillScopeKind.User, null),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(home, ".copilot", "skills", CatalogId.Value)), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_UserScope_RejectsRelativeCodexHome ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-relative-codex-home");
        var selector = CreateSelector(scope.GetPath("home"), "relative-codex-home");

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.User, null),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.UserTargetUnavailable, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_ProjectScopeWithExplicitTargetRoot_UsesExactRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-project-explicit");
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath, "custom-skills"),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(scope.GetPath("custom-skills")), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_UserScopeWithExplicitTargetRoot_UsesExactRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-explicit");
        var selector = CreateSelector(scope.GetPath("home"));
        var explicitTargetRoot = scope.GetPath("custom-skills");

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.GitHubCopilot, SkillScopeKind.User, null, explicitTargetRoot),
            CatalogId,
            [],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(explicitTargetRoot), result.Value!.TargetRoot.Value);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(HostKind.Codex, SkillScopeKind.Project)]
    [InlineData(HostKind.Codex, SkillScopeKind.User)]
    [InlineData(HostKind.GitHubCopilot, SkillScopeKind.Project)]
    [InlineData(HostKind.GitHubCopilot, SkillScopeKind.User)]
    public async Task SelectTargetAsync_DefaultTarget_UsesCompatibleFlatCatalogRoot (
        HostKind host,
        SkillScopeKind scopeKind)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-compatible-flat");
        var home = scope.GetPath("home");
        var codexHome = scope.GetPath("codex-home");
        var compatibleRoot = GetFlatTargetRoot(scope, host, scopeKind, home, codexHome);
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        await InstallAtExplicitTargetAsync(scope, host, scopeKind, compatibleRoot, packages);
        var selector = CreateSelector(home, codexHome);

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(
                host,
                scopeKind,
                scopeKind == SkillScopeKind.Project ? scope.FullPath : null),
            packages[0].Manifest.CatalogId,
            [new SkillName("new-skill")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(compatibleRoot), result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_DefaultTarget_RejectsSplitCatalogRoots ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-split-catalog");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var compatibleRoot = scope.GetPath(Path.Combine(".agents", "skills"));
        var preferredRoot = Path.Combine(compatibleRoot, packages[0].Manifest.CatalogId.Value);
        await InstallAtExplicitTargetAsync(scope, HostKind.Codex, SkillScopeKind.Project, compatibleRoot, [packages[0]]);
        await InstallAtExplicitTargetAsync(scope, HostKind.Codex, SkillScopeKind.Project, preferredRoot, [packages[1]]);
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            packages[0].Manifest.CatalogId,
            [packages[0].Manifest.SkillName],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetRootConflict, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(HostKind.Codex, SkillScopeKind.Project)]
    [InlineData(HostKind.Codex, SkillScopeKind.User)]
    [InlineData(HostKind.GitHubCopilot, SkillScopeKind.Project)]
    [InlineData(HostKind.GitHubCopilot, SkillScopeKind.User)]
    public async Task SelectTargetAsync_DefaultTarget_RejectsSkillNameOwnedBySiblingCatalog (
        HostKind host,
        SkillScopeKind scopeKind)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-sibling-catalog-collision");
        var home = scope.GetPath("home");
        var codexHome = scope.GetPath("codex-home");
        var hostRoot = GetFlatTargetRoot(scope, host, scopeKind, home, codexHome);
        var skillName = new SkillName("shared-skill");
        var foreignSkillDirectory = Path.Combine(hostRoot, "com.example.foreign-skills", skillName.Value);
        Directory.CreateDirectory(foreignSkillDirectory);
        var selector = CreateSelector(home, codexHome);

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(
                host,
                scopeKind,
                scopeKind == SkillScopeKind.Project ? scope.FullPath : null),
            CatalogId,
            [skillName],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
        Assert.False(Directory.Exists(Path.Combine(hostRoot, CatalogId.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_DefaultTarget_RejectsCatalogDirectoryOccupiedByFlatSkill ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-catalog-root-occupied-by-flat-skill");
        var catalogId = new SkillCatalogId("occupied-skill");
        var occupiedRoot = scope.CreateDirectory(Path.Combine(".agents", "skills", catalogId.Value));
        File.WriteAllText(Path.Combine(occupiedRoot, "SKILL.md"), "# Existing flat skill\n");
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            catalogId,
            [new SkillName("new-skill")],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetRootConflict, result.Failure!.Code);
        Assert.False(Directory.Exists(Path.Combine(occupiedRoot, "new-skill")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_DefaultTarget_IgnoresUnrelatedSiblingSymlinkOutsideHostRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-unrelated-sibling-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "target-unrelated-sibling-symlink-outside");
        var hostRoot = scope.CreateDirectory(Path.Combine(".agents", "skills"));
        Directory.CreateSymbolicLink(Path.Combine(hostRoot, "external-catalog"), outsideScope.FullPath);
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CatalogId,
            [new SkillName("new-skill")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            ResolveExpectedPath(Path.Combine(hostRoot, CatalogId.Value)),
            result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_DefaultTarget_RejectsSelectedSkillUnderSiblingSymlinkOutsideHostRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-selected-sibling-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "target-selected-sibling-symlink-outside");
        var hostRoot = scope.CreateDirectory(Path.Combine(".agents", "skills"));
        var skillName = new SkillName("new-skill");
        outsideScope.CreateDirectory(skillName.Value);
        Directory.CreateSymbolicLink(Path.Combine(hostRoot, "external-catalog"), outsideScope.FullPath);
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CatalogId,
            [skillName],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(Directory.Exists(Path.Combine(hostRoot, CatalogId.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_DefaultTarget_DoesNotTreatInRootCatalogAliasAsSiblingCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-in-root-catalog-alias");
        var hostRoot = scope.CreateDirectory(Path.Combine(".agents", "skills"));
        var realCatalogRoot = scope.CreateDirectory(Path.Combine(".agents", "skills", "real-catalog"));
        var catalogId = new SkillCatalogId("shared-name");
        var skillName = new SkillName(catalogId.Value);
        Directory.CreateDirectory(Path.Combine(realCatalogRoot, skillName.Value));
        Directory.CreateSymbolicLink(
            Path.Combine(hostRoot, catalogId.Value),
            realCatalogRoot);
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            catalogId,
            [skillName],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            ResolveExpectedPath(Path.Combine(hostRoot, catalogId.Value)),
            result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_DefaultTarget_DoesNotTreatNestedDirectoryInFlatSkillAsSiblingCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-flat-skill-nested-directory");
        var hostRoot = scope.GetPath(Path.Combine(".agents", "skills"));
        var flatSkillRoot = Path.Combine(hostRoot, "existing-skill");
        Directory.CreateDirectory(Path.Combine(flatSkillRoot, "references"));
        File.WriteAllText(Path.Combine(flatSkillRoot, "SKILL.md"), "# Existing skill\n");
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CatalogId,
            [new SkillName("references")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            ResolveExpectedPath(Path.Combine(hostRoot, CatalogId.Value)),
            result.Value!.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SelectTargetAsync_ExplicitTarget_DoesNotSelectCompatibleCatalogRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-explicit-skips-compatible");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var compatibleRoot = scope.GetPath(Path.Combine(".agents", "skills"));
        await InstallAtExplicitTargetAsync(scope, HostKind.Codex, SkillScopeKind.Project, compatibleRoot, packages);
        var explicitRoot = scope.GetPath("custom-skills");
        var selector = CreateSelector(scope.GetPath("home"));

        var result = await selector.SelectTargetAsync(
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath, explicitRoot),
            packages[0].Manifest.CatalogId,
            packages.Select(static package => package.Manifest.SkillName).ToArray(),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(explicitRoot), result.Value!.TargetRoot.Value);
    }

    private static SkillCatalogTargetRootSelector CreateSelector (
        string homeDirectory,
        string? codexHome = null)
    {
        var targetResolver = new SkillInstallTargetResolver(
            new SkillUserTargetRootResolver(
                () => homeDirectory,
                name => string.Equals(name, "CODEX_HOME", StringComparison.Ordinal) ? codexHome : null));
        return new SkillCatalogTargetRootSelector(
            targetResolver,
            SkillTestData.CreateInstalledManifestReader());
    }

    private static string ResolveExpectedPath (string path)
    {
        return AbsolutePath.Parse(Path.GetFullPath(path)).Value;
    }

    private static string GetFlatTargetRoot (
        TestDirectoryScope scope,
        HostKind host,
        SkillScopeKind scopeKind,
        string home,
        string codexHome)
    {
        return (host, scopeKind) switch
        {
            (HostKind.Codex, SkillScopeKind.Project) => scope.GetPath(Path.Combine(".agents", "skills")),
            (HostKind.Codex, SkillScopeKind.User) => Path.Combine(codexHome, "skills"),
            (HostKind.GitHubCopilot, SkillScopeKind.Project) => scope.GetPath(Path.Combine(".github", "skills")),
            (HostKind.GitHubCopilot, SkillScopeKind.User) => Path.Combine(home, ".copilot", "skills"),
            _ => throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported compatibility test host."),
        };
    }

    private static async Task InstallAtExplicitTargetAsync (
        TestDirectoryScope scope,
        HostKind host,
        SkillScopeKind scopeKind,
        string targetRoot,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var installResult = await SkillTestData.CreateInstallService().InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(
                host,
                scopeKind,
                scopeKind == SkillScopeKind.Project ? scope.FullPath : null,
                targetRoot),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
    }

}
