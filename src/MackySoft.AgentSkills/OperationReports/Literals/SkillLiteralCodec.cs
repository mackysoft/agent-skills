using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Converts AgentSkills domain values to and from stable product-neutral literals. </summary>
public static class SkillLiteralCodec
{
    private static readonly ActionLiteralDefinition<SkillInstallActionKind>[] InstallActionDefinitions =
    [
        new(SkillInstallActionKind.Created, "created", SkillOperationActionStatus.Changed),
        new(SkillInstallActionKind.Updated, "updated", SkillOperationActionStatus.Changed),
        new(SkillInstallActionKind.NoOp, "noOp", SkillOperationActionStatus.NoOp),
        new(SkillInstallActionKind.BlockedManagedOverwrite, "blockedManagedOverwrite", SkillOperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedLocalModification, "blockedLocalModification", SkillOperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedUnmanaged, "blockedUnmanaged", SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionLiteralDefinition<SkillUpdateActionKind>[] UpdateActionDefinitions =
    [
        new(SkillUpdateActionKind.Created, "created", SkillOperationActionStatus.Changed),
        new(SkillUpdateActionKind.Updated, "updated", SkillOperationActionStatus.Changed),
        new(SkillUpdateActionKind.NoOp, "noOp", SkillOperationActionStatus.NoOp),
        new(SkillUpdateActionKind.BlockedLocalModification, "blockedLocalModification", SkillOperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedUnmanaged, "blockedUnmanaged", SkillOperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedVersionAhead, "blockedVersionAhead", SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionLiteralDefinition<SkillUninstallActionKind>[] UninstallActionDefinitions =
    [
        new(SkillUninstallActionKind.Deleted, "deleted", SkillOperationActionStatus.Changed),
        new(SkillUninstallActionKind.NoOp, "noOp", SkillOperationActionStatus.NoOp),
        new(SkillUninstallActionKind.SkippedUnmanaged, "skippedUnmanaged", SkillOperationActionStatus.Skipped),
        new(SkillUninstallActionKind.BlockedLocalModification, "blockedLocalModification", SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionLiteralDefinition<SkillPruneActionKind>[] PruneActionDefinitions =
    [
        new(SkillPruneActionKind.Deleted, "deleted", SkillOperationActionStatus.Changed),
        new(SkillPruneActionKind.SkippedCurrent, "skippedCurrent", SkillOperationActionStatus.NoOp),
        new(SkillPruneActionKind.SkippedForeignCatalog, "skippedForeignCatalog", SkillOperationActionStatus.Skipped),
        new(SkillPruneActionKind.SkippedUnmanaged, "skippedUnmanaged", SkillOperationActionStatus.Skipped),
        new(SkillPruneActionKind.BlockedLocalModification, "blockedLocalModification", SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedManifestInvalid, "blockedManifestInvalid", SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedNameCollision, "blockedNameCollision", SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedHostConflict, "blockedHostConflict", SkillOperationActionStatus.Blocked),
    ];

    private static readonly SkillOperationActionStatus[] ActionStatusOrder =
    [
        SkillOperationActionStatus.Changed,
        SkillOperationActionStatus.NoOp,
        SkillOperationActionStatus.Skipped,
        SkillOperationActionStatus.Blocked,
    ];

    /// <summary> Normalizes a supported host literal to the canonical host key. </summary>
    /// <param name="host"> The caller-provided host literal. Null, empty, and whitespace values return a host-unsupported failure. </param>
    /// <param name="hostAdapters"> The supported host adapter set used as the canonical host source. </param>
    /// <returns> The canonical host key, or a host-unsupported failure when <paramref name="host" /> is not supported. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="hostAdapters" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<string> NormalizeHost (
        string? host,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(hostAdapters);

        var adapterResult = hostAdapters.GetAdapter(host!);
        return adapterResult.IsSuccess
            ? SkillOperationResult<string>.Success(adapterResult.Value!.Descriptor.HostKey)
            : SkillOperationResult<string>.FailureResult(adapterResult.Failure!.Code, adapterResult.Failure.Message);
    }

    /// <summary> Formats an install scope as a stable lower camel literal. </summary>
    /// <param name="scope"> The install scope to format. </param>
    /// <returns> The stable literal for <paramref name="scope" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="scope" /> is not a defined <see cref="SkillScopeKind" /> value. </exception>
    public static string FormatScope (SkillScopeKind scope)
    {
        return scope switch
        {
            SkillScopeKind.Project => "project",
            SkillScopeKind.User => "user",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope."),
        };
    }

    /// <summary> Parses a stable install scope literal. </summary>
    /// <param name="literal"> The scope literal to parse. Null, empty, and whitespace values are rejected. </param>
    /// <param name="scope"> The parsed scope when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="literal" /> is a supported scope literal; otherwise <see langword="false" />. </returns>
    public static bool TryParseScope (
        string? literal,
        out SkillScopeKind scope)
    {
        if (string.Equals(literal, "project", StringComparison.OrdinalIgnoreCase))
        {
            scope = SkillScopeKind.Project;
            return true;
        }

        if (string.Equals(literal, "user", StringComparison.OrdinalIgnoreCase))
        {
            scope = SkillScopeKind.User;
            return true;
        }

        scope = default;
        return false;
    }

    /// <summary> Formats an export format as a stable lower camel literal. </summary>
    /// <param name="format"> The export format to format. </param>
    /// <returns> The stable literal for <paramref name="format" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="format" /> is not a defined <see cref="SkillExportFormat" /> value. </exception>
    public static string FormatExportFormat (SkillExportFormat format)
    {
        return format switch
        {
            SkillExportFormat.Directory => "directory",
            SkillExportFormat.Zip => "zip",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported SKILL export format."),
        };
    }

    /// <summary> Parses a stable export format literal. </summary>
    /// <param name="literal"> The export format literal to parse. Null, empty, and whitespace values are rejected. </param>
    /// <param name="format"> The parsed export format when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="literal" /> is a supported export format literal; otherwise <see langword="false" />. </returns>
    public static bool TryParseExportFormat (
        string? literal,
        out SkillExportFormat format)
    {
        if (string.Equals(literal, "directory", StringComparison.OrdinalIgnoreCase))
        {
            format = SkillExportFormat.Directory;
            return true;
        }

        if (string.Equals(literal, "zip", StringComparison.OrdinalIgnoreCase))
        {
            format = SkillExportFormat.Zip;
            return true;
        }

        format = default;
        return false;
    }

    /// <summary> Formats an install action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The install action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillInstallActionKind" /> value. </exception>
    public static string FormatInstallAction (SkillInstallActionKind actionKind)
    {
        return FormatAction(InstallActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL install action kind.");
    }

    /// <summary> Formats an update action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The update action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillUpdateActionKind" /> value. </exception>
    public static string FormatUpdateAction (SkillUpdateActionKind actionKind)
    {
        return FormatAction(UpdateActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL update action kind.");
    }

    /// <summary> Formats an uninstall action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The uninstall action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillUninstallActionKind" /> value. </exception>
    public static string FormatUninstallAction (SkillUninstallActionKind actionKind)
    {
        return FormatAction(UninstallActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL uninstall action kind.");
    }

    /// <summary> Formats a prune action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The prune action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillPruneActionKind" /> value. </exception>
    public static string FormatPruneAction (SkillPruneActionKind actionKind)
    {
        return FormatAction(PruneActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL prune action kind.");
    }

    /// <summary> Formats a coarse action status as a stable lower camel literal. </summary>
    /// <param name="status"> The operation action status to format. </param>
    /// <returns> The stable literal for <paramref name="status" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="status" /> is not a defined <see cref="SkillOperationActionStatus" /> value. </exception>
    internal static string FormatActionStatus (SkillOperationActionStatus status)
    {
        return status switch
        {
            SkillOperationActionStatus.Changed => "changed",
            SkillOperationActionStatus.NoOp => "noOp",
            SkillOperationActionStatus.Skipped => "skipped",
            SkillOperationActionStatus.Blocked => "blocked",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported SKILL operation action status."),
        };
    }

    /// <summary> Formats a blocked reason as a stable lower camel literal. </summary>
    /// <param name="reason"> The blocked reason to format. </param>
    /// <returns> The stable literal for <paramref name="reason" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="reason" /> is not a defined <see cref="SkillBlockedReason" /> value. </exception>
    public static string FormatBlockedReason (SkillBlockedReason reason)
    {
        return reason switch
        {
            SkillBlockedReason.ManagedOverwriteRequiresForce => "managedOverwriteRequiresForce",
            SkillBlockedReason.LocalModificationRequiresForce => "localModificationRequiresForce",
            SkillBlockedReason.UnmanagedTarget => "unmanagedTarget",
            SkillBlockedReason.InstalledVersionAhead => "installedVersionAhead",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unsupported SKILL blocked reason."),
        };
    }

    /// <summary> Formats a target state kind as a stable lower camel literal. </summary>
    /// <param name="kind"> The target state kind to format. </param>
    /// <returns> The stable literal for <paramref name="kind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not a defined <see cref="SkillInstalledTargetStateKind" /> value. </exception>
    public static string FormatTargetStateKind (SkillInstalledTargetStateKind kind)
    {
        return kind switch
        {
            SkillInstalledTargetStateKind.Missing => "missing",
            SkillInstalledTargetStateKind.Current => "current",
            SkillInstalledTargetStateKind.CleanOutdated => "cleanOutdated",
            SkillInstalledTargetStateKind.LocalModified => "localModification",
            SkillInstalledTargetStateKind.Unmanaged => "unmanagedTarget",
            SkillInstalledTargetStateKind.ManifestDrift => "manifestDrift",
            SkillInstalledTargetStateKind.CommonContentDrift => "commonContentDrift",
            SkillInstalledTargetStateKind.FrontmatterDrift => "frontmatterDrift",
            SkillInstalledTargetStateKind.HostArtifactDrift => "hostArtifactDrift",
            SkillInstalledTargetStateKind.FileSetDrift => "fileSetDrift",
            SkillInstalledTargetStateKind.NameCollision => "nameCollision",
            SkillInstalledTargetStateKind.HostConflict => "hostConflict",
            SkillInstalledTargetStateKind.VersionAhead => "versionAhead",
            SkillInstalledTargetStateKind.RemovedFromCatalog => "removedFromCatalog",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported SKILL target state kind."),
        };
    }

    /// <summary> Formats a diff change kind as a stable lower camel literal. </summary>
    /// <param name="changeKind"> The diff change kind to format. </param>
    /// <returns> The stable literal for <paramref name="changeKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="changeKind" /> is not a defined <see cref="SkillDiffChangeKind" /> value. </exception>
    public static string FormatDiffChangeKind (SkillDiffChangeKind changeKind)
    {
        return changeKind switch
        {
            SkillDiffChangeKind.Added => "added",
            SkillDiffChangeKind.Modified => "modified",
            SkillDiffChangeKind.Deleted => "deleted",
            _ => throw new ArgumentOutOfRangeException(nameof(changeKind), changeKind, "Unsupported SKILL diff change kind."),
        };
    }

    /// <summary> Formats a doctor severity as a stable lower camel literal. </summary>
    /// <param name="severity"> The doctor severity to format. </param>
    /// <returns> The stable literal for <paramref name="severity" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="severity" /> is not a defined <see cref="SkillDoctorSeverity" /> value. </exception>
    public static string FormatDoctorSeverity (SkillDoctorSeverity severity)
    {
        return severity switch
        {
            SkillDoctorSeverity.Info => "info",
            SkillDoctorSeverity.Error => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported SKILL doctor severity."),
        };
    }

    /// <summary> Formats a failure code as its stable machine-readable value. </summary>
    /// <param name="code"> The failure code to format. Must contain a non-empty value. </param>
    /// <returns> The raw stable failure code value. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="code" /> does not contain a valid value. </exception>
    public static string FormatFailureCode (SkillFailureCode code)
    {
        if (!code.IsValid)
        {
            throw new ArgumentException("Failure code must be valid.", nameof(code));
        }

        return code.Value;
    }

    internal static IReadOnlyList<string> GetInstallActionLiterals ()
    {
        return GetActionLiterals(InstallActionDefinitions);
    }

    internal static IReadOnlyList<string> GetUpdateActionLiterals ()
    {
        return GetActionLiterals(UpdateActionDefinitions);
    }

    internal static IReadOnlyList<string> GetUninstallActionLiterals ()
    {
        return GetActionLiterals(UninstallActionDefinitions);
    }

    internal static IReadOnlyList<string> GetPruneActionLiterals ()
    {
        return GetActionLiterals(PruneActionDefinitions);
    }

    internal static IReadOnlyList<string> GetActionStatusLiterals ()
    {
        return ActionStatusOrder
            .Select(FormatActionStatus)
            .ToArray();
    }

    internal static SkillOperationActionStatus GetInstallActionStatus (SkillInstallActionKind actionKind)
    {
        return GetActionStatus(InstallActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL install action kind.");
    }

    internal static SkillOperationActionStatus GetUpdateActionStatus (SkillUpdateActionKind actionKind)
    {
        return GetActionStatus(UpdateActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL update action kind.");
    }

    internal static SkillOperationActionStatus GetUninstallActionStatus (SkillUninstallActionKind actionKind)
    {
        return GetActionStatus(UninstallActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL uninstall action kind.");
    }

    internal static SkillOperationActionStatus GetPruneActionStatus (SkillPruneActionKind actionKind)
    {
        return GetActionStatus(PruneActionDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL prune action kind.");
    }

    private static IReadOnlyList<string> GetActionLiterals<TActionKind> (IReadOnlyList<ActionLiteralDefinition<TActionKind>> definitions)
        where TActionKind : struct, Enum
    {
        return definitions
            .Select(static definition => definition.Literal)
            .ToArray();
    }

    private static string FormatAction<TActionKind> (
        IReadOnlyList<ActionLiteralDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        string parameterName,
        string message)
        where TActionKind : struct, Enum
    {
        return TryGetActionDefinition(definitions, actionKind, out var definition)
            ? definition.Literal
            : throw new ArgumentOutOfRangeException(parameterName, actionKind, message);
    }

    private static SkillOperationActionStatus GetActionStatus<TActionKind> (
        IReadOnlyList<ActionLiteralDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        string parameterName,
        string message)
        where TActionKind : struct, Enum
    {
        return TryGetActionDefinition(definitions, actionKind, out var definition)
            ? definition.Status
            : throw new ArgumentOutOfRangeException(parameterName, actionKind, message);
    }

    private static bool TryGetActionDefinition<TActionKind> (
        IReadOnlyList<ActionLiteralDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        out ActionLiteralDefinition<TActionKind> definition)
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

        definition = default;
        return false;
    }

    private readonly record struct ActionLiteralDefinition<TActionKind> (
        TActionKind Kind,
        string Literal,
        SkillOperationActionStatus Status)
        where TActionKind : struct, Enum;
}
