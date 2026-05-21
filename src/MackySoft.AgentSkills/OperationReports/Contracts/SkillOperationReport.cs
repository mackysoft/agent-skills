namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a product-neutral install, update, or uninstall report. </summary>
/// <param name="Host"> The canonical host key used for the operation. </param>
/// <param name="Scope"> The stable install scope literal used for the operation. </param>
/// <param name="TargetRoot"> The canonical absolute host target root. </param>
/// <param name="DryRun"> Whether the report represents a plan without writes. </param>
/// <param name="Force"> Whether the operation used force semantics. </param>
/// <param name="ReloadGuidance"> The host-specific guidance for reloading installed or updated SKILLs. </param>
/// <param name="Actions"> The per-skill action reports sorted by skill name using ordinal comparison. </param>
/// <param name="ActionCounts"> Counts for every action literal supported by the source operation kind, in stable literal order. </param>
/// <param name="StatusCounts"> Counts for every coarse status literal, in stable literal order. </param>
public sealed record SkillOperationReport (
    string Host,
    string Scope,
    string TargetRoot,
    bool DryRun,
    bool Force,
    string ReloadGuidance,
    IReadOnlyList<SkillOperationActionReport> Actions,
    IReadOnlyList<SkillOperationCountReport> ActionCounts,
    IReadOnlyList<SkillOperationCountReport> StatusCounts);
