namespace MackySoft.AgentSkills.Agents.Sources;

/// <summary> Represents one validated host binding authored for an agent definition. </summary>
internal sealed class AgentHostBindingSource
{
    /// <summary> Initializes one host binding source. </summary>
    public AgentHostBindingSource (AgentHostKind hostId, string json)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        HostId = hostId;
        Json = json;
    }

    /// <summary> Gets the host identifier derived from the file name. </summary>
    public AgentHostKind HostId { get; }

    /// <summary> Gets canonical binding JSON text. </summary>
    public string Json { get; }
}
