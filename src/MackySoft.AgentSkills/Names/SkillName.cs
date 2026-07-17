using System.Diagnostics.CodeAnalysis;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Names;

/// <summary> Represents a validated canonical SKILL name. </summary>
public sealed record SkillName
{
    /// <summary> Initializes a new instance of the <see cref="SkillName" /> type. </summary>
    /// <param name="value"> The canonical lowercase SKILL name. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a safe SKILL name. </exception>
    public SkillName (string value)
    {
        if (!SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            throw new ArgumentException($"SKILL name literal is invalid: {value}", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the canonical lowercase SKILL name. </summary>
    public string Value { get; }

    /// <summary> Tries to create a SKILL name without throwing validation exceptions. </summary>
    /// <param name="value"> The candidate literal. </param>
    /// <param name="skillName"> The created SKILL name when validation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is safe; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        [NotNullWhen(true)] out SkillName? skillName)
    {
        if (SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            skillName = new SkillName(value!);
            return true;
        }

        skillName = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }
}
