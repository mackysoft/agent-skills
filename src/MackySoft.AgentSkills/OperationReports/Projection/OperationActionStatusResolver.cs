using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.OperationReports.Literals;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Resolves operation-specific action kinds to coarse operation report statuses. </summary>
internal static class OperationActionStatusResolver
{
    private static readonly ActionStatusDefinition<SkillInstallActionKind>[] InstallActionStatusDefinitions =
    [
        new(SkillInstallActionKind.Created, OperationActionStatus.Changed),
        new(SkillInstallActionKind.Updated, OperationActionStatus.Changed),
        new(SkillInstallActionKind.NoOp, OperationActionStatus.NoOp),
        new(SkillInstallActionKind.BlockedManagedOverwrite, OperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedLocalModification, OperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedUnmanaged, OperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillUpdateActionKind>[] UpdateActionStatusDefinitions =
    [
        new(SkillUpdateActionKind.Created, OperationActionStatus.Changed),
        new(SkillUpdateActionKind.Updated, OperationActionStatus.Changed),
        new(SkillUpdateActionKind.NoOp, OperationActionStatus.NoOp),
        new(SkillUpdateActionKind.BlockedLocalModification, OperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedUnmanaged, OperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedVersionAhead, OperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillUninstallActionKind>[] UninstallActionStatusDefinitions =
    [
        new(SkillUninstallActionKind.Deleted, OperationActionStatus.Changed),
        new(SkillUninstallActionKind.NoOp, OperationActionStatus.NoOp),
        new(SkillUninstallActionKind.SkippedUnmanaged, OperationActionStatus.Skipped),
        new(SkillUninstallActionKind.BlockedLocalModification, OperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillPruneActionKind>[] PruneActionStatusDefinitions =
    [
        new(SkillPruneActionKind.Deleted, OperationActionStatus.Changed),
        new(SkillPruneActionKind.SkippedCurrent, OperationActionStatus.NoOp),
        new(SkillPruneActionKind.SkippedForeignCatalog, OperationActionStatus.Skipped),
        new(SkillPruneActionKind.SkippedUnmanaged, OperationActionStatus.Skipped),
        new(SkillPruneActionKind.BlockedLocalModification, OperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedManifestInvalid, OperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedNameCollision, OperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedHostConflict, OperationActionStatus.Blocked),
    ];

    public static OperationActionStatus Resolve (SkillInstallActionKind actionKind)
    {
        return Resolve(InstallActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    public static OperationActionStatus Resolve (SkillUpdateActionKind actionKind)
    {
        return Resolve(UpdateActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    public static OperationActionStatus Resolve (SkillUninstallActionKind actionKind)
    {
        return Resolve(UninstallActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    public static OperationActionStatus Resolve (SkillPruneActionKind actionKind)
    {
        return Resolve(PruneActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    private static OperationActionStatus Resolve<TActionKind> (
        IReadOnlyList<ActionStatusDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        string parameterName)
        where TActionKind : struct, Enum
    {
        return TryGetDefinition(definitions, actionKind, out var definition)
            ? definition!.Status
            : throw new ArgumentOutOfRangeException(parameterName, actionKind, $"Unsupported SKILL {typeof(TActionKind).Name} value.");
    }

    private static bool TryGetDefinition<TActionKind> (
        IReadOnlyList<ActionStatusDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        out ActionStatusDefinition<TActionKind>? definition)
        where TActionKind : struct, Enum
    {
        foreach (var candidate in definitions)
        {
            if (EqualityComparer<TActionKind>.Default.Equals(candidate.Kind, actionKind))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null;
        return false;
    }

    private sealed class ActionStatusDefinition<TActionKind>
        where TActionKind : struct, Enum
    {
        internal ActionStatusDefinition (
            TActionKind kind,
            OperationActionStatus status)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported action kind.");
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported operation action status.");
            }

            Kind = kind;
            Status = status;
        }

        internal TActionKind Kind { get; }

        internal OperationActionStatus Status { get; }
    }
}
