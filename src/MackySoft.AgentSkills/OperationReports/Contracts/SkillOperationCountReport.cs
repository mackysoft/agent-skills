namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a deterministic count for one stable operation literal. </summary>
/// <param name="Literal"> The stable action or status literal being counted. </param>
/// <param name="Count"> The number of projected actions that used <paramref name="Literal" />. </param>
public sealed record SkillOperationCountReport (
    string Literal,
    int Count);
