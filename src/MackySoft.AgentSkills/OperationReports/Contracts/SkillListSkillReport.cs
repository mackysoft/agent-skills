namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for one canonical skill package. </summary>
/// <param name="SchemaVersion"> The manifest schema version. </param>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name shown by SKILL listing commands. </param>
/// <param name="Description"> The host-independent SKILL description. </param>
/// <param name="Tier"> The product-owned SKILL tier literal. </param>
/// <param name="CatalogId"> The stable SKILL catalog ID literal. </param>
/// <param name="ContentDigest"> The host-independent content digest. </param>
/// <param name="ManifestDigest"> The canonical manifest digest excluding this field. </param>
/// <param name="HostArtifacts"> The host-specific artifact reports sorted by host key using ordinal comparison. </param>
public sealed record SkillListSkillReport (
    int SchemaVersion,
    string SkillName,
    string DisplayName,
    string Description,
    string Tier,
    string CatalogId,
    string ContentDigest,
    string ManifestDigest,
    IReadOnlyList<SkillHostArtifactReport> HostArtifacts);
