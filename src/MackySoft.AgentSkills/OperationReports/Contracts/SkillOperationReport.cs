using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a product-neutral install, update, uninstall, or prune report. </summary>
public sealed class SkillOperationReport
{
    internal SkillOperationReport (
        SkillHostKind host,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> skillNames,
        SkillScopeKind scope,
        string? repositoryRoot,
        string targetRoot,
        bool dryRun,
        bool force,
        string reloadGuidance,
        IReadOnlyList<SkillOperationActionReport> actions,
        IReadOnlyList<SkillOperationCountReport> actionCounts,
        IReadOnlyList<SkillOperationCountReport> statusCounts)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!ContractLiteralCodec.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported install scope.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        Host = host;
        Categories = OperationReportContractGuard.SnapshotRequiredStrings(categories, nameof(categories));
        SkillNames = OperationReportContractGuard.SnapshotRequiredStrings(skillNames, nameof(skillNames));
        Scope = scope;
        RepositoryRoot = OperationReportContractGuard.NormalizeRepositoryRoot(scope, repositoryRoot, nameof(repositoryRoot));
        TargetRoot = OperationReportContractGuard.NormalizeTargetRoot(scope, RepositoryRoot, targetRoot, nameof(targetRoot));
        DryRun = dryRun;
        Force = force;
        ReloadGuidance = reloadGuidance;
        Actions = OperationReportContractGuard.SnapshotRequiredItems(actions, nameof(actions));
        ActionCounts = OperationReportContractGuard.SnapshotRequiredItems(actionCounts, nameof(actionCounts));
        StatusCounts = OperationReportContractGuard.SnapshotRequiredItems(statusCounts, nameof(statusCounts));
    }

    /// <summary> Gets the host used for the operation. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the selected category literals. </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary> Gets the exact SKILL name selection. Empty means no name filter. </summary>
    public IReadOnlyList<string> SkillNames { get; }

    /// <summary> Gets the install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute repository root for project scope, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets whether the report describes a dry-run plan. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether force semantics were enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets host-specific reload guidance. </summary>
    public string ReloadGuidance { get; }

    /// <summary> Gets per-SKILL action reports in ordinal name order. </summary>
    public IReadOnlyList<SkillOperationActionReport> Actions { get; }

    /// <summary> Gets counts for every action literal supported by the operation. </summary>
    public IReadOnlyList<SkillOperationCountReport> ActionCounts { get; }

    /// <summary> Gets counts for every coarse status literal. </summary>
    public IReadOnlyList<SkillOperationCountReport> StatusCounts { get; }
}
