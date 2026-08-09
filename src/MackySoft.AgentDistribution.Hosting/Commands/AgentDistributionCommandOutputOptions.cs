namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary> Represents output formatting options for the default Agent Distribution command emitter. </summary>
public sealed class AgentDistributionCommandOutputOptions
{
    /// <summary> Initializes output formatting options. </summary>
    /// <param name="pretty"> Whether JSON output should be indented. </param>
    public AgentDistributionCommandOutputOptions (bool pretty = false)
    {
        Pretty = pretty;
    }

    /// <summary> Gets whether JSON output should be indented. </summary>
    public bool Pretty { get; }
}
