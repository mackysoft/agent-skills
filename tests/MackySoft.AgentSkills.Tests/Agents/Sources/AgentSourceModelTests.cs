using MackySoft.AgentSkills.Agents.Sources;

namespace MackySoft.AgentSkills.Tests.Agents.Sources;

public sealed class AgentSourceModelTests
{
    [Theory]
    [InlineData("Architect\u0001", "Creates an implementation-ready design contract.")]
    [InlineData("Architect", "Creates an implementation-ready\u000B design contract.")]
    [Trait("Size", "Small")]
    public void MetadataConstructor_WhenHumanReadableMetadataContainsControlCharacters_RejectsSource (
        string displayName,
        string description)
    {
        Assert.Throws<ArgumentException>(() => new AgentSourceMetadata(
            schemaVersion: 1,
            new AgentCategory("orchestration"),
            new AgentName("architect"),
            displayName,
            description,
            Array.Empty<SkillName>()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefinitionConstructor_WhenInstructionsContainUnsupportedControlCharacter_RejectsSource ()
    {
        Assert.Throws<ArgumentException>(() => new AgentSourceDefinition(
            CreateMetadata(),
            "Plan the implementation.\u0001\n",
            CreateBindings()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefinitionConstructor_WhenInstructionsContainLfAndTab_AcceptsSource ()
    {
        var definition = new AgentSourceDefinition(
            CreateMetadata(),
            "Plan the implementation.\n\tKeep the result concise.\n",
            CreateBindings());

        Assert.Equal("Plan the implementation.\n\tKeep the result concise.\n", definition.InstructionsTemplate);
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

    private static IReadOnlyList<AgentHostBindingSource> CreateBindings ()
    {
        return [new AgentHostBindingSource(HostKind.Codex, "{\"schemaVersion\":1}")];
    }
}
