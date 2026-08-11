namespace MackySoft.AgentDistribution.Shared;

/// <summary> Resolves target-state kinds from installation failure codes and classifies managed drift states. </summary>
internal static class SkillTargetStateClassifier
{
    private static readonly StateClassification[] DriftClassifications =
    [
        new(AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch, SkillTargetStateKind.ManifestDrift, 0),
        new(AgentDistributionFailureCodes.InstallTargetHostArtifactDigestMismatch, SkillTargetStateKind.HostArtifactDrift, 1),
        new(AgentDistributionFailureCodes.InstallTargetFileSetMismatch, SkillTargetStateKind.FileSetDrift, 2),
        new(AgentDistributionFailureCodes.InstallTargetFrontmatterDigestMismatch, SkillTargetStateKind.FrontmatterDrift, 3),
        new(AgentDistributionFailureCodes.InstallTargetContentDigestMismatch, SkillTargetStateKind.CommonContentDrift, 4),
        new(AgentDistributionFailureCodes.InstallTargetDigestMismatch, SkillTargetStateKind.LocalModified, 5),
        new(AgentDistributionFailureCodes.InstallTargetLocalModification, SkillTargetStateKind.LocalModified, 5),
    ];

    private static readonly StateClassification[] BlockingClassifications =
    [
        new(AgentDistributionFailureCodes.InstallTargetUnmanaged, SkillTargetStateKind.Unmanaged, 0),
        new(AgentDistributionFailureCodes.InstallTargetNameCollision, SkillTargetStateKind.NameCollision, 0),
        new(AgentDistributionFailureCodes.InstallTargetHostConflict, SkillTargetStateKind.HostConflict, 0),
    ];

    /// <summary> Resolves a failure code that represents managed target drift. </summary>
    public static bool TryResolveDriftKind (
        AgentDistributionFailureCode code,
        out SkillTargetStateKind kind)
    {
        return TryResolve(DriftClassifications, code, out kind);
    }

    /// <summary> Resolves a failure code that represents a non-drift target state. </summary>
    public static bool TryResolveNonDriftFailureKind (
        AgentDistributionFailureCode code,
        out SkillTargetStateKind kind)
    {
        if (TryResolveBlockingKind(code, out kind))
        {
            return true;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetOutdated)
        {
            kind = SkillTargetStateKind.CleanOutdated;
            return true;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetVersionAhead)
        {
            kind = SkillTargetStateKind.VersionAhead;
            return true;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog)
        {
            kind = SkillTargetStateKind.RemovedFromCatalog;
            return true;
        }

        kind = default;
        return false;
    }

    /// <summary> Resolves a failure code that blocks normal replacement or deletion. </summary>
    public static bool TryResolveBlockingKind (
        AgentDistributionFailureCode code,
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
        AgentDistributionFailureCode code,
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
            AgentDistributionFailureCode code,
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

        public AgentDistributionFailureCode Code { get; }

        public SkillTargetStateKind Kind { get; }

        public int Priority { get; }
    }
}
