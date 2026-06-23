using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.Tiers;

public sealed class SkillTierLiteralParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_AcceptsProductLiteralArray ()
    {
        var result = SkillTierLiteralParser.ParseDefinedTiers(["basic", "advanced", "developer"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["basic", "advanced", "developer"], result.Value!.Select(static tier => tier.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_RejectsEmptyDefinitionArray ()
    {
        var result = SkillTierLiteralParser.ParseDefinedTiers([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_RejectsDuplicateLiteral ()
    {
        var result = SkillTierLiteralParser.ParseDefinedTiers(["basic", "basic"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tier")]
    [InlineData("developer_tier")]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_RejectsInvalidLiteral (string literal)
    {
        var result = SkillTierLiteralParser.ParseDefinedTiers([literal]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedTiers_NormalizesExactTierLiterals ()
    {
        var result = SkillTierLiteralParser.ParseSelectedTiers(
            ["basic", "advanced", "developer"],
            ["advanced", "basic", "advanced"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["advanced", "basic"], result.Value!.Select(static tier => tier.Value).ToArray());
    }

    [Theory]
    [InlineData("Tier")]
    [InlineData("advanced ")]
    [InlineData("unknown")]
    [Trait("Size", "Small")]
    public void ParseSelectedTiers_ReturnsInputFailure_WhenSelectedLiteralIsNotExact (string literal)
    {
        var result = SkillTierLiteralParser.ParseSelectedTiers(["basic", "advanced", "developer"], [literal]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedTiers_ReturnsInputFailure_WhenSelectionIsRequiredAndEmpty ()
    {
        var result = SkillTierLiteralParser.ParseSelectedTiers(["basic"], []);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }
}
