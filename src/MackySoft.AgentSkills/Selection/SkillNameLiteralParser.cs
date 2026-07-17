using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Selection;

/// <summary> Parses exact SKILL name selections. </summary>
public static class SkillNameLiteralParser
{
    /// <summary> Parses selected SKILL name literals. </summary>
    /// <param name="selectedSkillNames"> The exact SKILL name literals selected by the caller. </param>
    /// <returns> An immutable snapshot of the normalized selected SKILL names, or an input failure. Duplicate values are removed after their first occurrence. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="selectedSkillNames" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<IReadOnlyList<SkillName>> ParseSelectedSkillNames (IReadOnlyList<string> selectedSkillNames)
    {
        ArgumentNullException.ThrowIfNull(selectedSkillNames);

        if (selectedSkillNames.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<SkillName>>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "At least one SKILL name must be selected.");
        }

        return ParseOptionalSkillNames(selectedSkillNames);
    }

    internal static SkillOperationResult<IReadOnlyList<SkillName>> ParseOptionalSkillNames (IReadOnlyList<string> selectedSkillNames)
    {
        ArgumentNullException.ThrowIfNull(selectedSkillNames);

        var normalizedSkillNames = new List<SkillName>(selectedSkillNames.Count);
        var selectedSkillNameSet = new HashSet<SkillName>();
        foreach (var skillNameLiteral in selectedSkillNames)
        {
            if (!SkillName.TryCreate(skillNameLiteral, out var skillName))
            {
                return SkillOperationResult<IReadOnlyList<SkillName>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"SKILL name literal is invalid: {skillNameLiteral ?? "<null>"}.");
            }

            if (selectedSkillNameSet.Add(skillName))
            {
                normalizedSkillNames.Add(skillName);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillName>>.Success(
            Array.AsReadOnly(normalizedSkillNames.ToArray()));
    }
}
