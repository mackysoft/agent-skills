using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Contracts;
using MackySoft.AgentDistribution.OperationReports.Literals;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.OperationReports.Projection;

/// <summary> Builds product-neutral reports from AgentDistribution operation result models. </summary>
public static class SkillOperationReportBuilder
{
    /// <summary> Creates list report data from a validated package catalog. </summary>
    /// <param name="catalog"> The validated bundled package catalog. Must not be <see langword="null" />. </param>
    /// <returns> A report whose skills and hosts are sorted using ordinal comparison. </returns>
    public static SkillListReport CreateListReport (SkillPackageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return CreateListReport(catalog.Packages, catalog.SelectedCategories, catalog.SelectedSkillNames, catalog.AvailableCategories);
    }

    private static SkillListReport CreateListReport (
        IReadOnlyList<CanonicalSkillPackage> packages,
        IReadOnlyList<SkillCategory> selectedCategories,
        IReadOnlyList<SkillName> selectedSkillNames,
        IReadOnlyList<SkillCategoryPackageCount> availableCategories)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(selectedCategories);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        ArgumentNullException.ThrowIfNull(availableCategories);

        var skills = packages
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .Select(static package => CreateListSkillReport(package))
            .ToArray();
        var categoryReports = CreateAvailableCategoryReports(availableCategories);
        var hosts = HostRegistration.Registrations
            .OrderBy(static registration => registration.Host)
            .Select(static registration => CreateHostReport(registration.Host, registration.Skill))
            .ToArray();

