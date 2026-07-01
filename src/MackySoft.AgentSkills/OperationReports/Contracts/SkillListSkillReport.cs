namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for one canonical skill package. </summary>
/// <param name="SchemaVersion"> The manifest schema version. </param>
/// <param name="SkillBundleVersion"> The product-owned SKILL bundle version. </param>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name shown by SKILL listing commands. </param>
/// <param name="Description"> The host-independent SKILL description. </param>
/// <param name="Dependencies"> The dependent skill name literals. </param>
/// <param name="Tier"> The product-owned SKILL tier literal. </param>
/// <param name="CatalogId"> The stable SKILL catalog ID literal. </param>
/// <param name="ContentDigest"> The host-independent content digest. </param>
/// <param name="ManifestDigest"> The canonical manifest digest excluding this field. </param>
/// <param name="HostArtifacts"> The host-specific artifact reports sorted by host key using ordinal comparison. </param>
public sealed record SkillListSkillReport (
    int SchemaVersion,
    int SkillBundleVersion,
    string SkillName,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Dependencies,
    string Tier,
    string CatalogId,
    string ContentDigest,
    string ManifestDigest,
    IReadOnlyList<SkillHostArtifactReport> HostArtifacts)
{
    /// <summary> Initializes a report using the pre-skillBundleVersion constructor shape. </summary>
    public SkillListSkillReport (
        int SchemaVersion,
        string SkillName,
        string DisplayName,
        string Description,
        string Tier,
        string CatalogId,
        string ContentDigest,
        string ManifestDigest,
        IReadOnlyList<SkillHostArtifactReport> HostArtifacts)
        : this(
            SchemaVersion,
            0,
            SkillName,
            DisplayName,
            Description,
            [],
            Tier,
            CatalogId,
            ContentDigest,
            ManifestDigest,
            HostArtifacts)
    {
    }

    /// <summary> Deconstructs a report using the pre-skillBundleVersion tuple shape. </summary>
    public void Deconstruct (
        out int SchemaVersion,
        out string SkillName,
        out string DisplayName,
        out string Description,
        out string Tier,
        out string CatalogId,
        out string ContentDigest,
        out string ManifestDigest,
        out IReadOnlyList<SkillHostArtifactReport> HostArtifacts)
    {
        SchemaVersion = this.SchemaVersion;
        SkillName = this.SkillName;
        DisplayName = this.DisplayName;
        Description = this.Description;
        Tier = this.Tier;
        CatalogId = this.CatalogId;
        ContentDigest = this.ContentDigest;
        ManifestDigest = this.ManifestDigest;
        HostArtifacts = this.HostArtifacts;
    }
}
