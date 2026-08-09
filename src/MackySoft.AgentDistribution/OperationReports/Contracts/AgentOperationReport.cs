using MackySoft.AgentDistribution.OperationReports.Literals;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents a product-neutral custom-agent install, update, uninstall, or prune report. </summary>
public sealed class AgentOperationReport
{
    internal AgentOperationReport (
        HostKind host,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> agentNames,
        OperationScopeKind scope,
        string? repositoryRoot,
        string artifactRoot,
        string stateRoot,
        bool dryRun,
        bool force,
        IReadOnlyList<AgentOperationActionReport> actions,
        IReadOnlyList<OperationCountReport> actionCounts,
        IReadOnlyList<OperationCountReport> statusCounts,
        SkillOperationReport? skillReport)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported agent host.");
        }

        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported agent install scope.");
        }

        Host = host;
        Categories = OperationReportContractGuard.SnapshotRequiredStrings(categories, nameof(categories));
        AgentNames = OperationReportContractGuard.SnapshotRequiredStrings(agentNames, nameof(agentNames));
        Scope = scope;
        RepositoryRoot = OperationReportContractGuard.NormalizeRepositoryRoot(scope, repositoryRoot, nameof(repositoryRoot));
        ArtifactRoot = OperationReportContractGuard.NormalizeTargetRoot(
            scope,
            RepositoryRoot,
            artifactRoot,
            nameof(artifactRoot));
        StateRoot = OperationReportContractGuard.NormalizeTargetRoot(
            scope,
            RepositoryRoot,
            stateRoot,
            nameof(stateRoot));
        DryRun = dryRun;
        Force = force;
        Actions = OperationReportContractGuard.SnapshotRequiredItems(actions, nameof(actions));
        ActionCounts = OperationReportContractGuard.SnapshotRequiredItems(actionCounts, nameof(actionCounts));
        StatusCounts = OperationReportContractGuard.SnapshotRequiredItems(statusCounts, nameof(statusCounts));
        SkillReport = skillReport;
    }

    /// <summary> Gets the custom-agent host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets selected agent categories. </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary> Gets exact selected agent names. </summary>
    public IReadOnlyList<string> AgentNames { get; }

    /// <summary> Gets the install scope. </summary>
    public OperationScopeKind Scope { get; }

    /// <summary> Gets the project repository root, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the host-observed artifact root. </summary>
    public string ArtifactRoot { get; }

    /// <summary> Gets the Agent Distribution ownership-state root. </summary>
    public string StateRoot { get; }

    /// <summary> Gets whether this report describes a write-free plan. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether force semantics were enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets per-agent actions. </summary>
    public IReadOnlyList<AgentOperationActionReport> Actions { get; }

    /// <summary> Gets counts for every supported action literal. </summary>
    public IReadOnlyList<OperationCountReport> ActionCounts { get; }

    /// <summary> Gets counts for every coarse status literal. </summary>
    public IReadOnlyList<OperationCountReport> StatusCounts { get; }

    /// <summary> Gets the distinct resolved-SKILL operation report for install or update. </summary>
    public SkillOperationReport? SkillReport { get; }
}
