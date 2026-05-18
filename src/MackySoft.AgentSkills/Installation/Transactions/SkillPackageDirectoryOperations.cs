using MackySoft.AgentSkills.Installation.Contracts;
namespace MackySoft.AgentSkills.Installation.Transactions;

/// <summary> Executes SKILL package directory primitives against the local file system. </summary>
public sealed class SkillPackageDirectoryOperations : ISkillPackageDirectoryOperations
{
    /// <inheritdoc />
    public bool Exists (string path)
    {
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public void Create (string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public void Move (
        string sourceDirectoryName,
        string destinationDirectoryName)
    {
        Directory.Move(sourceDirectoryName, destinationDirectoryName);
    }

    /// <inheritdoc />
    public void Delete (
        string path,
        bool recursive)
    {
        Directory.Delete(path, recursive);
    }
}
