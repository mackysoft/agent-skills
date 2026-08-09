using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Hosts.GitHubCopilot;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosts.GitHubCopilot;

public sealed class GitHubCopilotAgentHostArtifactAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_WithEmptyTools_GeneratesToolDisabledAgentProfile ()
    {
        var artifacts = new GitHubCopilotAgentHostArtifactAdapter().BuildArtifacts(
            CreateMetadata(),
            "Plan the implementation.\n",
            """
            {
              "schemaVersion": 1,
              "target": "github-copilot",
              "tools": [],
              "model": "claude-sonnet-4.6",
              "disableModelInvocation": true,
              "userInvocable": true
            }
            """);

        var artifact = Assert.Single(artifacts.Files);
        Assert.Equal("architect.agent.md", artifact.RelativePath.Value);
        Assert.Equal(
            "---\n"
            + "description: \"Creates an implementation-ready design contract.\"\n"
            + "target: \"github-copilot\"\n"
            + "tools: []\n"
            + "model: \"claude-sonnet-4.6\"\n"
            + "disable-model-invocation: true\n"
            + "user-invocable: true\n"
            + "---\n"
            + "\n"
            + "Plan the implementation.\n",
            artifact.Content);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidateBinding_WhenTargetIsUnsupported_ReturnsSourceFailure ()
    {
        var result = new GitHubCopilotAgentHostArtifactAdapter().ValidateBinding(
            """{"schemaVersion":1,"target":"unknown"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    private static AgentSourceMetadata CreateMetadata ()
    {
        return new AgentSourceMetadata(
            schemaVersion: 1,
            new AgentCategory("orchestration"),
            new AgentName("architect"),
            "Architect",
            "Creates an implementation-ready design contract.",
            Array.Empty<SkillName>());
    }
}
