using MackySoft.AgentSkills.Commands;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Commands;

public sealed class SkillCommandValueParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ParseHostLiteral_CanonicalizesRegisteredHost ()
    {
        var result = SkillCommandValueParser.ParseHostLiteral(
            "OpenAI",
            SkillTestData.CreateDefaultHostAdapterSet());

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillHostKind.OpenAi, result.Value!.Host);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ParseHostLiteral_ReturnsUnsupportedHost_ForUnknownHost ()
    {
        var result = SkillCommandValueParser.ParseHostLiteral(
            "generic",
            SkillTestData.CreateDefaultHostAdapterSet());

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ParseHostLiteral_ReturnsInputInvalid_ForBlankHost (string? host)
    {
        var result = SkillCommandValueParser.ParseHostLiteral(
            host,
            SkillTestData.CreateDefaultHostAdapterSet());

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
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
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("directory", SkillExportFormat.Directory)]
    [InlineData("ZIP", SkillExportFormat.Zip)]
    public void ParseExportFormatLiteral_ReturnsStableExportFormat (
        string literal,
        SkillExportFormat expected)
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
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
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
            SkillCommandValueParser.ParseHostLiteral(literal, SkillTestData.CreateDefaultHostAdapterSet()).Failure,
            SkillCommandValueParser.ParseScopeLiteral(literal).Failure,
            SkillCommandValueParser.ParseExportFormatLiteral(literal).Failure,
        };

        Assert.All(
            failures.Where(static failure => failure is not null),
            static failure => Assert.DoesNotContain("--", failure!.Message, StringComparison.Ordinal));
    }

}
