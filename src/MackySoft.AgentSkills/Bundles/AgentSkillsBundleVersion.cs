using System.Diagnostics.CodeAnalysis;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Represents the positive release version shared by a v2 mixed bundle. </summary>
public sealed record AgentSkillsBundleVersion : IComparable<AgentSkillsBundleVersion>
{
    /// <summary> Initializes a bundle version. </summary>
    public AgentSkillsBundleVersion (int value)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Bundle version must be positive.");
        }

        Value = value;
    }

    /// <summary> Gets the positive version value. </summary>
    public int Value { get; }

    /// <summary> Tries to create a version. </summary>
    public static bool TryCreate (int value, [NotNullWhen(true)] out AgentSkillsBundleVersion? version)
    {
        if (value >= 1)
        {
            version = new AgentSkillsBundleVersion(value);
            return true;
        }

        version = null;
        return false;
    }

    /// <summary> Gets the following version. </summary>
    public AgentSkillsBundleVersion Next () => new(checked(Value + 1));

    /// <inheritdoc />
    public int CompareTo (AgentSkillsBundleVersion? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString () => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
