using MackySoft.AgentSkills.OperationReports.Literals;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents the installed custom-agent state observed while planning an action. </summary>
public sealed class AgentTargetStateReport
{
    internal AgentTargetStateReport (AgentOperationTargetState kind, string? detail)
    {
        if (!Vocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported agent target state.");
        }

        Kind = kind;
        Detail = detail;
    }

    /// <summary> Gets the observed target state. </summary>
    public AgentOperationTargetState Kind { get; }

    /// <summary> Gets optional state detail. </summary>
    public string? Detail { get; }
}
