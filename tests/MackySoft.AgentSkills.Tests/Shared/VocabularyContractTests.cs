using MackySoft.AgentSkills.Agents.Doctor;
using MackySoft.AgentSkills.Agents.Installation.Results;
using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class VocabularyContractTests
{
    public static TheoryData<Type, string[]> StableVocabularyContracts => new()
    {
        { typeof(HostKind), ["codex", "claude-code", "github-copilot"] },
        { typeof(SkillBundleTargetRootLayout), ["flat", "catalog-directory"] },
        { typeof(SkillScopeKind), ["project", "user"] },
        { typeof(SkillExportFormat), ["directory", "zip"] },
        { typeof(SkillInstallActionKind), ["created", "updated", "noOp", "blockedManagedOverwrite", "blockedLocalModification", "blockedUnmanaged"] },
        { typeof(SkillUpdateActionKind), ["created", "updated", "noOp", "blockedLocalModification", "blockedUnmanaged", "blockedVersionAhead"] },
        { typeof(SkillUninstallActionKind), ["deleted", "noOp", "skippedUnmanaged", "blockedLocalModification"] },
        {
            typeof(SkillPruneActionKind),
            [
                "deleted",
                "skippedCurrent",
                "skippedForeignCatalog",
                "skippedUnmanaged",
                "blockedLocalModification",
                "blockedManifestInvalid",
                "blockedNameCollision",
                "blockedHostConflict",
            ]
        },
        { typeof(OperationActionStatus), ["changed", "noOp", "skipped", "blocked"] },
        { typeof(OperationScopeKind), ["project", "user"] },
        { typeof(AgentDiagnosticArea), ["package", "hostArtifact", "targetState"] },
        { typeof(AgentOperationTargetState), ["missing", "current", "locallyModified", "unmanaged", "otherCatalog", "invalid", "cleanOutdated"] },
        {
            typeof(SkillBlockedReason),
            [
                "managedOverwriteRequiresForce",
                "localModificationRequiresForce",
                "unmanagedTarget",
                "installedVersionAhead",
            ]
        },
        {
            typeof(SkillTargetStateKind),
            [
                "missing",
                "current",
                "cleanOutdated",
                "localModification",
                "unmanagedTarget",
                "manifestDrift",
                "commonContentDrift",
                "frontmatterDrift",
                "hostArtifactDrift",
                "fileSetDrift",
                "nameCollision",
                "hostConflict",
                "versionAhead",
                "removedFromCatalog",
            ]
        },
        { typeof(SkillDiffChangeKind), ["added", "modified", "deleted"] },
        { typeof(SkillDoctorSeverity), ["info", "error"] },
        { typeof(AgentInstallScopeKind), ["project", "user"] },
        { typeof(AgentDoctorDiagnosticArea), ["package", "hostArtifact", "targetState"] },
        { typeof(AgentInstalledTargetStateKind), ["missing", "current", "locallyModified", "unmanaged", "otherCatalog", "invalid", "cleanOutdated"] },
        {
            typeof(AgentReconcileActionKind),
            [
                "created",
                "updated",
                "noOp",
                "blockedManagedOverwrite",
                "blockedLocalModification",
                "blockedUnmanaged",
                "blockedForeignCatalog",
                "blockedInvalid",
            ]
        },
        {
            typeof(AgentRemovalActionKind),
            [
                "deleted",
                "noOp",
                "skippedCurrent",
                "blockedLocalModification",
                "blockedUnmanaged",
                "blockedForeignCatalog",
                "blockedInvalid",
            ]
        },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(StableVocabularyContracts))]
    public void GetTexts_ReturnsStableProductContract (
        Type vocabularyType,
        string[] expectedTexts)
    {
        var method = typeof(Vocabulary)
            .GetMethod(nameof(Vocabulary.GetTexts), Type.EmptyTypes)!
            .MakeGenericMethod(vocabularyType);

        var actualTexts = (IReadOnlyList<string>)method.Invoke(null, null)!;

        Assert.Equal(expectedTexts, actualTexts);
    }
}
