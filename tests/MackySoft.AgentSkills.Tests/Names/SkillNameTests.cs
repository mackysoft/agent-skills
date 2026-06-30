using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Tests.Names;

public sealed class SkillNameTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_AcceptsSafeLiteral ()
    {
        var skillName = new SkillName("sample-skill");

        Assert.True(skillName.IsInitialized);
        Assert.Equal("sample-skill", skillName.Value);
        Assert.Equal("sample-skill", skillName.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SampleSkill")]
    [InlineData("sample_skill")]
    [InlineData("-sample")]
    [InlineData("sample/skill")]
    [Trait("Size", "Small")]
    public void TryCreate_ReturnsFalse_WhenLiteralIsUnsafe (string? literal)
    {
        var result = SkillName.TryCreate(literal, out var skillName);

        Assert.False(result);
        Assert.False(skillName.IsInitialized);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefaultValue_IsNotInitialized ()
    {
        SkillName skillName = default;

        Assert.False(skillName.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => skillName.Value);
        Assert.Throws<InvalidOperationException>(() => skillName.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_UsesOrdinalLiteralComparison ()
    {
        var left = new SkillName("sample-skill");
        var same = new SkillName("sample-skill");
        var different = new SkillName("sample-helper");
        var set = new HashSet<SkillName> { left };

        Assert.Equal(left, same);
        Assert.True(left == same);
        Assert.False(left != same);
        Assert.NotEqual(left, different);
        Assert.True(left != different);
        Assert.False(left == different);
        Assert.Contains(same, set);
        Assert.DoesNotContain(different, set);
    }
}
