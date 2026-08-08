namespace MackySoft.AgentSkills.Bundles;

using MackySoft.FileSystem;

/// <summary> Defines file-system primitives required by the source and generated bundle transaction. </summary>
internal interface ISkillBundleBuildFileSystem
{
    /// <summary> Returns whether a directory exists at the specified path. </summary>
    bool DirectoryExists (AbsolutePath path);

    /// <summary> Moves one directory to a new path on the same file system. </summary>
    void MoveDirectory (
        AbsolutePath sourcePath,
        AbsolutePath destinationPath);

    /// <summary> Deletes one directory and all of its contents. </summary>
    void DeleteDirectory (AbsolutePath path);

    /// <summary> Atomically replaces the authored source <c>bundle.json</c> while preserving the existing file on failure. </summary>
    ValueTask WriteSourceBundleAsync (
        AbsolutePath path,
        string contents,
        CancellationToken cancellationToken);
}
