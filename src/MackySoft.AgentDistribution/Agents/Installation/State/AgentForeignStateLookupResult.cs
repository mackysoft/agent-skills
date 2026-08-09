namespace MackySoft.AgentDistribution.Agents.Installation.State;

/// <summary> Represents whether a foreign ownership state was found during inspection. </summary>
internal sealed class AgentForeignStateLookupResult
{
    public AgentForeignStateLookupResult (AgentInstalledTargetState? state)
    {
        State = state;
    }

    public AgentInstalledTargetState? State { get; }
}
