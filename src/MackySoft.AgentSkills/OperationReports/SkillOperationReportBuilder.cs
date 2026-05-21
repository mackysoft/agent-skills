using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Builds product-neutral reports from AgentSkills operation result models. </summary>
public static class SkillOperationReportBuilder
{
    private static readonly SkillOperationActionStatus[] StatusOrder =
    [
        SkillOperationActionStatus.Changed,
        SkillOperationActionStatus.NoOp,
        SkillOperationActionStatus.Skipped,
        SkillOperationActionStatus.Blocked,
    ];

    /// <summary> Creates list report data from canonical packages and host descriptors. </summary>
    /// <param name="packages"> The canonical packages to list. Must not be <see langword="null" />. </param>
    /// <param name="hostAdapters"> The supported host adapter set. Must not be <see langword="null" />. </param>
    /// <returns> A report whose skills and hosts are sorted using ordinal comparison. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="packages" /> or <paramref name="hostAdapters" /> is <see langword="null" />. </exception>
    public static SkillListReport CreateListReport (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(hostAdapters);

        var skills = packages
            .OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal)
            .Select(static package => SortHostArtifacts(package.Manifest))
            .ToArray();
        var hosts = hostAdapters.Adapters
            .Select(static adapter => adapter.Descriptor)
            .OrderBy(static descriptor => descriptor.HostKey, StringComparer.Ordinal)
            .ToArray();

