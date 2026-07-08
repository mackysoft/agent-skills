using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class SkillOperationActionStatusResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillInstallActionKind.Created, "changed")]
    [InlineData(SkillInstallActionKind.Updated, "changed")]
    [InlineData(SkillInstallActionKind.NoOp, "noOp")]
    [InlineData(SkillInstallActionKind.BlockedManagedOverwrite, "blocked")]
    [InlineData(SkillInstallActionKind.BlockedLocalModification, "blocked")]
    [InlineData(SkillInstallActionKind.BlockedUnmanaged, "blocked")]
    public void Resolve_ReturnsInstallActionStatus (
        SkillInstallActionKind actionKind,
        string expectedStatus)
    {
        Assert.Equal(expectedStatus, ContractLiteralCodec.ToValue(SkillOperationActionStatusResolver.Resolve(actionKind)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillUpdateActionKind.Created, "changed")]
    [InlineData(SkillUpdateActionKind.Updated, "changed")]
    [InlineData(SkillUpdateActionKind.NoOp, "noOp")]
    [InlineData(SkillUpdateActionKind.BlockedLocalModification, "blocked")]
    [InlineData(SkillUpdateActionKind.BlockedUnmanaged, "blocked")]
    [InlineData(SkillUpdateActionKind.BlockedVersionAhead, "blocked")]
    public void Resolve_ReturnsUpdateActionStatus (
        SkillUpdateActionKind actionKind,
        string expectedStatus)
    {
        Assert.Equal(expectedStatus, ContractLiteralCodec.ToValue(SkillOperationActionStatusResolver.Resolve(actionKind)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillUninstallActionKind.Deleted, "changed")]
    [InlineData(SkillUninstallActionKind.NoOp, "noOp")]
    [InlineData(SkillUninstallActionKind.SkippedUnmanaged, "skipped")]
    [InlineData(SkillUninstallActionKind.BlockedLocalModification, "blocked")]
    public void Resolve_ReturnsUninstallActionStatus (
        SkillUninstallActionKind actionKind,
        string expectedStatus)
    {
        Assert.Equal(expectedStatus, ContractLiteralCodec.ToValue(SkillOperationActionStatusResolver.Resolve(actionKind)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillPruneActionKind.Deleted, "changed")]
    [InlineData(SkillPruneActionKind.SkippedCurrent, "noOp")]
    [InlineData(SkillPruneActionKind.SkippedForeignCatalog, "skipped")]
    [InlineData(SkillPruneActionKind.SkippedUnmanaged, "skipped")]
    [InlineData(SkillPruneActionKind.BlockedLocalModification, "blocked")]
    [InlineData(SkillPruneActionKind.BlockedManifestInvalid, "blocked")]
    [InlineData(SkillPruneActionKind.BlockedNameCollision, "blocked")]
    [InlineData(SkillPruneActionKind.BlockedHostConflict, "blocked")]
    public void Resolve_ReturnsPruneActionStatus (
        SkillPruneActionKind actionKind,
        string expectedStatus)
    {
        Assert.Equal(expectedStatus, ContractLiteralCodec.ToValue(SkillOperationActionStatusResolver.Resolve(actionKind)));
    }
}
