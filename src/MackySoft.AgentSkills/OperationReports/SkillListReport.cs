using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Manifests;

namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Represents product-neutral list data for generated skills and supported hosts. </summary>
/// <param name="Skills"> The canonical skill manifests sorted by skill name using ordinal comparison. </param>
/// <param name="SupportedHosts"> The supported host descriptors sorted by host key using ordinal comparison. </param>
public sealed record SkillListReport (
    IReadOnlyList<SkillManifest> Skills,
    IReadOnlyList<SkillHostDescriptor> SupportedHosts);
