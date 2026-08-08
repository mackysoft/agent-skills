using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Resolves the bundled canonical <c>skills</c> package root. </summary>
public sealed class BundledSkillPackageRootResolver
{
    private readonly AbsolutePath baseDirectory;

    /// <summary> Initializes a new instance of the <see cref="BundledSkillPackageRootResolver" /> class. </summary>
    /// <param name="baseDirectory"> The application base directory containing bundled package files. </param>
    public BundledSkillPackageRootResolver (AbsolutePath baseDirectory)
    {
        this.baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
    }

    /// <summary> Resolves the bundled <c>skills</c> directory from the current base directory. </summary>
    /// <returns> The resolved canonical SKILL package root. </returns>
    /// <exception cref="DirectoryNotFoundException"> Thrown when the package root cannot be found. </exception>
    public AbsolutePath Resolve ()
    {
        var candidate = ContainedPath.Create(this.baseDirectory, RootRelativePath.Parse("skills")).Target;

        if (Directory.Exists(candidate.Value))
        {
            return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate bundled skills package root: {candidate.Value}");
    }
}
