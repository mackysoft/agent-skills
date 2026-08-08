using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Agents.Sources;

public sealed class AgentSourceDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentsNamespaceIsAbsent_ReturnsEmptySnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "absent-namespace");

        var result = await CreateReader().ReadAllAsync(scope.GetPath("agents"), CancellationToken.None);

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

        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "dangling-namespace");
        var agentsRoot = scope.GetPath("agents");
        if (!TryCreateDirectorySymbolicLink(agentsRoot, scope.GetPath("missing-target")))
        {
            return;
        }

        var result = await CreateReader().ReadAllAsync(agentsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Theory]
    [InlineData("empty-category")]
    [InlineData("empty-hosts")]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenRequiredDirectoryIsEmpty_ReturnsSourceInvalid (string scenario)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", scenario);
        if (scenario == "empty-category")
        {
            scope.CreateDirectory("core");
        }
        else
        {
            WriteDefinition(scope, writeHostBinding: false);
        }

        var result = await CreateReader().ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Theory]
    [InlineData("agent.json")]
    [InlineData("AGENT.md.template")]
    [InlineData("hosts/openai.json")]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAuthoredFileIsSymbolicLink_ReturnsSourceInvalid (string relativeFile)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "linked-file");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-agents", "linked-file-outside");
        WriteDefinition(scope);
        var linkPath = scope.GetPath(Path.Combine("core/architect", relativeFile));
        File.Delete(linkPath);
        var targetPath = outsideScope.WriteFile("outside.txt", GetReplacementContent(relativeFile));
        if (!TryCreateFileSymbolicLink(linkPath, targetPath))
        {
            return;
        }

        var result = await CreateReader().ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenCategoryDirectoryIsSymbolicLink_ReturnsSourceInvalid ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "linked-category");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-agents", "linked-category-outside");
        outsideScope.CreateDirectory("core");
        if (!TryCreateDirectorySymbolicLink(scope.GetPath("core"), outsideScope.GetPath("core")))
        {
            return;
        }

        var result = await CreateReader().ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenAgentDirectoryContainsUnexpectedNode_ReturnsSourceInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "unexpected-node");
        WriteDefinition(scope);
        scope.WriteFile("core/architect/notes.txt", "not part of the authored layout\n");

        var result = await CreateReader().ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_WhenHostBindingIsNotDefined_ReturnsHostUnsupported ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "unsupported-host-binding");
        WriteDefinition(scope, writeHostBinding: false);
        scope.WriteFile("core/architect/hosts/unknown.json", CreateOpenAiBinding());

        var result = await CreateReader().ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    private static AgentSourceDefinitionReader CreateReader ()
    {
        return new AgentSourceDefinitionReader(new AgentHostAdapterSet());
    }

    private static void WriteDefinition (
        TestDirectoryScope scope,
        bool writeHostBinding = true)
    {
        scope.WriteFile(
            "core/architect/agent.json",
            """
            {
              "schemaVersion": 1,
              "displayName": "Architect",
              "description": "Creates an implementation-ready design.",
              "skillDependencies": []
            }
            """);
        scope.WriteFile("core/architect/AGENT.md.template", "Create an implementation-ready design.\n");
        scope.CreateDirectory("core/architect/hosts");
        if (writeHostBinding)
        {
            scope.WriteFile("core/architect/hosts/openai.json", CreateOpenAiBinding());
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
            _ => CreateOpenAiBinding(),
        };
    }

    private static string CreateOpenAiBinding ()
    {
        return """
            {
              "schemaVersion": 1,
              "modelProvider": "openai",
              "model": "gpt-5.6-terra",
              "reasoningEffort": "high",
              "verbosity": "low",
              "sandboxMode": "workspace-write",
              "features": {
                "multiAgent": false
              },
              "overridesBuiltIn": false
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
