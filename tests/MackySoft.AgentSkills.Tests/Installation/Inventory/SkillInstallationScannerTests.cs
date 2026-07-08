using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Inventory;

public sealed class SkillInstallationScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_ReadsInstalledManifestsFromTargetRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-installed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value!.TargetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, scanResult.Value!.Select(static skill => skill.Identity.SkillName.Value).Order(StringComparer.Ordinal).ToArray());
        Assert.All(scanResult.Value!, skill =>
        {
            Assert.Equal(OpenAiSkillHostAdapter.HostKey, skill.Identity.Host);
            Assert.Equal(installResult.Value.TargetRoot, skill.Identity.TargetRoot);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsSchemaVersionOneManifestWithoutDependenciesAsManifestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-legacy-manifest-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var package = packages[0];
        var legacySerializer = new SkillInstalledManifestLegacyCompatibilitySerializer();
        var legacyManifest = package.Manifest with
        {
            Dependencies = [],
        };
        legacyManifest = legacyManifest with
        {
            ManifestDigest = legacySerializer.ComputeSchemaVersionOneWithoutDependenciesManifestDigest(legacyManifest),
        };
        File.WriteAllText(
            Path.Combine(installResult.Value!.TargetRoot, package.Manifest.SkillName.Value, "agent-skill.json"),
            legacySerializer.SerializeSchemaVersionOneWithoutDependencies(legacyManifest));
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value.TargetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_UsesRequestedScopeInInstalledIdentity ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-user-scope");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(
            packages,
            installResult.Value!.TargetRoot,
            OpenAiSkillHostAdapter.HostKey,
            SkillScopeKind.User,
            CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.All(scanResult.Value!, static skill => Assert.Equal(SkillScopeKind.User, skill.Identity.Scope));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-unsupported-host");
        var scanner = SkillTestData.CreateInstallationScanner();

        var result = await scanner.ScanAsync(Array.Empty<CanonicalSkillPackage>(), scope.FullPath, "generic", cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_ReturnsInputInvalid_WhenScopeIsUndefined ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-undefined-scope");
        var scanner = SkillTestData.CreateInstallationScanner();

        var result = await scanner.ScanAsync(
            Array.Empty<CanonicalSkillPackage>(),
            scope.FullPath,
            OpenAiSkillHostAdapter.HostKey,
            (SkillScopeKind)42,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Equal(SkillFailureCategory.InvalidInput, SkillFailureClassifier.Classify(result.Failure));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsInvalidManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-invalid-manifest");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/sample-skill/agent-skill.json", "{}");
        var scanner = SkillTestData.CreateInstallationScanner();

        var result = await scanner.ScanAsync(packages, targetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsManifestWhoseSkillNameDoesNotMatchDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-directory-mismatch");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var manifest = packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/not-the-skill/agent-skill.json", manifest);
        var scanner = SkillTestData.CreateInstallationScanner();

        var result = await scanner.ScanAsync(packages, targetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsSkillMaterializedForDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value!.TargetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsModifiedInstalledSkillBody ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-body-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.AppendAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md"), "\nInjected instruction.\n");
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value.TargetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsUnexpectedInstalledFile ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.WriteAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "references", "extra.md"), "# Extra\n");
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value.TargetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsSameCatalogSkillOutsideCanonicalPackageSetAsUnmanaged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-external-managed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var serializer = new SkillManifestJsonSerializer();
        var externalManifest = packages[0].Manifest with
        {
            SkillName = new SkillName("external-skill"),
        };
        externalManifest = SkillTestData.WithComputedManifestDigest(externalManifest);
        scope.WriteFile(".agents/skills/external-skill/agent-skill.json", serializer.Serialize(externalManifest));
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, targetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsForeignCatalogSkillOutsideCanonicalPackageSetAsUnmanaged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-foreign-managed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var serializer = new SkillManifestJsonSerializer();
        var externalManifest = packages[0].Manifest with
        {
            CatalogId = new SkillCatalogId("com.example.foreign-skills"),
            SkillName = new SkillName("external-skill"),
        };
        externalManifest = SkillTestData.WithComputedManifestDigest(externalManifest);
        scope.WriteFile(".agents/skills/external-skill/agent-skill.json", serializer.Serialize(externalManifest));
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, targetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsMalformedManagedManifestBeforePackageLookup ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-external-malformed");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var serializer = new SkillManifestJsonSerializer();
        var externalManifest = packages[0].Manifest with
        {
            SkillName = new SkillName("external-skill"),
            HostArtifacts = Array.Empty<SkillHostArtifactManifest>(),
        };
        externalManifest = SkillTestData.WithComputedManifestDigest(externalManifest);
        scope.WriteFile(".agents/skills/external-skill/agent-skill.json", serializer.Serialize(externalManifest));
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, targetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_IgnoresNestedStrayManifestOutsideSkillDirectories ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "scan-nested-stray");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        scope.WriteFile(Path.Combine(".agents", "skills", "unmanaged", "nested", "agent-skill.json"), "{}");
        var scanner = SkillTestData.CreateInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value!.TargetRoot, OpenAiSkillHostAdapter.HostKey, cancellationToken: CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, scanResult.Value!.Count);
    }
}
