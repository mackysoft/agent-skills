namespace MackySoft.AgentSkills.Distribution;

/// <summary> Resolves the bundled v2 mixed package root. </summary>
public sealed class BundledAgentSkillsPackageRootResolver
{
    private readonly string packageBaseDirectory;

    /// <summary> Initializes a resolver for one product package base directory. </summary>
    /// <param name="packageBaseDirectory"> The product package base directory containing the generated <c>skills</c> directory. </param>
    public BundledAgentSkillsPackageRootResolver (string packageBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageBaseDirectory);

        this.packageBaseDirectory = Path.GetFullPath(packageBaseDirectory);
    }

    /// <summary> Resolves the generated v2 mixed bundle root. </summary>
    /// <returns>The generated bundle directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the generated bundle directory does not exist.</exception>
    public string Resolve ()
    {
        var candidate = Path.Combine(packageBaseDirectory, "skills");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate bundled agent skills package root: {candidate}");
    }
}
