using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Targeting;

public sealed class SkillUserTargetRootResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDefaultTargetRoot_UsesEnvironmentRootWhenPolicyHasNoChildDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "user-target-env-root");
        var environmentRoot = scope.GetPath("env-root");
        var resolver = new SkillUserTargetRootResolver(
            () => scope.GetPath("home"),
            name => string.Equals(name, "TEST_SKILLS_HOME", StringComparison.Ordinal) ? environmentRoot : null);
        var descriptor = new SkillHostDescriptor(
            SkillHostKind.OpenAi,
            ".test/project-skills",
            "${TEST_SKILLS_HOME}",
            new SkillUserTargetRootPolicy("TEST_SKILLS_HOME", null, ".test/skills"),
            SkillBundleTargetRootLayout.Flat,
            [],
            null,
            "Reload test skills.");

        var result = resolver.ResolveDefaultTargetRoot(descriptor);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(Path.GetFullPath(environmentRoot), result.Value);
    }

}
