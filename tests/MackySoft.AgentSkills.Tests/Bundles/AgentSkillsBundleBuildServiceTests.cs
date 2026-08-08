using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class AgentSkillsBundleBuildServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithValidMixedSource_IsDeterministicAndCheckDoesNotWrite ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "deterministic-build");
        WriteMixedSource(scope);
        var service = AgentSkillsBundleBuildService.CreateDefault();

        var initialResult = await service.BuildAsync(scope.FullPath, null, check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        Assert.True(initialResult.Value!.Changed);
        var generatedFiles = CaptureFiles(scope.GetPath("generated"));
        var checkResult = await service.BuildAsync(scope.FullPath, null, check: true, CancellationToken.None);

        Assert.True(checkResult.IsSuccess, checkResult.Failure?.Message);
        Assert.False(checkResult.Value!.Changed);
        Assert.Equal(generatedFiles, CaptureFiles(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithExplicitNextVersion_PublishesMatchingSourceAndGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "next-version");
        WriteMixedSource(scope);
        var serializer = new AgentSkillsBundleJsonSerializer();

        var result = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(
            scope.FullPath,
            bundleVersion: 2,
            check: false,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.Changed);
        var sourceDefinition = serializer.DeserializeDefinition(File.ReadAllText(scope.GetPath("bundle.json")));
        var generatedDescriptor = serializer.DeserializeDescriptor(File.ReadAllText(scope.GetPath("generated/bundle.json")));
        Assert.Equal(2, sourceDefinition.BundleVersion.Value);
        Assert.Equal(sourceDefinition.BundleVersion, generatedDescriptor.BundleVersion);
        Assert.Equal(result.Value.Descriptor.BundleDigest, generatedDescriptor.BundleDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenSourcePublicationFails_RestoresPreviousGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "publication-rollback");
        WriteMixedSource(scope);
        var initialResult = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(
            scope.FullPath,
            bundleVersion: null,
            check: false,
            CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var expectedDefinition = File.ReadAllText(scope.GetPath("bundle.json"));
        var expectedGeneratedFiles = CaptureFiles(scope.GetPath("generated"));
        var fileSystem = new SourceWriteFailureFileSystem();

        await Assert.ThrowsAsync<IOException>(async () =>
            await AgentSkillsBundleBuildService.Create(fileSystem).BuildAsync(
                scope.FullPath,
                bundleVersion: 2,
                check: false,
                CancellationToken.None));

        Assert.Equal(expectedDefinition, File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.Equal(expectedGeneratedFiles, CaptureFiles(scope.GetPath("generated")));
        Assert.True(fileSystem.SourceWriteAttempted);
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(scope.FullPath),
            static path => Path.GetFileName(path).StartsWith(".generated.build-backup.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithNonPositiveVersion_ReturnsInputFailureWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "invalid-version");
        WriteMixedSource(scope);

        var result = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(
            scope.FullPath,
            bundleVersion: 0,
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenAgentReferencesMissingSkill_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "missing-skill");
        WriteMixedSource(scope, dependency: "missing-skill");
        var result = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(scope.FullPath, null, check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenHostBindingIsUnknown_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "unknown-host");
        WriteMixedSource(scope, hostId: "unknown");
        var result = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(scope.FullPath, null, check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenAgentUsesBuiltInCodexName_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "built-in-codex-name");
        WriteMixedSource(scope, agentName: "worker");

        var result = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(
            scope.FullPath,
            null,
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("reserved", result.Failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenDefinitionsContainsUnknownNamespace_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "unknown-definition-namespace");
        WriteMixedSource(scope);
        scope.WriteFile("definitions/tools/config.json", "{}\n");

        var result = await AgentSkillsBundleBuildService.CreateDefault().BuildAsync(
            scope.FullPath,
            null,
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("unsupported entry", result.Failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    private static void WriteMixedSource (
        TestDirectoryScope scope,
        string? dependency = "example-skill",
        string? instructions = null,
        string hostId = "codex",
        string agentName = "architect")
    {
        scope.WriteFile(
            "bundle.json",
            """
            {
              "schemaVersion": 2,
              "catalogId": "com.mackysoft.agent-skills.tests",
              "bundleVersion": 1
            }
            """ + "\n");
        scope.WriteFile(
            "definitions/skills/core/example-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "displayName": "Example Skill",
              "description": "Provides an example workflow.",
              "dependencies": []
            }
            """);
        scope.WriteFile("definitions/skills/core/example-skill/SKILL.md.template", "Follow the example workflow.\n");
        scope.WriteFile(
            $"definitions/agents/core/{agentName}/agent.json",
            $$"""
            {
              "schemaVersion": 1,
              "displayName": "Architect",
              "description": "Creates an implementation-ready design.",
              "skillDependencies": [{{(dependency is null ? "" : $"\"{dependency}\"")}}]
            }
            """);
        scope.WriteFile(
            $"definitions/agents/core/{agentName}/AGENT.md.template",
            instructions ?? "Follow the example workflow before producing the design.\n");
        scope.WriteFile(
            $"definitions/agents/core/{agentName}/hosts/{hostId}.json",
            """
            {
              "schemaVersion": 1,
              "model": "gpt-5.6-terra",
              "reasoningEffort": "high",
              "sandboxMode": "workspace-write"
            }
            """);
    }

    private static IReadOnlyDictionary<string, string> CaptureFiles (string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
    }

    private sealed class SourceWriteFailureFileSystem : ISkillBundleBuildFileSystem
    {
        public bool SourceWriteAttempted { get; private set; }

        public bool DirectoryExists (AbsolutePath path)
        {
            return Directory.Exists(path.Value);
        }

        public void MoveDirectory (
            AbsolutePath sourcePath,
            AbsolutePath destinationPath)
        {
            Directory.Move(sourcePath.Value, destinationPath.Value);
        }

        public void DeleteDirectory (AbsolutePath path)
        {
            Directory.Delete(path.Value, recursive: true);
        }

        public ValueTask WriteSourceBundleAsync (
            AbsolutePath path,
            string contents,
            CancellationToken cancellationToken)
        {
            SourceWriteAttempted = true;
            return ValueTask.FromException(new IOException("Injected source bundle write failure."));
        }
    }
}
