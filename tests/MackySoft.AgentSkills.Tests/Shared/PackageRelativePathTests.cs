using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class PackageRelativePathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_CanonicalPackagePath_PreservesCanonicalValue ()
    {
        var isParsed = PackageRelativePath.TryParse("agents/openai/reviewer.md", out var path);

        Assert.True(isParsed);
        Assert.Equal("agents/openai/reviewer.md", path.Value);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("../escape.md")]
    [InlineData("/absolute.md")]
    [InlineData("nested\\artifact.md")]
    [InlineData("nested:artifact.md")]
    [InlineData("nested//artifact.md")]
    [InlineData("./nested/artifact.md")]
    [InlineData("nested/ ")]
    [InlineData("nested/\u0001artifact.md")]
    public void TryParse_PathOutsidePackageTextContract_RejectsValue (string value)
    {
        Assert.False(PackageRelativePath.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseSegment_RejectsPathWithMultipleSegments ()
    {
        Assert.True(PackageRelativePath.TryParseSegment("openai", out var segment));
        Assert.Equal("openai", segment.Value);
        Assert.False(PackageRelativePath.TryParseSegment("hosts/openai", out _));
    }
}
