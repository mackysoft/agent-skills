using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tiers;

/// <summary> Represents a stable product-owned SKILL tier literal. </summary>
public sealed record SkillTier
{
    /// <summary> Initializes a new instance of the <see cref="SkillTier" /> class. </summary>
    /// <param name="value"> The stable lowercase tier literal. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a safe tier literal. </exception>
    public SkillTier (string value)
    {
        if (!IsSafeLiteral(value))
        {
            throw new ArgumentException($"SKILL tier literal is invalid: {value}", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the stable lowercase tier literal. </summary>
    public string Value { get; }

    /// <summary> Tries to create a tier literal without throwing validation exceptions. </summary>
    /// <param name="value"> The candidate literal. </param>
    /// <param name="tier"> The created tier when validation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is safe; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out SkillTier? tier)
    {
        if (value is not null && IsSafeLiteral(value))
        {
            tier = new SkillTier(value);
            return true;
        }

        tier = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }

    private static bool IsSafeLiteral (string? value)
    {
        return SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value);
    }
}
