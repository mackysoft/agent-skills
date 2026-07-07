using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Provides shared classification for installed target state drift failure codes. </summary>
internal static class SkillInstalledTargetStateClassifier
{
    private static readonly StateClassification[] DriftClassifications =
    [
        new(SkillFailureCodes.InstallTargetManifestDigestMismatch, SkillInstalledTargetStateKind.ManifestDrift, 0),
        new(SkillFailureCodes.InstallTargetHostArtifactDigestMismatch, SkillInstalledTargetStateKind.HostArtifactDrift, 1),
        new(SkillFailureCodes.InstallTargetFileSetMismatch, SkillInstalledTargetStateKind.FileSetDrift, 2),
        new(SkillFailureCodes.InstallTargetFrontmatterDigestMismatch, SkillInstalledTargetStateKind.FrontmatterDrift, 3),
        new(SkillFailureCodes.InstallTargetContentDigestMismatch, SkillInstalledTargetStateKind.CommonContentDrift, 4),
        new(SkillFailureCodes.InstallTargetDigestMismatch, SkillInstalledTargetStateKind.LocalModified, 5),
        new(SkillFailureCodes.InstallTargetLocalModification, SkillInstalledTargetStateKind.LocalModified, 5),
    ];

    /// <summary> Resolves a validation failure code to the target state kind that represents a drifted managed target. </summary>
    /// <param name="code"> The failure code emitted by target validation or installed package integrity verification. </param>
    /// <param name="kind"> The resolved drift state kind when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="code" /> represents a managed target drift state; otherwise <see langword="false" />. </returns>
    public static bool TryResolveDriftKind (
        SkillFailureCode code,
        out SkillInstalledTargetStateKind kind)
    {
        return TryResolve(DriftClassifications, code, out kind);
    }

    /// <summary> Resolves a failure code to the target state kind that should be exposed in operation reports. </summary>
    /// <param name="code"> The failure code emitted by target analysis, validation, or doctor diagnostics. </param>
    /// <param name="kind"> The resolved target state kind when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="code" /> has a target state representation; otherwise <see langword="false" />. </returns>
    public static bool TryResolveReportableKind (
        SkillFailureCode code,
        out SkillInstalledTargetStateKind kind)
    {
        if (code == SkillFailureCodes.InstallTargetOutdated)
        {
            kind = SkillInstalledTargetStateKind.CleanOutdated;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetVersionAhead)
        {
            kind = SkillInstalledTargetStateKind.VersionAhead;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetRemovedFromCatalog)
        {
            kind = SkillInstalledTargetStateKind.RemovedFromCatalog;
            return true;
        }

        return TryResolveDriftKind(code, out kind);
    }

    /// <summary> Gets the ordering priority used when multiple managed drift signals are present for the same target. </summary>
    /// <param name="kind"> The target state kind to classify. </param>
    /// <returns> The drift priority where lower values win; <see cref="int.MaxValue" /> when <paramref name="kind" /> is not a managed drift state. </returns>
    public static int GetDriftPriority (SkillInstalledTargetStateKind kind)
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

    /// <summary> Gets whether an install, update, or uninstall operation must treat the state as local modification drift. </summary>
    /// <param name="kind"> The target state kind to classify. </param>
    /// <returns> <see langword="true" /> when the state is a managed drift state that requires force to replace or delete; otherwise <see langword="false" />. </returns>
    public static bool IsLocalModificationDrift (SkillInstalledTargetStateKind kind)
    {
        return GetDriftPriority(kind) != int.MaxValue;
    }

    private static bool TryResolve (
        IReadOnlyList<StateClassification> classifications,
        SkillFailureCode code,
        out SkillInstalledTargetStateKind kind)
    {
        foreach (var classification in classifications)
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

    private readonly record struct StateClassification (
        SkillFailureCode Code,
        SkillInstalledTargetStateKind Kind,
        int Priority);
}
