using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Commands;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Selection;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Runs product CLI Agent Skills commands after normalizing raw command input. </summary>
public sealed class AgentSkillsCommandRunner
{
    private readonly AgentSkillsCommandRuntimeOptions options;
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
        AgentSkillsCommandRuntimeOptions options,
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
        this.options = options ?? throw new ArgumentNullException(nameof(options));
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

        var selectionResult = NormalizeOptionalPackageSelection(request.Tier, request.Skill);
        if (!selectionResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.List, selectionResult.Failure!);
        }

        var catalogResult = await GetPackageCatalogAsync(selectionResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.List, catalogResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateListReport(catalogResult.Value!, hostAdapters);
        return AgentSkillsCommandResult.Success(AgentSkillsCommandNames.List, report);
    }

    /// <summary> Runs <c>skills export</c> and returns product-neutral export report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> ExportAsync (
        AgentSkillsExportCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionResult = NormalizeRequiredPackageSelection(request.Tier, request.Skill);
        if (!selectionResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Export, selectionResult.Failure!);
        }

        var hostResult = SkillCommandValueParser.ParseHostLiteral(request.Host, hostAdapters);
        if (!hostResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Export, hostResult.Failure!);
        }

        var outputResult = NormalizeRequiredFullPath(request.Output, "Option '--output' is required.");
        if (!outputResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Export, outputResult.Failure!);
        }

        var formatResult = NormalizeExportFormat(request.Format);
        if (!formatResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Export, formatResult.Failure!);
        }

        var catalogResult = await GetPackageCatalogAsync(selectionResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Export, catalogResult.Failure!);
        }

        var packages = catalogResult.Value!.Packages;
        var exportResult = await exportService.ExportAsync(
                packages,
                hostResult.Value!.HostKey,
                outputResult.Value!,
                formatResult.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (!exportResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Export, exportResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateExportReport(
            exportResult.Value!,
            packages,
            hostResult.Value!,
            formatResult.Value,
            catalogResult.Value.SelectedTiers,
            catalogResult.Value.SelectedSkillNames.Select(static skillName => skillName.Value).ToArray());
        return AgentSkillsCommandResult.Success(AgentSkillsCommandNames.Export, report);
    }

    /// <summary> Runs <c>skills install</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> InstallAsync (
        AgentSkillsInstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Tier,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Install, preparedResult.Failure!);
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
            return Failure(AgentSkillsCommandNames.Install, installResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateInstallReport(
            installResult.Value!,
            CreateReportContext(prepared));
        return AgentSkillsCommandResult.Success(AgentSkillsCommandNames.Install, report);
    }

    /// <summary> Runs <c>skills update</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> UpdateAsync (
        AgentSkillsUpdateCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Tier,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Update, preparedResult.Failure!);
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
            return Failure(AgentSkillsCommandNames.Update, updateResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateUpdateReport(
            updateResult.Value!,
            CreateReportContext(prepared));
        return AgentSkillsCommandResult.Success(AgentSkillsCommandNames.Update, report);
    }

    /// <summary> Runs <c>skills uninstall</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> UninstallAsync (
        AgentSkillsUninstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Tier,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Uninstall, preparedResult.Failure!);
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
            return Failure(AgentSkillsCommandNames.Uninstall, uninstallResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreateUninstallReport(
            uninstallResult.Value!,
            CreateReportContext(prepared));
        return AgentSkillsCommandResult.Success(AgentSkillsCommandNames.Uninstall, report);
    }

    /// <summary> Runs <c>skills prune</c> and returns product-neutral operation report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> PruneAsync (
        AgentSkillsPruneCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Tier,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Prune, preparedResult.Failure!);
        }

        var currentCatalogResult = await packageProvider.GetPackageCatalogAsync(options.DefinedTiers, cancellationToken).ConfigureAwait(false);
        if (!currentCatalogResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Prune, currentCatalogResult.Failure!);
        }

        var prepared = preparedResult.Value!;
        var pruneResult = await pruneService.PruneAsync(
                new SkillPruneInput(
                    new SkillCatalogId(options.CatalogId),
                    currentCatalogResult.Value!.Packages,
                    prepared.Target.Request,
                    request.DryRun,
                    request.Force),
                cancellationToken)
            .ConfigureAwait(false);
        if (!pruneResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Prune, pruneResult.Failure!);
        }

        var report = SkillOperationReportBuilder.CreatePruneReport(
            pruneResult.Value!,
            CreateReportContext(prepared));
        return AgentSkillsCommandResult.Success(AgentSkillsCommandNames.Prune, report);
    }

    /// <summary> Runs <c>skills doctor</c> and returns product-neutral doctor report data. </summary>
    public async ValueTask<AgentSkillsCommandResult> DoctorAsync (
        AgentSkillsDoctorCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var preparedResult = await PrepareTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.TargetDir,
                request.Tier,
                request.Skill,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparedResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Doctor, preparedResult.Failure!);
        }

        var prepared = preparedResult.Value!;
        var targetResult = targetResolver.ResolveTarget(prepared.Target.Request);
        if (!targetResult.IsSuccess)
        {
            return Failure(AgentSkillsCommandNames.Doctor, targetResult.Failure!);
        }

        var doctorResult = await doctorService.DiagnoseAsync(
                prepared.Catalog.Packages,
                prepared.Target.HostDescriptor.HostKey,
                targetResult.Value!.TargetRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var report = SkillOperationReportBuilder.CreateDoctorReport(
            doctorResult,
            prepared.Target.Scope,
            prepared.Catalog.SelectedTiers,
            prepared.Catalog.SelectedSkillNames.Select(static skillName => skillName.Value).ToArray());
        return AgentSkillsCommandResult.Success(
            AgentSkillsCommandNames.Doctor,
            report,
            report.IsHealthy ? 0 : 1);
    }

    private async ValueTask<SkillOperationResult<PreparedTargetOperation>> PrepareTargetOperationAsync (
        string? host,
        string? scope,
        string? repositoryRoot,
        string? targetDir,
        string[]? tiers,
        string[]? skillNames,
        CancellationToken cancellationToken)
    {
        var selectionResult = NormalizeRequiredPackageSelection(tiers, skillNames);
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
            options.DefinedTiers,
            selection.Tiers,
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

        var scopeResult = SkillCommandValueParser.ParseScopeLiteral(scope, hostResult.Value!);
        if (!scopeResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(scopeResult.Failure!.Code, scopeResult.Failure.Message);
        }

        var repositoryRootResult = NormalizeRepositoryRoot(scopeResult.Value, repositoryRoot);
        if (!repositoryRootResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedTargetRequest>.FailureResult(repositoryRootResult.Failure!.Code, repositoryRootResult.Failure.Message);
        }

        var request = new SkillInstallRequest(
            hostResult.Value!.HostKey,
            scopeResult.Value,
            repositoryRootResult.Value!.Value,
            NormalizeOptionalPath(targetDir));
        return SkillOperationResult<NormalizedTargetRequest>.Success(new NormalizedTargetRequest(hostResult.Value, scopeResult.Value, request));
    }

    private SkillOperationResult<NormalizedPackageSelection> NormalizeOptionalPackageSelection (
        string[]? tierLiterals,
        string[]? skillNameLiterals)
    {
        var tierValues = ExpandOptionValues(tierLiterals);
        var skillNameValues = ExpandOptionValues(skillNameLiterals);
        var tiersResult = tierValues.Length == 0
            ? SkillTierLiteralParser.ParseDefinedTiers(options.DefinedTiers)
            : SkillTierLiteralParser.ParseSelectedTiers(options.DefinedTiers, tierValues);
        if (!tiersResult.IsSuccess)
        {
            return SkillOperationResult<NormalizedPackageSelection>.FailureResult(tiersResult.Failure!.Code, tiersResult.Failure.Message);
        }

        var skillNamesResult = NormalizeSkillNames(skillNameValues);
        return skillNamesResult.IsSuccess
            ? SkillOperationResult<NormalizedPackageSelection>.Success(new NormalizedPackageSelection(tiersResult.Value!, skillNamesResult.Value!))
            : SkillOperationResult<NormalizedPackageSelection>.FailureResult(skillNamesResult.Failure!.Code, skillNamesResult.Failure.Message);
    }

    private SkillOperationResult<NormalizedPackageSelection> NormalizeRequiredPackageSelection (
        string[]? tierLiterals,
        string[]? skillNameLiterals)
    {
        var tierValues = ExpandOptionValues(tierLiterals);
        var skillNameValues = ExpandOptionValues(skillNameLiterals);
        if (tierValues.Length == 0 && skillNameValues.Length == 0)
        {
            return SkillOperationResult<NormalizedPackageSelection>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "Option '--tier' or '--skill' is required.");
        }

        return NormalizeOptionalPackageSelection(tierValues, skillNameValues);
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

    private static SkillOperationResult<NormalizedRepositoryRoot> NormalizeRepositoryRoot (
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
            ? Directory.GetCurrentDirectory()
            : repositoryRoot;
        var result = NormalizeRequiredFullPath(root, "Project-scope SKILL operation requires a repository root.");
        return result.IsSuccess
            ? SkillOperationResult<NormalizedRepositoryRoot>.Success(new NormalizedRepositoryRoot(result.Value!))
            : SkillOperationResult<NormalizedRepositoryRoot>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    private static string? NormalizeOptionalPath (string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static string[] ExpandOptionValues (string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var expanded = new List<string>();
        foreach (var value in values)
        {
            foreach (var item in (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(item))
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

    private static SkillOperationReportContext CreateReportContext (PreparedTargetOperation prepared)
    {
        return new SkillOperationReportContext(
            prepared.Target.HostDescriptor,
            prepared.Target.Scope,
            prepared.Catalog.SelectedTiers)
        {
            SelectedSkillNames = prepared.Catalog.SelectedSkillNames.Select(static skillName => skillName.Value).ToArray(),
        };
    }

    private sealed record NormalizedPackageSelection (
        IReadOnlyList<SkillTier> Tiers,
        IReadOnlyList<string> SkillNames);

    private sealed record NormalizedTargetRequest (
        SkillHostDescriptor HostDescriptor,
        SkillScopeKind Scope,
        SkillInstallRequest Request);

    private sealed record NormalizedRepositoryRoot (string? Value);

    private sealed record PreparedTargetOperation (
        NormalizedTargetRequest Target,
        SkillPackageCatalog Catalog);
}
