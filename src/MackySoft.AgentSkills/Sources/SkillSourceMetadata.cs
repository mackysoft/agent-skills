using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Sources;

/// <summary> Represents host-independent metadata from source <c>skill.json</c>. </summary>
/// <param name="SchemaVersion"> The source schema version. </param>
/// <param name="SkillBundleVersion"> The product-owned SKILL bundle version stamped into generated packages. </param>
/// <param name="CatalogId"> The stable SKILL catalog ID. </param>
/// <param name="Tier"> The product-owned SKILL tier. </param>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name. </param>
/// <param name="Description"> The skill description. </param>
/// <param name="Dependencies"> The dependent skill names. </param>
/// <param name="References"> The reference file names. </param>
public sealed record SkillSourceMetadata (
    int SchemaVersion,
    int SkillBundleVersion,
    SkillCatalogId CatalogId,
    SkillTier Tier,
    SkillName SkillName,
    string DisplayName,
    string Description,
    IReadOnlyList<SkillName> Dependencies,
    IReadOnlyList<string> References)
{
    /// <summary> Gets the current source metadata schema version. </summary>
    public const int CurrentSchemaVersion = 1;
}
