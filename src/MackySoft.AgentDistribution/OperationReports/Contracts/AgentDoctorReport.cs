using MackySoft.AgentDistribution.OperationReports.Literals;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents product-neutral custom-agent and resolved-SKILL diagnostics. </summary>
public sealed class AgentDoctorReport
{
    internal AgentDoctorReport (
        HostKind host,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> agentNames,
        OperationScopeKind scope,
        string? repositoryRoot,
        string artifactRoot,
        string stateRoot,
        IReadOnlyList<AgentDoctorDiagnosticReport> diagnostics,
        SkillDoctorReport skillReport)
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
        ArtifactRoot = OperationReportContractGuard.NormalizeTargetRoot(scope, RepositoryRoot, artifactRoot, nameof(artifactRoot));
        StateRoot = OperationReportContractGuard.NormalizeTargetRoot(scope, RepositoryRoot, stateRoot, nameof(stateRoot));
        Diagnostics = OperationReportContractGuard.SnapshotRequiredItems(diagnostics, nameof(diagnostics));
        SkillReport = skillReport ?? throw new ArgumentNullException(nameof(skillReport));
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

    /// <summary> Gets the diagnosed artifact root. </summary>
    public string ArtifactRoot { get; }

    /// <summary> Gets the diagnosed ownership-state root. </summary>
    public string StateRoot { get; }

    /// <summary> Gets custom-agent diagnostics. </summary>
    public IReadOnlyList<AgentDoctorDiagnosticReport> Diagnostics { get; }

    /// <summary> Gets the distinct resolved-SKILL diagnostic report. </summary>
    public SkillDoctorReport SkillReport { get; }

    /// <summary> Gets whether both custom agents and resolved SKILL dependencies are healthy. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => diagnostic.Severity != Doctor.SkillDoctorSeverity.Error)
        && SkillReport.IsHealthy;
}
