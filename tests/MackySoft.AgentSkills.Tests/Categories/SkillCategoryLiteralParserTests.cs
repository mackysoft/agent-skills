using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Categories;

public sealed class SkillCategoryLiteralParserTests
{
    private static readonly SkillCategory[] AvailableCategories =
    [
        new("core"),
        new("advanced"),
        new("developer"),
    ];

    [Theory]
    [InlineData("")]
    [InlineData("Category")]
    [InlineData("developer_category")]
    [InlineData("developer ")]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInvalidLiteral (string literal)
    {
        Assert.Throws<ArgumentException>(() => new SkillCategory(literal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedCategories_NormalizesExactLiteralsInCallerOrder ()
    {
        var result = SkillCategoryLiteralParser.ParseSelectedCategories(
            AvailableCategories,
            ["advanced", "core", "advanced"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["advanced", "core"], result.Value!.Select(static category => category.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedCategories_WithoutAvailableSet_AcceptsRemovedCategory ()
    {
        var result = SkillCategoryLiteralParser.ParseSelectedCategories(["removed", "removed"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["removed"], result.Value!.Select(static category => category.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedCategories_WithoutAvailableSet_RejectsUnsafeLiteral ()
    {
        var result = SkillCategoryLiteralParser.ParseSelectedCategories(["Removed"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("category literal is invalid", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Category")]
    [InlineData("advanced ")]
    [InlineData("unknown")]
    [Trait("Size", "Small")]
    public void ParseSelectedCategories_ReturnsInputFailure_WhenSelectedLiteralIsNotAvailable (string literal)
    {
        var result = SkillCategoryLiteralParser.ParseSelectedCategories(AvailableCategories, [literal]);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Supported categories: core, advanced, developer", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseSelectedCategories_ReturnsInputFailure_WhenSelectionIsEmpty ()
    {
        var result = SkillCategoryLiteralParser.ParseSelectedCategories(AvailableCategories, []);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("At least one SKILL category", result.Failure.Message, StringComparison.Ordinal);
    }
}
