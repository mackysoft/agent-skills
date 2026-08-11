using MackySoft.AgentDistribution.Catalogs;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Represents an authored v3 bundle containing skills, agents, or both. </summary>
public sealed class AgentDistributionBundleDefinition
{
    /// <summary> Gets the v3 source schema version. </summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary> Initializes a v3 source bundle definition. </summary>
    public AgentDistributionBundleDefinition (int schemaVersion, AgentDistributionCatalogId catalogId, AgentDistributionBundleVersion bundleVersion)
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
    public AgentDistributionCatalogId CatalogId { get; }

    /// <summary> Gets the mixed-bundle release version. </summary>
    public AgentDistributionBundleVersion BundleVersion { get; }
}
