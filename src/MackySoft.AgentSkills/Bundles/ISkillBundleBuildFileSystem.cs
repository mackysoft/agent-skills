namespace MackySoft.AgentSkills.Bundles;

/// <summary> Defines file-system primitives required by the source and generated bundle transaction. </summary>
internal interface ISkillBundleBuildFileSystem
{
    /// <summary> Returns whether a directory exists at the specified path. </summary>
    bool DirectoryExists (string path);

    /// <summary> Moves one directory to a new path on the same file system. </summary>
    void MoveDirectory (
        string sourcePath,
        string destinationPath);

    /// <summary> Deletes one directory and all of its contents. </summary>
    void DeleteDirectory (string path);

    /// <summary> Atomically replaces the authored source <c>bundle.json</c> while preserving the existing file on failure. </summary>
    ValueTask WriteSourceBundleAsync (
        string path,
        string contents,
        CancellationToken cancellationToken);
}
