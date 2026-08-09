using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Projection;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class SkillOperationReportContextTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath("operation-report-context-repository");

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_CapturesImmutableSelectionSnapshot ()
    {
        var categories = new List<SkillCategory> { new("core") };
        var skillNames = new List<SkillName> { new("sample-skill") };
        var context = new SkillOperationReportContext(
            HostKind.Codex,
            SkillScopeKind.Project,
            RepositoryRoot,
            categories,
            skillNames);

        categories[0] = new SkillCategory("advanced");
        skillNames[0] = new SkillName("other-skill");

        Assert.Equal("core", Assert.Single(context.SelectedCategories).Value);
        Assert.Equal("sample-skill", Assert.Single(context.SelectedSkillNames).Value);
        Assert.Equal(RepositoryRoot, context.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsNullSelectedCategories ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new SkillOperationReportContext(
                HostKind.Codex,
                SkillScopeKind.Project,
                RepositoryRoot,
                null!,
                []);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsNullCategoryItem ()
    {
        Assert.Throws<ArgumentException>(() => new SkillOperationReportContext(
            HostKind.Codex,
            SkillScopeKind.Project,
            RepositoryRoot,
            [null!],
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsNullSkillName ()
    {
        Assert.Throws<ArgumentException>(() => new SkillOperationReportContext(
            HostKind.Codex,
            SkillScopeKind.Project,
            RepositoryRoot,
            [new SkillCategory("core")],
            [null!]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUndefinedScope ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillOperationReportContext(
            HostKind.Codex,
            (SkillScopeKind)42,
            RepositoryRoot,
            [new SkillCategory("core")],
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RequiresRepositoryRootOnlyForProjectScope ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillOperationReportContext(
            HostKind.Codex,
            SkillScopeKind.Project,
            repositoryRoot: null,
            [],
            []));
        Assert.Throws<ArgumentException>(() => new SkillOperationReportContext(
            HostKind.Codex,
            SkillScopeKind.User,
            RepositoryRoot,
            [],
            []));

        var userContext = new SkillOperationReportContext(
            HostKind.Codex,
            SkillScopeKind.User,
            repositoryRoot: null,
            [],
            []);

        Assert.Null(userContext.RepositoryRoot);
    }

}
