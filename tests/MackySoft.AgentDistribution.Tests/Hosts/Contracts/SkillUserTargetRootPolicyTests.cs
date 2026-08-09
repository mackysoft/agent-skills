namespace MackySoft.AgentDistribution.Tests.Hosts.Contracts;

public sealed class SkillUserTargetRootPolicyTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, true)]
    [InlineData(" ", false)]
    public void Constructor_RejectsInvalidRootPolicy (
        string? environmentVariableName,
        bool hasChildDirectory)
    {
        Assert.Throws<ArgumentException>(() => new SkillUserTargetRootPolicy(
            environmentVariableName,
            hasChildDirectory ? RootRelativePath.Parse("skills") : null,
            RootRelativePath.Parse(".agents/skills")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_AllowsEnvironmentVariableRootWithoutChildDirectory ()
    {
        var policy = new SkillUserTargetRootPolicy("AGENT_HOME", null, RootRelativePath.Parse(".agents/skills"));

        Assert.Equal("AGENT_HOME", policy.EnvironmentVariableName);
        Assert.Null(policy.EnvironmentVariableChildDirectory);
        Assert.Equal(".agents/skills", policy.HomeRelativeDirectory.Value);
    }
}
