using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Represents the canonical <c>agent-skill.json</c> manifest. </summary>
/// <param name="SchemaVersion"> The manifest schema version. </param>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name shown by SKILL listing commands. </param>
/// <param name="Description"> The host-independent SKILL description. </param>
/// <param name="Dependencies"> The dependent skill names. </param>
/// <param name="Tier"> The product-owned SKILL tier. </param>
/// <param name="CatalogId"> The stable SKILL catalog ID. </param>
/// <param name="ContentDigest"> The host-independent content digest. </param>
/// <param name="ManifestDigest"> The canonical manifest digest excluding this field. </param>
/// <param name="HostArtifacts"> The host-specific artifact digests. </param>
public sealed record SkillManifest (
    int SchemaVersion,
    SkillName SkillName,
    string DisplayName,
    string Description,
    IReadOnlyList<SkillName> Dependencies,
    SkillTier Tier,
    SkillCatalogId CatalogId,
    string ContentDigest,
    string ManifestDigest,
    IReadOnlyList<SkillHostArtifactManifest> HostArtifacts)
{
    /// <summary> Gets the current manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;
}
