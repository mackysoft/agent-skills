using MackySoft.AgentSkills.Hosts.Contracts;

namespace MackySoft.AgentSkills.Tests.Hosts.Contracts;

public sealed class SkillUserTargetRootPolicyTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, null, "../skills")]
    [InlineData(null, "skills", ".agents/skills")]
    [InlineData("AGENT_HOME", "../skills", ".agents/skills")]
    [InlineData(" ", null, ".agents/skills")]
    public void Constructor_RejectsInvalidRootPolicy (
        string? environmentVariableName,
        string? environmentVariableChildDirectory,
        string homeRelativeDirectory)
    {
        Assert.Throws<ArgumentException>(() => new SkillUserTargetRootPolicy(
            environmentVariableName,
            environmentVariableChildDirectory,
            homeRelativeDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_AllowsEnvironmentVariableRootWithoutChildDirectory ()
    {
        var policy = new SkillUserTargetRootPolicy("AGENT_HOME", null, ".agents/skills");

        Assert.Equal("AGENT_HOME", policy.EnvironmentVariableName);
        Assert.Null(policy.EnvironmentVariableChildDirectory);
        Assert.Equal(".agents/skills", policy.HomeRelativeDirectory);
    }
}
