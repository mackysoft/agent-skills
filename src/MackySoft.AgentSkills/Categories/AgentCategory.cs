using System.Diagnostics.CodeAnalysis;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Categories;

/// <summary> Represents a product-owned custom-agent category. </summary>
public sealed record AgentCategory
{
    /// <summary> Initializes an agent category. </summary>
    public AgentCategory (string value)
    {
        if (!SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            throw new ArgumentException($"Agent category literal is invalid: {value}", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the canonical lowercase category. </summary>
    public string Value { get; }

    /// <summary> Tries to create an agent category without throwing. </summary>
    public static bool TryCreate (string? value, [NotNullWhen(true)] out AgentCategory? category)
    {
        if (SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            category = new AgentCategory(value!);
            return true;
        }

        category = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString () => Value;
}
