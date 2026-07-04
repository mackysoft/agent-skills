using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Sources;

internal static class SkillSourceDependencyReferenceValidator
{
    public static SkillOperationResult<bool> Validate (IReadOnlyList<SkillSourceDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var knownSkillNames = definitions
            .Select(static definition => definition.Metadata.SkillName)
            .ToHashSet();

        foreach (var definition in definitions.OrderBy(static definition => definition.Metadata.SkillName.Value, StringComparer.Ordinal))
        {
            var sourceTexts = new[] { definition.SkillTemplate }
                .Concat(definition.References.Select(static reference => reference.Template));
            var referencedSkillNames = SkillDependencyReferenceScanner.FindReferences(sourceTexts)
                .Where(knownSkillNames.Contains)
                .Where(skillName => !string.Equals(skillName.Value, definition.Metadata.SkillName.Value, StringComparison.Ordinal))
                .ToHashSet();
            var declaredSkillNames = definition.Metadata.Dependencies.ToHashSet();

            var missingReferences = declaredSkillNames
                .Except(referencedSkillNames)
                .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
                .Select(static skillName => skillName.Value)
                .ToArray();
            if (missingReferences.Length != 0)
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"skill.json dependencies are not referenced in source text for '{definition.Metadata.SkillName.Value}': {string.Join(", ", missingReferences)}.");
            }

            var missingDeclarations = referencedSkillNames
                .Except(declaredSkillNames)
                .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
                .Select(static skillName => skillName.Value)
                .ToArray();
            if (missingDeclarations.Length != 0)
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"Source text references undeclared skill dependencies for '{definition.Metadata.SkillName.Value}': {string.Join(", ", missingDeclarations)}.");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }
}
