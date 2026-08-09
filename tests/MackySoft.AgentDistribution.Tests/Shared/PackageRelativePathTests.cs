using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Shared;

public sealed class PackageRelativePathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_CanonicalPackagePath_PreservesCanonicalValue ()
    {
        var isParsed = PackageRelativePath.TryParse("agents/openai/reviewer.md", out var path);

        Assert.True(isParsed);
        Assert.NotNull(path);
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
        Assert.True(PackageRelativePath.TryParseSegment("codex", out var segment));
        Assert.NotNull(segment);
        Assert.Equal("codex", segment.Value);
        Assert.False(PackageRelativePath.TryParseSegment("hosts/codex", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_UsesCurrentPlatformPathIdentity ()
    {
        var first = PackageRelativePath.Parse("hosts/codex/architect.toml");
        var same = PackageRelativePath.Parse("hosts/codex/architect.toml");
        var differentCase = PackageRelativePath.Parse("hosts/codex/Architect.toml");

        Assert.True(first.IsSameAs(same));
        Assert.True(first == same);
        Assert.Equal(OperatingSystem.IsWindows(), first == differentCase);
        Assert.Equal(!OperatingSystem.IsWindows(), first != differentCase);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PortableFileSystemComparer_RejectsCaseOnlyPackagePathCollisions ()
    {
        var paths = new HashSet<PackageRelativePath>(PackageRelativePath.PortableFileSystemComparer)
        {
            PackageRelativePath.Parse("hosts/codex/architect.toml"),
        };

        Assert.False(paths.Add(PackageRelativePath.Parse("hosts/codex/Architect.toml")));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("references/example.md", "references", true)]
    [InlineData("references/nested/example.md", "references", true)]
    [InlineData("references", "references", false)]
    [InlineData("references-old/example.md", "references", false)]
    public void IsDescendantOf_RequiresCompleteDirectorySegment (
        string path,
        string directoryPath,
        bool expected)
    {
        Assert.Equal(
            expected,
            PackageRelativePath.Parse(path).IsDescendantOf(PackageRelativePath.Parse(directoryPath)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetRelativeTo_ReturnsTypedDescendantPath ()
    {
        var path = PackageRelativePath.Parse("hosts/codex/architect.toml");

        Assert.True(path.TryGetRelativeTo(PackageRelativePath.Parse("hosts/codex"), out var relativePath));
        Assert.Equal(PackageRelativePath.Parse("architect.toml"), relativePath);
        Assert.False(path.TryGetRelativeTo(PackageRelativePath.Parse("hosts/claude-code"), out _));
    }
}
