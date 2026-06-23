using MackySoft.AgentSkills.Selection;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Selection;

public sealed class SkillNameLiteralParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedSkillNames_NormalizesExactSkillNames ()
    {
        var result = SkillNameLiteralParser.ParseSelectedSkillNames(["skill-b", "skill-a", "skill-b"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["skill-b", "skill-a"], result.Value!);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Skill-A")]
    [InlineData("skill_a")]
    [InlineData("-skill-a")]
    public void ParseSelectedSkillNames_ReturnsInputFailure_WhenLiteralIsNotExactSkillName (string literal)
    {
        var result = SkillNameLiteralParser.ParseSelectedSkillNames([literal]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedSkillNames_ReturnsInputFailure_WhenSelectionIsEmpty ()
    {
        var result = SkillNameLiteralParser.ParseSelectedSkillNames([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("At least one SKILL name", result.Failure.Message, StringComparison.Ordinal);
    }
}
