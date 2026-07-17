using MackySoft.AgentSkills.Catalogs;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Represents the authored bundle identity and release version from source <c>bundle.json</c>. </summary>
public sealed class SkillBundleDefinition
{
    /// <summary> Gets the only source bundle schema version supported by this library version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Initializes one authored bundle definition. </summary>
    /// <param name="schemaVersion"> The source bundle schema version. </param>
    /// <param name="catalogId"> The stable catalog ID stamped into every generated package. </param>
    /// <param name="skillBundleVersion"> The positive release version stamped into every generated package. </param>
    public SkillBundleDefinition (
        int schemaVersion,
        SkillCatalogId catalogId,
        int skillBundleVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Bundle schema version must be {CurrentSchemaVersion}.");
        }

        if (skillBundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skillBundleVersion), skillBundleVersion, "SKILL bundle version must be positive.");
        }

        SchemaVersion = schemaVersion;
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        SkillBundleVersion = skillBundleVersion;
    }

    /// <summary> Gets the source bundle schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the stable catalog ID stamped into every generated package. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the release version stamped into every generated package. </summary>
    public int SkillBundleVersion { get; }
}
