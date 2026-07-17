using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Sources;

/// <summary> Represents host-independent metadata assembled from directory names, <c>skill.json</c>, and reference templates. </summary>
internal sealed class SkillSourceMetadata
{
    /// <summary> Gets the current source metadata schema version. </summary>
    internal const int CurrentSchemaVersion = 1;

    /// <summary> Initializes one immutable source metadata snapshot. </summary>
    /// <param name="schemaVersion"> The source schema version. </param>
    /// <param name="category"> The category derived from the source definition parent directory. </param>
    /// <param name="skillName"> The skill name derived from the source definition directory. </param>
    /// <param name="displayName"> The display name. </param>
    /// <param name="description"> The skill description. </param>
    /// <param name="dependencies"> The dependent skill names. </param>
    /// <param name="references"> The reference file names derived from source templates. </param>
    internal SkillSourceMetadata (
        int schemaVersion,
        SkillCategory category,
        SkillName skillName,
        string displayName,
        string description,
        IReadOnlyList<SkillName> dependencies,
        IReadOnlyList<string> references)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Source metadata schema version must be {CurrentSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(skillName);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > 1024)
        {
            throw new ArgumentException("Description must contain between 1 and 1024 characters.", nameof(description));
        }

        ArgumentNullException.ThrowIfNull(category);
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

        ArgumentNullException.ThrowIfNull(references);
        var referenceSnapshot = references.ToArray();
        var referenceSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in referenceSnapshot)
        {
            if (!SkillSourceReference.IsValidFileName(reference))
            {
                throw new ArgumentException("References must contain safe Markdown file names.", nameof(references));
            }

            if (!referenceSet.Add(reference))
            {
                throw new ArgumentException("References must not contain duplicates.", nameof(references));
            }
        }

        SchemaVersion = schemaVersion;
        Category = category;
        SkillName = skillName;
        DisplayName = displayName;
        Description = description;
        Dependencies = Array.AsReadOnly(dependencySnapshot);
        References = Array.AsReadOnly(referenceSnapshot);
    }

    /// <summary> Gets the source schema version. </summary>
    internal int SchemaVersion { get; }

    /// <summary> Gets the category derived from the source definition parent directory. </summary>
    internal SkillCategory Category { get; }

    /// <summary> Gets the skill name derived from the source definition directory. </summary>
    internal SkillName SkillName { get; }

    /// <summary> Gets the display name. </summary>
    internal string DisplayName { get; }

    /// <summary> Gets the skill description. </summary>
    internal string Description { get; }

    /// <summary> Gets an immutable snapshot of dependent skill names. </summary>
    internal IReadOnlyList<SkillName> Dependencies { get; }

    /// <summary> Gets an immutable snapshot of reference file names derived from source templates. </summary>
    internal IReadOnlyList<string> References { get; }
}
