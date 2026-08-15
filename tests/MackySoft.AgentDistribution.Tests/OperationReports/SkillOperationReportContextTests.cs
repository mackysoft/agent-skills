using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Projection;

namespace MackySoft.AgentDistribution.Tests.OperationReports;

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
            CodexHost,
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
                CodexHost,
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
            CodexHost,
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
            CodexHost,
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
            CodexHost,
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
            CodexHost,
            SkillScopeKind.Project,
            repositoryRoot: null,
            [],
            []));
        Assert.Throws<ArgumentException>(() => new SkillOperationReportContext(
            CodexHost,
            SkillScopeKind.User,
            RepositoryRoot,
            [],
            []));

        var userContext = new SkillOperationReportContext(
            CodexHost,
            SkillScopeKind.User,
            repositoryRoot: null,
            [],
            []);

        Assert.Null(userContext.RepositoryRoot);
    }

    private static SkillResolvedHost CodexHost => ResolveHost(HostKind.Codex);

    private static SkillResolvedHost ResolveHost (HostKind host)
    {
        var result = SkillTestData.CreateInstallTargetResolver().ResolveHost(host);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }
}
