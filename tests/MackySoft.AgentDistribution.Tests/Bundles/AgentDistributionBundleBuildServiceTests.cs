using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class AgentDistributionBundleBuildServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithValidMixedSource_IsDeterministicAndCheckDoesNotWrite ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "deterministic-build");
        WriteMixedSource(scope);
        var service = AgentDistributionBundleBuildService.CreateDefault();

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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "next-version");
        WriteMixedSource(scope);
        var serializer = new AgentDistributionBundleJsonSerializer();

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
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
    public async Task BuildAsync_WithNonPositiveVersion_ReturnsInputFailureWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "invalid-version");
        WriteMixedSource(scope);

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
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
    public async Task BuildAsync_WhenGeneratedOutputIsSymbolicLink_ReturnsPathUnsafe ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "generated-link");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-v3", "generated-link-outside");
        WriteMixedSource(scope);
        try
        {
            Directory.CreateSymbolicLink(scope.GetPath("generated"), outside.FullPath);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
            scope.FullPath,
            bundleVersion: null,
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenAgentReferencesMissingSkill_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "missing-skill");
        WriteMixedSource(scope, dependency: "missing-skill");
        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(scope.FullPath, null, check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenHostBindingIsUnknown_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "unknown-host");
        WriteMixedSource(scope, hostId: "unknown");
        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(scope.FullPath, null, check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
        Assert.False(Directory.Exists(scope.GetPath("generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenAgentUsesBuiltInCodexName_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "built-in-codex-name");
        WriteMixedSource(scope, agentName: "worker");

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "unknown-definition-namespace");
        WriteMixedSource(scope);
        scope.WriteFile("definitions/tools/config.json", "{}\n");

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
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
              "schemaVersion": 3,
              "catalogId": "com.mackysoft.agent-distribution.tests",
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
            $"definitions/agents/{agentName}/agent.json",
            $$"""
            {
              "schemaVersion": 1,
              "displayName": "Architect",
              "description": "Creates an implementation-ready design.",
              "skillDependencies": [{{(dependency is null ? "" : $"\"{dependency}\"")}}]
            }
            """);
        scope.WriteFile(
            $"definitions/agents/{agentName}/AGENT.md.template",
            instructions ?? "Follow the example workflow before producing the design.\n");
        scope.WriteFile(
            $"definitions/agents/{agentName}/hosts/{hostId}.json",
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

}
