using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Materialization;

public sealed class SkillMaterializedPackageTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_CapturesOrdinalFileSnapshot ()
    {
        var files = new List<PackageTextFile>
        {
            new(PackageRelativePath.Parse("references/b.md"), "b\n"),
            new(PackageRelativePath.Parse("SKILL.md"), "body\n"),
        };
        var package = new SkillMaterializedPackage(new SkillName("sample-skill"), HostKind.Codex, files);

        files[0] = new PackageTextFile(PackageRelativePath.Parse("references/a.md"), "a\n");

        Assert.Equal(
            [PackageRelativePath.Parse("SKILL.md"), PackageRelativePath.Parse("references/b.md")],
            package.Files.Select(static file => file.RelativePath).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInvalidIdentityAndFileSet ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillMaterializedPackage(null!, HostKind.Codex, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillMaterializedPackage(new SkillName("sample-skill"), (HostKind)42, []));
        Assert.Throws<ArgumentException>(() => new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            HostKind.Codex,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "body\n"), null!]));
        Assert.Throws<ArgumentException>(() => new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            HostKind.Codex,
            [
                new PackageTextFile(PackageRelativePath.Parse("references/a.md"), "a\n"),
                new PackageTextFile(PackageRelativePath.Parse("references/A.md"), "A\n"),
            ]));
    }
}
