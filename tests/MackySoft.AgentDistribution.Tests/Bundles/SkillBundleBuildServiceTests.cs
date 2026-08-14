using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class SkillBundleBuildServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithoutGeneratedBundle_UsesAuthoredVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-missing-generated");
        WriteSourceBundle(scope, skillBundleVersion: 4);
        var originalDefinition = File.ReadAllText(scope.GetPath("source/bundle.json"));
        var services = CreateServices();

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        var generatedResult = await services.Reader.ReadAsync(OutputRoot(scope), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(4, result.Value.Descriptor.SkillBundleVersion.Value);
        Assert.Equal(originalDefinition, File.ReadAllText(scope.GetPath("source/bundle.json")));
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(4, generatedResult.Value!.Descriptor.SkillBundleVersion.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithMatchingDigestAndVersion_DoesNotRewriteGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-no-op");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var descriptorPath = scope.GetPath("agent-distribution/bundle.json");
        File.SetLastWriteTimeUtc(descriptorPath, new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        var sentinelWriteTime = File.GetLastWriteTimeUtc(descriptorPath);

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: true, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.Changed);
        Assert.Equal(initialResult.Value!.Descriptor.BundleDigest, result.Value.Descriptor.BundleDigest);
        Assert.Equal(sentinelWriteTime, File.GetLastWriteTimeUtc(descriptorPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithChangedContent_PreservesAuthoredVersionAndPublishesGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-preserve-version");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var originalDefinition = File.ReadAllText(scope.GetPath("source/bundle.json"));
        WriteSkillTemplate(scope, "Changed source content.\n");

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        var sourceDefinition = await ReadSourceDefinitionAsync(scope, services.Serializer);
        var generatedResult = await services.Reader.ReadAsync(OutputRoot(scope), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(1, sourceDefinition.SkillBundleVersion.Value);
        Assert.Equal(originalDefinition, File.ReadAllText(scope.GetPath("source/bundle.json")));
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(1, generatedResult.Value!.Descriptor.SkillBundleVersion.Value);
        Assert.NotEqual(initialResult.Value!.Descriptor.BundleDigest, generatedResult.Value.Descriptor.BundleDigest);
        Assert.All(generatedResult.Value.Packages, static package => Assert.Equal(1, package.Manifest.SkillBundleVersion.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_PublishesNestedScriptsIntoCanonicalBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-scripts");
        WriteSourceBundle(scope);
        WriteScript(scope, "bench/collect.sh", "#!/bin/sh\necho collect\n");
        var services = CreateServices();

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        var generatedResult = await services.Reader.ReadAsync(OutputRoot(scope), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(
            "#!/bin/sh\necho collect\n",
            File.ReadAllText(Path.Combine(OutputRoot(scope).Value, "example-skill", "scripts", "bench", "collect.sh")));
        Assert.Contains(
            generatedResult.Value!.Packages[0].Files,
            static file => string.Equals(file.RelativePath.Value, "scripts/bench/collect.sh", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithChangedCatalogId_PreservesVersionAndPublishesNewCatalogIdentity ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-catalog-id-change");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var changedCatalogId = new AgentDistributionCatalogId("com.mackysoft.agent-distribution.changed");
        WriteBundleDefinition(scope, services.Serializer, skillBundleVersion: 1, catalogId: changedCatalogId);

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        var sourceDefinition = await ReadSourceDefinitionAsync(scope, services.Serializer);
        var generatedResult = await services.Reader.ReadAsync(OutputRoot(scope), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(1, sourceDefinition.SkillBundleVersion.Value);
        Assert.Equal(changedCatalogId, sourceDefinition.CatalogId);
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(1, generatedResult.Value!.Descriptor.SkillBundleVersion.Value);
        Assert.Equal(changedCatalogId, generatedResult.Value.Descriptor.CatalogId);
        Assert.All(generatedResult.Value.Packages, package => Assert.Equal(changedCatalogId, package.Manifest.CatalogId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithChangedDigestAndAuthoredNextVersion_DoesNotIncrementAgain ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-manual-version");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteBundleDefinition(scope, services.Serializer, skillBundleVersion: 2);
        WriteSkillTemplate(scope, "Changed source content.\n");

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        var sourceDefinition = await ReadSourceDefinitionAsync(scope, services.Serializer);
        var generatedResult = await services.Reader.ReadAsync(OutputRoot(scope), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(2, sourceDefinition.SkillBundleVersion.Value);
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(2, generatedResult.Value!.Descriptor.SkillBundleVersion.Value);
        Assert.NotEqual(initialResult.Value!.Descriptor.BundleDigest, generatedResult.Value.Descriptor.BundleDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_CheckWithRequiredChanges_ReturnsStructuredFailureWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "build-check-outdated");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationToken: CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteSkillTemplate(scope, "Changed source content.\n");
        var expectedFiles = CaptureFiles(scope.FullPath);

        var result = await services.BuildService.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: true, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.BundleUpdateRequired, result.Failure!.Code);
        Assert.Equal(expectedFiles, CaptureFiles(scope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildResult_RejectsMissingDescriptor ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillBundleBuildResult(changed: false, descriptor: null!));
    }

    private static BuildServices CreateServices ()
    {
        var serializer = new SkillBundleJsonSerializer();
        var bundleDigestCalculator = new SkillBundleDigestCalculator(new SkillManifestJsonSerializer());
        var bundleFactory = new CanonicalSkillBundle.Factory(bundleDigestCalculator);
        var reader = new CanonicalSkillBundleReader(
            SkillTestData.CreatePackageReader(),
            serializer,
            bundleFactory);
        var writer = new CanonicalSkillBundleWriter(
            SkillTestData.CreateCanonicalPackageWriter(),
            serializer,
            reader);
        var generationService = SkillTestData.CreatePackageGenerationService();
        var buildService = new SkillBundleBuildService(generationService, reader, writer);
        return new BuildServices(buildService, reader, serializer);
    }

    private static async Task<SkillBundleDefinition> ReadSourceDefinitionAsync (
        TestDirectoryScope scope,
        SkillBundleJsonSerializer serializer)
    {
        var reader = new SkillBundleDefinitionReader(serializer);
        var result = await reader.ReadAsync(SourceRoot(scope), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    private static void WriteSourceBundle (
        TestDirectoryScope scope,
        int skillBundleVersion = 1)
    {
        var serializer = new SkillBundleJsonSerializer();
        WriteBundleDefinition(scope, serializer, skillBundleVersion);
        scope.WriteFile(
            "source/definitions/core/example-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "displayName": "Example Skill",
              "description": "Use when testing bundle build reconciliation.",
              "dependencies": []
            }
            """);
        WriteSkillTemplate(scope, "Original source content.\n");
    }

    private static void WriteBundleDefinition (
        TestDirectoryScope scope,
        SkillBundleJsonSerializer serializer,
        int skillBundleVersion,
        AgentDistributionCatalogId? catalogId = null)
    {
        scope.WriteFile(
            "source/bundle.json",
            serializer.SerializeDefinition(new SkillBundleDefinition(
                SkillBundleDefinition.CurrentSchemaVersion,
                catalogId ?? new AgentDistributionCatalogId("com.mackysoft.agent-distribution.tests"),
                new SkillBundleVersion(skillBundleVersion))));
    }

    private static void WriteSkillTemplate (
        TestDirectoryScope scope,
        string contents)
    {
        scope.WriteFile("source/definitions/core/example-skill/SKILL.md.template", contents);
    }

    private static void WriteScript (
        TestDirectoryScope scope,
        string relativePath,
        string contents)
    {
        scope.WriteFile(Path.Combine("source", "definitions", "core", "example-skill", "scripts", relativePath), contents);
    }

    private static IReadOnlyDictionary<string, string> CaptureFiles (string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                File.ReadAllText,
                StringComparer.Ordinal);
    }

    private static AbsolutePath SourceRoot (TestDirectoryScope scope) => AbsolutePath.Parse(scope.GetPath("source"));

    private static AbsolutePath OutputRoot (TestDirectoryScope scope) => AbsolutePath.Parse(scope.GetPath("agent-distribution"));

    private sealed class BuildServices
    {
        public BuildServices (
            SkillBundleBuildService buildService,
            CanonicalSkillBundleReader reader,
            SkillBundleJsonSerializer serializer)
        {
            BuildService = buildService;
            Reader = reader;
            Serializer = serializer;
        }

        public SkillBundleBuildService BuildService { get; }

        public CanonicalSkillBundleReader Reader { get; }

        public SkillBundleJsonSerializer Serializer { get; }
    }

}
