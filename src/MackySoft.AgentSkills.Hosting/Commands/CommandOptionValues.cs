namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary>Owns the collection semantics of repeatable command options.</summary>
internal static class CommandOptionValues
{
    public static IReadOnlyList<string>? Snapshot (
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

    public static string[] Expand (IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .SelectMany(static value => value.Split(','))
            .Where(static value => value.Length != 0)
            .ToArray();
    }
}
