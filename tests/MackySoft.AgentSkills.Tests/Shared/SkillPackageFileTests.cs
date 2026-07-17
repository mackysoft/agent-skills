using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class SkillPackageFileTests
{
    [Theory]
    [InlineData("")]
    [InlineData("../escape.md")]
    [InlineData("/absolute.md")]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUnsafePath (string relativePath)
    {
        Assert.Throws<ArgumentException>(() => new SkillPackageFile(relativePath, "content\n"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsByteOrderMark ()
    {
        Assert.Throws<ArgumentException>(() => new SkillPackageFile("SKILL.md", "\uFEFFcontent\n"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_NormalizesLineEndings ()
    {
        var file = new SkillPackageFile("SKILL.md", "line 1\r\nline 2\r");

        Assert.Equal("line 1\nline 2\n", file.Content);
    }
}
