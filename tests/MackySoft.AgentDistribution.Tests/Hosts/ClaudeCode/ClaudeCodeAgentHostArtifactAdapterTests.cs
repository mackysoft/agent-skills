using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Hosts.ClaudeCode;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosts.ClaudeCode;

public sealed class ClaudeCodeAgentHostArtifactAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_GeneratesClaudeCodeSubagent ()
    {
        var artifacts = new ClaudeCodeAgentHostArtifactAdapter().BuildArtifacts(
            CreateMetadata(),
            "Line 1\r\nLine 2",
            """
            {
              "schemaVersion": 1,
              "model": "sonnet",
              "tools": ["Read", "Grep"],
              "disallowedTools": ["Write"],
              "permissionMode": "plan",
              "maxTurns": 20
            }
            """);

        var artifact = Assert.Single(artifacts.Files);
        Assert.Equal("architect.md", artifact.RelativePath.Value);
        Assert.Equal(
            "---\n"
            + "name: \"architect\"\n"
            + "description: \"Creates an implementation-ready design contract.\"\n"
            + "tools:\n"
            + "  - \"Read\"\n"
            + "  - \"Grep\"\n"
            + "disallowedTools:\n"
            + "  - \"Write\"\n"
            + "model: \"sonnet\"\n"
            + "permissionMode: \"plan\"\n"
            + "maxTurns: 20\n"
            + "---\n"
            + "\n"
            + "Line 1\nLine 2\n",
            artifact.Content);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidateBinding_WhenPermissionModeIsUnsupported_ReturnsSourceFailure ()
    {
        var result = new ClaudeCodeAgentHostArtifactAdapter().ValidateBinding(
            """{"schemaVersion":1,"permissionMode":"unknown"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_WhenPermissionModeIsManual_GeneratesManualPermissionMode ()
    {
        var artifacts = new ClaudeCodeAgentHostArtifactAdapter().BuildArtifacts(
            CreateMetadata(),
            "Review the change.\n",
            """{"schemaVersion":1,"permissionMode":"manual"}""");

        var artifact = Assert.Single(artifacts.Files);
        Assert.Equal(
            "---\n"
            + "name: \"architect\"\n"
            + "description: \"Creates an implementation-ready design contract.\"\n"
            + "permissionMode: \"manual\"\n"
            + "---\n"
            + "\n"
            + "Review the change.\n",
            artifact.Content);
    }

    private static AgentSourceMetadata CreateMetadata ()
    {
        return new AgentSourceMetadata(
            schemaVersion: 1,
            new AgentName("architect"),
            "Architect",
            "Creates an implementation-ready design contract.",
            Array.Empty<SkillName>());
    }
}
