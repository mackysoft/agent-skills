using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class SkillLiteralCodecTests
{
    public static TheoryData<Type> ContractLiteralEnumTypes =>
    [
        typeof(SkillScopeKind),
        typeof(SkillExportFormat),
        typeof(SkillInstallActionKind),
        typeof(SkillUpdateActionKind),
        typeof(SkillUninstallActionKind),
        typeof(SkillPruneActionKind),
        typeof(SkillOperationActionStatus),
        typeof(SkillBlockedReason),
        typeof(SkillTargetStateKind),
        typeof(SkillDiffChangeKind),
        typeof(SkillDoctorSeverity),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void NormalizeHost_ReturnsCanonicalHostKey ()
    {
        var result = SkillLiteralCodec.NormalizeHost("OPENAI", SkillTestData.CreateDefaultHostAdapterSet());

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(OpenAiSkillHostAdapter.HostKey, result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void NormalizeHost_ReturnsFailureForUnsupportedHost ()
    {
        var result = SkillLiteralCodec.NormalizeHost("unknown-host", SkillTestData.CreateDefaultHostAdapterSet());

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
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
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("repo")]
    public void TryParseScope_RejectsUnsupportedLiteral (string? literal)
    {
        Assert.False(SkillLiteralCodec.TryParseScope(literal, out _));
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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("DIRECTORY", SkillExportFormat.Directory)]
    [InlineData("zip", SkillExportFormat.Zip)]
    public void TryParseExportFormat_AcceptsStableLiteralCaseInsensitively (
        string literal,
        SkillExportFormat expected)
    {
        Assert.True(SkillLiteralCodec.TryParseExportFormat(literal, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillInstallActionKind.Created, "created")]
    [InlineData(SkillInstallActionKind.Updated, "updated")]
    [InlineData(SkillInstallActionKind.NoOp, "noOp")]
    [InlineData(SkillInstallActionKind.BlockedManagedOverwrite, "blockedManagedOverwrite")]
    [InlineData(SkillInstallActionKind.BlockedLocalModification, "blockedLocalModification")]
    [InlineData(SkillInstallActionKind.BlockedUnmanaged, "blockedUnmanaged")]
    public void FormatInstallAction_ReturnsStableLiteral (
        SkillInstallActionKind actionKind,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatInstallAction(actionKind));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillUpdateActionKind.Created, "created")]
    [InlineData(SkillUpdateActionKind.Updated, "updated")]
    [InlineData(SkillUpdateActionKind.NoOp, "noOp")]
    [InlineData(SkillUpdateActionKind.BlockedLocalModification, "blockedLocalModification")]
    [InlineData(SkillUpdateActionKind.BlockedUnmanaged, "blockedUnmanaged")]
    [InlineData(SkillUpdateActionKind.BlockedVersionAhead, "blockedVersionAhead")]
    public void FormatUpdateAction_ReturnsStableLiteral (
        SkillUpdateActionKind actionKind,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatUpdateAction(actionKind));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillUninstallActionKind.Deleted, "deleted")]
    [InlineData(SkillUninstallActionKind.NoOp, "noOp")]
    [InlineData(SkillUninstallActionKind.SkippedUnmanaged, "skippedUnmanaged")]
    [InlineData(SkillUninstallActionKind.BlockedLocalModification, "blockedLocalModification")]
    public void FormatUninstallAction_ReturnsStableLiteral (
        SkillUninstallActionKind actionKind,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatUninstallAction(actionKind));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0, "changed")]
    [InlineData(1, "noOp")]
    [InlineData(2, "skipped")]
    [InlineData(3, "blocked")]
    public void FormatActionStatus_ReturnsStableLiteral (
        int statusValue,
        string expected)
    {
        var status = (SkillOperationActionStatus)statusValue;

        Assert.Equal(expected, SkillLiteralCodec.FormatActionStatus(status));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillBlockedReason.ManagedOverwriteRequiresForce, "managedOverwriteRequiresForce")]
    [InlineData(SkillBlockedReason.LocalModificationRequiresForce, "localModificationRequiresForce")]
    [InlineData(SkillBlockedReason.UnmanagedTarget, "unmanagedTarget")]
    [InlineData(SkillBlockedReason.InstalledVersionAhead, "installedVersionAhead")]
    public void FormatBlockedReason_ReturnsStableLiteral (
        SkillBlockedReason reason,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatBlockedReason(reason));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillTargetStateKind.Missing, "missing")]
    [InlineData(SkillTargetStateKind.Current, "current")]
    [InlineData(SkillTargetStateKind.CleanOutdated, "cleanOutdated")]
    [InlineData(SkillTargetStateKind.LocalModified, "localModification")]
    [InlineData(SkillTargetStateKind.Unmanaged, "unmanagedTarget")]
    [InlineData(SkillTargetStateKind.ManifestDrift, "manifestDrift")]
    [InlineData(SkillTargetStateKind.CommonContentDrift, "commonContentDrift")]
    [InlineData(SkillTargetStateKind.FrontmatterDrift, "frontmatterDrift")]
    [InlineData(SkillTargetStateKind.HostArtifactDrift, "hostArtifactDrift")]
    [InlineData(SkillTargetStateKind.FileSetDrift, "fileSetDrift")]
    [InlineData(SkillTargetStateKind.NameCollision, "nameCollision")]
    [InlineData(SkillTargetStateKind.HostConflict, "hostConflict")]
    [InlineData(SkillTargetStateKind.VersionAhead, "versionAhead")]
    [InlineData(SkillTargetStateKind.RemovedFromCatalog, "removedFromCatalog")]
    public void FormatTargetStateKind_ReturnsStableLiteral (
        SkillTargetStateKind kind,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatTargetStateKind(kind));
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ContractLiteralEnumTypes))]
    public void ContractLiteralEnums_DefineLiteralForEveryValue (Type enumType)
    {
        var method = typeof(ContractLiteralCodec)
            .GetMethod(nameof(ContractLiteralCodec.GetLiterals), Type.EmptyTypes)!
            .MakeGenericMethod(enumType);

        var literals = (IReadOnlyList<string>)method.Invoke(null, null)!;

        Assert.Equal(Enum.GetValues(enumType).Length, literals.Count);
        Assert.DoesNotContain(literals, string.IsNullOrWhiteSpace);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillDiffChangeKind.Added, "added")]
    [InlineData(SkillDiffChangeKind.Modified, "modified")]
    [InlineData(SkillDiffChangeKind.Deleted, "deleted")]
    public void FormatDiffChangeKind_ReturnsStableLiteral (
        SkillDiffChangeKind changeKind,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatDiffChangeKind(changeKind));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillDoctorSeverity.Info, "info")]
    [InlineData(SkillDoctorSeverity.Error, "error")]
    public void FormatDoctorSeverity_ReturnsStableLiteral (
        SkillDoctorSeverity severity,
        string expected)
    {
        Assert.Equal(expected, SkillLiteralCodec.FormatDoctorSeverity(severity));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FormatFailureCode_ReturnsStableValue ()
    {
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged.Value, SkillLiteralCodec.FormatFailureCode(SkillFailureCodes.InstallTargetUnmanaged));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FormatOperationLiterals_RejectUndefinedEnumValues ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatScope((SkillScopeKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatInstallAction((SkillInstallActionKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatTargetStateKind((SkillTargetStateKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLiteralCodec.FormatDoctorSeverity((SkillDoctorSeverity)999));
    }
}
