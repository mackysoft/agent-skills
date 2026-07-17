namespace MackySoft.AgentSkills.Hosting.Commands;

internal static class AgentSkillsCommandRequestOptionSnapshot
{
    public static IReadOnlyList<string>? Create (
        IReadOnlyList<string>? values,
        string parameterName)
    {
        if (values is null)
        {
            return null;
        }

        var snapshot = values.ToArray();
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("Command option values must not contain null items.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}
