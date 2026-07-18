using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Represents a positive SKILL bundle version. </summary>
public sealed record SkillBundleVersion : IComparable<SkillBundleVersion>
{
    /// <summary> Initializes a SKILL bundle version. </summary>
    /// <param name="value"> The positive numeric version. </param>
    public SkillBundleVersion (int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "SKILL bundle version must be positive.");
        }

        Value = value;
    }

    /// <summary> Gets the numeric version. </summary>
    public int Value { get; }

    /// <summary> Tries to create a SKILL bundle version. </summary>
    /// <param name="value"> The candidate numeric version. </param>
    /// <param name="version"> The created version when <paramref name="value" /> is positive; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value is a valid version; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        int value,
        [NotNullWhen(true)] out SkillBundleVersion? version)
    {
        if (value > 0)
        {
            version = new SkillBundleVersion(value);
            return true;
        }

        version = null;
        return false;
    }

    /// <summary> Creates the immediately following version. </summary>
    /// <returns> The next version. </returns>
    /// <exception cref="OverflowException"> Thrown when the current version is already <see cref="int.MaxValue" />. </exception>
    public SkillBundleVersion Next ()
    {
        return new SkillBundleVersion(checked(Value + 1));
    }

    /// <inheritdoc />
    public int CompareTo (SkillBundleVersion? other)
    {
        return other is null ? 1 : Value.CompareTo(other.Value);
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
