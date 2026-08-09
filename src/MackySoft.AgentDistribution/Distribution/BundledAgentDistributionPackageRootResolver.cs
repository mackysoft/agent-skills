using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Resolves the bundled v2 mixed package root. </summary>
public sealed class BundledAgentDistributionPackageRootResolver
{
    private readonly AbsolutePath packageBaseDirectory;

    /// <summary> Initializes a resolver for one product package base directory. </summary>
    /// <param name="packageBaseDirectory"> The product package base directory containing the generated <c>skills</c> directory. </param>
    public BundledAgentDistributionPackageRootResolver (AbsolutePath packageBaseDirectory)
    {
        this.packageBaseDirectory = packageBaseDirectory ?? throw new ArgumentNullException(nameof(packageBaseDirectory));
    }

    /// <summary> Resolves the generated v2 mixed bundle root. </summary>
    /// <returns>The generated bundle directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the generated bundle directory does not exist.</exception>
    public AbsolutePath Resolve ()
    {
        var candidate = ContainedPath.Create(packageBaseDirectory, RootRelativePath.Parse("skills")).Target;
        if (Directory.Exists(candidate.Value))
        {
            return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate the bundled Agent Distribution package root: {candidate.Value}");
    }
}
