using System.Diagnostics.CodeAnalysis;

namespace MackySoft.AgentDistribution.Agents.Manifests;

/// <summary> Represents the positive release version shared by a v3 mixed bundle. </summary>
public sealed record AgentDistributionBundleVersion : IComparable<AgentDistributionBundleVersion>
{
    /// <summary> Initializes a bundle version. </summary>
    public AgentDistributionBundleVersion (int value)
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
    public static bool TryCreate (int value, [NotNullWhen(true)] out AgentDistributionBundleVersion? version)
    {
        if (value >= 1)
        {
            version = new AgentDistributionBundleVersion(value);
            return true;
        }

        version = null;
        return false;
    }

    /// <summary> Gets the following version. </summary>
    public AgentDistributionBundleVersion Next () => new(checked(Value + 1));

    /// <inheritdoc />
    public int CompareTo (AgentDistributionBundleVersion? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString () => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
