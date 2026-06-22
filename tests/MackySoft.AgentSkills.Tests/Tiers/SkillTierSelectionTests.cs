using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.Tiers;

public sealed class SkillTierSelectionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_AcceptsProductLiteralArray ()
    {
        var result = SkillTierSelection.ParseDefinedTiers(["basic", "advanced", "developer"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["basic", "advanced", "developer"], result.Value!.Select(static tier => tier.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_RejectsEmptyDefinitionArray ()
    {
        var result = SkillTierSelection.ParseDefinedTiers([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseDefinedTiers_RejectsDuplicateLiteral ()
    {
        var result = SkillTierSelection.ParseDefinedTiers(["basic", "basic"]);

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
        var result = SkillTierSelection.ParseDefinedTiers([literal]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_NormalizesExactTierLiterals ()
    {
        var result = SkillTierSelection.Parse(
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
    public void Parse_ReturnsInputFailure_WhenSelectedLiteralIsNotExact (string literal)
    {
        var result = SkillTierSelection.Parse(["basic", "advanced", "developer"], [literal]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_ReturnsInputFailure_WhenSelectionIsRequiredAndEmpty ()
    {
        var result = SkillTierSelection.Parse(["basic"], []);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }
}
