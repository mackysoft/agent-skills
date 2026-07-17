using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Tests.Installation.Targeting;

public sealed class SkillInstallContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Request_RejectsUndefinedContractEnums ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillInstallRequest(
            (SkillHostKind)999,
            SkillScopeKind.User,
            null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillInstallRequest(
            SkillHostKind.OpenAi,
            (SkillScopeKind)999,
            null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Request_RejectsPathsThatDoNotMatchScope ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            null));
        Assert.ThrowsAny<ArgumentException>(() => new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            "relative-repository"));
        Assert.ThrowsAny<ArgumentException>(() => new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.User,
            Path.GetFullPath("repository")));
        Assert.ThrowsAny<ArgumentException>(() => new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.User,
            null,
            "relative-target"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Request_CanonicalizesAbsoluteScopePaths ()
    {
        var repositoryRoot = Path.Combine(Path.GetFullPath("root"), "nested", "..");
        var targetRoot = Path.Combine(Path.GetFullPath("target"), "nested", "..");

        var projectRequest = new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            repositoryRoot);
        var userRequest = new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.User,
            null,
            targetRoot);

        Assert.Equal(Path.GetFullPath(repositoryRoot), projectRequest.RepositoryRoot);
        Assert.Equal(Path.GetFullPath(targetRoot), userRequest.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Identity_RejectsInvalidIdentityValues ()
    {
        Assert.Throws<ArgumentException>(() => new SkillInstallIdentity(
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            "relative-target",
            new SkillName("sample-skill")));
        Assert.Throws<ArgumentNullException>(() => new SkillInstallIdentity(
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            Path.GetFullPath("target"),
            null!));
    }
}
