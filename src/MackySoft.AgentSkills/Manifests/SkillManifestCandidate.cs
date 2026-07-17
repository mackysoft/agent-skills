using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Holds a field-valid manifest candidate before relational and digest validation. </summary>
internal sealed class SkillManifestCandidate
{
    internal SkillManifestCandidate (
        int schemaVersion,
        int skillBundleVersion,
        SkillCatalogId catalogId,
        SkillCategory category,
        SkillName skillName,
        string displayName,
        string description,
        IReadOnlyList<SkillName> dependencies,
        Sha256Digest contentDigest,
        Sha256Digest? manifestDigest,
        IReadOnlyList<SkillHostArtifactManifest> hostArtifacts)
    {
        if (schemaVersion != SkillManifest.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Manifest schema version must be {SkillManifest.CurrentSchemaVersion}.");
        }

        if (skillBundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skillBundleVersion), skillBundleVersion, "SKILL bundle version must be positive.");
        }

        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        SkillName = skillName ?? throw new ArgumentNullException(nameof(skillName));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > 1024)
        {
            throw new ArgumentException("Description must contain between 1 and 1024 characters.", nameof(description));
        }

        ArgumentNullException.ThrowIfNull(dependencies);
        var dependencySnapshot = dependencies.ToArray();
        var dependencySet = new HashSet<SkillName>();
        foreach (var dependency in dependencySnapshot)
        {
            if (dependency is null)
            {
                throw new ArgumentException("Dependencies must not contain null SKILL names.", nameof(dependencies));
            }

            if (dependency == skillName)
            {
                throw new ArgumentException("A SKILL must not depend on itself.", nameof(dependencies));
            }

            if (!dependencySet.Add(dependency))
            {
                throw new ArgumentException("Dependencies must not contain duplicates.", nameof(dependencies));
            }
        }

        ArgumentNullException.ThrowIfNull(hostArtifacts);
        var hostArtifactSnapshot = hostArtifacts.ToArray();
        if (hostArtifactSnapshot.Any(static artifact => artifact is null))
        {
            throw new ArgumentException("Host artifacts must not contain null items.", nameof(hostArtifacts));
        }

        if (hostArtifactSnapshot.Select(static artifact => artifact.Host).Distinct().Count() != hostArtifactSnapshot.Length)
        {
            throw new ArgumentException("Host artifacts must not contain duplicate hosts.", nameof(hostArtifacts));
        }

        SchemaVersion = schemaVersion;
        SkillBundleVersion = skillBundleVersion;
        DisplayName = displayName;
        Description = description;
        Dependencies = Array.AsReadOnly(dependencySnapshot);
        ContentDigest = contentDigest ?? throw new ArgumentNullException(nameof(contentDigest));
        ManifestDigest = manifestDigest;
        HostArtifacts = Array.AsReadOnly(hostArtifactSnapshot);
    }

    internal int SchemaVersion { get; }

    internal int SkillBundleVersion { get; }

    internal SkillCatalogId CatalogId { get; }

    internal SkillCategory Category { get; }

    internal SkillName SkillName { get; }

    internal string DisplayName { get; }

    internal string Description { get; }

    internal IReadOnlyList<SkillName> Dependencies { get; }

    internal Sha256Digest ContentDigest { get; }

    /// <summary> Gets the digest declared by parsed input, or <see langword="null" /> for generated candidates. </summary>
    internal Sha256Digest? ManifestDigest { get; }

    internal IReadOnlyList<SkillHostArtifactManifest> HostArtifacts { get; }
}
