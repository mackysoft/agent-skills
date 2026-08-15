using System.Text.Json.Nodes;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;

namespace MackySoft.AgentDistribution.Tests.Installation.Inventory;

public sealed class SkillInstallationScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_ReadsInstalledManifestsFromTargetRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-installed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(installResult.Value!.TargetRoot.Value), CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, scanResult.Value!.Select(static skill => skill.Identity.SkillName.Value).Order(StringComparer.Ordinal).ToArray());
        Assert.All(scanResult.Value!, skill =>
        {
            Assert.Equal(HostKind.Codex, skill.Identity.Host);
            Assert.Equal(installResult.Value.TargetRoot.Value, skill.Identity.TargetRoot.Value);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsUnsupportedSchemaVersionManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-legacy-manifest-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var package = packages[0];
        var manifestPath = Path.Combine(installResult.Value!.TargetRoot.Value, package.Manifest.SkillName.Value, "agent-skill.json");
        var unsupportedSchemaVersionText = File.ReadAllText(manifestPath)
            .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 0", StringComparison.Ordinal);
        File.WriteAllText(manifestPath, unsupportedSchemaVersionText);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(installResult.Value.TargetRoot.Value), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_UsesRequestedScopeInInstalledIdentity ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-user-scope");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(
            packages,
            ResolveTarget(installResult.Value!.TargetRoot.Value, scope: SkillScopeKind.User),
            CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.All(scanResult.Value!, static skill => Assert.Equal(SkillScopeKind.User, skill.Identity.Scope));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsInvalidManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-invalid-manifest");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/sample-skill/agent-skill.json", "{}");
        var scanner = SkillTestData.CreateInstallationScanner();

        var result = await scanner.ScanAsync(packages, ResolveTarget(targetRoot), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsManifestWhoseSkillNameDoesNotMatchDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-directory-mismatch");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var manifestPath = PackageRelativePath.Parse("agent-skill.json");
        var manifest = packages[0].Files.Single(file => file.RelativePath.Equals(manifestPath)).Content;
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/not-the-skill/agent-skill.json", manifest);
        var scanner = SkillTestData.CreateInstallationScanner();

        var result = await scanner.ScanAsync(packages, ResolveTarget(targetRoot), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsSkillMaterializedForDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.ClaudeCode, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(installResult.Value!.TargetRoot.Value), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetHostConflict, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsModifiedInstalledSkillBody ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-body-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.AppendAllText(Path.Combine(installResult.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md"), "\nInjected instruction.\n");
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(installResult.Value.TargetRoot.Value), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetContentDigestMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsUnexpectedInstalledFile ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.WriteAllText(Path.Combine(installResult.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "references", "extra.md"), "# Extra\n");
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(installResult.Value.TargetRoot.Value), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetFileSetMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsSameCatalogSkillOutsideCanonicalPackageSetAsUnmanaged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-external-managed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var serializer = new SkillManifestJsonSerializer();
        var externalManifestCandidate = SkillTestData.CopyManifest(
            packages[0].Manifest,
            skillName: new SkillName("external-skill"));
        var externalManifest = SkillTestData.WithComputedManifestDigest(externalManifestCandidate);
        scope.WriteFile(".agents/skills/external-skill/agent-skill.json", serializer.Serialize(externalManifest));
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(targetRoot), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetUnmanaged, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsForeignCatalogSkillOutsideCanonicalPackageSetAsUnmanaged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-foreign-managed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var serializer = new SkillManifestJsonSerializer();
        var externalManifestCandidate = SkillTestData.CopyManifest(
            packages[0].Manifest,
            catalogId: new AgentDistributionCatalogId("com.example.foreign-skills"),
            skillName: new SkillName("external-skill"));
        var externalManifest = SkillTestData.WithComputedManifestDigest(externalManifestCandidate);
        scope.WriteFile(".agents/skills/external-skill/agent-skill.json", serializer.Serialize(externalManifest));
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(targetRoot), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetUnmanaged, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsMalformedManagedManifestBeforePackageLookup ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-external-malformed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var serializer = new SkillManifestJsonSerializer();
        var externalManifestCandidate = SkillTestData.CopyManifest(
            packages[0].Manifest,
            skillName: new SkillName("external-skill"));
        var externalManifest = SkillTestData.WithComputedManifestDigest(externalManifestCandidate);
        var manifestJson = JsonNode.Parse(serializer.Serialize(externalManifest))!.AsObject();
        manifestJson["hostArtifacts"] = new JsonArray();
        scope.WriteFile(".agents/skills/external-skill/agent-skill.json", manifestJson.ToJsonString());
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(targetRoot), CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_IgnoresNestedStrayManifestOutsideSkillDirectories ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "scan-nested-stray");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        scope.WriteFile(Path.Combine(".agents", "skills", "unmanaged", "nested", "agent-skill.json"), "{}");
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, ResolveTarget(installResult.Value!.TargetRoot.Value), CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, scanResult.Value!.Count);
    }
    private static SkillResolvedInstallTarget ResolveTarget (
        string targetRoot,
        HostKind host = HostKind.Codex,
        SkillScopeKind scope = SkillScopeKind.Project)
    {
        var absoluteTargetRoot = AbsolutePath.Parse(Path.GetFullPath(targetRoot));
        var request = new SkillInstallRequest(
            host,
            scope,
            scope == SkillScopeKind.Project ? absoluteTargetRoot : null,
            absoluteTargetRoot);
        var result = SkillTestData.CreateInstallTargetResolver().ResolveTarget(
            request,
            new AgentDistributionCatalogId("com.mackysoft.agent-distribution"));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }
}
