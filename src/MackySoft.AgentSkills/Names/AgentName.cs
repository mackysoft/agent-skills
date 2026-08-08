using System.Diagnostics.CodeAnalysis;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Names;

/// <summary> Represents a validated canonical custom-agent name. </summary>
public sealed record AgentName
{
    /// <summary> Initializes a custom-agent name. </summary>
    /// <param name="value"> The canonical lowercase name. </param>
    public AgentName (string value)
    {
        if (!SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            throw new ArgumentException($"Agent name literal is invalid: {value}", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the canonical lowercase name. </summary>
    public string Value { get; }

    /// <summary> Tries to create an agent name without throwing. </summary>
    public static bool TryCreate (string? value, [NotNullWhen(true)] out AgentName? agentName)
    {
        if (SkillIdentifierValidator.IsSafeLowercaseHyphenLiteral(value))
        {
            agentName = new AgentName(value!);
            return true;
        }

        agentName = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString () => Value;
}
