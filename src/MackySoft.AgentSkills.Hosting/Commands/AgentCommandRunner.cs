using MackySoft.AgentSkills.Agents.Doctor;
using MackySoft.AgentSkills.Agents.Installation.Requests;
using MackySoft.AgentSkills.Agents.Installation.Services;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Agents.Selection;
using MackySoft.AgentSkills.Commands;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Runs product CLI custom-agent commands after normalizing raw command input. </summary>
public sealed class AgentCommandRunner
{
    private readonly AgentSkillsCommandRuntimeConfiguration configuration;
    private readonly AgentPackageProvider packageProvider;
    private readonly AgentExportService exportService;
    private readonly AgentInstallService installService;
    private readonly AgentUpdateService updateService;
    private readonly AgentUninstallService uninstallService;
    private readonly AgentPruneService pruneService;
    private readonly AgentDoctorService doctorService;

    /// <summary> Initializes a new instance of the <see cref="AgentCommandRunner" /> class. </summary>
    public AgentCommandRunner (
        AgentSkillsCommandRuntimeConfiguration configuration,
        AgentPackageProvider packageProvider,
        AgentExportService exportService,
        AgentInstallService installService,
        AgentUpdateService updateService,
        AgentUninstallService uninstallService,
        AgentPruneService pruneService,
        AgentDoctorService doctorService)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        this.uninstallService = uninstallService ?? throw new ArgumentNullException(nameof(uninstallService));
        this.pruneService = pruneService ?? throw new ArgumentNullException(nameof(pruneService));
        this.doctorService = doctorService ?? throw new ArgumentNullException(nameof(doctorService));
    }

    /// <summary> Runs <c>agents list</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> ListAsync (
        AgentListCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.list";
        var selection = NormalizeOptionalSelection(request.Category, request.Agent);
        if (!selection.IsSuccess)
        {
            return Failure(commandName, selection.Failure!);
        }

        var catalog = await GetCatalogAsync(selection.Value!, cancellationToken).ConfigureAwait(false);
        return catalog.IsSuccess
            ? AgentSkillsCommandResult.Success(commandName, AgentOperationReportBuilder.CreateListReport(catalog.Value!))
            : Failure(commandName, catalog.Failure!);
    }

    /// <summary> Runs <c>agents export</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> ExportAsync (
        AgentExportCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.export";
        var selection = NormalizeRequiredSelection(request.Category, request.Agent);
        if (!selection.IsSuccess)
        {
            return Failure(commandName, selection.Failure!);
        }

        var host = SkillCommandValueParser.ParseHostLiteral(request.Host);
        if (!host.IsSuccess)
        {
            return Failure(commandName, host.Failure!);
        }

        var output = CommandPathResolver.ResolveRequired(request.Output, "Option '--output' is required.");
        if (!output.IsSuccess)
        {
            return Failure(commandName, output.Failure!);
        }

        var format = string.IsNullOrWhiteSpace(request.Format)
            ? SkillOperationResult<SkillExportFormat>.Success(SkillExportFormat.Directory)
            : SkillCommandValueParser.ParseExportFormatLiteral(request.Format);
        if (!format.IsSuccess)
        {
            return Failure(commandName, format.Failure!);
        }

        var catalog = await GetCatalogAsync(selection.Value!, cancellationToken).ConfigureAwait(false);
        if (!catalog.IsSuccess)
        {
            return Failure(commandName, catalog.Failure!);
        }

        var agentHost = host.Value;
        var export = await exportService.ExportAsync(catalog.Value!, agentHost, output.Value!, format.Value, cancellationToken).ConfigureAwait(false);
        return export.IsSuccess
            ? AgentSkillsCommandResult.Success(commandName, AgentOperationReportBuilder.CreateExportReport(export.Value!, catalog.Value!, agentHost, format.Value))
            : Failure(commandName, export.Failure!);
    }

    /// <summary> Runs <c>agents install</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> InstallAsync (
        AgentInstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.install";
        var prepared = await PrepareSelectedTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.AgentTargetDir,
                request.SkillTargetDir,
                request.Category,
                request.Agent,
                cancellationToken)
            .ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            return Failure(commandName, prepared.Failure!);
        }

        var result = await installService.InstallAsync(
                new AgentInstallInput(
                    prepared.Value!.Catalog,
                    prepared.Value.Target.AgentTargetRequest,
                    prepared.Value.Target.SkillTargetRequest,
                    request.DryRun,
                    request.Force,
                    request.PrintDiff),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? AgentSkillsCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreateInstallReport(result.Value!, CreateReportContext(prepared.Value)))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents update</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> UpdateAsync (
        AgentUpdateCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.update";
        var prepared = await PrepareSelectedTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.AgentTargetDir,
                request.SkillTargetDir,
                request.Category,
                request.Agent,
                cancellationToken)
            .ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            return Failure(commandName, prepared.Failure!);
        }

        var result = await updateService.UpdateAsync(
                new AgentUpdateInput(
                    prepared.Value!.Catalog,
                    prepared.Value.Target.AgentTargetRequest,
                    prepared.Value.Target.SkillTargetRequest,
                    request.DryRun,
                    request.Force,
                    request.PrintDiff),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? AgentSkillsCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreateUpdateReport(result.Value!, CreateReportContext(prepared.Value)))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents uninstall</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> UninstallAsync (
        AgentUninstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.uninstall";
        var prepared = await PrepareSelectedTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.AgentTargetDir,
                skillTargetDir: null,
                request.Category,
                request.Agent,
                cancellationToken)
            .ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            return Failure(commandName, prepared.Failure!);
        }

        var result = await uninstallService.UninstallAsync(
                new AgentUninstallInput(prepared.Value!.Catalog, prepared.Value.Target.AgentTargetRequest, request.DryRun, request.Force),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? AgentSkillsCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreateUninstallReport(result.Value!, CreateReportContext(prepared.Value)))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents prune</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> PruneAsync (
        AgentPruneCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.prune";
        var selection = NormalizeRequiredSelection(request.Category, request.Agent);
        if (!selection.IsSuccess)
        {
            return Failure(commandName, selection.Failure!);
        }

        var target = NormalizeTarget(request.Host, request.Scope, request.RepositoryRoot, request.AgentTargetDir, skillTargetDir: null);
        if (!target.IsSuccess)
        {
            return Failure(commandName, target.Failure!);
        }

        var catalog = await packageProvider.GetPackageCatalogAsync(cancellationToken).ConfigureAwait(false);
        if (!catalog.IsSuccess)
        {
            return Failure(commandName, catalog.Failure!);
        }

        var pruneSelection = NormalizePruneSelection(selection.Value!);
        if (!pruneSelection.IsSuccess)
        {
            return Failure(commandName, pruneSelection.Failure!);
        }

        var result = await pruneService.PruneAsync(
                new AgentPruneInput(
                    catalog.Value!,
                    target.Value!.AgentTargetRequest,
                    request.DryRun,
                    request.Force,
                    pruneSelection.Value!.Categories,
                    pruneSelection.Value.AgentNames),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? AgentSkillsCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreatePruneReport(
                    result.Value!,
                    CreateReportContext(
                        target.Value!,
                        pruneSelection.Value.Categories,
                        pruneSelection.Value.AgentNames,
                        catalog.Value!.ResolvedSkills.Select(static package => package.Manifest.SkillName).ToArray())))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents doctor</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> DoctorAsync (
        AgentDoctorCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.doctor";
        var prepared = await PrepareSelectedTargetOperationAsync(
                request.Host,
                request.Scope,
                request.RepositoryRoot,
                request.AgentTargetDir,
                request.SkillTargetDir,
                request.Category,
                request.Agent,
                cancellationToken)
            .ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            return Failure(commandName, prepared.Failure!);
        }

        var result = await doctorService.DiagnoseAsync(
                new AgentDoctorInput(
                    prepared.Value!.Catalog,
                    prepared.Value.Target.AgentTargetRequest,
                    prepared.Value.Target.SkillTargetRequest),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Failure(commandName, result.Failure!);
        }

        var report = AgentOperationReportBuilder.CreateDoctorReport(result.Value!, CreateReportContext(prepared.Value));
        return AgentSkillsCommandResult.Success(commandName, report, report.IsHealthy ? 0 : 1);
    }

    private async ValueTask<SkillOperationResult<PreparedAgentTargetOperation>> PrepareSelectedTargetOperationAsync (
        string? host,
        string? scope,
        string? repositoryRoot,
        string? agentTargetDir,
        string? skillTargetDir,
        IReadOnlyList<string>? category,
        IReadOnlyList<string>? agent,
        CancellationToken cancellationToken)
    {
        var selection = NormalizeRequiredSelection(category, agent);
        if (!selection.IsSuccess)
        {
            return Failure<PreparedAgentTargetOperation>(selection.Failure!);
        }

        var target = NormalizeTarget(host, scope, repositoryRoot, agentTargetDir, skillTargetDir);
        if (!target.IsSuccess)
        {
            return Failure<PreparedAgentTargetOperation>(target.Failure!);
        }

        var catalog = await GetCatalogAsync(selection.Value!, cancellationToken).ConfigureAwait(false);
        return catalog.IsSuccess
            ? SkillOperationResult<PreparedAgentTargetOperation>.Success(new PreparedAgentTargetOperation(target.Value!, catalog.Value!))
            : Failure<PreparedAgentTargetOperation>(catalog.Failure!);
    }

    private ValueTask<SkillOperationResult<AgentPackageCatalog>> GetCatalogAsync (
        AgentSelection selection,
        CancellationToken cancellationToken)
    {
        return packageProvider.GetPackageCatalogAsync(selection.Categories, selection.Agents, cancellationToken);
    }

    private SkillOperationResult<AgentTargetPair> NormalizeTarget (
        string? host,
        string? scope,
        string? repositoryRoot,
        string? agentTargetDir,
        string? skillTargetDir)
    {
        var hostResult = SkillCommandValueParser.ParseHostLiteral(host);
        if (!hostResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(hostResult.Failure!);
        }

        var scopeResult = SkillCommandValueParser.ParseScopeLiteral(scope);
        if (!scopeResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(scopeResult.Failure!);
        }

        var repositoryContextResult = CommandPathResolver.ResolveRepositoryContext(scopeResult.Value, repositoryRoot, configuration);
        if (!repositoryContextResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(repositoryContextResult.Failure!);
        }

        var repositoryContext = repositoryContextResult.Value!;
        AbsolutePath? agentTargetRoot = null;
        if (!string.IsNullOrWhiteSpace(agentTargetDir))
        {
            var agentTargetResult = CommandPathResolver.ResolveTarget(agentTargetDir, repositoryContext.RepositoryRoot, "agent-target-dir");
            if (!agentTargetResult.IsSuccess)
            {
                return Failure<AgentTargetPair>(agentTargetResult.Failure!);
            }

            agentTargetRoot = agentTargetResult.Value;
        }

        AbsolutePath? skillTargetRoot = null;
        if (!string.IsNullOrWhiteSpace(skillTargetDir))
        {
            var skillTargetResult = CommandPathResolver.ResolveTarget(skillTargetDir, repositoryContext.RepositoryRoot, "skill-target-dir");
            if (!skillTargetResult.IsSuccess)
            {
                return Failure<AgentTargetPair>(skillTargetResult.Failure!);
            }

            skillTargetRoot = skillTargetResult.Value;
        }

        var agentScope = repositoryContext.Scope == SkillScopeKind.Project
            ? AgentInstallScopeKind.Project
            : AgentInstallScopeKind.User;
        var hostKind = hostResult.Value;

        return SkillOperationResult<AgentTargetPair>.Success(new AgentTargetPair(
            new AgentTargetRequest(hostKind, agentScope, repositoryContext.RepositoryRoot, agentTargetRoot),
            new SkillInstallRequest(hostKind, repositoryContext.Scope, repositoryContext.RepositoryRoot, skillTargetRoot),
            hostKind));
    }

    private SkillOperationResult<AgentSelection> NormalizeOptionalSelection (
        IReadOnlyList<string>? category,
        IReadOnlyList<string>? agent)
    {
        var categories = CommandOptionValues.Expand(category);
        var agents = CommandOptionValues.Expand(agent);
        return SkillOperationResult<AgentSelection>.Success(new AgentSelection(categories, agents));
    }

    private SkillOperationResult<AgentSelection> NormalizeRequiredSelection (
        IReadOnlyList<string>? category,
        IReadOnlyList<string>? agent)
    {
        var selection = NormalizeOptionalSelection(category, agent);
        if (selection.Value!.Categories.Count == 0 && selection.Value.Agents.Count == 0)
        {
            return SkillOperationResult<AgentSelection>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "Option '--category' or '--agent' is required.");
        }

        return selection;
    }

    private static SkillOperationResult<AgentPruneSelection> NormalizePruneSelection (AgentSelection selection)
    {
        var categoriesResult = AgentCategoryLiteralParser.ParseOptionalCategories(selection.Categories);
        if (!categoriesResult.IsSuccess)
        {
            return Failure<AgentPruneSelection>(categoriesResult.Failure!);
        }

        var agentNamesResult = AgentNameLiteralParser.ParseOptionalAgentNames(selection.Agents);
        return agentNamesResult.IsSuccess
            ? SkillOperationResult<AgentPruneSelection>.Success(
                new AgentPruneSelection(categoriesResult.Value!, agentNamesResult.Value!))
            : Failure<AgentPruneSelection>(agentNamesResult.Failure!);
    }

    private static AgentSkillsCommandResult Failure (string command, SkillFailure failure)
    {
        return AgentSkillsCommandResult.FailureResult(command, failure);
    }

    private static SkillOperationResult<T> Failure<T> (SkillFailure failure)
    {
        return SkillOperationResult<T>.FailureResult(failure.Code, failure.Message);
    }

    private static AgentOperationReportContext CreateReportContext (PreparedAgentTargetOperation prepared)
    {
        return CreateReportContext(
            prepared.Target,
            prepared.Catalog.SelectedCategories,
            prepared.Catalog.SelectedAgentNames,
            prepared.Catalog.ResolvedSkills.Select(static package => package.Manifest.SkillName).ToArray());
    }

    private static AgentOperationReportContext CreateReportContext (
        AgentTargetPair target,
        IReadOnlyList<AgentCategory> selectedCategories,
        IReadOnlyList<AgentName> selectedAgentNames,
        IReadOnlyList<SkillName> resolvedSkillNames)
    {
        var agentRequest = target.AgentTargetRequest;
        var skillRequest = target.SkillTargetRequest;
        return new AgentOperationReportContext(
            agentRequest.HostId,
            agentRequest.Scope,
            agentRequest.RepositoryRoot?.Value,
            selectedCategories,
            selectedAgentNames,
            new SkillOperationReportContext(
                target.Host,
                skillRequest.Scope,
                skillRequest.RepositoryRoot?.Value,
                [],
                resolvedSkillNames));
    }

    private sealed class AgentSelection
    {
        public AgentSelection (IReadOnlyList<string> categories, IReadOnlyList<string> agents)
        {
            ArgumentNullException.ThrowIfNull(categories);
            ArgumentNullException.ThrowIfNull(agents);
            Categories = Array.AsReadOnly(categories.ToArray());
            Agents = Array.AsReadOnly(agents.ToArray());
        }

        public IReadOnlyList<string> Categories { get; }

        public IReadOnlyList<string> Agents { get; }
    }

    private sealed class AgentTargetPair
    {
        public AgentTargetPair (
            AgentTargetRequest agentTargetRequest,
            SkillInstallRequest skillTargetRequest,
            HostKind host)
        {
            AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
            SkillTargetRequest = skillTargetRequest ?? throw new ArgumentNullException(nameof(skillTargetRequest));
            Host = host;
        }

        public AgentTargetRequest AgentTargetRequest { get; }

        public SkillInstallRequest SkillTargetRequest { get; }

        public HostKind Host { get; }
    }

    private sealed class AgentPruneSelection
    {
        public AgentPruneSelection (
            IReadOnlyList<AgentCategory> categories,
            IReadOnlyList<AgentName> agentNames)
        {
            Categories = categories ?? throw new ArgumentNullException(nameof(categories));
            AgentNames = agentNames ?? throw new ArgumentNullException(nameof(agentNames));
        }

        public IReadOnlyList<AgentCategory> Categories { get; }

        public IReadOnlyList<AgentName> AgentNames { get; }
    }

    private sealed class PreparedAgentTargetOperation
    {
        public PreparedAgentTargetOperation (AgentTargetPair target, AgentPackageCatalog catalog)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public AgentTargetPair Target { get; }

        public AgentPackageCatalog Catalog { get; }
    }
}
