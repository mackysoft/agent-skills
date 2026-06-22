using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Provides operation context that is owned by the caller rather than stored on every result model. </summary>
/// <param name="HostDescriptor"> The descriptor for the host used for the operation. </param>
/// <param name="Scope"> The install scope used for the operation. </param>
/// <param name="SelectedTiers"> The selected product-owned SKILL tiers. </param>
public sealed record SkillOperationReportContext (
    SkillHostDescriptor HostDescriptor,
    SkillScopeKind Scope,
    IReadOnlyList<SkillTier> SelectedTiers)
{
    /// <summary> Gets selected product-owned SKILL tiers. </summary>
    public IReadOnlyList<SkillTier> SelectedTiers { get; init; } = SelectedTiers ?? throw new ArgumentNullException(nameof(SelectedTiers));
}
