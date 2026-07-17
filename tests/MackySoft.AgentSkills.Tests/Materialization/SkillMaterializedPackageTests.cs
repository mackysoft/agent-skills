using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Materialization;

public sealed class SkillMaterializedPackageTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_CapturesOrdinalFileSnapshot ()
    {
        var files = new List<SkillPackageFile>
        {
            new("references/b.md", "b\n"),
            new("SKILL.md", "body\n"),
        };
        var package = new SkillMaterializedPackage(new SkillName("sample-skill"), SkillHostKind.OpenAi, files);

        files[0] = new SkillPackageFile("references/a.md", "a\n");

        Assert.Equal(["SKILL.md", "references/b.md"], package.Files.Select(static file => file.RelativePath).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInvalidIdentityAndFileSet ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillMaterializedPackage(null!, SkillHostKind.OpenAi, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillMaterializedPackage(new SkillName("sample-skill"), (SkillHostKind)42, []));
        Assert.Throws<ArgumentException>(() => new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            SkillHostKind.OpenAi,
            [new SkillPackageFile("SKILL.md", "body\n"), null!]));
        Assert.Throws<ArgumentException>(() => new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            SkillHostKind.OpenAi,
            [new SkillPackageFile("references/a.md", "a\n"), new SkillPackageFile("references/A.md", "A\n")]));
    }
}
