using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Agents.Sources;

public sealed class AgentSourceDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenFlatAgentDefinitionIsValid_ReturnsDefinition ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "flat-definition");
        WriteDefinition(scope);

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var definition = Assert.Single(result.Value!);
        Assert.Equal("architect", definition.Metadata.AgentName.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentIsNestedBelowCategory_ReturnsSourceInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "categorized-definition");
        WriteDefinition(scope, agentDirectory: "orchestration/architect");

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentsNamespaceIsAbsent_ReturnsEmptySnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "absent-namespace");

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.GetPath("agents")), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentsNamespaceIsDanglingSymbolicLink_ReturnsSourceInvalid ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "dangling-namespace");
        var agentsRoot = scope.GetPath("agents");
        if (!TryCreateDirectorySymbolicLink(agentsRoot, scope.GetPath("missing-target")))
        {
            return;
        }

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(agentsRoot), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenHostsDirectoryIsEmpty_ReturnsSourceInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "empty-hosts");
        WriteDefinition(scope, writeHostBinding: false);

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Theory]
    [InlineData("agent.json")]
    [InlineData("AGENT.md.template")]
    [InlineData("hosts/codex.json")]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAuthoredFileIsSymbolicLink_ReturnsSourceInvalid (string relativeFile)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "linked-file");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-agents", "linked-file-outside");
        WriteDefinition(scope);
        var linkPath = scope.GetPath(Path.Combine("architect", relativeFile));
        File.Delete(linkPath);
        var targetPath = outsideScope.WriteFile("outside.txt", GetReplacementContent(relativeFile));
        if (!TryCreateFileSymbolicLink(linkPath, targetPath))
        {
            return;
        }

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentDirectoryIsSymbolicLink_ReturnsSourceInvalid ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "linked-agent");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-agents", "linked-agent-outside");
        outsideScope.CreateDirectory("architect");
        if (!TryCreateDirectorySymbolicLink(scope.GetPath("architect"), outsideScope.GetPath("architect")))
        {
            return;
        }

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentDirectoryContainsUnexpectedNode_ReturnsSourceInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "unexpected-node");
        WriteDefinition(scope);
        scope.WriteFile("architect/notes.txt", "not part of the authored layout\n");

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenHostBindingIsNotDefined_ReturnsHostUnsupported ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "unsupported-host-binding");
        WriteDefinition(scope, writeHostBinding: false);
        scope.WriteFile("architect/hosts/unknown.json", CreateCodexBinding());

        var result = await CreateReader().ReadAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    private static AgentSourceDefinitionReader CreateReader ()
    {
        return new AgentSourceDefinitionReader();
    }

    private static void WriteDefinition (
        TestDirectoryScope scope,
        bool writeHostBinding = true,
        string agentDirectory = "architect")
    {
        scope.WriteFile(
            $"{agentDirectory}/agent.json",
            """
            {
              "schemaVersion": 1,
              "displayName": "Architect",
              "description": "Creates an implementation-ready design.",
              "skillDependencies": []
            }
            """);
        scope.WriteFile($"{agentDirectory}/AGENT.md.template", "Create an implementation-ready design.\n");
        scope.CreateDirectory($"{agentDirectory}/hosts");
        if (writeHostBinding)
        {
            scope.WriteFile($"{agentDirectory}/hosts/codex.json", CreateCodexBinding());
        }
    }

    private static string GetReplacementContent (string relativeFile)
    {
        return relativeFile switch
        {
            "agent.json" => """
                {
                  "schemaVersion": 1,
                  "displayName": "Architect",
                  "description": "Creates an implementation-ready design.",
                  "skillDependencies": []
                }
                """,
            "AGENT.md.template" => "Create an implementation-ready design.\n",
            _ => CreateCodexBinding(),
        };
    }

    private static string CreateCodexBinding ()
    {
        return """
            {
              "schemaVersion": 1,
              "model": "gpt-5.6-terra",
              "reasoningEffort": "high",
              "sandboxMode": "workspace-write"
            }
            """;
    }

    private static bool TryCreateFileSymbolicLink (
        string linkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink (
        string linkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
