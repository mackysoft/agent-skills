using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Catalogs;

/// <summary> Represents a stable SKILL catalog ID. </summary>
public sealed record SkillCatalogId
{
    /// <summary> Initializes a new instance of the <see cref="SkillCatalogId" /> class. </summary>
    /// <param name="value"> The stable catalog ID.</param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a safe catalog ID. </exception>
    public SkillCatalogId (string value)
    {
        if (!IsSafeLiteral(value))
        {
            throw new ArgumentException($"SKILL catalog ID is invalid: {value}", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the stable catalog ID. </summary>
    public string Value { get; }

    /// <summary> Tries to create a catalog ID without throwing validation exceptions. </summary>
    /// <param name="value"> The candidate literal. </param>
    /// <param name="catalogId"> The created catalog ID when validation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is safe; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out SkillCatalogId? catalogId)
    {
        if (value is not null && IsSafeLiteral(value))
        {
            catalogId = new SkillCatalogId(value);
            return true;
        }

        catalogId = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }

    private static bool IsSafeLiteral (string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255)
        {
            return false;
        }

        var segmentStart = true;
        var previous = '\0';
        foreach (var character in value)
        {
            if (character == '.')
            {
                if (segmentStart || previous is '.' or '-')
                {
                    return false;
                }

                segmentStart = true;
                previous = character;
                continue;
            }

            if (segmentStart)
            {
                if (!SkillIdentifierValidator.IsAsciiLowercaseLetterOrDigit(character))
                {
                    return false;
                }

                segmentStart = false;
                previous = character;
                continue;
            }

            if (character != '-' && !SkillIdentifierValidator.IsAsciiLowercaseLetterOrDigit(character))
            {
                return false;
            }

            previous = character;
        }

        return !segmentStart && previous != '-';
    }
}
