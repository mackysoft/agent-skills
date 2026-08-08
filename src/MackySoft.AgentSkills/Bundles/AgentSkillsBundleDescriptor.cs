using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Represents a generated v2 mixed bundle descriptor. </summary>
public sealed class AgentSkillsBundleDescriptor
{
    /// <summary> Initializes a generated descriptor. </summary>
    public AgentSkillsBundleDescriptor (int schemaVersion, SkillCatalogId catalogId, AgentSkillsBundleVersion bundleVersion, Sha256Digest bundleDigest)
    {
        if (schemaVersion != AgentSkillsBundleDefinition.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Bundle schema version must be {AgentSkillsBundleDefinition.CurrentSchemaVersion}.");
        }

        SchemaVersion = schemaVersion;
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        BundleVersion = bundleVersion ?? throw new ArgumentNullException(nameof(bundleVersion));
        BundleDigest = bundleDigest ?? throw new ArgumentNullException(nameof(bundleDigest));
    }

    /// <summary> Gets the generated schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the stable catalog identity. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the release version. </summary>
    public AgentSkillsBundleVersion BundleVersion { get; }

    /// <summary> Gets the version-independent complete file-set digest. </summary>
    public Sha256Digest BundleDigest { get; }
}
