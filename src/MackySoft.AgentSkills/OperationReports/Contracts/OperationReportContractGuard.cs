namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Validates and snapshots values stored by operation report contracts. </summary>
internal static class OperationReportContractGuard
{
    public static IReadOnlyList<string> SnapshotRequiredStrings (
        IReadOnlyList<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var snapshot = values.ToArray();
        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Report string collections must not contain null, empty, or whitespace values.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }

    public static IReadOnlyList<T> SnapshotRequiredItems<T> (
        IReadOnlyList<T> values,
        string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var snapshot = values.ToArray();
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("Report collections must not contain null items.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }

    public static IReadOnlyList<string> SnapshotSafeRelativePaths (
        IReadOnlyList<string> relativePaths,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(relativePaths, parameterName);

        var snapshot = relativePaths.ToArray();
        foreach (var relativePath in snapshot)
        {
            ValidateSafeRelativePath(relativePath, parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return Array.AsReadOnly(snapshot);
    }

    public static void ValidateOptionalText (
        string? value,
        string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Optional report text must be null or non-whitespace.", parameterName);
        }
    }

    public static void ValidateSafeRelativePath (
        string? relativePath,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.Contains(':', StringComparison.Ordinal)
            || relativePath.Any(char.IsControl)
            || relativePath.Split('/').Any(static segment =>
                string.IsNullOrWhiteSpace(segment)
                || segment is "." or ".."
                || Path.IsPathRooted(segment)))
        {
            throw new ArgumentException($"Operation report path must be a safe slash-separated relative path: {relativePath}", parameterName);
        }
    }
}
