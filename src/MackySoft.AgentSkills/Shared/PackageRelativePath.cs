using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Shared;

/// <summary> Represents canonical slash-separated path text relative to a distributed package root. </summary>
/// <remarks>
/// This type owns the serialized package-path contract, including characters that would make the
/// same package path platform-dependent. It does not observe the file system. Callers that require
/// a physical file must separately establish containment and entry kind at the operation boundary.
/// </remarks>
internal readonly struct PackageRelativePath
{
    private readonly RootRelativePath pathValue;

    private PackageRelativePath (RootRelativePath path)
    {
        pathValue = path;
    }

    /// <summary> Gets the canonical slash-separated path text. </summary>
    public string Value => pathValue.Value;

    /// <summary> Gets the guarded root-relative path. </summary>
    internal RootRelativePath RootRelativePath => pathValue;

    /// <summary> Tries to parse canonical path text relative to a package root. </summary>
    /// <param name="value"> The path text to parse. </param>
    /// <param name="path"> The parsed path when successful. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> satisfies the package-relative path contract. </returns>
    public static bool TryParse (string? value, out PackageRelativePath path)
    {
        path = default;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal)
            || value.Any(char.IsControl)
            || !RootRelativePath.TryParse(value, out var rootRelativePath, out _)
            || rootRelativePath.IsRoot
            || !string.Equals(rootRelativePath.Value, value, StringComparison.Ordinal)
            || rootRelativePath.Value.Split('/').Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        path = new PackageRelativePath(rootRelativePath);
        return true;
    }

    /// <summary> Tries to parse one canonical package-relative path segment. </summary>
    /// <param name="value"> The segment text to parse. </param>
    /// <param name="path"> The parsed segment when successful. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is one package-relative path segment. </returns>
    public static bool TryParseSegment (string? value, out PackageRelativePath path)
    {
        return TryParse(value, out path)
            && !path.Value.Contains('/', StringComparison.Ordinal);
    }
}