        return new SkillListReport(CreateCategoryLiterals(selectedCategories), CreateSkillNameLiterals(selectedSkillNames), categoryReports, skills, hosts);
    }

    /// <summary> Creates export report data from a successful export operation. </summary>
    /// <param name="outputPath"> The absolute output directory or zip file path returned by export. </param>
    /// <param name="packages"> The exported packages. Must not be <see langword="null" />. </param>
    /// <param name="host"> The host used for export. </param>
    /// <param name="format"> The export format used for export. </param>
    /// <param name="selectedCategories"> The selected product-owned SKILL categories. Must not be <see langword="null" />. </param>
    /// <returns> A report whose skill names are sorted using ordinal comparison. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="outputPath" />, <paramref name="packages" />, <paramref name="selectedCategories" />, or an item in <paramref name="selectedCategories" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="format" /> is not a supported export format. </exception>
    public static SkillExportReport CreateExportReport (
        AbsolutePath outputPath,
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        PackageExportFormat format,
        IReadOnlyList<SkillCategory> selectedCategories)
    {
        return CreateExportReport(outputPath, packages, host, format, selectedCategories, []);
    }

    /// <summary> Creates export report data from a successful export operation. </summary>
    /// <param name="outputPath"> The absolute output directory or zip file path returned by export. </param>
    /// <param name="packages"> The exported packages. Must not be <see langword="null" />. </param>
    /// <param name="host"> The host used for export. </param>
    /// <param name="format"> The export format used for export. </param>
    /// <param name="selectedCategories"> The selected product-owned SKILL categories. Must not be <see langword="null" />. </param>
    /// <param name="selectedSkillNames"> The exact selected SKILL names. Empty means no name filter. </param>
    /// <returns> A report whose skill names are sorted using ordinal comparison. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="outputPath" />, <paramref name="packages" />, <paramref name="selectedCategories" />, <paramref name="selectedSkillNames" />, or an item in <paramref name="selectedCategories" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a selected SKILL name is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="format" /> is not a supported export format. </exception>
    public static SkillExportReport CreateExportReport (
        AbsolutePath outputPath,
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        PackageExportFormat format,
        IReadOnlyList<SkillCategory> selectedCategories,
        IReadOnlyList<SkillName> selectedSkillNames)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(selectedCategories);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        var registration = GetRequiredHostRegistration(host);

        var skills = packages
            .Select(static package => package.Manifest.SkillName)
            .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
            .Select(static skillName => skillName.Value)
            .ToArray();

        return new SkillExportReport(
            host,
            CreateCategoryLiterals(selectedCategories),
            CreateSkillNameLiterals(selectedSkillNames),
            format,
            outputPath,
            skills,
            skills.Length,
            registration.Skill.ReloadGuidance);
    }

    /// <summary> Creates export report data from a successful export operation for one selected category. </summary>
    public static SkillExportReport CreateExportReport (
        AbsolutePath outputPath,
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        PackageExportFormat format,
        SkillCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return CreateExportReport(outputPath, packages, host, format, [category], []);
    }

    /// <summary> Creates product-neutral report data from a successful install operation. </summary>
    /// <param name="result"> The successful install result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the install operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, target state kind, or diff change kind is unsupported. </exception>
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
            static action => Vocabulary.GetText(action.ActionKind),
            static action => OperationActionStatusResolver.Resolve(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static action => action.Diffs,
            Vocabulary.GetTexts<SkillInstallActionKind>());
    }

    /// <summary> Creates product-neutral report data from a successful update operation. </summary>
    /// <param name="result"> The successful update result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the update operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, target state kind, or diff change kind is unsupported. </exception>
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
            static action => Vocabulary.GetText(action.ActionKind),
            static action => OperationActionStatusResolver.Resolve(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static action => action.Diffs,
            Vocabulary.GetTexts<SkillUpdateActionKind>());
    }

    /// <summary> Creates product-neutral report data from a successful uninstall operation. </summary>
    /// <param name="result"> The successful uninstall result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the uninstall operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, target state kind, or diff change kind is unsupported. </exception>
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
            static action => Vocabulary.GetText(action.ActionKind),
            static action => OperationActionStatusResolver.Resolve(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static _ => null,
            Vocabulary.GetTexts<SkillUninstallActionKind>());
    }

    /// <summary> Creates product-neutral report data from a successful prune operation. </summary>
    /// <param name="result"> The successful prune result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized host and scope used for the prune operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose actions and counts are deterministic. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result target root or context host descriptor is invalid, when <paramref name="context" /> does not match an action identity in <paramref name="result" />, or when an action target state contains an invalid failure code. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the context scope, action kind, or target state kind is unsupported. </exception>
    public static SkillOperationReport CreatePruneReport (
        SkillPruneResult result,
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
            static action => Vocabulary.GetText(action.ActionKind),
            static action => OperationActionStatusResolver.Resolve(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.TargetState,
            static action => action.FileChanges,
            static _ => null,
            Vocabulary.GetTexts<SkillPruneActionKind>());
    }

    /// <summary> Creates product-neutral report data from a doctor result. </summary>
    /// <param name="result"> The doctor result to report. Must not be <see langword="null" />. </param>
    /// <param name="context"> The normalized target and package selection used for the doctor operation. Must not be <see langword="null" />. </param>
    /// <returns> A report whose diagnostics are sorted deterministically. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> or <paramref name="context" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the result host does not match <paramref name="context" />, or when a project-scope target root is outside the context repository root. </exception>
    public static SkillDoctorReport CreateDoctorReport (
        SkillDoctorResult result,
        SkillOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);
        if (result.Host != context.Host)
        {
            throw new ArgumentException("Doctor result host must match the report context host.", nameof(context));
        }

        var diagnostics = result.Diagnostics
            .OrderBy(static diagnostic => diagnostic.SkillName is null ? 0 : 1)
            .ThenBy(static diagnostic => diagnostic.SkillName?.Value, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code.Value, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ThenBy(static diagnostic => (int)diagnostic.Severity)
            .Select(static diagnostic => new SkillDoctorDiagnosticReport(
                diagnostic.Severity,
                diagnostic.Code.Value,
                diagnostic.Message,
                diagnostic.SkillName?.Value,
                ResolveDiagnosticTargetState(diagnostic.Code)))
            .ToArray();

        return new SkillDoctorReport(
            result.Host,
            CreateCategoryLiterals(context.SelectedCategories),
            CreateSkillNameLiterals(context.SelectedSkillNames),
            SkillOperationReportContext.ToOperationScope(context.Scope),
            context.RepositoryRoot,
            result.TargetRoot.Value,
            GetRequiredHostRegistration(context.Host).Skill.ReloadGuidance,
            diagnostics);
    }

    private static SkillOperationReport CreateOperationReport<TAction> (
        AbsolutePath targetRoot,
        IReadOnlyList<TAction> actions,
        bool dryRun,
        bool force,
        bool printDiff,
        SkillOperationReportContext context,
        Func<TAction, SkillInstallIdentity> getIdentity,
        Func<TAction, string> getActionLiteral,
        Func<TAction, OperationActionStatus> getStatus,
        Func<TAction, SkillBlockedReason?> getBlockedReason,
        Func<TAction, SkillActionTargetState?> getTargetState,
        Func<TAction, SkillActionFileChanges?> getFileChanges,
        Func<TAction, IReadOnlyList<SkillActionDiff>?> getDiffs,
        IReadOnlyList<string> actionLiteralOrder)
    {
        ArgumentNullException.ThrowIfNull(targetRoot);
        ArgumentNullException.ThrowIfNull(actions);

        var projectedActions = actions
            .OrderBy(action => getIdentity(action).SkillName.Value, StringComparer.Ordinal)
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
            context.Host,
            CreateCategoryLiterals(context.SelectedCategories),
            CreateSkillNameLiterals(context.SelectedSkillNames),
            SkillOperationReportContext.ToOperationScope(context.Scope),
            context.RepositoryRoot,
            targetRoot.Value,
            dryRun,
            force,
            GetRequiredHostRegistration(context.Host).Skill.ReloadGuidance,
            projectedActions,
            CreateCounts(actionLiteralOrder, projectedActions, static action => action.Action),
            CreateStatusCounts(projectedActions));
    }

    private static IReadOnlyList<string> CreateCategoryLiterals (IReadOnlyList<SkillCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var literals = new List<string>();
        foreach (var category in categories)
        {
            ArgumentNullException.ThrowIfNull(category);
            literals.Add(category.Value);
        }

        return literals;
    }

    private static IReadOnlyList<string> CreateSkillNameLiterals (IReadOnlyList<SkillName> skillNames)
    {
        ArgumentNullException.ThrowIfNull(skillNames);

        var literals = new List<string>(skillNames.Count);
        foreach (var skillName in skillNames)
        {
            ArgumentNullException.ThrowIfNull(skillName);
            literals.Add(skillName.Value);
        }

        return literals;
    }

    private static IReadOnlyList<SkillListCategoryReport> CreateAvailableCategoryReports (IReadOnlyList<SkillCategoryPackageCount> availableCategories)
    {
        ArgumentNullException.ThrowIfNull(availableCategories);

        var reports = new List<SkillListCategoryReport>(availableCategories.Count);
        foreach (var availableCategory in availableCategories)
        {
            ArgumentNullException.ThrowIfNull(availableCategory);
            ArgumentNullException.ThrowIfNull(availableCategory.Category);
            reports.Add(new SkillListCategoryReport(availableCategory.Category.Value, availableCategory.PackageCount));
        }

        return reports;
    }

    private static SkillOperationActionReport CreateActionReport<TAction> (
        AbsolutePath targetRoot,
        SkillOperationReportContext context,
        TAction action,
        Func<TAction, SkillInstallIdentity> getIdentity,
        Func<TAction, string> getActionLiteral,
        Func<TAction, OperationActionStatus> getStatus,
        Func<TAction, SkillBlockedReason?> getBlockedReason,
        Func<TAction, SkillActionTargetState?> getTargetState,
        Func<TAction, SkillActionFileChanges?> getFileChanges,
        bool printDiff,
        Func<TAction, IReadOnlyList<SkillActionDiff>?> getDiffs)
    {
        var identity = getIdentity(action);
        EnsureActionIdentityMatchesOperation(identity, targetRoot, context);

        var blockedReason = getBlockedReason(action);
        var status = getStatus(action);
        return new SkillOperationActionReport(
            identity.SkillName.Value,
            getActionLiteral(action),
            status,
            blockedReason,
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

        return new SkillTargetStateReport(state);
    }

    private static SkillOperationFileChangesReport? CreateFileChangesReport (SkillActionFileChanges? fileChanges)
    {
        return fileChanges is null
            ? null
            : new SkillOperationFileChangesReport(
                fileChanges.ReplacedFiles.Select(static path => path.Value).ToArray(),
                fileChanges.RemovedFiles.Select(static path => path.Value).ToArray());
    }

    private static IReadOnlyList<OperationFileDiffReport> CreateFileDiffReports (IReadOnlyList<SkillActionDiff>? diffs)
    {
        var files = new List<OperationFileDiffReport>();
        foreach (var diff in diffs ?? Array.Empty<SkillActionDiff>())
        {
            ArgumentNullException.ThrowIfNull(diff);
            ArgumentNullException.ThrowIfNull(diff.Files);

            foreach (var file in diff.Files)
            {
                ArgumentNullException.ThrowIfNull(file);
                files.Add(new OperationFileDiffReport(
                    file.RelativePath.Value,
                    file.ChangeKind,
                    file.BeforeContent,
                    file.AfterContent));
            }
        }

        return files
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ThenBy(static file => file.ChangeKind)
            .ToArray();
    }

    private static IReadOnlyList<OperationCountReport> CreateStatusCounts (
        IReadOnlyList<SkillOperationActionReport> actions)
    {
        return Vocabulary.GetTexts<OperationActionStatus>()
            .Select(literal => new OperationCountReport(
                literal,
                actions.Count(action => Vocabulary.Matches(literal, action.Status))))
            .ToArray();
    }

    private static IReadOnlyList<OperationCountReport> CreateCounts (
        IReadOnlyList<string> literalOrder,
        IReadOnlyList<SkillOperationActionReport> actions,
        Func<SkillOperationActionReport, string> getLiteral)
    {
        return literalOrder
            .Select(literal => new OperationCountReport(
                literal,
                actions.Count(action => string.Equals(getLiteral(action), literal, StringComparison.Ordinal))))
            .ToArray();
    }

    private static SkillTargetStateKind? ResolveDiagnosticTargetState (AgentDistributionFailureCode code)
    {
        return SkillDiagnosticTargetStateResolver.TryResolve(code, out var stateKind)
            ? stateKind
            : null;
    }

    private static SkillListSkillReport CreateListSkillReport (CanonicalSkillPackage package)
    {
        var manifest = package.Manifest;
        return new SkillListSkillReport(
            manifest.SchemaVersion,
            manifest.SkillBundleVersion.Value,
            manifest.SkillName.Value,
            manifest.DisplayName,
            manifest.Description,
            CreateSkillNameLiterals(manifest.Dependencies),
            manifest.Category.Value,
            manifest.CatalogId.Value,
            manifest.ContentDigest,
            manifest.ManifestDigest,
            manifest.HostArtifacts
                .OrderBy(static artifact => artifact.Host)
                .Select(static artifact => new SkillHostArtifactReport(
                    artifact.Host,
                    artifact.Path?.Value,
                    artifact.Digest,
                    artifact.MaterializedFrontmatterDigest))
                .ToArray());
    }

    private static SkillHostReport CreateHostReport (HostKind host, SkillHostDescriptor descriptor)
    {

        return new SkillHostReport(
            host,
            descriptor.ProjectDefaultTargetPath.Value,
            FormatUserDefaultTargetPath(descriptor.UserTargetRootPolicy),
            new SkillUserTargetRootPolicyReport(
                descriptor.UserTargetRootPolicy.EnvironmentVariableName,
                descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory?.Value,
                descriptor.UserTargetRootPolicy.HomeRelativeDirectory.Value),
            Vocabulary.GetText(descriptor.BundleTargetRootLayout),
            descriptor.CompatiblePreviousBundleTargetRootLayouts
                .Select(Vocabulary.GetText)
                .ToArray(),
            descriptor.MetadataArtifactPath?.Value,
            descriptor.ReloadGuidance);
    }

    private static string FormatUserDefaultTargetPath (SkillUserTargetRootPolicy policy)
    {
        var homeTarget = $"~/{policy.HomeRelativeDirectory.Value}";
        if (policy.EnvironmentVariableName is null)
        {
            return homeTarget;
        }

        var environmentTarget = $"${{{policy.EnvironmentVariableName}}}";
        if (policy.EnvironmentVariableChildDirectory is not null)
        {
            environmentTarget += $"/{policy.EnvironmentVariableChildDirectory.Value}";
        }

        return $"{environmentTarget} or {homeTarget}";
    }

    private static void EnsureActionIdentityMatchesOperation (
        SkillInstallIdentity identity,
        AbsolutePath targetRoot,
        SkillOperationReportContext context)
    {
        if (identity.Host != context.Host)
        {
            throw new ArgumentException("Operation report context host must match every action identity host.", nameof(context));
        }

        if (identity.Scope != context.Scope)
        {
            throw new ArgumentException("Operation report context scope must match every action identity scope.", nameof(context));
        }

        if (!identity.TargetRoot.IsSameAs(targetRoot))
        {
            throw new ArgumentException("Operation result target root must match every action identity target root.", nameof(targetRoot));
        }
    }

    private static HostRegistration GetRequiredHostRegistration (HostKind host)
    {
        var result = HostRegistration.Get(host);
        return result.IsSuccess
            ? result.Value!
            : throw new ArgumentOutOfRangeException(nameof(host), host, result.Failure!.Message);
    }

}
