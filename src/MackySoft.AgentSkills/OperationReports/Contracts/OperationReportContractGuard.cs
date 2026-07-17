using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.FileSystem;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Validates and snapshots values stored by operation report contracts. </summary>
internal static class OperationReportContractGuard
{
    public static string? NormalizeRepositoryRoot (
        SkillScopeKind scope,
        string? repositoryRoot,
        string parameterName)
    {
        if (scope == SkillScopeKind.User)
        {
            if (repositoryRoot is not null)
            {
                throw new ArgumentException("User-scope reports must not contain a repository root.", parameterName);
            }

            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot, parameterName);
        return NormalizeAbsolutePath(repositoryRoot, parameterName);
    }

    public static string NormalizeAbsolutePath (
        string path,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Report paths must be absolute.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    public static string NormalizeTargetRoot (
        SkillScopeKind scope,
        string? repositoryRoot,
        string targetRoot,
        string parameterName)
    {
        var normalizedTargetRoot = NormalizeAbsolutePath(targetRoot, parameterName);
        var allowedRoot = scope == SkillScopeKind.Project
            ? repositoryRoot!
            : normalizedTargetRoot;
        var result = SkillPathBoundary.ResolveUnderRoot(
            allowedRoot,
            normalizedTargetRoot,
            SkillFailureCodes.PathUnsafe,
            "Report target path");
        if (!result.IsSuccess)
        {
            throw new ArgumentException(result.Failure!.Message, parameterName);
        }

        return result.Value!;
    }

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
