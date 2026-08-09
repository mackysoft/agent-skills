using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Transactions;

/// <summary> Executes SKILL package directory primitives against the local file system. </summary>
public sealed class SkillPackageDirectoryOperations : ISkillPackageDirectoryOperations
{
    /// <inheritdoc />
    public bool Exists (AbsolutePath path)
    {
        return Directory.Exists(path.Value);
    }

    /// <inheritdoc />
    public void Create (AbsolutePath path)
    {
        Directory.CreateDirectory(path.Value);
    }

    /// <inheritdoc />
    public void Move (
        AbsolutePath sourceDirectoryName,
        AbsolutePath destinationDirectoryName)
    {
        Directory.Move(sourceDirectoryName.Value, destinationDirectoryName.Value);
    }

    /// <inheritdoc />
    public void Delete (
        AbsolutePath path,
        bool recursive)
    {
        Directory.Delete(path.Value, recursive);
    }
}
