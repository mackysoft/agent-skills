namespace MackySoft.AgentSkills.Sources;

/// <summary> Represents one complete source SKILL definition. </summary>
internal sealed class SkillSourceDefinition
{
    /// <summary> Initializes one immutable source definition. </summary>
    /// <param name="metadata"> The source metadata. </param>
    /// <param name="skillTemplate"> The source <c>SKILL.md.template</c> content. </param>
    /// <param name="references"> The source reference templates. </param>
    internal SkillSourceDefinition (
        SkillSourceMetadata metadata,
        string skillTemplate,
        IReadOnlyList<SkillSourceReference> references)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(skillTemplate);
        ArgumentNullException.ThrowIfNull(references);
        var referenceSnapshot = references.ToArray();

        var trimmedSkillTemplate = skillTemplate.TrimStart();
        if (trimmedSkillTemplate.StartsWith("---", StringComparison.Ordinal))
        {
            throw new ArgumentException("SKILL template must not contain frontmatter.", nameof(skillTemplate));
        }

        if (trimmedSkillTemplate.StartsWith("# ", StringComparison.Ordinal))
        {
            throw new ArgumentException("SKILL template must not contain a top-level heading.", nameof(skillTemplate));
        }

        if (referenceSnapshot.Any(static reference => reference is null))
        {
            throw new ArgumentException("Reference templates must not contain null items.", nameof(references));
        }

        var referenceNames = referenceSnapshot.Select(static reference => reference.FileName).ToHashSet(StringComparer.Ordinal);
        if (referenceNames.Count != referenceSnapshot.Length
            || !referenceNames.SetEquals(metadata.References))
        {
            throw new ArgumentException("Reference templates must match the reference file names in the source metadata.", nameof(references));
        }

        Metadata = metadata;
        SkillTemplate = skillTemplate;
        References = Array.AsReadOnly(referenceSnapshot);
    }

    /// <summary> Gets the source metadata. </summary>
    internal SkillSourceMetadata Metadata { get; }

    /// <summary> Gets the source <c>SKILL.md.template</c> content. </summary>
    internal string SkillTemplate { get; }

    /// <summary> Gets an immutable snapshot of source reference templates. </summary>
    internal IReadOnlyList<SkillSourceReference> References { get; }
}
