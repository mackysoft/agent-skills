using MackySoft.AgentSkills.Bundles;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class SkillBundleVersionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [Trait("Size", "Small")]
    public void Constructor_RejectsNonPositiveValue (int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillBundleVersion(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_ReturnsValidatedVersion ()
    {
        Assert.True(SkillBundleVersion.TryCreate(3, out var version));
        Assert.Equal(3, version.Value);
        Assert.False(SkillBundleVersion.TryCreate(0, out var invalidVersion));
        Assert.Null(invalidVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Next_CreatesImmediatelyFollowingVersion ()
    {
        var version = new SkillBundleVersion(3);

        var next = version.Next();

        Assert.Equal(3, version.Value);
        Assert.Equal(4, next.Value);
        Assert.True(next.CompareTo(version) > 0);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Next_RejectsOverflow ()
    {
        var version = new SkillBundleVersion(int.MaxValue);

        Assert.Throws<OverflowException>(version.Next);
    }
}
