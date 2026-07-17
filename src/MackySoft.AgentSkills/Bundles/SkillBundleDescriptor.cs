using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Represents the runtime descriptor for one generated canonical SKILL bundle. </summary>
public sealed class SkillBundleDescriptor
{
    /// <summary> Initializes one generated bundle descriptor. </summary>
    /// <param name="schemaVersion"> The bundle descriptor schema version. </param>
    /// <param name="catalogId"> The catalog ID shared by every package in the bundle. </param>
    /// <param name="skillBundleVersion"> The release version shared by every package in the bundle. </param>
    /// <param name="bundleDigest"> The SHA-256 digest of the version-independent canonical package set. </param>
    public SkillBundleDescriptor (
        int schemaVersion,
        SkillCatalogId catalogId,
        int skillBundleVersion,
        Sha256Digest bundleDigest)
    {
        if (schemaVersion != SkillBundleDefinition.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Bundle schema version must be {SkillBundleDefinition.CurrentSchemaVersion}.");
        }

        if (skillBundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skillBundleVersion), skillBundleVersion, "SKILL bundle version must be positive.");
        }

        SchemaVersion = schemaVersion;
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        SkillBundleVersion = skillBundleVersion;
        BundleDigest = bundleDigest ?? throw new ArgumentNullException(nameof(bundleDigest));
    }

    /// <summary> Gets the bundle descriptor schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the catalog ID shared by every package in the bundle. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the release version shared by every package in the bundle. </summary>
    public int SkillBundleVersion { get; }

    /// <summary> Gets the digest of the version-independent canonical package set. </summary>
    public Sha256Digest BundleDigest { get; }
}
