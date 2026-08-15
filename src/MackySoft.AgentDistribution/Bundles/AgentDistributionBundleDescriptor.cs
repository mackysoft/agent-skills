using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Represents a generated v3 mixed bundle descriptor. </summary>
public sealed class AgentDistributionBundleDescriptor
{
    /// <summary> Gets the generated descriptor schema version. </summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary> Initializes a generated descriptor. </summary>
    public AgentDistributionBundleDescriptor (int schemaVersion, AgentDistributionCatalogId catalogId, AgentDistributionBundleVersion bundleVersion, Sha256Digest bundleDigest)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Bundle schema version must be {CurrentSchemaVersion}.");
        }

        SchemaVersion = schemaVersion;
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        BundleVersion = bundleVersion ?? throw new ArgumentNullException(nameof(bundleVersion));
        BundleDigest = bundleDigest ?? throw new ArgumentNullException(nameof(bundleDigest));
    }

    /// <summary> Gets the generated schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the stable catalog identity. </summary>
    public AgentDistributionCatalogId CatalogId { get; }

    /// <summary> Gets the release version. </summary>
    public AgentDistributionBundleVersion BundleVersion { get; }

    /// <summary> Gets the version-independent complete file-set digest. </summary>
    public Sha256Digest BundleDigest { get; }
}
