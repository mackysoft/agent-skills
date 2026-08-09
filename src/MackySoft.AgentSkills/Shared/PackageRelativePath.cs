using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Shared;

/// <summary> Represents canonical slash-separated path text relative to a distributed package root. </summary>
/// <remarks>
/// This type owns the serialized package-path contract, including characters that would make the
/// same package path platform-dependent. It does not observe the file system. Callers that require
/// a physical file must separately establish containment and entry kind at the operation boundary.
/// </remarks>
public sealed class PackageRelativePath : IEquatable<PackageRelativePath>
{
    private sealed class PortableFileSystemPathComparer : IEqualityComparer<PackageRelativePath>
    {
        public bool Equals (PackageRelativePath? left, PackageRelativePath? right)
        {
            return ReferenceEquals(left, right)
                || (left is not null
                    && right is not null
                    && StringComparer.OrdinalIgnoreCase.Equals(left.Value, right.Value));
        }

        public int GetHashCode (PackageRelativePath path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return StringComparer.OrdinalIgnoreCase.GetHashCode(path.Value);
        }
    }

    /// <summary>Compares package paths by the case-insensitive identity required to prevent cross-platform file collisions.</summary>
    internal static IEqualityComparer<PackageRelativePath> PortableFileSystemComparer { get; } = new PortableFileSystemPathComparer();

    private PackageRelativePath (RootRelativePath path)
    {
        RootRelativePath = path;
    }

    /// <summary> Gets the canonical slash-separated path text. </summary>
    public string Value => RootRelativePath.Value;

    /// <summary> Gets the guarded root-relative path. </summary>
    public RootRelativePath RootRelativePath { get; }

    /// <summary> Parses canonical package-relative path text. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> does not satisfy the package path contract. </exception>
    public static PackageRelativePath Parse (string value)
    {
        if (!TryParse(value, out var path))
        {
            throw new ArgumentException("Value must be a canonical package-relative path.", nameof(value));
        }

        return path;
    }

    /// <summary> Tries to parse canonical path text relative to a package root. </summary>
    /// <param name="value"> The path text to parse. </param>
    /// <param name="path"> The parsed path when successful. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> satisfies the package-relative path contract. </returns>
    public static bool TryParse (string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PackageRelativePath? path)
    {
        path = null;
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
    public static bool TryParseSegment (string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PackageRelativePath? path)
    {
        return TryParse(value, out path)
            && !path.Value.Contains('/', StringComparison.Ordinal);
    }

    /// <summary>Determines whether another canonical package path has the same identity under the current platform path policy.</summary>
    /// <param name="candidate">The canonical package path to compare.</param>
    /// <returns><see langword="true" /> when both paths identify the same package entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="candidate" /> is <see langword="null" />.</exception>
    public bool IsSameAs (PackageRelativePath candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return RootRelativePath.IsSameAs(candidate.RootRelativePath);
    }

    /// <summary>Determines whether this path is below a canonical package directory path.</summary>
    /// <param name="directoryPath">The canonical package directory path that may contain this path.</param>
    /// <returns><see langword="true" /> when this path is a strict descendant of <paramref name="directoryPath" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directoryPath" /> is <see langword="null" />.</exception>
    public bool IsDescendantOf (PackageRelativePath directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        return Value.Length > directoryPath.Value.Length
            && Value.StartsWith(directoryPath.Value, StringComparison.Ordinal)
            && Value[directoryPath.Value.Length] == '/';
    }

    /// <summary>Tries to derive the canonical path below a package directory.</summary>
    /// <param name="directoryPath">The canonical package directory path that may contain this path.</param>
    /// <param name="relativePath">The canonical path below <paramref name="directoryPath" /> when this path is a strict descendant.</param>
    /// <returns><see langword="true" /> when the relative path was derived.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directoryPath" /> is <see langword="null" />.</exception>
    public bool TryGetRelativeTo (
        PackageRelativePath directoryPath,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PackageRelativePath? relativePath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        if (!IsDescendantOf(directoryPath))
        {
            relativePath = null;
            return false;
        }

        return TryParse(Value[(directoryPath.Value.Length + 1)..], out relativePath);
    }

    /// <inheritdoc />
    public bool Equals (PackageRelativePath? other)
    {
        return other is not null && IsSameAs(other);
    }

    /// <inheritdoc />
    public override bool Equals (object? obj)
    {
        return obj is PackageRelativePath other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode ()
    {
        return RootRelativePath.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }

    /// <summary>Compares two canonical package paths under the current platform path policy.</summary>
    public static bool operator == (
        PackageRelativePath? left,
        PackageRelativePath? right)
    {
        return ReferenceEquals(left, right)
            || (left is not null && left.Equals(right));
    }

    /// <summary>Compares two canonical package paths under the current platform path policy.</summary>
    public static bool operator != (
        PackageRelativePath? left,
        PackageRelativePath? right)
    {
        return !(left == right);
    }
}
