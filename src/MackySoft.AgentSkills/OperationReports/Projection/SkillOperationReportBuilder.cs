using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Builds product-neutral reports from AgentSkills operation result models. </summary>
public static class SkillOperationReportBuilder
{
    /// <summary> Creates list report data from a validated package catalog and host descriptors. </summary>
    /// <param name="catalog"> The validated bundled package catalog. Must not be <see langword="null" />. </param>
    /// <param name="hostAdapters"> The supported host adapter set. Must not be <see langword="null" />. </param>
    /// <returns> A report whose skills and hosts are sorted using ordinal comparison. </returns>
    public static SkillListReport CreateListReport (
        SkillPackageCatalog catalog,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return CreateListReport(catalog.Packages, hostAdapters, catalog.SelectedTiers, catalog.AvailableTiers);
    }

    private static SkillListReport CreateListReport (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostAdapterSet hostAdapters,
        IReadOnlyList<SkillTier> selectedTiers,
        IReadOnlyList<SkillTierPackageCount> availableTiers)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(hostAdapters);
        ArgumentNullException.ThrowIfNull(selectedTiers);
        ArgumentNullException.ThrowIfNull(availableTiers);

        var skills = packages
            .OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal)
            .Select(static package => CreateListSkillReport(package))
            .ToArray();
        var tierReports = CreateAvailableTierReports(availableTiers);
        var hosts = hostAdapters.Adapters
            .Select(static adapter => adapter.Descriptor)
            .OrderBy(static descriptor => descriptor.HostKey, StringComparer.Ordinal)
            .Select(static descriptor => CreateHostReport(descriptor))
            .ToArray();

        return new SkillListReport(CreateTierLiterals(selectedTiers), tierReports, skills, hosts);
    }

    /// <summary> Creates export report data from a successful export operation. </summary>
    /// <param name="outputPath"> The output directory or zip file path returned by export. Must not be null, empty, or whitespace. </param>
    /// <param name="packages"> The exported packages. Must not be <see langword="null" />. </param>
    /// <param name="hostDescriptor"> The descriptor for the host used for export. Must not be <see langword="null" />. </param>
    /// <param name="format"> The export format used for export. </param>
    /// <param name="selectedTiers"> The selected product-owned SKILL tiers. Must not be <see langword="null" />. </param>
    /// <returns> A report whose skill names are sorted using ordinal comparison. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="packages" />, <paramref name="hostDescriptor" />, <paramref name="selectedTiers" />, or an item in <paramref name="selectedTiers" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="outputPath" />, the host key, or reload guidance is null, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="format" /> is not a supported export format. </exception>
    public static SkillExportReport CreateExportReport (
        string outputPath,
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostDescriptor hostDescriptor,
        SkillExportFormat format,
        IReadOnlyList<SkillTier> selectedTiers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(selectedTiers);
        ValidateHostDescriptor(hostDescriptor);

        var skills = packages
            .Select(static package => package.Manifest.SkillName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new SkillExportReport(
            hostDescriptor.HostKey,
            CreateTierLiterals(selectedTiers),
            SkillLiteralCodec.FormatExportFormat(format),
            outputPath,
            skills,
            skills.Length,
            hostDescriptor.ReloadGuidance);
    }

    /// <summary> Creates export report data from a successful export operation for one selected tier. </summary>
    public static SkillExportReport CreateExportReport (
        string outputPath,
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostDescriptor hostDescriptor,
        SkillExportFormat format,
        SkillTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);

        return CreateExportReport(outputPath, packages, hostDescriptor, format, [tier]);
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
            static action => SkillLiteralCodec.GetInstallActionStatus(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static action => action.Diffs,
            SkillLiteralCodec.GetInstallActionLiterals());
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
            static action => SkillLiteralCodec.GetUpdateActionStatus(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static action => action.Diffs,
            SkillLiteralCodec.GetUpdateActionLiterals());
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
            static action => SkillLiteralCodec.GetUninstallActionStatus(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static _ => null,
            SkillLiteralCodec.GetUninstallActionLiterals());
    }

    /// <summary> Creates product-neutral report data from a doctor result. </summary>
    /// <param name="result"> The doctor result to report. Must not be <see langword="null" />. </param>
    /// <param name="scope"> The install scope used to resolve the diagnosed target root. </param>
    /// <param name="selectedTiers"> The selected product-owned SKILL tiers. Must not be <see langword="null" />. </param>
    /// <returns> A report whose diagnostics are sorted deterministically. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" />, <paramref name="selectedTiers" />, or an item in <paramref name="selectedTiers" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="scope" /> is not a supported install scope. </exception>
    public static SkillDoctorReport CreateDoctorReport (
        SkillDoctorResult result,
        SkillScopeKind scope,
        IReadOnlyList<SkillTier> selectedTiers)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(selectedTiers);

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
            CreateTierLiterals(selectedTiers),
            SkillLiteralCodec.FormatScope(scope),
            result.TargetRoot,
            result.IsHealthy,
            diagnostics);
    }

    /// <summary> Creates product-neutral report data from a doctor result for one selected tier. </summary>
    public static SkillDoctorReport CreateDoctorReport (
        SkillDoctorResult result,
        SkillScopeKind scope,
        SkillTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);

        return CreateDoctorReport(result, scope, [tier]);
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
            CreateTierLiterals(context.SelectedTiers),
            SkillLiteralCodec.FormatScope(context.Scope),
            targetRoot,
            dryRun,
            force,
            context.HostDescriptor.ReloadGuidance,
            projectedActions,
            CreateCounts(actionLiteralOrder, projectedActions, static action => action.Action),
            CreateCounts(
                SkillLiteralCodec.GetActionStatusLiterals(),
                projectedActions,
                static action => action.Status));
    }

    private static IReadOnlyList<string> CreateTierLiterals (IReadOnlyList<SkillTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        var literals = new List<string>();
        foreach (var tier in tiers)
        {
            ArgumentNullException.ThrowIfNull(tier);
            literals.Add(tier.Value);
        }

        return literals;
    }

    private static IReadOnlyList<SkillListTierReport> CreateAvailableTierReports (IReadOnlyList<SkillTierPackageCount> availableTiers)
    {
        ArgumentNullException.ThrowIfNull(availableTiers);

        var reports = new List<SkillListTierReport>(availableTiers.Count);
        foreach (var availableTier in availableTiers)
        {
            ArgumentNullException.ThrowIfNull(availableTier);
            ArgumentNullException.ThrowIfNull(availableTier.Tier);
            reports.Add(new SkillListTierReport(availableTier.Tier.Value, availableTier.PackageCount));
        }

        return reports;
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
            CreateTargetFileSetReport(state.FileSet));
    }

    private static SkillOperationFileChangesReport? CreateFileChangesReport (SkillActionFileChanges? fileChanges)
    {
        return fileChanges is null
            ? null
            : new SkillOperationFileChangesReport(
                CreateSafeRelativePathReports(fileChanges.ReplacedFiles, nameof(fileChanges)),
                CreateSafeRelativePathReports(fileChanges.RemovedFiles, nameof(fileChanges)));
    }

    private static IReadOnlyList<SkillOperationFileDiffReport> CreateFileDiffReports (IReadOnlyList<SkillActionDiff>? diffs)
    {
        var files = new List<SkillOperationFileDiffReport>();
        foreach (var diff in diffs ?? Array.Empty<SkillActionDiff>())
        {
            ArgumentNullException.ThrowIfNull(diff);
            ArgumentNullException.ThrowIfNull(diff.Files);

            foreach (var file in diff.Files)
            {
                ArgumentNullException.ThrowIfNull(file);
                ValidateSafeRelativePath(file.RelativePath, nameof(diffs));
                files.Add(new SkillOperationFileDiffReport(
                    file.RelativePath,
                    SkillLiteralCodec.FormatDiffChangeKind(file.ChangeKind),
                    file.BeforeContent,
                    file.AfterContent));
            }
        }

        return files
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ThenBy(static file => file.ChangeKind, StringComparer.Ordinal)
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

    private static SkillListSkillReport CreateListSkillReport (CanonicalSkillPackage package)
    {
        var manifest = package.Manifest;
        return new SkillListSkillReport(
            manifest.SchemaVersion,
            manifest.SkillName,
            manifest.DisplayName,
            manifest.Description,
            manifest.Tier.Value,
            manifest.CatalogId.Value,
            manifest.ContentDigest,
            manifest.ManifestDigest,
            manifest.HostArtifacts
                .OrderBy(static artifact => artifact.Host, StringComparer.Ordinal)
                .Select(static artifact => new SkillHostArtifactReport(
                    artifact.Host,
                    artifact.Path,
                    artifact.Digest,
                    artifact.MaterializedFrontmatterDigest))
                .ToArray());
    }

    private static SkillHostReport CreateHostReport (SkillHostDescriptor descriptor)
    {
        ValidateHostDescriptor(descriptor);

        return new SkillHostReport(
            descriptor.HostKey,
            descriptor.SupportsProjectScope,
            descriptor.SupportsUserScope,
            descriptor.ProjectDefaultTargetPath,
            descriptor.UserDefaultTargetPath,
            descriptor.UserTargetRootPolicy is null
                ? null
                : new SkillUserTargetRootPolicyReport(
                    descriptor.UserTargetRootPolicy.EnvironmentVariableName,
                    descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory,
                    descriptor.UserTargetRootPolicy.HomeRelativeDirectory),
            descriptor.RequiresMetadataArtifact,
            descriptor.MetadataArtifactPath,
            descriptor.ReloadGuidance);
    }

    private static SkillTargetFileSetReport? CreateTargetFileSetReport (SkillActionTargetFileSet? fileSet)
    {
        return fileSet is null
            ? null
            : new SkillTargetFileSetReport(
                CreateSafeRelativePathReports(fileSet.MissingFiles, nameof(fileSet)),
                CreateSafeRelativePathReports(fileSet.ExtraFiles, nameof(fileSet)),
                CreateSafeRelativePathReports(fileSet.ExtraDirectories, nameof(fileSet)));
    }

    private static IReadOnlyList<string> CreateSafeRelativePathReports (
        IReadOnlyList<string> relativePaths,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);

        foreach (var relativePath in relativePaths)
        {
            ValidateSafeRelativePath(relativePath, parameterName);
        }

        return relativePaths
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateSafeRelativePath (
        string relativePath,
        string parameterName)
    {
        if (!IsSafeReportRelativePath(relativePath))
        {
            throw new ArgumentException($"Operation report path must be a safe slash-separated relative file path: {relativePath}", parameterName);
        }
    }

    private static bool IsSafeReportRelativePath (string? relativePath)
    {
        return SkillRelativePath.IsSafeFilePath(relativePath)
            && relativePath is not null
            && !relativePath.Contains(':', StringComparison.Ordinal)
            && !relativePath.Any(char.IsControl);
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
