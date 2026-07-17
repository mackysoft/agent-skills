using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Tests.Digests;

public sealed class SkillDigestInputFileTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_NormalizesContentToLf ()
    {
        var input = new SkillDigestInputFile("references/example.md", "first\r\nsecond\rthird\n");

        Assert.Equal("first\nsecond\nthird\n", input.Content);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("../outside.md")]
    [InlineData("references\\example.md")]
    [InlineData("/absolute.md")]
    public void Constructor_RejectsUnsafeRelativePath (string relativePath)
    {
        Assert.Throws<ArgumentException>(() => new SkillDigestInputFile(relativePath, "content"));
    }
}
