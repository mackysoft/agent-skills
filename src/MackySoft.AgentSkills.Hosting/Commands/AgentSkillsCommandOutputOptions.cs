namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents output formatting options for the default Agent Skills command emitter. </summary>
public sealed class AgentSkillsCommandOutputOptions
{
    /// <summary> Initializes output formatting options. </summary>
    /// <param name="pretty"> Whether JSON output should be indented. </param>
    public AgentSkillsCommandOutputOptions (bool pretty = false)
    {
        Pretty = pretty;
    }

    /// <summary> Gets whether JSON output should be indented. </summary>
    public bool Pretty { get; }
}
