using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class SkillBundleBuildServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithoutGeneratedBundle_UsesAuthoredVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-missing-generated");
        WriteSourceBundle(scope, skillBundleVersion: 4);
        var originalDefinition = File.ReadAllText(scope.GetPath("bundle.json"));
        var services = CreateServices();

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        var generatedResult = await services.Reader.ReadAsync(scope.GetPath("generated"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(4, result.Value.Descriptor.SkillBundleVersion);
        Assert.Equal(originalDefinition, File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(4, generatedResult.Value!.Descriptor.SkillBundleVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithMatchingDigestAndVersion_DoesNotRewriteGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-no-op");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var descriptorPath = scope.GetPath("generated/bundle.json");
        File.SetLastWriteTimeUtc(descriptorPath, new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        var sentinelWriteTime = File.GetLastWriteTimeUtc(descriptorPath);

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: true, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.Changed);
        Assert.Equal(initialResult.Value!.Descriptor.BundleDigest, result.Value.Descriptor.BundleDigest);
        Assert.Equal(sentinelWriteTime, File.GetLastWriteTimeUtc(descriptorPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithChangedDigestAtGeneratedVersion_IncrementsVersionAndPublishesBothTrees ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-auto-version");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteSkillTemplate(scope, "Changed source content.\n");

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        var sourceDefinition = await ReadSourceDefinitionAsync(scope, services.Serializer);
        var generatedResult = await services.Reader.ReadAsync(scope.GetPath("generated"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(2, sourceDefinition.SkillBundleVersion);
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(2, generatedResult.Value!.Descriptor.SkillBundleVersion);
        Assert.NotEqual(initialResult.Value!.Descriptor.BundleDigest, generatedResult.Value.Descriptor.BundleDigest);
        Assert.All(generatedResult.Value.Packages, static package => Assert.Equal(2, package.Manifest.SkillBundleVersion));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithChangedCatalogId_IncrementsVersionAndPublishesNewCatalogIdentity ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-catalog-id-change");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var changedCatalogId = new SkillCatalogId("com.mackysoft.agent-skills.changed");
        WriteBundleDefinition(scope, services.Serializer, skillBundleVersion: 1, catalogId: changedCatalogId);

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        var sourceDefinition = await ReadSourceDefinitionAsync(scope, services.Serializer);
        var generatedResult = await services.Reader.ReadAsync(scope.GetPath("generated"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(2, sourceDefinition.SkillBundleVersion);
        Assert.Equal(changedCatalogId, sourceDefinition.CatalogId);
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(2, generatedResult.Value!.Descriptor.SkillBundleVersion);
        Assert.Equal(changedCatalogId, generatedResult.Value.Descriptor.CatalogId);
        Assert.All(generatedResult.Value.Packages, package => Assert.Equal(changedCatalogId, package.Manifest.CatalogId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithChangedDigestAndAuthoredNextVersion_DoesNotIncrementAgain ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-manual-version");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteBundleDefinition(scope, services.Serializer, skillBundleVersion: 2);
        WriteSkillTemplate(scope, "Changed source content.\n");

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        var sourceDefinition = await ReadSourceDefinitionAsync(scope, services.Serializer);
        var generatedResult = await services.Reader.ReadAsync(scope.GetPath("generated"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        Assert.Equal(2, sourceDefinition.SkillBundleVersion);
        Assert.True(generatedResult.IsSuccess, generatedResult.Failure?.Message);
        Assert.Equal(2, generatedResult.Value!.Descriptor.SkillBundleVersion);
        Assert.NotEqual(initialResult.Value!.Descriptor.BundleDigest, generatedResult.Value.Descriptor.BundleDigest);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithVersionStateOutsideReconciliationContract_ReturnsStructuredFailureWithoutWriting (
        int authoredVersion,
        bool changeSource)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", $"build-version-conflict-{authoredVersion}");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteBundleDefinition(scope, services.Serializer, authoredVersion);
        if (changeSource)
        {
            WriteSkillTemplate(scope, "Changed source content.\n");
        }

        var expectedFiles = CaptureFiles(scope.FullPath);

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.BundleVersionConflict, result.Failure!.Code);
        Assert.Equal(expectedFiles, CaptureFiles(scope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_CheckWithRequiredChanges_ReturnsStructuredFailureWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-check-outdated");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteSkillTemplate(scope, "Changed source content.\n");
        var expectedFiles = CaptureFiles(scope.FullPath);

        var result = await services.BuildService.BuildAsync(scope.FullPath, check: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.BundleUpdateRequired, result.Failure!.Code);
        Assert.Equal(expectedFiles, CaptureFiles(scope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenSourcePublicationFails_RestoresPreviousGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-publication-rollback");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteSkillTemplate(scope, "Changed source content.\n");
        var expectedDefinition = File.ReadAllText(scope.GetPath("bundle.json"));
        var expectedGeneratedFiles = CaptureFiles(scope.GetPath("generated"));
        var fileSystem = new SourceWriteFailureFileSystem();
        var failingServices = CreateServices(fileSystem);

        await Assert.ThrowsAsync<IOException>(async () =>
            await failingServices.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None));

        Assert.Equal(expectedDefinition, File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.Equal(expectedGeneratedFiles, CaptureFiles(scope.GetPath("generated")));
        Assert.True(fileSystem.SourceWriteAttempted);
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(scope.FullPath),
            static path => Path.GetFileName(path).StartsWith(".generated.build-backup.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenCancelledAfterGeneratedBackup_RestoresPreviousGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "build-publication-cancel-rollback");
        WriteSourceBundle(scope);
        var services = CreateServices();
        var initialResult = await services.BuildService.BuildAsync(scope.FullPath, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        WriteSkillTemplate(scope, "Changed source content.\n");
        var expectedDefinition = File.ReadAllText(scope.GetPath("bundle.json"));
        var expectedGeneratedFiles = CaptureFiles(scope.GetPath("generated"));
        using var cancellationSource = new CancellationTokenSource();
        var fileSystem = new CancelAfterBackupFileSystem(cancellationSource);
        var cancellingServices = CreateServices(fileSystem);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await cancellingServices.BuildService.BuildAsync(scope.FullPath, check: false, cancellationSource.Token));

        Assert.Equal(expectedDefinition, File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.Equal(expectedGeneratedFiles, CaptureFiles(scope.GetPath("generated")));
        Assert.True(fileSystem.BackupCreated);
        Assert.False(fileSystem.SourceWriteAttempted);
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(scope.FullPath),
            static path => Path.GetFileName(path).StartsWith(".generated.build-backup.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildResult_RejectsMissingDescriptor ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillBundleBuildResult(changed: false, descriptor: null!));
    }

    private static BuildServices CreateServices (ISkillBundleBuildFileSystem? fileSystem = null)
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
        var buildService = fileSystem is null
            ? new SkillBundleBuildService(generationService, reader, writer, serializer)
            : new SkillBundleBuildService(
                generationService,
                reader,
                new SkillBundleBuildPublisher(writer, serializer, fileSystem));
        return new BuildServices(buildService, reader, serializer);
    }

    private static async Task<SkillBundleDefinition> ReadSourceDefinitionAsync (
        TestDirectoryScope scope,
        SkillBundleJsonSerializer serializer)
    {
        var reader = new SkillBundleDefinitionReader(serializer);
        var result = await reader.ReadAsync(scope.FullPath, CancellationToken.None);
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
            "definitions/core/example-skill/skill.json",
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
        SkillCatalogId? catalogId = null)
    {
        scope.WriteFile(
            "bundle.json",
            serializer.SerializeDefinition(new SkillBundleDefinition(
                SkillBundleDefinition.CurrentSchemaVersion,
                catalogId ?? new SkillCatalogId("com.mackysoft.agent-skills.tests"),
                skillBundleVersion)));
    }

    private static void WriteSkillTemplate (
        TestDirectoryScope scope,
        string contents)
    {
        scope.WriteFile("definitions/core/example-skill/SKILL.md.template", contents);
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

    private sealed class SourceWriteFailureFileSystem : ISkillBundleBuildFileSystem
    {
        public bool SourceWriteAttempted { get; private set; }

        public bool DirectoryExists (string path)
        {
            return Directory.Exists(path);
        }

        public void MoveDirectory (
            string sourcePath,
            string destinationPath)
        {
            Directory.Move(sourcePath, destinationPath);
        }

        public void DeleteDirectory (string path)
        {
            Directory.Delete(path, recursive: true);
        }

        public ValueTask WriteSourceBundleAsync (
            string path,
            string contents,
            CancellationToken cancellationToken)
        {
            SourceWriteAttempted = true;
            return ValueTask.FromException(new IOException("Injected source bundle write failure."));
        }
    }

    private sealed class CancelAfterBackupFileSystem : ISkillBundleBuildFileSystem
    {
        private readonly CancellationTokenSource cancellationSource;

        public CancelAfterBackupFileSystem (CancellationTokenSource cancellationSource)
        {
            this.cancellationSource = cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource));
        }

        public bool BackupCreated { get; private set; }

        public bool SourceWriteAttempted { get; private set; }

        public bool DirectoryExists (string path)
        {
            return Directory.Exists(path);
        }

        public void MoveDirectory (
            string sourcePath,
            string destinationPath)
        {
            Directory.Move(sourcePath, destinationPath);
            if (Path.GetFileName(destinationPath).StartsWith(".generated.build-backup.", StringComparison.Ordinal))
            {
                BackupCreated = true;
                cancellationSource.Cancel();
            }
        }

        public void DeleteDirectory (string path)
        {
            Directory.Delete(path, recursive: true);
        }

        public ValueTask WriteSourceBundleAsync (
            string path,
            string contents,
            CancellationToken cancellationToken)
        {
            SourceWriteAttempted = true;
            return ValueTask.FromCanceled(cancellationToken);
        }
    }
}
