using MackySoft.AgentSkills.Agents;
using MackySoft.AgentSkills.Agents.Doctor;
using MackySoft.AgentSkills.Agents.Installation.Requests;
using MackySoft.AgentSkills.Agents.Installation.Services;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Agents.Selection;
using MackySoft.AgentSkills.Commands;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Shared;
using MackySoft.Text.Vocabularies;

namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Runs product CLI custom-agent commands after normalizing raw command input. </summary>
public sealed class AgentSkillsAgentsCommandRunner
{
    private readonly AgentSkillsCommandRuntimeConfiguration configuration;
    private readonly AgentPackageProvider packageProvider;
    private readonly SkillHostAdapterSet skillHostAdapters;
    private readonly AgentExportService exportService;
    private readonly AgentInstallService installService;
    private readonly AgentUpdateService updateService;
    private readonly AgentUninstallService uninstallService;
    private readonly AgentPruneService pruneService;
    private readonly AgentDoctorService doctorService;

    /// <summary> Initializes a new instance of the <see cref="AgentSkillsAgentsCommandRunner" /> class. </summary>
    public AgentSkillsAgentsCommandRunner (
        AgentSkillsCommandRuntimeConfiguration configuration,
        AgentPackageProvider packageProvider,
        SkillHostAdapterSet skillHostAdapters,
        AgentExportService exportService,
        AgentInstallService installService,
        AgentUpdateService updateService,
        AgentUninstallService uninstallService,
        AgentPruneService pruneService,
        AgentDoctorService doctorService)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.skillHostAdapters = skillHostAdapters ?? throw new ArgumentNullException(nameof(skillHostAdapters));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        this.uninstallService = uninstallService ?? throw new ArgumentNullException(nameof(uninstallService));
        this.pruneService = pruneService ?? throw new ArgumentNullException(nameof(pruneService));
        this.doctorService = doctorService ?? throw new ArgumentNullException(nameof(doctorService));
    }

    /// <summary> Runs <c>agents list</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> ListAsync (
        AgentSkillsAgentListCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.ListSubcommand);
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
        AgentSkillsAgentExportCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.ExportSubcommand);
        var selection = NormalizeRequiredSelection(request.Category, request.Agent);
        if (!selection.IsSuccess)
        {
            return Failure(commandName, selection.Failure!);
        }

        var host = SkillCommandValueParser.ParseHostLiteral(request.Host, skillHostAdapters);
        if (!host.IsSuccess)
        {
            return Failure(commandName, host.Failure!);
        }

        var output = NormalizeRequiredFullPath(request.Output, "Option '--output' is required.");
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

        var agentHost = ResolveAgentHost(host.Value!.Host);
        if (!agentHost.IsSuccess)
        {
            return Failure(commandName, agentHost.Failure!);
        }

        var export = await exportService.ExportAsync(catalog.Value!, agentHost.Value, output.Value!, format.Value, cancellationToken).ConfigureAwait(false);
        return export.IsSuccess
            ? AgentSkillsCommandResult.Success(commandName, AgentOperationReportBuilder.CreateExportReport(export.Value!, catalog.Value!, agentHost.Value, format.Value))
            : Failure(commandName, export.Failure!);
    }

    /// <summary> Runs <c>agents install</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> InstallAsync (
        AgentSkillsAgentInstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.InstallSubcommand);
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
            ? AgentSkillsCommandResult.Success(commandName, result.Value!)
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents update</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> UpdateAsync (
        AgentSkillsAgentUpdateCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.UpdateSubcommand);
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
            ? AgentSkillsCommandResult.Success(commandName, result.Value!)
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents uninstall</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> UninstallAsync (
        AgentSkillsAgentUninstallCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.UninstallSubcommand);
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
            ? AgentSkillsCommandResult.Success(commandName, result.Value!)
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents prune</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> PruneAsync (
        AgentSkillsAgentPruneCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.PruneSubcommand);
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
            ? AgentSkillsCommandResult.Success(commandName, result.Value!)
            : Failure(commandName, result.Failure!);
    }

    /// <summary> Runs <c>agents doctor</c>. </summary>
    public async ValueTask<AgentSkillsCommandResult> DoctorAsync (
        AgentSkillsAgentDoctorCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var commandName = CreateCommandName(AgentSkillsAgentsCommandNames.DoctorSubcommand);
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

        return AgentSkillsCommandResult.Success(commandName, result.Value!, result.Value!.IsHealthy ? 0 : 1);
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
        var hostResult = SkillCommandValueParser.ParseHostLiteral(host, skillHostAdapters);
        if (!hostResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(hostResult.Failure!);
        }

        var scopeResult = SkillCommandValueParser.ParseScopeLiteral(scope);
        if (!scopeResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(scopeResult.Failure!);
        }

        var rootResult = NormalizeRepositoryRoot(scopeResult.Value, repositoryRoot);
        if (!rootResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(rootResult.Failure!);
        }

        var agentTargetResult = NormalizeTargetRoot(scopeResult.Value, rootResult.Value!.Value, agentTargetDir, "agent-target-dir");
        if (!agentTargetResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(agentTargetResult.Failure!);
        }

        var skillTargetResult = NormalizeTargetRoot(scopeResult.Value, rootResult.Value!.Value, skillTargetDir, "skill-target-dir");
        if (!skillTargetResult.IsSuccess)
        {
            return Failure<AgentTargetPair>(skillTargetResult.Failure!);
        }

        var agentScope = scopeResult.Value == SkillScopeKind.Project
            ? AgentInstallScopeKind.Project
            : AgentInstallScopeKind.User;
        var hostKind = hostResult.Value!.Host;
        var agentHost = ResolveAgentHost(hostKind);
        if (!agentHost.IsSuccess)
        {
            return Failure<AgentTargetPair>(agentHost.Failure!);
        }

        return SkillOperationResult<AgentTargetPair>.Success(new AgentTargetPair(
            new AgentTargetRequest(agentHost.Value, agentScope, rootResult.Value.Value, agentTargetResult.Value!.Value),
            new SkillInstallRequest(hostKind, scopeResult.Value, rootResult.Value.Value, skillTargetResult.Value!.Value)));
    }

    private static SkillOperationResult<AgentHostKind> ResolveAgentHost (SkillHostKind skillHost)
    {
        var hostLiteral = Vocabulary.GetText(skillHost);
        return Vocabulary.TryGetValue(hostLiteral, out AgentHostKind agentHost)
            ? SkillOperationResult<AgentHostKind>.Success(agentHost)
            : SkillOperationResult<AgentHostKind>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"The selected SKILL host does not support custom agents: {hostLiteral}");
    }

    private SkillOperationResult<AgentSelection> NormalizeOptionalSelection (
        IReadOnlyList<string>? category,
        IReadOnlyList<string>? agent)
    {
        var categories = ExpandOptionValues(category);
        var agents = ExpandOptionValues(agent);
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

    private SkillOperationResult<NormalizedRepositoryRoot> NormalizeRepositoryRoot (SkillScopeKind scope, string? repositoryRoot)
    {
        if (scope == SkillScopeKind.User)
        {
            return string.IsNullOrWhiteSpace(repositoryRoot)
                ? SkillOperationResult<NormalizedRepositoryRoot>.Success(new NormalizedRepositoryRoot(null))
                : SkillOperationResult<NormalizedRepositoryRoot>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    "Option '--repository-root' is not supported when '--scope user' is used.");
        }

        var root = string.IsNullOrWhiteSpace(repositoryRoot)
            ? configuration.RepositoryRootResolver(Directory.GetCurrentDirectory())
            : repositoryRoot;
        var normalized = NormalizeRequiredFullPath(root, "Project-scope custom-agent operation requires a repository root.");
        return normalized.IsSuccess
            ? SkillOperationResult<NormalizedRepositoryRoot>.Success(new NormalizedRepositoryRoot(normalized.Value))
            : Failure<NormalizedRepositoryRoot>(normalized.Failure!);
    }

    private static SkillOperationResult<NormalizedTargetRoot> NormalizeTargetRoot (
        SkillScopeKind scope,
        string? repositoryRoot,
        string? targetRoot,
        string optionName)
    {
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            return SkillOperationResult<NormalizedTargetRoot>.Success(new NormalizedTargetRoot(null));
        }

        if (scope == SkillScopeKind.User && !Path.IsPathFullyQualified(targetRoot))
        {
            return SkillOperationResult<NormalizedTargetRoot>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"User-scope {optionName} must be an absolute path.");
        }

        try
        {
            return SkillOperationResult<NormalizedTargetRoot>.Success(
                new NormalizedTargetRoot(Path.IsPathFullyQualified(targetRoot)
                    ? Path.GetFullPath(targetRoot)
                    : Path.GetFullPath(targetRoot, repositoryRoot!)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return SkillOperationResult<NormalizedTargetRoot>.FailureResult(SkillFailureCodes.PathUnsafe, exception.Message);
        }
    }

    private static SkillOperationResult<string> NormalizeRequiredFullPath (string? path, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return SkillOperationResult<string>.FailureResult(SkillFailureCodes.InputInvalid, missingMessage);
        }

        try
        {
            return SkillOperationResult<string>.Success(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return SkillOperationResult<string>.FailureResult(SkillFailureCodes.PathUnsafe, exception.Message);
        }
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

    private string CreateCommandName (string subcommand)
    {
        return AgentSkillsAgentsCommandNames.CreateCommandName(configuration.AgentsCommandRoot, subcommand);
    }

    private static AgentSkillsCommandResult Failure (string command, SkillFailure failure)
    {
        return AgentSkillsCommandResult.FailureResult(command, failure);
    }

    private static SkillOperationResult<T> Failure<T> (SkillFailure failure)
    {
        return SkillOperationResult<T>.FailureResult(failure.Code, failure.Message);
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

    private sealed class NormalizedRepositoryRoot
    {
        public NormalizedRepositoryRoot (string? value)
        {
            Value = value;
        }

        public string? Value { get; }
    }

    private sealed class NormalizedTargetRoot
    {
        public NormalizedTargetRoot (string? value)
        {
            Value = value;
        }

        public string? Value { get; }
    }

    private sealed class AgentTargetPair
    {
        public AgentTargetPair (AgentTargetRequest agentTargetRequest, SkillInstallRequest skillTargetRequest)
        {
            AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
            SkillTargetRequest = skillTargetRequest ?? throw new ArgumentNullException(nameof(skillTargetRequest));
        }

        public AgentTargetRequest AgentTargetRequest { get; }

        public SkillInstallRequest SkillTargetRequest { get; }
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
