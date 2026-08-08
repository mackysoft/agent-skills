using MackySoft.AgentSkills.Agents;
using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Hosts.OpenAi;

public sealed class OpenAiAgentHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void DefaultAdapterSet_RegistersOpenAiAdapter ()
    {
        var adapterResult = new AgentHostAdapterSet().GetAdapter(AgentHostKind.OpenAi);

        Assert.True(adapterResult.IsSuccess, adapterResult.Failure?.Message);
        Assert.IsType<OpenAiAgentHostAdapter>(adapterResult.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidateBinding_WhenBindingIsCanonical_AcceptsCurrentCustomAgentSettings ()
    {
        var result = new OpenAiAgentHostAdapter().ValidateBinding(CreateBinding());

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("{\"schemaVersion\":1,\"modelProvider\":\"openai\",\"model\":\"gpt-5.6-terra\",\"reasoningEffort\":\"high\",\"verbosity\":\"low\",\"sandboxMode\":\"workspace-write\",\"overridesBuiltIn\":false,\"features\":{\"multiAgent\":false}}")]
    [InlineData("{\"schemaVersion\":1,\"modelProvider\":\"openai\",\"model\":\"gpt-5.6-terra\",\"reasoningEffort\":\"high\",\"verbosity\":\"low\",\"sandboxMode\":\"workspace-write\",\"features\":{\"multiAgent\":false},\"overridesBuiltIn\":false,\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"modelProvider\":\"openai\",\"model\":\"gpt-5.6-terra\",\"reasoningEffort\":\"high\",\"verbosity\":\"low\",\"sandboxMode\":\"workspace-write\",\"features\":{\"multiAgent\":false}}")]
    [InlineData("{\"schemaVersion\":1,\"modelProvider\":\"openai\",\"model\":\"gpt-5.6-terra\",\"reasoningEffort\":\"high\",\"verbosity\":\"low\",\"sandboxMode\":\"workspace-write\",\"features\":{\"other\":false},\"overridesBuiltIn\":false}")]
    public void ValidateBinding_WhenPropertiesAreUnknownOrNotCanonical_ReturnsSourceFailure (string bindingJson)
    {
        var result = new OpenAiAgentHostAdapter().ValidateBinding(bindingJson);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_GeneratesDeterministicAgentTomlWithoutSharedHostConfiguration ()
    {
        var metadata = CreateMetadata("architect");
        var artifacts = new OpenAiAgentHostAdapter().BuildArtifacts(metadata, "Line 1\nLine 2\n", CreateBinding());

        var artifact = Assert.Single(artifacts.Files);
        Assert.Equal("architect.toml", artifact.RelativePath);
        Assert.Equal(
            """
            name = "architect"
            description = "Creates an implementation-ready design contract."
            model_provider = "openai"
            model = "gpt-5.6-terra"
            model_reasoning_effort = "high"
            model_verbosity = "low"
            sandbox_mode = "workspace-write"
            developer_instructions = "Line 1\nLine 2\n"

            [features]
            multi_agent = false

            """.ReplaceLineEndings("\n"),
            artifact.Content);
        Assert.DoesNotContain(artifacts.Files, static file => string.Equals(file.RelativePath, ".codex/config.toml", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_WhenBuiltInAgentDoesNotDeclareOverride_Throws ()
    {
        var adapter = new OpenAiAgentHostAdapter();

        var exception = Assert.Throws<ArgumentException>(() => adapter.BuildArtifacts(CreateMetadata("worker"), "Do the work.\n", CreateBinding()));

        Assert.Contains("requires overridesBuiltIn=true", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_WhenNonBuiltInAgentDeclaresOverride_Throws ()
    {
        var adapter = new OpenAiAgentHostAdapter();

        var exception = Assert.Throws<ArgumentException>(() => adapter.BuildArtifacts(CreateMetadata("architect"), "Design the work.\n", CreateBinding(overridesBuiltIn: true)));

        Assert.Contains("only for worker and explorer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_WhenBuiltInAgentDeclaresOverride_GeneratesArtifact ()
    {
        var artifacts = new OpenAiAgentHostAdapter().BuildArtifacts(CreateMetadata("explorer"), "Explore the source.\n", CreateBinding(overridesBuiltIn: true));

        Assert.Equal("explorer.toml", Assert.Single(artifacts.Files).RelativePath);
    }

    private static AgentSourceMetadata CreateMetadata (string agentName)
    {
        return new AgentSourceMetadata(
            schemaVersion: 1,
            new AgentCategory("orchestration"),
            new AgentName(agentName),
            "Architect",
            "Creates an implementation-ready design contract.",
            Array.Empty<SkillName>());
    }

    private static string CreateBinding (bool overridesBuiltIn = false)
    {
        return $$"""
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
          "overridesBuiltIn": {{overridesBuiltIn.ToString().ToLowerInvariant()}}
        }
        """;
    }
}
