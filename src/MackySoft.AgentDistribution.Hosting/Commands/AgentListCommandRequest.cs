namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary> Represents raw input for a custom-agent list command. </summary>
public sealed class AgentListCommandRequest
{
    /// <summary> Initializes one list request. </summary>
    public AgentListCommandRequest (IReadOnlyList<string>? category = null, IReadOnlyList<string>? agent = null)
    {
        Category = CommandOptionValues.Snapshot(category, nameof(category));
        Agent = CommandOptionValues.Snapshot(agent, nameof(agent));
    }

    /// <summary> Gets raw selected custom-agent categories. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets raw selected exact custom-agent names. </summary>
    public IReadOnlyList<string>? Agent { get; }
}
