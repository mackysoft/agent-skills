using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Selection;

/// <summary> Parses exact SKILL name selections. </summary>
public static class SkillNameLiteralParser
{
    /// <summary> Parses selected SKILL name literals. </summary>
    /// <param name="selectedSkillNames"> The exact SKILL name literals selected by the caller. </param>
    /// <returns> The normalized selected SKILL names, or an input failure. Duplicate values are removed after their first occurrence. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="selectedSkillNames" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<IReadOnlyList<string>> ParseSelectedSkillNames (IReadOnlyList<string> selectedSkillNames)
    {
        ArgumentNullException.ThrowIfNull(selectedSkillNames);

        if (selectedSkillNames.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<string>>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "At least one SKILL name must be selected.");
        }

        return ParseOptionalSkillNames(selectedSkillNames);
    }

    internal static SkillOperationResult<IReadOnlyList<string>> ParseOptionalSkillNames (IReadOnlyList<string> selectedSkillNames)
    {
        ArgumentNullException.ThrowIfNull(selectedSkillNames);

        var normalizedSkillNames = new List<string>(selectedSkillNames.Count);
        var selectedSkillNameSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skillName in selectedSkillNames)
        {
            if (!SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(skillName))
            {
                return SkillOperationResult<IReadOnlyList<string>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"SKILL name literal is invalid: {skillName ?? "<null>"}.");
            }

            if (selectedSkillNameSet.Add(skillName))
            {
                normalizedSkillNames.Add(skillName);
            }
        }

        return SkillOperationResult<IReadOnlyList<string>>.Success(normalizedSkillNames);
    }
}
