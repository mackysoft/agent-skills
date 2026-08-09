using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Contracts;
using MackySoft.AgentDistribution.OperationReports.Literals;

namespace MackySoft.AgentDistribution.OperationReports.Projection;

/// <summary> Provides caller-owned selection and target context for custom-agent operation reports. </summary>
public sealed class AgentOperationReportContext
{
    /// <summary> Initializes immutable context for one custom-agent operation report. </summary>
    public AgentOperationReportContext (
        HostKind host,
        AgentInstallScopeKind scope,
        string? repositoryRoot,
        IReadOnlyList<AgentName> selectedAgentNames,
        SkillOperationReportContext skillContext)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported agent host.");
        }

        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported agent install scope.");
        }

        ArgumentNullException.ThrowIfNull(selectedAgentNames);
        var agentNameSnapshot = selectedAgentNames.ToArray();
        if (agentNameSnapshot.Any(static agentName => agentName is null))
        {
            throw new ArgumentException("Agent report selections must not contain null values.");
        }

        Host = host;
        Scope = scope;
        RepositoryRoot = OperationReportContractGuard.NormalizeRepositoryRoot(ToOperationScope(scope), repositoryRoot, nameof(repositoryRoot));
        SelectedAgentNames = Array.AsReadOnly(agentNameSnapshot);
        SkillContext = skillContext ?? throw new ArgumentNullException(nameof(skillContext));
    }

    /// <summary> Gets the custom-agent host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the custom-agent install scope. </summary>
    public AgentInstallScopeKind Scope { get; }

    /// <summary> Gets the project repository root, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets exact selected agent names. </summary>
    public IReadOnlyList<AgentName> SelectedAgentNames { get; }

    /// <summary> Gets the independently resolved SKILL operation context. </summary>
    public SkillOperationReportContext SkillContext { get; }

    internal static OperationScopeKind ToOperationScope (AgentInstallScopeKind scope)
    {
        return scope switch
        {
            AgentInstallScopeKind.Project => OperationScopeKind.Project,
            AgentInstallScopeKind.User => OperationScopeKind.User,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported agent install scope."),
        };
    }
}