        return new SkillListReport(skills, hosts);
    }

    /// <summary> Creates export report data from a successful export operation. </summary>
    /// <param name="outputPath"> The output directory or zip file path returned by export. Must not be null, empty, or whitespace. </param>
    /// <param name="packages"> The exported packages. Must not be <see langword="null" />. </param>
    /// <param name="hostDescriptor"> The descriptor for the host used for export. Must not be <see langword="null" />. </param>
    /// <param name="format"> The export format used for export. </param>
    /// <returns> A report whose skill names are sorted using ordinal comparison. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="packages" /> or <paramref name="hostDescriptor" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="outputPath" />, the host key, or reload guidance is null, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="format" /> is not a supported export format. </exception>
    public static SkillExportReport CreateExportReport (
        string outputPath,
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostDescriptor hostDescriptor,
        SkillExportFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(packages);
        ValidateHostDescriptor(hostDescriptor);

        var skills = packages
            .Select(static package => package.Manifest.SkillName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new SkillExportReport(
            hostDescriptor.HostKey,
            SkillLiteralCodec.FormatExportFormat(format),
            outputPath,
            skills,
            skills.Length,
            hostDescriptor.ReloadGuidance);
    }

    /// <summary> Creates product-neutral report data from a successful install operation. </summary>
    /// <param name="result"> The successful install result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the install operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an unsupported kind or invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, or diff change kind is unsupported. </exception>
    public static SkillOperationReport CreateInstallReport (
        SkillInstallResult result,
        SkillOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.TargetRoot,
            result.Actions,
            result.DryRun,
            result.Force,
            result.PrintDiff,
            context,
            static action => action.Identity,
            static action => SkillLiteralCodec.FormatInstallAction(action.ActionKind),
            static action => GetStatus(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static action => action.Diffs,
            Enum.GetValues<SkillInstallActionKind>()
                .OrderBy(static actionKind => (int)actionKind)
                .Select(SkillLiteralCodec.FormatInstallAction)
                .ToArray());
    }

    /// <summary> Creates product-neutral report data from a successful update operation. </summary>
    /// <param name="result"> The successful update result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the update operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an unsupported kind or invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, or diff change kind is unsupported. </exception>
    public static SkillOperationReport CreateUpdateReport (
        SkillUpdateResult result,
        SkillOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.TargetRoot,
            result.Actions,
            result.DryRun,
            result.Force,
            result.PrintDiff,
            context,
            static action => action.Identity,
            static action => SkillLiteralCodec.FormatUpdateAction(action.ActionKind),
            static action => GetStatus(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static action => action.Diffs,
            Enum.GetValues<SkillUpdateActionKind>()
                .OrderBy(static actionKind => (int)actionKind)
                .Select(SkillLiteralCodec.FormatUpdateAction)
                .ToArray());
    }

    /// <summary> Creates product-neutral report data from a successful uninstall operation. </summary>
    /// <param name="result"> The successful uninstall result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the uninstall operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an unsupported kind or invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, or diff change kind is unsupported. </exception>
    public static SkillOperationReport CreateUninstallReport (
        SkillUninstallResult result,
        SkillOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.TargetRoot,
            result.Actions,
            result.DryRun,
            result.Force,
            printDiff: false,
            context,
            static action => action.Identity,
            static action => SkillLiteralCodec.FormatUninstallAction(action.ActionKind),
            static action => GetStatus(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static _ => null,
            Enum.GetValues<SkillUninstallActionKind>()
                .OrderBy(static actionKind => (int)actionKind)
                .Select(SkillLiteralCodec.FormatUninstallAction)
                .ToArray());
    }

    /// <summary> Creates product-neutral report data from a doctor result. </summary>
    /// <param name="result"> The doctor result to report. Must not be <see langword="null" />. </param>
    /// <param name="scope"> The install scope used to resolve the diagnosed target root. </param>
    /// <returns> A report whose diagnostics are sorted deterministically. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="scope" /> is not a supported install scope. </exception>
    public static SkillDoctorReport CreateDoctorReport (
        SkillDoctorResult result,
        SkillScopeKind scope)
    {
        ArgumentNullException.ThrowIfNull(result);

        var diagnostics = result.Diagnostics
            .OrderBy(static diagnostic => diagnostic.SkillName is null ? 0 : 1)
            .ThenBy(static diagnostic => diagnostic.SkillName, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ThenBy(static diagnostic => (int)diagnostic.Severity)
            .Select(static diagnostic => new SkillDoctorDiagnosticReport(
                SkillLiteralCodec.FormatDoctorSeverity(diagnostic.Severity),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SkillName,
                ResolveDiagnosticTargetState(diagnostic.Code)))
            .ToArray();

        return new SkillDoctorReport(
            result.Host,
            SkillLiteralCodec.FormatScope(scope),
            result.TargetRoot,
            result.IsHealthy,
            diagnostics);
    }

    private static SkillOperationReport CreateOperationReport<TAction> (
        string targetRoot,
        IReadOnlyList<TAction> actions,
        bool dryRun,
        bool force,
        bool printDiff,
        SkillOperationReportContext context,
        Func<TAction, SkillInstallIdentity> getIdentity,
        Func<TAction, string> getActionLiteral,
        Func<TAction, SkillOperationActionStatus> getStatus,
        Func<TAction, SkillBlockedReason?> getBlockedReason,
        Func<TAction, SkillActionTargetState?> getTargetState,
        Func<TAction, SkillActionFileChanges?> getFileChanges,
        Func<TAction, IReadOnlyList<SkillActionDiff>?> getDiffs,
        IReadOnlyList<string> actionLiteralOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentNullException.ThrowIfNull(actions);
        ValidateHostDescriptor(context.HostDescriptor);

        var projectedActions = actions
            .OrderBy(action => getIdentity(action).SkillName, StringComparer.Ordinal)
            .Select(action => CreateActionReport(
                targetRoot,
                context,
                action,
                getIdentity,
                getActionLiteral,
                getStatus,
                getBlockedReason,
                getTargetState,
                getFileChanges,
                printDiff,
                getDiffs))
            .ToArray();

        return new SkillOperationReport(
            context.HostDescriptor.HostKey,
            SkillLiteralCodec.FormatScope(context.Scope),
            targetRoot,
            dryRun,
            force,
            printDiff,
            context.HostDescriptor.ReloadGuidance,
            projectedActions,
            CreateCounts(actionLiteralOrder, projectedActions, static action => action.Action),
            CreateCounts(
                StatusOrder.Select(SkillLiteralCodec.FormatActionStatus).ToArray(),
                projectedActions,
                static action => action.Status));
    }

    private static SkillOperationActionReport CreateActionReport<TAction> (
        string targetRoot,
        SkillOperationReportContext context,
        TAction action,
        Func<TAction, SkillInstallIdentity> getIdentity,
        Func<TAction, string> getActionLiteral,
        Func<TAction, SkillOperationActionStatus> getStatus,
        Func<TAction, SkillBlockedReason?> getBlockedReason,
        Func<TAction, SkillActionTargetState?> getTargetState,
        Func<TAction, SkillActionFileChanges?> getFileChanges,
        bool printDiff,
        Func<TAction, IReadOnlyList<SkillActionDiff>?> getDiffs)
    {
        var identity = getIdentity(action);
        ValidateIdentity(identity, targetRoot, context);

        var blockedReason = getBlockedReason(action);
        var status = getStatus(action);
        return new SkillOperationActionReport(
            identity.SkillName,
            getActionLiteral(action),
            SkillLiteralCodec.FormatActionStatus(status),
            blockedReason.HasValue ? SkillLiteralCodec.FormatBlockedReason(blockedReason.Value) : null,
            CreateTargetStateReport(getTargetState(action)),
            CreateFileChangesReport(getFileChanges(action)),
            CreateFileDiffReports(printDiff ? getDiffs(action) : null));
    }

    private static SkillOperationActionStatus GetStatus (SkillInstallActionKind actionKind)
    {
        return actionKind switch
        {
            SkillInstallActionKind.Created or SkillInstallActionKind.Updated => SkillOperationActionStatus.Changed,
            SkillInstallActionKind.NoOp => SkillOperationActionStatus.NoOp,
            SkillInstallActionKind.BlockedManagedOverwrite or SkillInstallActionKind.BlockedLocalModification or SkillInstallActionKind.BlockedUnmanaged => SkillOperationActionStatus.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Unsupported SKILL install action kind."),
        };
    }

    private static SkillOperationActionStatus GetStatus (SkillUpdateActionKind actionKind)
    {
        return actionKind switch
        {
            SkillUpdateActionKind.Created or SkillUpdateActionKind.Updated => SkillOperationActionStatus.Changed,
            SkillUpdateActionKind.NoOp => SkillOperationActionStatus.NoOp,
            SkillUpdateActionKind.BlockedLocalModification or SkillUpdateActionKind.BlockedUnmanaged => SkillOperationActionStatus.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Unsupported SKILL update action kind."),
        };
    }

    private static SkillOperationActionStatus GetStatus (SkillUninstallActionKind actionKind)
    {
        return actionKind switch
        {
            SkillUninstallActionKind.Deleted => SkillOperationActionStatus.Changed,
            SkillUninstallActionKind.NoOp => SkillOperationActionStatus.NoOp,
            SkillUninstallActionKind.SkippedUnmanaged => SkillOperationActionStatus.Skipped,
            SkillUninstallActionKind.BlockedLocalModification => SkillOperationActionStatus.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Unsupported SKILL uninstall action kind."),
        };
    }

    private static SkillTargetStateReport? CreateTargetStateReport (SkillActionTargetState? state)
    {
        if (state is null)
        {
            return null;
        }

        if (!Enum.TryParse<SkillInstalledTargetStateKind>(state.Kind, ignoreCase: false, out var stateKind))
        {
            throw new ArgumentException($"Unsupported SKILL target state kind: {state.Kind}", nameof(state));
        }

        return new SkillTargetStateReport(
            SkillLiteralCodec.FormatTargetStateKind(stateKind),
            state.Code.HasValue ? SkillLiteralCodec.FormatFailureCode(state.Code.Value) : null,
            state.Message,
            state.FileSet);
    }

    private static SkillActionFileChanges? CreateFileChangesReport (SkillActionFileChanges? fileChanges)
    {
        return fileChanges is null
            ? null
            : new SkillActionFileChanges(
                fileChanges.ReplacedFiles.Order(StringComparer.Ordinal).ToArray(),
                fileChanges.RemovedFiles.Order(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<SkillOperationFileDiffReport> CreateFileDiffReports (IReadOnlyList<SkillActionDiff>? diffs)
    {
        return (diffs ?? Array.Empty<SkillActionDiff>())
            .SelectMany(static diff => diff.Files)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ThenBy(static file => (int)file.ChangeKind)
            .Select(static file => new SkillOperationFileDiffReport(
                file.RelativePath,
                SkillLiteralCodec.FormatDiffChangeKind(file.ChangeKind),
                file.BeforeContent,
                file.AfterContent))
            .ToArray();
    }

    private static IReadOnlyList<SkillOperationCountReport> CreateCounts (
        IReadOnlyList<string> literalOrder,
        IReadOnlyList<SkillOperationActionReport> actions,
        Func<SkillOperationActionReport, string> getLiteral)
    {
        return literalOrder
            .Select(literal => new SkillOperationCountReport(
                literal,
                actions.Count(action => string.Equals(getLiteral(action), literal, StringComparison.Ordinal))))
            .ToArray();
    }

    private static string? ResolveDiagnosticTargetState (string code)
    {
        return SkillFailureCode.TryCreate(code, out var failureCode)
            && SkillInstalledTargetStateClassifier.TryResolveDriftKind(failureCode, out var stateKind)
                ? SkillLiteralCodec.FormatTargetStateKind(stateKind)
                : null;
    }

    private static SkillManifest SortHostArtifacts (SkillManifest manifest)
    {
        return manifest with
        {
            HostArtifacts = manifest.HostArtifacts
                .OrderBy(static artifact => artifact.Host, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static void ValidateIdentity (
        SkillInstallIdentity identity,
        string targetRoot,
        SkillOperationReportContext context)
    {
        if (!string.Equals(identity.Host, context.HostDescriptor.HostKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Operation report context host must match every action identity host.", nameof(context));
        }

        if (identity.Scope != context.Scope)
        {
            throw new ArgumentException("Operation report context scope must match every action identity scope.", nameof(context));
        }

        if (!string.Equals(identity.TargetRoot, targetRoot, StringComparison.Ordinal))
        {
            throw new ArgumentException("Operation result target root must match every action identity target root.", nameof(targetRoot));
        }
    }

    private static void ValidateHostDescriptor (SkillHostDescriptor hostDescriptor)
    {
        ArgumentNullException.ThrowIfNull(hostDescriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostDescriptor.HostKey, nameof(hostDescriptor));
        ArgumentException.ThrowIfNullOrWhiteSpace(hostDescriptor.ReloadGuidance, nameof(hostDescriptor));
    }
}
