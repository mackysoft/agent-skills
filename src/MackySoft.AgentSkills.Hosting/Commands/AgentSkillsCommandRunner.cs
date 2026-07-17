using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Commands;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Selection;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Runs product CLI Agent Skills commands after normalizing raw command input. </summary>
public sealed class AgentSkillsCommandRunner
{
    private readonly AgentSkillsCommandRuntimeConfiguration configuration;
    private readonly SkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillExportService exportService;
    private readonly SkillInstallService installService;
    private readonly SkillUpdateService updateService;
    private readonly SkillUninstallService uninstallService;
    private readonly SkillPruneService pruneService;
    private readonly SkillDoctorService doctorService;
    private readonly SkillInstallTargetResolver targetResolver;

    /// <summary> Initializes a new instance of the <see cref="AgentSkillsCommandRunner" /> class. </summary>
    public AgentSkillsCommandRunner (
        AgentSkillsCommandRuntimeConfiguration configuration,
        SkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        SkillExportService exportService,
        SkillInstallService installService,
        SkillUpdateService updateService,
        SkillUninstallService uninstallService,
        SkillPruneService pruneService,
        SkillDoctorService doctorService,
        SkillInstallTargetResolver targetResolver)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        this.uninstallService = uninstallService ?? throw new ArgumentNullException(nameof(uninstallService));
        this.pruneService = pruneService ?? throw new ArgumentNullException(nameof(pruneService));
        this.doctorService = doctorService ?? throw new ArgumentNullException(nameof(doctorService));
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
    }

    /// <summary> Runs <c>skills list</c> and returns product-neutral list report data. </summary>
    /// <param name="request"> The raw list command request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A command result with <see cref="OperationReports.Contracts.SkillListReport" /> payload, or a structured failure. </returns>
    public async ValueTask<AgentSkillsCommandResult> ListAsync (
        AgentSkillsListCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.ListSubcommand);
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

        var report = SkillOperationReportBuilder.CreateListReport(catalogResult.Value!, hostAdapters);
        return AgentSkillsCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills export</c> and returns product-neutral export report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> ExportAsync (
        AgentSkillsExportCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.ExportSubcommand);
        var selectionResult = NormalizeRequiredPackageSelection(request.Category, request.Skill);
        if (!selectionResult.IsSuccess)
        {
            return Failure(commandName, selectionResult.Failure!);
        }

        var hostResult = SkillCommandValueParser.ParseHostLiteral(request.Host, hostAdapters);
        if (!hostResult.IsSuccess)
        {
            return Failure(commandName, hostResult.Failure!);
        }

        var outputResult = NormalizeRequiredFullPath(request.Output, "Option '--output' is required.");
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
                hostResult.Value!.Host,
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
            hostResult.Value!,
            formatResult.Value,
            catalogResult.Value.SelectedCategories,
            catalogResult.Value.SelectedSkillNames);
        return AgentSkillsCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills install</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> InstallAsync (
        AgentSkillsInstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.InstallSubcommand);
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
        return AgentSkillsCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills update</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> UpdateAsync (
        AgentSkillsUpdateCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.UpdateSubcommand);
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
        return AgentSkillsCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills uninstall</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> UninstallAsync (
        AgentSkillsUninstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.UninstallSubcommand);
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
        return AgentSkillsCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills prune</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> PruneAsync (
        AgentSkillsPruneCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.PruneSubcommand);
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
        return AgentSkillsCommandResult.Success(commandName, report);
    }

    /// <summary> Runs <c>skills doctor</c> and returns product-neutral doctor report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> DoctorAsync (
        AgentSkillsDoctorCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsCommandNames.DoctorSubcommand);
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
        var targetResult = targetResolver.ResolveTarget(prepared.Target.Request);
        if (!targetResult.IsSuccess)
        {
            return Failure(commandName, targetResult.Failure!);
        }

        var packages = prepared.Catalog.Packages;
        var doctorResult = packages.Count == 0
            ? new SkillDoctorResult(prepared.Target.HostDescriptor.Host, targetResult.Value!.TargetRoot, Array.Empty<SkillDoctorDiagnostic>())
            : await doctorService.DiagnoseAsync(
                    packages,
                    prepared.Target.HostDescriptor.Host,
                    targetResult.Value!.TargetRoot,
                    cancellationToken)
                .ConfigureAwait(false);
        var report = SkillOperationReportBuilder.CreateDoctorReport(
            doctorResult,
            CreateReportContext(prepared));
        return AgentSkillsCommandResult.Success(
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
        var hostResult = SkillCommandValueParser.ParseHostLiteral(host, hostAdapters);
        if (!hostResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(hostResult.Failure!.Code, hostResult.Failure.Message);
        }

        var scopeResult = SkillCommandValueParser.ParseScopeLiteral(scope);
        if (!scopeResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(scopeResult.Failure!.Code, scopeResult.Failure.Message);
        }

        var repositoryRootResult = NormalizeRepositoryRoot(scopeResult.Value, repositoryRoot);
        if (!repositoryRootResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(repositoryRootResult.Failure!.Code, repositoryRootResult.Failure.Message);
        }

        var targetRootResult = NormalizeTargetRoot(
            scopeResult.Value,
            repositoryRootResult.Value!.Value,
            targetDir);
        if (!targetRootResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(targetRootResult.Failure!.Code, targetRootResult.Failure.Message);
        }

        var request = new SkillInstallRequest(
            hostResult.Value!.Host,
            scopeResult.Value,
            repositoryRootResult.Value!.Value,
            targetRootResult.Value!.Value);
        return SkillOperationResult<NormalizedTargetRequest>.Success(new NormalizedTargetRequest(hostResult.Value, scopeResult.Value, request));
    }

    private SkillOperationResult<NormalizedPackageSelection> NormalizeOptionalPackageSelection (
        IReadOnlyList<string>? categoryLiterals,
        IReadOnlyList<string>? skillNameLiterals)
    {
        var categoryValues = ExpandOptionValues(categoryLiterals);
        var skillNameValues = ExpandOptionValues(skillNameLiterals);
        var skillNamesResult = NormalizeSkillNames(skillNameValues);
        return skillNamesResult.IsSuccess
            ? SkillOperationResult<NormalizedPackageSelection>.Success(new NormalizedPackageSelection(categoryValues, skillNamesResult.Value!))
            : SkillOperationResult<NormalizedPackageSelection>.FailureResult(skillNamesResult.Failure!.Code, skillNamesResult.Failure.Message);
    }

    private SkillOperationResult<NormalizedPackageSelection> NormalizeRequiredPackageSelection (
        IReadOnlyList<string>? categoryLiterals,
        IReadOnlyList<string>? skillNameLiterals)
    {
        var categoryValues = ExpandOptionValues(categoryLiterals);
        var skillNameValues = ExpandOptionValues(skillNameLiterals);
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

    private static SkillOperationResult<string> NormalizeRequiredFullPath (
        string? path,
        string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.InputInvalid,
                missingMessage);
        }

        try
        {
            return SkillOperationResult<string>.Success(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                ex.Message);
        }
    }

    private static SkillOperationResult<SkillExportFormat> NormalizeExportFormat (string? format)
    {
        return string.IsNullOrWhiteSpace(format)
            ? SkillOperationResult<SkillExportFormat>.Success(SkillExportFormat.Directory)
            : SkillCommandValueParser.ParseExportFormatLiteral(format);
    }

    private SkillOperationResult<NormalizedRepositoryRoot> NormalizeRepositoryRoot (
        SkillScopeKind scope,
        string? repositoryRoot)
    {
        if (scope == SkillScopeKind.User)
        {
            return string.IsNullOrWhiteSpace(repositoryRoot)
                ? SkillOperationResult<NormalizedRepositoryRoot>.Success(new NormalizedRepositoryRoot(null))
                : SkillOperationResult<NormalizedRepositoryRoot>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    "Option '--repositoryRoot' is not supported when '--scope user' is used.");
        }

        string root = string.IsNullOrWhiteSpace(repositoryRoot)
            ? configuration.RepositoryRootResolver(Directory.GetCurrentDirectory())
            : repositoryRoot;
        var result = NormalizeRequiredFullPath(root, "Project-scope SKILL operation requires a repository root.");
        return result.IsSuccess
            ? SkillOperationResult<NormalizedRepositoryRoot>.Success(new NormalizedRepositoryRoot(result.Value!))
            : SkillOperationResult<NormalizedRepositoryRoot>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    private static SkillOperationResult<NormalizedTargetRoot> NormalizeTargetRoot (
        SkillScopeKind scope,
        string? repositoryRoot,
        string? targetRoot)
    {
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            return SkillOperationResult<NormalizedTargetRoot>.Success(new NormalizedTargetRoot(null));
        }

        if (scope == SkillScopeKind.User && !Path.IsPathFullyQualified(targetRoot))
        {
            return SkillOperationResult<NormalizedTargetRoot>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                "User-scope SKILL targetDir must be an absolute path.");
        }

        if (!Path.IsPathFullyQualified(targetRoot))
        {
            try
            {
                return SkillOperationResult<NormalizedTargetRoot>.Success(new NormalizedTargetRoot(
                    Path.GetFullPath(targetRoot, repositoryRoot!)));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return SkillOperationResult<NormalizedTargetRoot>.FailureResult(SkillFailureCodes.PathUnsafe, ex.Message);
            }
        }

        var result = NormalizeRequiredFullPath(targetRoot, "SKILL targetDir is required.");
        return result.IsSuccess
            ? SkillOperationResult<NormalizedTargetRoot>.Success(new NormalizedTargetRoot(result.Value!))
            : SkillOperationResult<NormalizedTargetRoot>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    private static string[] ExpandOptionValues (IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var expanded = new List<string>();
        foreach (var value in values)
        {
            foreach (var item in value.Split(','))
            {
                if (item.Length != 0)
                {
                    expanded.Add(item);
                }
            }
        }

        return expanded.ToArray();
    }

    private static AgentSkillsCommandResult Failure (
        string command,
        SkillFailure failure)
    {
        return AgentSkillsCommandResult.FailureResult(command, failure);
    }

    private string CreateCommandName (string subcommand)
    {
        return AgentSkillsCommandNames.CreateCommandName(configuration.CommandRoot, subcommand);
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
            target.HostDescriptor,
            target.Scope,
            target.Request.RepositoryRoot,
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
            SkillHostDescriptor hostDescriptor,
            SkillScopeKind scope,
            SkillInstallRequest request)
        {
            ArgumentNullException.ThrowIfNull(hostDescriptor);
            ArgumentNullException.ThrowIfNull(request);
            if (hostDescriptor.Host != request.Host || scope != request.Scope)
            {
                throw new ArgumentException("Normalized target values must identify the same host and scope.");
            }

            HostDescriptor = hostDescriptor;
            Scope = scope;
            Request = request;
        }

        public SkillHostDescriptor HostDescriptor { get; }

        public SkillScopeKind Scope { get; }

        public SkillInstallRequest Request { get; }
    }

    private sealed class NormalizedRepositoryRoot
    {
        public NormalizedRepositoryRoot (string? value)
        {
            if (value is not null && !Path.IsPathFullyQualified(value))
            {
                throw new ArgumentException("Normalized repository root must be an absolute path.", nameof(value));
            }

            Value = value is null ? null : Path.GetFullPath(value);
        }

        public string? Value { get; }
    }

    private sealed class NormalizedTargetRoot
    {
        public NormalizedTargetRoot (string? value)
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
            }

            Value = value;
        }

        public string? Value { get; }
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
