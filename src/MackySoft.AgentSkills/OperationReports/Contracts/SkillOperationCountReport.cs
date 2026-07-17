namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a deterministic count for one stable operation literal. </summary>
public sealed class SkillOperationCountReport
{
    internal SkillOperationCountReport (
        string literal,
        int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(literal);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        Literal = literal;
        Count = count;
    }

    /// <summary> Gets the stable action or status literal being counted. </summary>
    public string Literal { get; }

    /// <summary> Gets the number of projected actions that used <see cref="Literal" />. </summary>
    public int Count { get; }
}
