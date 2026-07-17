namespace MackySoft.AgentSkills.Shared;

/// <summary> Resolves target-state kinds from installation failure codes and classifies managed drift states. </summary>
internal static class SkillTargetStateClassifier
{
    private static readonly StateClassification[] DriftClassifications =
    [
        new(SkillFailureCodes.InstallTargetManifestDigestMismatch, SkillTargetStateKind.ManifestDrift, 0),
        new(SkillFailureCodes.InstallTargetHostArtifactDigestMismatch, SkillTargetStateKind.HostArtifactDrift, 1),
        new(SkillFailureCodes.InstallTargetFileSetMismatch, SkillTargetStateKind.FileSetDrift, 2),
        new(SkillFailureCodes.InstallTargetFrontmatterDigestMismatch, SkillTargetStateKind.FrontmatterDrift, 3),
        new(SkillFailureCodes.InstallTargetContentDigestMismatch, SkillTargetStateKind.CommonContentDrift, 4),
        new(SkillFailureCodes.InstallTargetDigestMismatch, SkillTargetStateKind.LocalModified, 5),
        new(SkillFailureCodes.InstallTargetLocalModification, SkillTargetStateKind.LocalModified, 5),
    ];

    private static readonly StateClassification[] BlockingClassifications =
    [
        new(SkillFailureCodes.InstallTargetUnmanaged, SkillTargetStateKind.Unmanaged, 0),
        new(SkillFailureCodes.InstallTargetNameCollision, SkillTargetStateKind.NameCollision, 0),
        new(SkillFailureCodes.InstallTargetHostConflict, SkillTargetStateKind.HostConflict, 0),
    ];

    /// <summary> Resolves a failure code that represents managed target drift. </summary>
    public static bool TryResolveDriftKind (
        SkillFailureCode code,
        out SkillTargetStateKind kind)
    {
        return TryResolve(DriftClassifications, code, out kind);
    }

    /// <summary> Resolves a failure code that represents a non-drift target state. </summary>
    public static bool TryResolveNonDriftFailureKind (
        SkillFailureCode code,
        out SkillTargetStateKind kind)
    {
        if (TryResolveBlockingKind(code, out kind))
        {
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetOutdated)
        {
            kind = SkillTargetStateKind.CleanOutdated;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetVersionAhead)
        {
            kind = SkillTargetStateKind.VersionAhead;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetRemovedFromCatalog)
        {
            kind = SkillTargetStateKind.RemovedFromCatalog;
            return true;
        }

        kind = default;
        return false;
    }

    /// <summary> Resolves a failure code that blocks normal replacement or deletion. </summary>
    public static bool TryResolveBlockingKind (
        SkillFailureCode code,
        out SkillTargetStateKind kind)
    {
        return TryResolve(BlockingClassifications, code, out kind);
    }

    /// <summary> Gets the ordering priority used when multiple managed drift signals are present. </summary>
    public static int GetDriftPriority (SkillTargetStateKind kind)
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

    /// <summary> Gets whether the state is managed drift that requires force to replace or delete. </summary>
    public static bool IsLocalModificationDrift (SkillTargetStateKind kind)
    {
        return GetDriftPriority(kind) != int.MaxValue;
    }

    private static bool TryResolve (
        IReadOnlyList<StateClassification> classifications,
        SkillFailureCode code,
        out SkillTargetStateKind kind)
    {
        ArgumentNullException.ThrowIfNull(code);

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

    private sealed class StateClassification
    {
        public StateClassification (
            SkillFailureCode code,
            SkillTargetStateKind kind,
            int priority)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported target state kind.");
            }

            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority), priority, "Classification priority must not be negative.");
            }

            Kind = kind;
            Priority = priority;
        }

        public SkillFailureCode Code { get; }

        public SkillTargetStateKind Kind { get; }

        public int Priority { get; }
    }
}
