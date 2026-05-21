using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class SkillLiteralCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void NormalizeHost_ReturnsCanonicalHostKey ()
    {
        var result = SkillLiteralCodec.NormalizeHost("OPENAI", SkillTestData.CreateDefaultHostAdapterSet());

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(OpenAiSkillHostAdapter.HostKey, result.Value);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillScopeKind.Project, "project")]
    [InlineData(SkillScopeKind.User, "user")]
    public void FormatScope_ReturnsStableLiteral (
        SkillScopeKind scope,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatScope(scope));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("PROJECT", SkillScopeKind.Project)]
    [InlineData("user", SkillScopeKind.User)]
    public void TryParseScope_AcceptsStableLiteralCaseInsensitively (
        string literal,
        SkillScopeKind expected)
    {
        Assert.True(SkillLiteralCodec.TryParseScope(literal, out var scope));
        Assert.Equal(expected, scope);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillExportFormat.Directory, "directory")]
    [InlineData(SkillExportFormat.Zip, "zip")]
    public void FormatExportFormat_ReturnsStableLiteral (
        SkillExportFormat format,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatExportFormat(format));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseExportFormat_RejectsMissingLiteral ()
    {
        Assert.False(SkillLiteralCodec.TryParseExportFormat(null, out _));
        Assert.False(SkillLiteralCodec.TryParseExportFormat(string.Empty, out _));
        Assert.False(SkillLiteralCodec.TryParseExportFormat(" ", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FormatOperationLiterals_ReturnsStableLowerCamelValues ()
    {
        Assert.Equal("blockedLocalModification", SkillLiteralCodec.FormatInstallAction(SkillInstallActionKind.BlockedLocalModification));
        Assert.Equal("blockedUnmanaged", SkillLiteralCodec.FormatUpdateAction(SkillUpdateActionKind.BlockedUnmanaged));
        Assert.Equal("skippedUnmanaged", SkillLiteralCodec.FormatUninstallAction(SkillUninstallActionKind.SkippedUnmanaged));
        Assert.Equal("blocked", SkillLiteralCodec.FormatActionStatus(SkillOperationActionStatus.Blocked));
        Assert.Equal("localModificationRequiresForce", SkillLiteralCodec.FormatBlockedReason(SkillBlockedReason.LocalModificationRequiresForce));
        Assert.Equal("localModification", SkillLiteralCodec.FormatTargetStateKind(SkillInstalledTargetStateKind.LocalModified));
        Assert.Equal("hostArtifactDrift", SkillLiteralCodec.FormatTargetStateKind(SkillInstalledTargetStateKind.HostArtifactDrift));
        Assert.Equal("modified", SkillLiteralCodec.FormatDiffChangeKind(SkillDiffChangeKind.Modified));
        Assert.Equal("error", SkillLiteralCodec.FormatDoctorSeverity(SkillDoctorSeverity.Error));
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged.Value, SkillLiteralCodec.FormatFailureCode(SkillFailureCodes.InstallTargetUnmanaged));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FormatOperationLiterals_RejectUndefinedEnumValues ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatScope((SkillScopeKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatInstallAction((SkillInstallActionKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatTargetStateKind((SkillInstalledTargetStateKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatDoctorSeverity((SkillDoctorSeverity)999));
    }
}
