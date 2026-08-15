using MackySoft.AgentDistribution.Hosts.Codex;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosts.Codex;

public sealed class CodexAgentHostArtifactAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_GeneratesDeterministicCodexToml ()
    {
        var artifacts = new CodexAgentHostArtifactAdapter().BuildArtifacts(
            CreateRequest("architect", "Line 1\nLine 2\n", CreateBinding()));

        var artifact = Assert.Single(artifacts.Files);
        Assert.Equal("architect.toml", artifact.RelativePath.Value);
        Assert.Equal(
            "name = \"architect\"\n"
            + "description = \"Creates an implementation-ready design contract.\"\n"
            + "model = \"gpt-5.6-terra\"\n"
            + "model_reasoning_effort = \"high\"\n"
            + "sandbox_mode = \"workspace-write\"\n"
            + "developer_instructions = \"Line 1\\nLine 2\\n\"\n",
            artifact.Content);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("{\"schemaVersion\":1,\"unknown\":true}")]
    [InlineData("{\"schemaVersion\":1,\"reasoningEffort\":\"\"}")]
    [InlineData("{\"schemaVersion\":1,\"sandboxMode\":\"invalid\"}")]
    public void ValidateBinding_WhenBindingViolatesCodexContract_ReturnsSourceFailure (string bindingJson)
    {
        var result = new CodexAgentHostArtifactAdapter().ValidateBinding(bindingJson);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_RejectsBuiltInAgentName ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CodexAgentHostArtifactAdapter().BuildArtifacts(
                CreateRequest("worker", "Do the work.\n", CreateBinding())));

        Assert.Contains("reserved", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_WhenInstructionsContainControlCharacter_EscapesItInToml ()
    {
        var artifacts = new CodexAgentHostArtifactAdapter().BuildArtifacts(
            CreateRequest("architect", "Review\u0001the change.\n", CreateBinding()));

        var artifact = Assert.Single(artifacts.Files);
        Assert.Contains("developer_instructions = \"Review\\u0001the change.\\n\"\n", artifact.Content, StringComparison.Ordinal);
        Assert.DoesNotContain('\u0001', artifact.Content);
    }

    private static AgentHostArtifactRequest CreateRequest (string agentName, string instructions, string bindingJson)
    {
        return new AgentHostArtifactRequest(
            new AgentName(agentName),
            "Creates an implementation-ready design contract.",
            instructions,
            bindingJson);
    }

    private static string CreateBinding ()
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
}
