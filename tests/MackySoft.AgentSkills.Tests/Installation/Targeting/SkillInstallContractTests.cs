using MackySoft.AgentSkills.Installation.Targeting;

namespace MackySoft.AgentSkills.Tests.Installation.Targeting;

public sealed class SkillInstallContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Request_RejectsUndefinedContractEnums ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillInstallRequest(
            (HostKind)999,
            SkillScopeKind.User,
            null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillInstallRequest(
            HostKind.Codex,
            (SkillScopeKind)999,
            null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Request_RejectsPathsThatDoNotMatchScope ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillInstallRequest(
            HostKind.Codex,
            SkillScopeKind.Project,
            null));
        Assert.Throws<ArgumentException>(() => new SkillInstallRequest(
            HostKind.Codex,
            SkillScopeKind.User,
            AbsolutePath.Parse(Path.GetFullPath("repository"))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Identity_RejectsInvalidIdentityValues ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillInstallIdentity(
            HostKind.Codex,
            SkillScopeKind.Project,
            null!,
            new SkillName("sample-skill")));
        Assert.Throws<ArgumentNullException>(() => new SkillInstallIdentity(
            HostKind.Codex,
            SkillScopeKind.Project,
            AbsolutePath.Parse(Path.GetFullPath("target")),
            null!));
    }
}
