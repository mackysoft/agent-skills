using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
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
            HostKey: "test",
            SupportsProjectScope: true,
            SupportsUserScope: true,
            ProjectDefaultTargetPath: ".test/project-skills",
            UserDefaultTargetPath: "${TEST_SKILLS_HOME}",
            UserTargetRootPolicy: new SkillUserTargetRootPolicy("TEST_SKILLS_HOME", null, ".test/skills"),
            RequiresMetadataArtifact: false,
            MetadataArtifactPath: null,
            ReloadGuidance: "Reload test skills.");

        var result = resolver.ResolveDefaultTargetRoot(descriptor);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(Path.GetFullPath(environmentRoot), result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDefaultTargetRoot_RejectsEnvironmentChildDirectoryWithoutEnvironmentVariable ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "user-target-child-without-env");
        var resolver = new SkillUserTargetRootResolver(
            () => scope.GetPath("home"),
            static _ => null);
        var descriptor = new SkillHostDescriptor(
            HostKey: "test",
            SupportsProjectScope: true,
            SupportsUserScope: true,
            ProjectDefaultTargetPath: ".test/project-skills",
            UserDefaultTargetPath: "~/.test/skills",
            UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, "skills", ".test/skills"),
            RequiresMetadataArtifact: false,
            MetadataArtifactPath: null,
            ReloadGuidance: "Reload test skills.");

        var result = resolver.ResolveDefaultTargetRoot(descriptor);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.UserTargetUnavailable, result.Failure!.Code);
    }
}
