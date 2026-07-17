using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.OperationReports.Literals;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Resolves operation-specific action kinds to coarse operation report statuses. </summary>
internal static class SkillOperationActionStatusResolver
{
    private static readonly ActionStatusDefinition<SkillInstallActionKind>[] InstallActionStatusDefinitions =
    [
        new(SkillInstallActionKind.Created, SkillOperationActionStatus.Changed),
        new(SkillInstallActionKind.Updated, SkillOperationActionStatus.Changed),
        new(SkillInstallActionKind.NoOp, SkillOperationActionStatus.NoOp),
        new(SkillInstallActionKind.BlockedManagedOverwrite, SkillOperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedUnmanaged, SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillUpdateActionKind>[] UpdateActionStatusDefinitions =
    [
        new(SkillUpdateActionKind.Created, SkillOperationActionStatus.Changed),
        new(SkillUpdateActionKind.Updated, SkillOperationActionStatus.Changed),
        new(SkillUpdateActionKind.NoOp, SkillOperationActionStatus.NoOp),
        new(SkillUpdateActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedUnmanaged, SkillOperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedVersionAhead, SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillUninstallActionKind>[] UninstallActionStatusDefinitions =
    [
        new(SkillUninstallActionKind.Deleted, SkillOperationActionStatus.Changed),
        new(SkillUninstallActionKind.NoOp, SkillOperationActionStatus.NoOp),
        new(SkillUninstallActionKind.SkippedUnmanaged, SkillOperationActionStatus.Skipped),
        new(SkillUninstallActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillPruneActionKind>[] PruneActionStatusDefinitions =
    [
        new(SkillPruneActionKind.Deleted, SkillOperationActionStatus.Changed),
        new(SkillPruneActionKind.SkippedCurrent, SkillOperationActionStatus.NoOp),
        new(SkillPruneActionKind.SkippedForeignCatalog, SkillOperationActionStatus.Skipped),
        new(SkillPruneActionKind.SkippedUnmanaged, SkillOperationActionStatus.Skipped),
        new(SkillPruneActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedManifestInvalid, SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedNameCollision, SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedHostConflict, SkillOperationActionStatus.Blocked),
    ];

    public static SkillOperationActionStatus Resolve (SkillInstallActionKind actionKind)
    {
        return Resolve(InstallActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    public static SkillOperationActionStatus Resolve (SkillUpdateActionKind actionKind)
    {
        return Resolve(UpdateActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    public static SkillOperationActionStatus Resolve (SkillUninstallActionKind actionKind)
    {
        return Resolve(UninstallActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    public static SkillOperationActionStatus Resolve (SkillPruneActionKind actionKind)
    {
        return Resolve(PruneActionStatusDefinitions, actionKind, nameof(actionKind));
    }

    private static SkillOperationActionStatus Resolve<TActionKind> (
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
            SkillOperationActionStatus status)
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

        internal SkillOperationActionStatus Status { get; }
    }
}
