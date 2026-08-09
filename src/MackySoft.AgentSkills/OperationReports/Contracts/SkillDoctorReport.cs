using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.OperationReports.Literals;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral doctor result data. </summary>
public sealed class SkillDoctorReport
{
    internal SkillDoctorReport (
        HostKind host,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> skillNames,
        OperationScopeKind scope,
        string? repositoryRoot,
        string targetRoot,
        string reloadGuidance,
        IReadOnlyList<SkillDoctorDiagnosticReport> diagnostics)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!Vocabulary.IsDefined(scope))
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
        ReloadGuidance = reloadGuidance;
        Diagnostics = OperationReportContractGuard.SnapshotRequiredItems(diagnostics, nameof(diagnostics));
    }

    /// <summary> Gets the host diagnosed by the doctor workflow. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the selected category literals. </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary> Gets the exact SKILL name selection. Empty means no name filter. </summary>
    public IReadOnlyList<string> SkillNames { get; }

    /// <summary> Gets the install scope. </summary>
    public OperationScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute repository root for project scope, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance { get; }

    /// <summary> Gets whether no error diagnostics were reported. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => diagnostic.Severity != SkillDoctorSeverity.Error);

    /// <summary> Gets the diagnostic reports in deterministic order. </summary>
    public IReadOnlyList<SkillDoctorDiagnosticReport> Diagnostics { get; }
}
