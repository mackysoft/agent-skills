using MackySoft.AgentDistribution.Agents.Distribution;
using MackySoft.AgentDistribution.Agents.Doctor;
using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Services;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Agents.Selection;
using MackySoft.AgentDistribution.Commands;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Projection;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary> Runs product CLI custom-agent commands after normalizing raw command input. </summary>
public sealed class AgentCommandRunner
{
    private readonly AgentDistributionCommandRuntimeConfiguration configuration;
    private readonly AgentPackageProvider packageProvider;
    private readonly AgentExportService exportService;
    private readonly AgentInstallService installService;
    private readonly AgentUpdateService updateService;
    private readonly AgentUninstallService uninstallService;
    private readonly AgentPruneService pruneService;
    private readonly AgentDoctorService doctorService;
    private readonly SkillInstallTargetResolver skillTargetResolver;

    /// <summary> Initializes a new instance of the <see cref="AgentCommandRunner" /> class. </summary>
    public AgentCommandRunner (
        AgentDistributionCommandRuntimeConfiguration configuration,
        AgentPackageProvider packageProvider,
        AgentExportService exportService,
        AgentInstallService installService,
        AgentUpdateService updateService,
        AgentUninstallService uninstallService,
        AgentPruneService pruneService,
        AgentDoctorService doctorService,
        SkillInstallTargetResolver skillTargetResolver)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        this.uninstallService = uninstallService ?? throw new ArgumentNullException(nameof(uninstallService));
        this.pruneService = pruneService ?? throw new ArgumentNullException(nameof(pruneService));
        this.doctorService = doctorService ?? throw new ArgumentNullException(nameof(doctorService));
        this.skillTargetResolver = skillTargetResolver ?? throw new ArgumentNullException(nameof(skillTargetResolver));
    }

    /// <summary> Runs <c>agents list</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> ListAsync (
        AgentListCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.list";
        var catalog = await GetCatalogAsync(NormalizeOptionalAgentNames(request.Agent), cancellationToken).ConfigureAwait(false);
        return catalog.IsSuccess
            ? AgentDistributionCommandResult.Success(commandName, AgentOperationReportBuilder.CreateListReport(catalog.Value!))
            : Failure(commandName, catalog.Failure!);
    }

    /// <summary> Runs <c>agents export</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> ExportAsync (
        AgentExportCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.export";
        var selection = NormalizeRequiredAgentNames(request.Agent);
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
            ? AgentDistributionOperationResult<PackageExportFormat>.Success(PackageExportFormat.Directory)
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
            ? AgentDistributionCommandResult.Success(commandName, AgentOperationReportBuilder.CreateExportReport(export.Value!, catalog.Value!, agentHost, format.Value))
            : Failure(commandName, export.Failure!);
    }

    /// <summary> Runs <c>agents install</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> InstallAsync (
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
            ? AgentDistributionCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreateInstallReport(result.Value!, CreateReportContext(prepared.Value)))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents update</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> UpdateAsync (
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
            ? AgentDistributionCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreateUpdateReport(result.Value!, CreateReportContext(prepared.Value)))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents uninstall</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> UninstallAsync (
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
            ? AgentDistributionCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreateUninstallReport(result.Value!, CreateReportContext(prepared.Value)))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents prune</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> PruneAsync (
        AgentPruneCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        const string commandName = "agents.prune";
        var selection = NormalizeRequiredAgentNames(request.Agent);
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

        var pruneSelection = AgentNameLiteralParser.ParseOptionalAgentNames(selection.Value!);
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
                    pruneSelection.Value!),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? AgentDistributionCommandResult.Success(
                commandName,
                AgentOperationReportBuilder.CreatePruneReport(
                    result.Value!,
                    CreateReportContext(
                        target.Value!,
                        pruneSelection.Value!,
                        catalog.Value!.ResolvedSkills.Select(static package => package.Manifest.SkillName).ToArray())))
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents doctor</c>. </summary>
    public async ValueTask<AgentDistributionCommandResult> DoctorAsync (
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
        return AgentDistributionCommandResult.Success(commandName, report, report.IsHealthy ? 0 : 1);
    }

    private async ValueTask<AgentDistributionOperationResult<PreparedAgentTargetOperation>> PrepareSelectedTargetOperationAsync (
        string? host,
        string? scope,
        string? repositoryRoot,
        string? agentTargetDir,
        string? skillTargetDir,
        IReadOnlyList<string>? agent,
        CancellationToken cancellationToken)
    {
        var selection = NormalizeRequiredAgentNames(agent);
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
            ? AgentDistributionOperationResult<PreparedAgentTargetOperation>.Success(new PreparedAgentTargetOperation(target.Value!, catalog.Value!))
            : Failure<PreparedAgentTargetOperation>(catalog.Failure!);
    }

    private ValueTask<AgentDistributionOperationResult<AgentPackageCatalog>> GetCatalogAsync (
        IReadOnlyList<string> selectedAgentNames,
        CancellationToken cancellationToken)
    {
        return packageProvider.GetPackageCatalogAsync(selectedAgentNames, cancellationToken);
    }

    private AgentDistributionOperationResult<AgentTargetPair> NormalizeTarget (
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
        var resolvedHostResult = skillTargetResolver.ResolveHost(hostKind);
        if (!resolvedHostResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(resolvedHostResult.Failure!);
        }

        return AgentDistributionOperationResult<AgentTargetPair>.Success(new AgentTargetPair(
            new AgentTargetRequest(hostKind, agentScope, repositoryContext.RepositoryRoot, agentTargetRoot),
            new SkillInstallRequest(hostKind, repositoryContext.Scope, repositoryContext.RepositoryRoot, skillTargetRoot),
            resolvedHostResult.Value!));
    }

    private static IReadOnlyList<string> NormalizeOptionalAgentNames (IReadOnlyList<string>? agent)
    {
        return CommandOptionValues.Expand(agent);
    }

    private static AgentDistributionOperationResult<IReadOnlyList<string>> NormalizeRequiredAgentNames (IReadOnlyList<string>? agent)
    {
        var selection = NormalizeOptionalAgentNames(agent);
        if (selection.Count == 0)
        {
            return AgentDistributionOperationResult<IReadOnlyList<string>>.FailureResult(
                AgentDistributionFailureCodes.InputInvalid,
                "Option '--agent' is required.");
        }

        return AgentDistributionOperationResult<IReadOnlyList<string>>.Success(selection);
    }

    private static AgentDistributionCommandResult Failure (string command, AgentDistributionFailure failure)
    {
        return AgentDistributionCommandResult.FailureResult(command, failure);
    }

    private static AgentDistributionOperationResult<T> Failure<T> (AgentDistributionFailure failure)
    {
        return AgentDistributionOperationResult<T>.FailureResult(failure.Code, failure.Message);
    }

    private static AgentOperationReportContext CreateReportContext (PreparedAgentTargetOperation prepared)
    {
        return CreateReportContext(
            prepared.Target,
            prepared.Catalog.SelectedAgentNames,
            prepared.Catalog.ResolvedSkills.Select(static package => package.Manifest.SkillName).ToArray());
    }

    private static AgentOperationReportContext CreateReportContext (
        AgentTargetPair target,
        IReadOnlyList<AgentName> selectedAgentNames,
        IReadOnlyList<SkillName> resolvedSkillNames)
    {
        var agentRequest = target.AgentTargetRequest;
        var skillRequest = target.SkillTargetRequest;
        return new AgentOperationReportContext(
            agentRequest.HostId,
            agentRequest.Scope,
            agentRequest.RepositoryRoot?.Value,
            selectedAgentNames,
            new SkillOperationReportContext(
                target.ResolvedHost,
                skillRequest.Scope,
                skillRequest.RepositoryRoot?.Value,
                [],
                resolvedSkillNames));
    }

    private sealed class AgentTargetPair
    {
        public AgentTargetPair (
            AgentTargetRequest agentTargetRequest,
            SkillInstallRequest skillTargetRequest,
            SkillResolvedHost resolvedHost)
        {
            AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
            SkillTargetRequest = skillTargetRequest ?? throw new ArgumentNullException(nameof(skillTargetRequest));
            ResolvedHost = resolvedHost ?? throw new ArgumentNullException(nameof(resolvedHost));
            if (AgentTargetRequest.HostId != ResolvedHost.Host || SkillTargetRequest.Host != ResolvedHost.Host)
            {
                throw new ArgumentException("Agent and skill targets must identify the resolved host.");
            }
        }

        public AgentTargetRequest AgentTargetRequest { get; }

        public SkillInstallRequest SkillTargetRequest { get; }

        public SkillResolvedHost ResolvedHost { get; }

        public HostKind Host => ResolvedHost.Host;
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
