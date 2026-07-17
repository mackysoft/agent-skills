using System.Diagnostics.CodeAnalysis;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Categories;

/// <summary> Represents a stable product-owned SKILL category literal. </summary>
public sealed record SkillCategory
{
    /// <summary> Initializes a new instance of the <see cref="SkillCategory" /> class. </summary>
    /// <param name="value"> The stable lowercase category literal. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a safe category literal. </exception>
    public SkillCategory (string value)
    {
        if (!SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            throw new ArgumentException($"SKILL category literal is invalid: {value}", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the stable lowercase category literal. </summary>
    public string Value { get; }

    /// <summary> Tries to create a category literal without throwing validation exceptions. </summary>
    /// <param name="value"> The candidate literal. </param>
    /// <param name="category"> The created category when validation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is safe; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        [NotNullWhen(true)] out SkillCategory? category)
    {
        if (SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            category = new SkillCategory(value!);
            return true;
        }

        category = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }
}
