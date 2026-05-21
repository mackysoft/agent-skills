using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

internal static class SkillInstalledTargetStateKindExtensions
{
    private static readonly DriftClassification[] DriftClassifications =
    [
        new(SkillFailureCodes.InstallTargetManifestDigestMismatch, SkillInstalledTargetStateKind.ManifestDrift, 0),
        new(SkillFailureCodes.InstallTargetHostArtifactDigestMismatch, SkillInstalledTargetStateKind.HostArtifactDrift, 1),
        new(SkillFailureCodes.InstallTargetFileSetMismatch, SkillInstalledTargetStateKind.FileSetDrift, 2),
        new(SkillFailureCodes.InstallTargetFrontmatterDigestMismatch, SkillInstalledTargetStateKind.FrontmatterDrift, 3),
        new(SkillFailureCodes.InstallTargetContentDigestMismatch, SkillInstalledTargetStateKind.CommonContentDrift, 4),
        new(SkillFailureCodes.InstallTargetDigestMismatch, SkillInstalledTargetStateKind.LocalModified, 5),
        new(SkillFailureCodes.InstallTargetLocalModification, SkillInstalledTargetStateKind.LocalModified, 5),
    ];

    public static bool TryResolveDriftKind (
        SkillFailureCode code,
        out SkillInstalledTargetStateKind kind)
    {
        foreach (var classification in DriftClassifications)
        {
            if (classification.Code == code)
            {
                kind = classification.Kind;
                return true;
            }
        }

        kind = default;
        return false;
    }

    public static int GetDriftPriority (this SkillInstalledTargetStateKind kind)
    {
        var priority = int.MaxValue;
        foreach (var classification in DriftClassifications)
        {
            if (classification.Kind == kind && classification.Priority < priority)
            {
                priority = classification.Priority;
            }
        }

        return priority;
    }

    public static bool IsLocalModificationDrift (this SkillInstalledTargetStateKind kind)
    {
        return kind.GetDriftPriority() != int.MaxValue;
    }

    private readonly record struct DriftClassification (
        SkillFailureCode Code,
        SkillInstalledTargetStateKind Kind,
        int Priority);
}
