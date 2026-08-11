using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Agents.Installation.State;

public sealed class AgentInstalledTargetInspectorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenStateAndArtifactAreAbsent_ReturnsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-missing");

        var result = await CreateInspector().InspectAsync(CreateManifest("com.example.catalog"), ResolveProjectTarget(scope.FullPath));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.Missing, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenStateAndArtifactMatch_ReturnsCurrent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-current");
        var target = ResolveProjectTarget(scope.FullPath);
        var manifest = CreateManifest("com.example.catalog");
        var content = "name = \"architect\"\n";
        Directory.CreateDirectory(target.ArtifactRoot.Value);
        await File.WriteAllTextAsync(Path.Combine(target.ArtifactRoot.Value, "architect.toml"), content);
        await WriteStateAsync(target, manifest, content);

        var result = await CreateInspector().InspectAsync(manifest, target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.Current, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenManagedArtifactChanged_ReturnsLocallyModified ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-modified");
        var target = ResolveProjectTarget(scope.FullPath);
        var manifest = CreateManifest("com.example.catalog");
        Directory.CreateDirectory(target.ArtifactRoot.Value);
        await File.WriteAllTextAsync(Path.Combine(target.ArtifactRoot.Value, "architect.toml"), "changed\n");
        await WriteStateAsync(target, manifest, "original\n");

        var result = await CreateInspector().InspectAsync(manifest, target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.LocallyModified, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenArtifactExistsWithoutState_ReturnsUnmanaged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-unmanaged");
        var target = ResolveProjectTarget(scope.FullPath);
        Directory.CreateDirectory(target.ArtifactRoot.Value);
        await File.WriteAllTextAsync(Path.Combine(target.ArtifactRoot.Value, "architect.toml"), "unmanaged\n");

        var result = await CreateInspector().InspectAsync(CreateManifest("com.example.catalog"), target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.Unmanaged, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenNestedHostArtifactExistsWithoutState_ReturnsUnmanaged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-unmanaged-nested");
        var target = ResolveProjectTarget(scope.FullPath);
        Directory.CreateDirectory(Path.Combine(target.ArtifactRoot.Value, "profiles"));
        await File.WriteAllTextAsync(Path.Combine(target.ArtifactRoot.Value, "profiles", "architect.toml"), "unmanaged\n");
        var manifest = CreateManifest("com.example.catalog", "hosts/codex/profiles/architect.toml");

        var result = await CreateInspector().InspectAsync(manifest, target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.Unmanaged, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenManagedStatePathsDoNotMatchHostArtifacts_ReturnsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-invalid-artifact-path");
        var target = ResolveProjectTarget(scope.FullPath);
        var manifest = CreateManifest("com.example.catalog", "hosts/codex/profiles/architect.toml");
        Directory.CreateDirectory(target.ArtifactRoot.Value);
        await File.WriteAllTextAsync(Path.Combine(target.ArtifactRoot.Value, "architect.toml"), "original\n");
        await WriteStateAsync(target, manifest, "original\n");

        var result = await CreateInspector().InspectAsync(manifest, target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.Invalid, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenStateIsNotCanonical_ReturnsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-invalid");
        var target = ResolveProjectTarget(scope.FullPath);
        var manifest = CreateManifest("com.example.catalog");
        var statePathResult = new AgentInstallationStatePathResolver().Resolve(target, manifest.CatalogId, manifest.AgentName);
        Assert.True(statePathResult.IsSuccess, statePathResult.Failure?.Message);
        Directory.CreateDirectory(Path.GetDirectoryName(statePathResult.Value!.Value)!);
        await File.WriteAllTextAsync(statePathResult.Value.Value, "{}\n");

        var result = await CreateInspector().InspectAsync(manifest, target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.Invalid, result.Value!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InspectAsync_WhenStateBelongsToAnotherCatalog_ReturnsOtherCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "inspect-foreign");
        var target = ResolveProjectTarget(scope.FullPath);
        var requested = CreateManifest("com.example.catalog");
        var foreign = CreateManifest("com.other.catalog");
        Directory.CreateDirectory(target.ArtifactRoot.Value);
        await File.WriteAllTextAsync(Path.Combine(target.ArtifactRoot.Value, "architect.toml"), "foreign\n");
        await WriteStateAsync(target, foreign, "foreign\n");

        var result = await CreateInspector().InspectAsync(requested, target);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentInstalledTargetStateKind.OtherCatalog, result.Value!.Kind);
    }

    private static AgentInstalledTargetInspector CreateInspector ()
    {
        var serializer = new AgentInstallationStateJsonSerializer();
        return new AgentInstalledTargetInspector(
            new AgentInstallationStatePathResolver(),
            new AgentInstallationStateStore(serializer),
            new PackageContentDigestCalculator());
    }

    private static AgentResolvedTarget ResolveProjectTarget (string repositoryRoot)
    {
        var resolver = new AgentInstallTargetResolver(
            new AgentUserTargetRootResolver(() => repositoryRoot, _ => null));
        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, repositoryRoot));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    private static async Task WriteStateAsync (AgentResolvedTarget target, AgentManifest manifest, string content)
    {
        var digestCalculator = new PackageContentDigestCalculator();
        var state = new AgentInstallationState(
            AgentInstallationState.CurrentSchemaVersion,
            manifest.BundleVersion,
            manifest.CatalogId,
            target.HostId,
            manifest.AgentName,
            manifest.ManifestDigest,
            [new AgentInstalledArtifact(
                PackageRelativePath.Parse("architect.toml"),
                digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("architect.toml"), content))]);
        var statePathResult = new AgentInstallationStatePathResolver().Resolve(target, manifest.CatalogId, manifest.AgentName);
        Assert.True(statePathResult.IsSuccess, statePathResult.Failure?.Message);
        var writeResult = await new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer()).WriteAsync(statePathResult.Value!, state);
        Assert.True(writeResult.IsSuccess, writeResult.Failure?.Message);
    }

    private static AgentManifest CreateManifest (string catalogId, string hostArtifactPath = "hosts/codex/architect.toml")
    {
        var digest = Sha256Digest.Parse(new string('a', 64));
        return new AgentManifest(
            AgentManifest.CurrentSchemaVersion,
            new AgentDistributionBundleVersion(1),
            new AgentDistributionCatalogId(catalogId),
            new AgentName("architect"),
            "Architect",
            "Creates a design.",
            Array.Empty<SkillName>(),
            digest,
            digest,
            [new AgentHostArtifactManifest(HostKind.Codex, PackageRelativePath.Parse(hostArtifactPath), digest)]);
    }
}
