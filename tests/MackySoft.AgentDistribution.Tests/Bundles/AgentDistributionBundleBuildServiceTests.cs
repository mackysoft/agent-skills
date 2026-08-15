using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Bundles.Generation;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class AgentDistributionBundleBuildServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WithValidV4MixedSource_IsDeterministicAndCheckDoesNotWrite ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "deterministic-build");
        WriteMixedSource(scope);
        var service = AgentDistributionBundleBuildService.CreateDefault();

        var initialResult = await service.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, CancellationToken.None);
        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        Assert.True(initialResult.Value!.Changed);
        var generatedFiles = CaptureFiles(OutputRoot(scope).Value);
        var checkResult = await service.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: true, CancellationToken.None);

        Assert.True(checkResult.IsSuccess, checkResult.Failure?.Message);
        Assert.False(checkResult.Value!.Changed);
        Assert.Equal(AgentDistributionBundleDescriptor.CurrentSchemaVersion, initialResult.Value.Descriptor.SchemaVersion);
        Assert.Equal(generatedFiles, CaptureFiles(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenOutputIsSymbolicLink_ReturnsPathUnsafe ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "output-link");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-v4", "output-link-outside");
        WriteMixedSource(scope);
        try
        {
            Directory.CreateSymbolicLink(OutputRoot(scope).Value, outside.FullPath);
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
            SourceRoot(scope),
            OutputRoot(scope),
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenAgentReferencesMissingSkill_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "missing-skill");
        WriteMixedSource(scope, dependency: "missing-skill");
        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.False(Directory.Exists(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenHostBindingIsUnknown_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "unknown-host");
        WriteMixedSource(scope, hostId: "unknown");
        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.HostUnsupported, result.Failure!.Code);
        Assert.False(Directory.Exists(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenAgentUsesBuiltInCodexName_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "built-in-codex-name");
        WriteMixedSource(scope, agentName: "worker");

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
            SourceRoot(scope),
            OutputRoot(scope),
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("reserved", result.Failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenSourceContainsUnknownRootEntry_FailsBeforeWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "unknown-root-entry");
        WriteMixedSource(scope);
        scope.WriteFile("source/unknown/config.json", "{}\n");

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
            SourceRoot(scope),
            OutputRoot(scope),
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("unsupported entry", result.Failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_CheckWithMissingOutput_ReturnsUpdateRequiredWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "missing-output-check");
        WriteMixedSource(scope);

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
            SourceRoot(scope),
            OutputRoot(scope),
            check: true,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.BundleUpdateRequired, result.Failure!.Code);
        Assert.False(Directory.Exists(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_CheckWithInvalidOutput_ReturnsManifestInvalidWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "invalid-output-check");
        WriteMixedSource(scope);
        scope.WriteFile("agent-distribution/bundle.json", "{}\n");

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
            SourceRoot(scope),
            OutputRoot(scope),
            check: true,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Equal("{}\n", File.ReadAllText(scope.GetPath("agent-distribution/bundle.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenOutputIsContainedBySource_ReturnsPathUnsafe ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "contained-output");
        WriteMixedSource(scope);
        var containedOutput = AbsolutePath.Parse(scope.GetPath("source/agent-distribution"));

        var result = await AgentDistributionBundleBuildService.CreateDefault().BuildAsync(
            SourceRoot(scope),
            containedOutput,
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(Directory.Exists(containedOutput.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenMissingOutputParentDescendsFromSymbolicLink_ReturnsPathUnsafe ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "output-parent-link");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-v4", "output-parent-link-outside");
        WriteMixedSource(scope);
        var linkedParent = scope.GetPath("linked-output-parent");
        try
        {
            Directory.CreateSymbolicLink(linkedParent, outside.FullPath);
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
            SourceRoot(scope),
            AbsolutePath.Parse(Path.Combine(linkedParent, "missing-parent", "agent-distribution")),
            check: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenSourceFails_PreservesExistingOutput ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "source-failure-preserves-output");
        WriteMixedSource(scope);
        var service = AgentDistributionBundleBuildService.CreateDefault();
        var initial = await service.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, CancellationToken.None);
        Assert.True(initial.IsSuccess, initial.Failure?.Message);
        var expectedFiles = CaptureFiles(OutputRoot(scope).Value);
        WriteMixedSource(scope, dependency: "missing-skill");

        var result = await service.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Equal(expectedFiles, CaptureFiles(OutputRoot(scope).Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_WhenCanceled_PreservesExistingOutput ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-v4", "cancelled-build-preserves-output");
        WriteMixedSource(scope);
        var service = AgentDistributionBundleBuildService.CreateDefault();
        var initial = await service.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, CancellationToken.None);
        Assert.True(initial.IsSuccess, initial.Failure?.Message);
        var expectedFiles = CaptureFiles(OutputRoot(scope).Value);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.BuildAsync(SourceRoot(scope), OutputRoot(scope), check: false, cancellationSource.Token));

        Assert.Equal(expectedFiles, CaptureFiles(OutputRoot(scope).Value));
    }

    private static void WriteMixedSource (
        TestDirectoryScope scope,
        string? dependency = "example-skill",
        string? instructions = null,
        string hostId = "codex",
        string agentName = "architect")
    {
        scope.WriteFile(
            "source/bundle.json",
            """
            {
              "schemaVersion": 4,
              "catalogId": "com.mackysoft.agent-distribution.tests",
              "bundleVersion": 1
            }
            """ + "\n");
        scope.WriteFile(
            "source/skills/core/example-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "displayName": "Example Skill",
              "description": "Provides an example workflow.",
              "dependencies": []
            }
            """);
        scope.WriteFile("source/skills/core/example-skill/SKILL.md.template", "Follow the example workflow.\n");
        scope.WriteFile(
            $"source/agents/{agentName}/agent.json",
            $$"""
            {
              "schemaVersion": 1,
              "displayName": "Architect",
              "description": "Creates an implementation-ready design.",
              "skillDependencies": [{{(dependency is null ? "" : $"\"{dependency}\"")}}]
            }
            """);
        scope.WriteFile(
            $"source/agents/{agentName}/AGENT.md.template",
            instructions ?? "Follow the example workflow before producing the design.\n");
        scope.WriteFile(
            $"source/agents/{agentName}/hosts/{hostId}.json",
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

    private static AbsolutePath SourceRoot (TestDirectoryScope scope) => AbsolutePath.Parse(scope.GetPath("source"));

    private static AbsolutePath OutputRoot (TestDirectoryScope scope) => AbsolutePath.Parse(scope.GetPath("agent-distribution"));

}
