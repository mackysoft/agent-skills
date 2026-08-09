using MackySoft.AgentDistribution.Catalogs;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Represents an authored v2 bundle containing skills, agents, or both. </summary>
public sealed class AgentDistributionBundleDefinition
{
    /// <summary> Gets the v2 source schema version. </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary> Initializes a v2 source bundle definition. </summary>
    public AgentDistributionBundleDefinition (int schemaVersion, SkillCatalogId catalogId, AgentDistributionBundleVersion bundleVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Bundle schema version must be {CurrentSchemaVersion}.");
        }

        SchemaVersion = schemaVersion;
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        BundleVersion = bundleVersion ?? throw new ArgumentNullException(nameof(bundleVersion));
    }

    /// <summary> Gets the source schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the stable catalog identity. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the mixed-bundle release version. </summary>
    public AgentDistributionBundleVersion BundleVersion { get; }
}
