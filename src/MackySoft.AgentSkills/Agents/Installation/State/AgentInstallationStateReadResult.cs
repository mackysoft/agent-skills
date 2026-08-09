namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Represents an ownership-state file read result, including an absent file. </summary>
public sealed class AgentInstallationStateReadResult
{
    /// <summary> Initializes an absent state result. </summary>
    internal AgentInstallationStateReadResult ()
    {
    }

    /// <summary> Initializes a present state result. </summary>
    internal AgentInstallationStateReadResult (AgentInstallationState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary> Gets whether an ownership-state file was present. </summary>
    public bool IsPresent => State is not null;

    /// <summary> Gets the parsed state, or <see langword="null" /> when the file was absent. </summary>
    public AgentInstallationState? State { get; }
}
