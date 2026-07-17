using MackySoft.AgentSkills.Packaging.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Executes source and generated bundle transaction primitives against the local file system. </summary>
internal sealed class SkillBundleBuildFileSystem : ISkillBundleBuildFileSystem
{
    /// <inheritdoc />
    public bool DirectoryExists (string path)
    {
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public void MoveDirectory (
        string sourcePath,
        string destinationPath)
    {
        Directory.Move(sourcePath, destinationPath);
    }

    /// <inheritdoc />
    public void DeleteDirectory (string path)
    {
        Directory.Delete(path, recursive: true);
    }

    /// <inheritdoc />
    public ValueTask WriteSourceBundleAsync (
        string path,
        string contents,
        CancellationToken cancellationToken)
    {
        return SkillPackageFileWriter.WriteAllTextAtomicallyAsync(path, contents, cancellationToken);
    }
}
