using MackySoft.AgentDistribution.Commands;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Commands;

public sealed class SkillCommandValueParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ParseHostLiteral_CanonicalizesRegisteredHost ()
    {
        var result = SkillCommandValueParser.ParseHostLiteral("CODEX");

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(HostKind.Codex, result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseHostLiteral_ReturnsUnsupportedHost_ForUnknownHost ()
    {
        var result = SkillCommandValueParser.ParseHostLiteral("generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ParseHostLiteral_ReturnsInputInvalid_ForBlankHost (string? host)
    {
        var result = SkillCommandValueParser.ParseHostLiteral(host);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("project", SkillScopeKind.Project)]
    [InlineData("USER", SkillScopeKind.User)]
    public void ParseScopeLiteral_ReturnsStableScopeKind (
        string literal,
        SkillScopeKind expected)
    {
        var result = SkillCommandValueParser.ParseScopeLiteral(literal);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseScopeLiteral_ReturnsInputInvalid_ForUnknownScope ()
    {
        var result = SkillCommandValueParser.ParseScopeLiteral("global");

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("directory", PackageExportFormat.Directory)]
    [InlineData("ZIP", PackageExportFormat.Zip)]
    public void ParseExportFormatLiteral_ReturnsStableExportFormat (
        string literal,
        PackageExportFormat expected)
    {
        var result = SkillCommandValueParser.ParseExportFormatLiteral(literal);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tar")]
    public void ParseExportFormatLiteral_ReturnsInputInvalid_ForBlankOrUnknownFormat (string? format)
    {
        var result = SkillCommandValueParser.ParseExportFormatLiteral(format);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("global")]
    [InlineData("tar")]
    public void ParserFailures_DoNotContainProductSpecificOptionText (string literal)
    {
        var failures = new[]
        {
            SkillCommandValueParser.ParseHostLiteral(literal).Failure,
            SkillCommandValueParser.ParseScopeLiteral(literal).Failure,
            SkillCommandValueParser.ParseExportFormatLiteral(literal).Failure,
        };

        Assert.All(
            failures.Where(static failure => failure is not null),
            static failure => Assert.DoesNotContain("--", failure!.Message, StringComparison.Ordinal));
    }

}
