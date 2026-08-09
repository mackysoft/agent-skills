using MackySoft.AgentDistribution.Commands;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Installation.Inventory;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Services;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Projection;
using MackySoft.AgentDistribution.Selection;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary> Runs product CLI Agent Distribution commands after normalizing raw command input. </summary>
public sealed class SkillCommandRunner
{
    private readonly AgentDistributionCommandRuntimeConfiguration configuration;
    private readonly SkillPackageProvider packageProvider;
    private readonly SkillExportService exportService;
    private readonly SkillInstallService installService;
    private readonly SkillUpdateService updateService;
    private readonly SkillUninstallService uninstallService;
    private readonly SkillPruneService pruneService;
    private readonly SkillDoctorService doctorService;
    private readonly SkillCatalogTargetRootSelector targetSelector;

    /// <summary> Initializes a new instance of the <see cref="SkillCommandRunner" /> class. </summary>
    public SkillCommandRunner (
        AgentDistributionCommandRuntimeConfiguration configuration,
        SkillPackageProvider packageProvider,
        SkillExportService exportService,
        SkillInstallService installService,
        SkillUpdateService updateService,
        SkillUninstallService uninstallService,
        SkillPruneService pruneService,
        SkillDoctorService doctorService,
        SkillCatalogTargetRootSelector targetSelector)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        this.uninstallService = uninstallService ?? throw new ArgumentNullException(nameof(uninstallService));
        this.pruneService = pruneService ?? throw new ArgumentNullException(nameof(pruneService));
        this.doctorService = doctorService ?? throw new ArgumentNullException(nameof(doctorService));
        this.targetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
    }

    /// <summary> Runs <c>skills list</c> and returns product-neutral list report data. </summary>
    /// <param name="request"> The raw list command request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A command result with <see cref="OperationReports.Contracts.SkillListReport" /> payload, or a structured failure. </returns>
    public async ValueTask<AgentDistributionCommandResult> ListAsync (
        SkillListCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.list";
        var selectionResult = NormalizeOptionalPackageSelection(request.Category, request.Skill);
        if (!selectionResult.IsSuccess)
        {
            return Failure(commandName, selectionResult.Failure!);
        }

        var catalogResult = await GetPackageCatalogAsync(selectionResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return Failure(commandName, catalogResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateListReport(catalogResult.Value!);
        return AgentDistributionCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills export</c> and returns product-neutral export report data. </summary>
    public async ValueTask<AgentDistributionCommandResult> ExportAsync (
        SkillExportCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.export";
        var selectionResult = NormalizeRequiredPackageSelection(request.Category, request.Skill);
        if (!selectionResult.IsSuccess)
        {
            return Failure(commandName, selectionResult.Failure!);
        }

        var hostResult = SkillCommandValueParser.ParseHostLiteral(request.Host);
        if (!hostResult.IsSuccess)
        {
            return Failure(commandName, hostResult.Failure!);
        }

        var outputResult = CommandPathResolver.ResolveRequired(request.Output, "Option '--output' is required.");
        if (!outputResult.IsSuccess)
        {
            return Failure(commandName, outputResult.Failure!);
        }

        var formatResult = NormalizeExportFormat(request.Format);
        if (!formatResult.IsSuccess)
        {
            return Failure(commandName, formatResult.Failure!);
        }

        var catalogResult = await GetPackageCatalogAsync(selectionResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return Failure(commandName, catalogResult.Failure!);
        }

        var packages = catalogResult.Value!.Packages;
        var exportResult = await exportService.ExportAsync(
                packages,
                hostResult.Value,
                outputResult.Value!,
                formatResult.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (!exportResult.IsSuccess)
        {
            return Failure(commandName, exportResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateExportReport(
            exportResult.Value!,
            packages,
            hostResult.Value,
            formatResult.Value,
            catalogResult.Value.SelectedCategories,
            catalogResult.Value.SelectedSkillNames);
        return AgentDistributionCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills install</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentDistributionCommandResult> InstallAsync (
        SkillInstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.install";
        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Category,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(commandName, preparedResult.Failure!);
        }

        var prepared = preparedResult.Value!;
        var installResult = await installService.InstallAsync(
                new SkillInstallInput(
                    prepared.Catalog.BundleDescriptor.CatalogId,
                    prepared.Catalog.Packages,
                    prepared.Target.Request,
                    request.DryRun,
                    request.Force,
                    request.PrintDiff),
                cancellationToken)
            .ConfigureAwait(false);
        if (!installResult.IsSuccess)
        {
            return Failure(commandName, installResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateInstallReport(
            installResult.Value!,
            CreateReportContext(prepared));
        return AgentDistributionCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills update</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentDistributionCommandResult> UpdateAsync (
        SkillUpdateCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.update";
        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Category,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(commandName, preparedResult.Failure!);
        }

        var prepared = preparedResult.Value!;
        var updateResult = await updateService.UpdateAsync(
                new SkillUpdateInput(
                    prepared.Catalog.BundleDescriptor.CatalogId,
                    prepared.Catalog.Packages,
                    prepared.Target.Request,
                    request.DryRun,
                    request.Force,
                    request.PrintDiff),
                cancellationToken)
            .ConfigureAwait(false);
        if (!updateResult.IsSuccess)
        {
            return Failure(commandName, updateResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateUpdateReport(
            updateResult.Value!,
            CreateReportContext(prepared));
        return AgentDistributionCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills uninstall</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentDistributionCommandResult> UninstallAsync (
        SkillUninstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.uninstall";
        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Category,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(commandName, preparedResult.Failure!);
        }

        var prepared = preparedResult.Value!;
        var uninstallResult = await uninstallService.UninstallAsync(
                new SkillUninstallInput(
                    prepared.Catalog.BundleDescriptor.CatalogId,
                    prepared.Catalog.Packages,
                    prepared.Target.Request,
                    request.DryRun,
                    request.Force),
                cancellationToken)
            .ConfigureAwait(false);
        if (!uninstallResult.IsSuccess)
        {
            return Failure(commandName, uninstallResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateUninstallReport(
            uninstallResult.Value!,
            CreateReportContext(prepared));
        return AgentDistributionCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills prune</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentDistributionCommandResult> PruneAsync (
        SkillPruneCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.prune";
        var selectionResult = NormalizeRequiredPackageSelection(request.Category, request.Skill);
        if (!selectionResult.IsSuccess)
        {
            return Failure(commandName, selectionResult.Failure!);
        }

        var targetResult = NormalizeTarget(request.Host, request.Scope, request.RepositoryRoot, request.TargetDir);
        if (!targetResult.IsSuccess)
        {
            return Failure(commandName, targetResult.Failure!);
        }

        var currentCatalogResult = await packageProvider.GetPackageCatalogAsync(cancellationToken).ConfigureAwait(false);
        if (!currentCatalogResult.IsSuccess)
        {
            return Failure(commandName, currentCatalogResult.Failure!);
        }

        var pruneSelectionResult = NormalizePruneSelection(selectionResult.Value!, currentCatalogResult.Value!.AvailableCategories);
        if (!pruneSelectionResult.IsSuccess)
        {
            return Failure(commandName, pruneSelectionResult.Failure!);
        }

        var selection = pruneSelectionResult.Value!;
        var target = targetResult.Value!;
        var pruneResult = await pruneService.PruneAsync(
                new SkillPruneInput(
                    currentCatalogResult.Value.BundleDescriptor.CatalogId,
                    currentCatalogResult.Value.Packages,
                    target.Request,
                    request.DryRun,
                    request.Force,
                    selection.CategoryFilter,
                    selection.SkillNames),
                cancellationToken)
            .ConfigureAwait(false);
        if (!pruneResult.IsSuccess)
        {
            return Failure(commandName, pruneResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreatePruneReport(
            pruneResult.Value!,
            CreateReportContext(target, selection.ReportCategories, selection.SkillNames));
        return AgentDistributionCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills doctor</c> and returns product-neutral doctor report data. </summary>
    public async ValueTask<AgentDistributionCommandResult> DoctorAsync (
        SkillDoctorCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "skills.doctor";
        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Category,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(commandName, preparedResult.Failure!);
        }

        var prepared = preparedResult.Value!;
        var packages = prepared.Catalog.Packages;
        var targetResult = await targetSelector.SelectTargetAsync(
                prepared.Target.Request,
                prepared.Catalog.BundleDescriptor.CatalogId,
                packages.Select(static package => package.Manifest.SkillName).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResult.IsSuccess)
        {
            return Failure(commandName, targetResult.Failure!);
        }

        var doctorResult = packages.Count == 0
            ? new SkillDoctorResult(prepared.Target.Host, targetResult.Value!.TargetRoot, Array.Empty<SkillDoctorDiagnostic>())
            : await doctorService.DiagnoseAsync(
                    packages,
                    prepared.Target.Host,
                    targetResult.Value!.TargetRoot.Value,
                    cancellationToken)
                .ConfigureAwait(false);
        var report = SkillOperationReportBuilder.CreateDoctorReport(
            doctorResult,
            CreateReportContext(prepared));
        return AgentDistributionCommandResult.Success(
            commandName,
            report,
            report.IsHealthy ? 0 : 1);
    }

    private async ValueTask<SkillOperationResult<PreparedTargetOperation>> PrepareTargetOperationAsync (
        string? host,
        string? scope,
        string? repositoryRoot,
        string? targetDir,
        IReadOnlyList<string>? categories,
        IReadOnlyList<string>? skillNames,
        CancellationToken cancellationToken)
    {
        var selectionResult = NormalizeRequiredPackageSelection(categories, skillNames);
        if (!selectionResult.IsSuccess)
        {
            return SkillOperationResult<PreparedTargetOperation>.FailureResult(selectionResult.Failure!.Code, selectionResult.Failure.Message);
        }

        var targetResult = NormalizeTarget(host, scope, repositoryRoot, targetDir);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<PreparedTargetOperation>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var catalogResult = await GetPackageCatalogAsync(selectionResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return SkillOperationResult<PreparedTargetOperation>.FailureResult(catalogResult.Failure!.Code, catalogResult.Failure.Message);
        }

        return SkillOperationResult<PreparedTargetOperation>.Success(new PreparedTargetOperation(targetResult.Value!, catalogResult.Value!));
    }

    private ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        NormalizedPackageSelection selection,
        CancellationToken cancellationToken)
    {
        return packageProvider.GetPackageCatalogAsync(
            selection.Categories,
            selection.SkillNames,
            cancellationToken);
    }

    private SkillOperationResult<NormalizedTargetRequest> NormalizeTarget (
        string? host,
        string? scope,
        string? repositoryRoot,
        string? targetDir)
    {
        var hostResult = SkillCommandValueParser.ParseHostLiteral(host);
        if (!hostResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(hostResult.Failure!.Code, hostResult.Failure.Message);
        }

        var scopeResult = SkillCommandValueParser.ParseScopeLiteral(scope);
        if (!scopeResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(scopeResult.Failure!.Code, scopeResult.Failure.Message);
        }

        var repositoryContextResult = CommandPathResolver.ResolveRepositoryContext(scopeResult.Value, repositoryRoot, configuration);
        if (!repositoryContextResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(repositoryContextResult.Failure!.Code, repositoryContextResult.Failure.Message);
        }

        var repositoryContext = repositoryContextResult.Value!;
        AbsolutePath? targetRoot = null;
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            var targetRootResult = CommandPathResolver.ResolveTarget(targetDir, repositoryContext.RepositoryRoot, "target-dir");
            if (!targetRootResult.IsSuccess)
            {
                return SkillOperationResult<NormalizedTargetRequest>.FailureResult(targetRootResult.Failure!.Code, targetRootResult.Failure.Message);
            }

            targetRoot = targetRootResult.Value;
        }

        var request = new SkillInstallRequest(
            hostResult.Value,
            repositoryContext.Scope,
            repositoryContext.RepositoryRoot,
            targetRoot);
        return SkillOperationResult<NormalizedTargetRequest>.Success(new NormalizedTargetRequest(hostResult.Value, repositoryContext.Scope, request));
    }

    private SkillOperationResult<NormalizedPackageSelection> NormalizeOptionalPackageSelection (
        IReadOnlyList<string>? categoryLiterals,
        IReadOnlyList<string>? skillNameLiterals)
    {
        var categoryValues = CommandOptionValues.Expand(categoryLiterals);
        var skillNameValues = CommandOptionValues.Expand(skillNameLiterals);
        var skillNamesResult = NormalizeSkillNames(skillNameValues);
        return skillNamesResult.IsSuccess
            ? SkillOperationResult<NormalizedPackageSelection>.Success(new NormalizedPackageSelection(categoryValues, skillNamesResult.Value!))
            : SkillOperationResult<NormalizedPackageSelection>.FailureResult(skillNamesResult.Failure!.Code, skillNamesResult.Failure.Message);
    }

    private SkillOperationResult<NormalizedPackageSelection> NormalizeRequiredPackageSelection (
        IReadOnlyList<string>? categoryLiterals,
        IReadOnlyList<string>? skillNameLiterals)
    {
        var categoryValues = CommandOptionValues.Expand(categoryLiterals);
        var skillNameValues = CommandOptionValues.Expand(skillNameLiterals);
        if (categoryValues.Length == 0 && skillNameValues.Length == 0)
        {
            return SkillOperationResult<NormalizedPackageSelection>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "Option '--category' or '--skill' is required.");
        }

        return NormalizeOptionalPackageSelection(categoryValues, skillNameValues);
    }

    private static SkillOperationResult<NormalizedPruneSelection> NormalizePruneSelection (
        NormalizedPackageSelection selection,
        IReadOnlyList<SkillCategoryPackageCount> availableCategories)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(availableCategories);

        var allCategories = availableCategories.Select(static item => item.Category).ToArray();
        IReadOnlyList<SkillCategory> reportCategories;
        if (selection.Categories.Count == 0)
        {
            reportCategories = allCategories;
        }
        else
        {
            // NOTE: Prune filters installed manifests, so a valid category remains selectable after it disappears from the current bundle.
            var categoryResult = SkillCategoryLiteralParser.ParseSelectedCategories(selection.Categories);
            if (!categoryResult.IsSuccess)
            {
                return SkillOperationResult<NormalizedPruneSelection>.FailureResult(
                    categoryResult.Failure!.Code,
                    categoryResult.Failure.Message);
            }

            reportCategories = categoryResult.Value!;
        }

        var categoryFilter = selection.Categories.Count == 0
            ? Array.Empty<SkillCategory>()
            : reportCategories;
        var skillNamesResult = selection.SkillNames.Count == 0
            ? SkillOperationResult<IReadOnlyList<SkillName>>.Success(Array.Empty<SkillName>())
            : SkillNameLiteralParser.ParseSelectedSkillNames(selection.SkillNames);
        if (!skillNamesResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedPruneSelection>.FailureResult(skillNamesResult.Failure!.Code, skillNamesResult.Failure.Message);
        }

        return SkillOperationResult<NormalizedPruneSelection>.Success(new NormalizedPruneSelection(
            reportCategories,
            categoryFilter,
            skillNamesResult.Value!));
    }

    private static SkillOperationResult<IReadOnlyList<string>> NormalizeSkillNames (string[] skillNameValues)
    {
        if (skillNameValues.Length == 0)
        {
            return SkillOperationResult<IReadOnlyList<string>>.Success(Array.Empty<string>());
        }

        var result = SkillNameLiteralParser.ParseSelectedSkillNames(skillNameValues);
        return result.IsSuccess
            ? SkillOperationResult<IReadOnlyList<string>>.Success(result.Value!.Select(static skillName => skillName.Value).ToArray())
            : SkillOperationResult<IReadOnlyList<string>>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    private static SkillOperationResult<SkillExportFormat> NormalizeExportFormat (string? format)
    {
        return string.IsNullOrWhiteSpace(format)
            ? SkillOperationResult<SkillExportFormat>.Success(SkillExportFormat.Directory)
            : SkillCommandValueParser.ParseExportFormatLiteral(format);
    }

    private static AgentDistributionCommandResult Failure (
        string command,
        SkillFailure failure)
    {
        return AgentDistributionCommandResult.FailureResult(command, failure);
    }

    private static SkillOperationReportContext CreateReportContext (PreparedTargetOperation prepared)
    {
        return CreateReportContext(prepared.Target, prepared.Catalog.SelectedCategories, prepared.Catalog.SelectedSkillNames);
    }

    private static SkillOperationReportContext CreateReportContext (
        NormalizedTargetRequest target,
        IReadOnlyList<SkillCategory> selectedCategories,
        IReadOnlyList<SkillName> selectedSkillNames)
    {
        return new SkillOperationReportContext(
            target.Host,
            target.Scope,
            target.Request.RepositoryRoot?.Value,
            selectedCategories,
            selectedSkillNames);
    }

    private sealed class NormalizedPackageSelection
    {
        public NormalizedPackageSelection (
            IReadOnlyList<string> categories,
            IReadOnlyList<string> skillNames)
        {
            ArgumentNullException.ThrowIfNull(categories);
            ArgumentNullException.ThrowIfNull(skillNames);
            var categorySnapshot = categories.ToArray();
            var skillNameSnapshot = skillNames.ToArray();
            if (categorySnapshot.Any(static category => category is null)
                || skillNameSnapshot.Any(static skillName => skillName is null))
            {
                throw new ArgumentException("Normalized package selection must not contain null values.");
            }

            Categories = Array.AsReadOnly(categorySnapshot);
            SkillNames = Array.AsReadOnly(skillNameSnapshot);
        }

        public IReadOnlyList<string> Categories { get; }

        public IReadOnlyList<string> SkillNames { get; }
    }

    private sealed class NormalizedPruneSelection
    {
        public NormalizedPruneSelection (
            IReadOnlyList<SkillCategory> reportCategories,
            IReadOnlyList<SkillCategory> categoryFilter,
            IReadOnlyList<SkillName> skillNames)
        {
            ArgumentNullException.ThrowIfNull(reportCategories);
            ArgumentNullException.ThrowIfNull(categoryFilter);
            ArgumentNullException.ThrowIfNull(skillNames);
            var reportCategorySnapshot = reportCategories.ToArray();
            var categoryFilterSnapshot = categoryFilter.ToArray();
            var skillNameSnapshot = skillNames.ToArray();
            if (reportCategorySnapshot.Any(static category => category is null)
                || categoryFilterSnapshot.Any(static category => category is null)
                || skillNameSnapshot.Any(static skillName => skillName is null))
            {
                throw new ArgumentException("Normalized prune selection contains an invalid value.");
            }

            ReportCategories = Array.AsReadOnly(reportCategorySnapshot);
            CategoryFilter = Array.AsReadOnly(categoryFilterSnapshot);
            SkillNames = Array.AsReadOnly(skillNameSnapshot);
        }

        public IReadOnlyList<SkillCategory> ReportCategories { get; }

        public IReadOnlyList<SkillCategory> CategoryFilter { get; }

        public IReadOnlyList<SkillName> SkillNames { get; }
    }

    private sealed class NormalizedTargetRequest
    {
        public NormalizedTargetRequest (
            HostKind host,
            SkillScopeKind scope,
            SkillInstallRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (host != request.Host || scope != request.Scope)
            {
                throw new ArgumentException("Normalized target values must identify the same host and scope.");
            }

            Host = host;
            Scope = scope;
            Request = request;
        }

        public HostKind Host { get; }

        public SkillScopeKind Scope { get; }

        public SkillInstallRequest Request { get; }
    }

    private sealed class PreparedTargetOperation
    {
        public PreparedTargetOperation (
            NormalizedTargetRequest target,
            SkillPackageCatalog catalog)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public NormalizedTargetRequest Target { get; }

        public SkillPackageCatalog Catalog { get; }
    }
}
