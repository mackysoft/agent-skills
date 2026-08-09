using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Digests;

public sealed class SkillDigestInputFileTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_NormalizesContentToLf ()
    {
        var input = new SkillDigestInputFile(PackageRelativePath.Parse("references/example.md"), "first\r\nsecond\rthird\n");

        Assert.Equal("first\nsecond\nthird\n", input.Content);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_NullRelativePath_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillDigestInputFile(null!, "content"));
    }
}
