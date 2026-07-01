using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Names;

/// <summary> Represents a validated canonical SKILL name. </summary>
public readonly struct SkillName : IEquatable<SkillName>
{
    private readonly string? literal;

    /// <summary> Initializes a new instance of the <see cref="SkillName" /> type. </summary>
    /// <param name="value"> The canonical lowercase SKILL name. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a safe SKILL name. </exception>
    public SkillName (string value)
    {
        if (!SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            throw new ArgumentException($"SKILL name literal is invalid: {value}", nameof(value));
        }

        literal = value;
    }

    /// <summary> Gets the canonical lowercase SKILL name. </summary>
    /// <exception cref="InvalidOperationException"> Thrown when this value was not initialized by the validating constructor. </exception>
    public string Value => literal ?? throw new InvalidOperationException("Uninitialized SKILL name is invalid.");

    /// <summary> Gets whether this value was initialized by the validating constructor. </summary>
    public bool IsInitialized => literal is not null;

    /// <summary> Tries to create a SKILL name without throwing validation exceptions. </summary>
    /// <param name="value"> The candidate literal. </param>
    /// <param name="skillName"> The created SKILL name when validation succeeds; otherwise the default value. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is safe; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out SkillName skillName)
    {
        if (SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            skillName = new SkillName(value!);
            return true;
        }

        skillName = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals (SkillName other)
    {
        return string.Equals(literal, other.literal, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals (object? obj)
    {
        return obj is SkillName other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode ()
    {
        return literal is null ? 0 : StringComparer.Ordinal.GetHashCode(literal);
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }

    /// <summary> Checks whether two SKILL names are equal. </summary>
    public static bool operator == (
        SkillName left,
        SkillName right)
    {
        return left.Equals(right);
    }

    /// <summary> Checks whether two SKILL names are different. </summary>
    public static bool operator != (
        SkillName left,
        SkillName right)
    {
        return !left.Equals(right);
    }
}
