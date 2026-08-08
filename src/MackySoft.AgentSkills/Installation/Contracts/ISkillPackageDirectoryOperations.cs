using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Contracts;

/// <summary> Provides directory primitives used by SKILL package transactions. </summary>
public interface ISkillPackageDirectoryOperations
{
    /// <summary> Determines whether the directory exists. </summary>
    /// <param name="path"> The directory path to inspect. </param>
    /// <returns> <see langword="true" /> when the directory exists; otherwise <see langword="false" />. </returns>
    bool Exists (AbsolutePath path);

    /// <summary> Creates a directory and all missing parents. </summary>
    /// <param name="path"> The directory path to create. </param>
    void Create (AbsolutePath path);

    /// <summary> Moves a directory to a new path. </summary>
    /// <param name="sourceDirectoryName"> The existing directory path. </param>
    /// <param name="destinationDirectoryName"> The destination directory path. </param>
    void Move (AbsolutePath sourceDirectoryName, AbsolutePath destinationDirectoryName);

    /// <summary> Deletes a directory. </summary>
    /// <param name="path"> The directory path to delete. </param>
    /// <param name="recursive"> Whether child entries should be deleted. </param>
    void Delete (AbsolutePath path, bool recursive);
}
