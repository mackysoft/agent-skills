using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral doctor result data. </summary>
public sealed class SkillDoctorReport
{
    internal SkillDoctorReport (
        SkillHostKind host,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> skillNames,
        SkillScopeKind scope,
        string targetRoot,
        IReadOnlyList<SkillDoctorDiagnosticReport> diagnostics)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!ContractLiteralCodec.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported install scope.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        Host = host;
        Categories = OperationReportContractGuard.SnapshotRequiredStrings(categories, nameof(categories));
        SkillNames = OperationReportContractGuard.SnapshotRequiredStrings(skillNames, nameof(skillNames));
        Scope = scope;
        TargetRoot = targetRoot;
        Diagnostics = OperationReportContractGuard.SnapshotRequiredItems(diagnostics, nameof(diagnostics));
    }

    /// <summary> Gets the host diagnosed by the doctor workflow. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the selected category literals. </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary> Gets the exact SKILL name selection. Empty means no name filter. </summary>
    public IReadOnlyList<string> SkillNames { get; }

    /// <summary> Gets the install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets whether no error diagnostics were reported. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => diagnostic.Severity != SkillDoctorSeverity.Error);

    /// <summary> Gets the diagnostic reports in deterministic order. </summary>
    public IReadOnlyList<SkillDoctorDiagnosticReport> Diagnostics { get; }
}
