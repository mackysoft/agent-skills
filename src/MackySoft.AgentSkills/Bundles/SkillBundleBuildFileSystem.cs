using MackySoft.AgentSkills.Serialization;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Executes source and generated bundle transaction primitives against the local file system. </summary>
internal sealed class SkillBundleBuildFileSystem : ISkillBundleBuildFileSystem
{
    /// <inheritdoc />
    public bool DirectoryExists (AbsolutePath path)
    {
        return Directory.Exists(path.Value);
    }

    /// <inheritdoc />
    public void MoveDirectory (
        AbsolutePath sourcePath,
        AbsolutePath destinationPath)
    {
        Directory.Move(sourcePath.Value, destinationPath.Value);
    }

    /// <inheritdoc />
    public void DeleteDirectory (AbsolutePath path)
    {
        Directory.Delete(path.Value, recursive: true);
    }

    /// <inheritdoc />
    public ValueTask WriteSourceBundleAsync (
        AbsolutePath path,
        string contents,
        CancellationToken cancellationToken)
    {
        return CanonicalTextFilePublisher.PublishAsync(path, contents, cancellationToken);
    }
}
