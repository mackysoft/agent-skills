namespace MackySoft.AgentSkills.Bundles;

using MackySoft.FileSystem;

/// <summary> Publishes a verified staging directory while preserving the previous bundle on commit failure. </summary>
internal static class CanonicalSkillBundleDirectoryPublisher
{
    /// <summary> Swaps a staging directory into the output location and rolls back a displaced output on failure. </summary>
    /// <param name="stagingRoot"> The verified sibling staging directory. </param>
    /// <param name="outputRoot"> The authoritative output directory path. </param>
    /// <param name="backupRoot"> The unique sibling path used while committing. </param>
    internal static void Publish (
        AbsolutePath stagingRoot,
        AbsolutePath outputRoot,
        AbsolutePath backupRoot)
    {
        ArgumentNullException.ThrowIfNull(stagingRoot);
        ArgumentNullException.ThrowIfNull(outputRoot);
        ArgumentNullException.ThrowIfNull(backupRoot);

        var backupCreated = false;
        if (Directory.Exists(outputRoot.Value))
        {
            Directory.Move(outputRoot.Value, backupRoot.Value);
            backupCreated = true;
        }

        try
        {
            Directory.Move(stagingRoot.Value, outputRoot.Value);
        }
        catch (Exception publicationException)
        {
            if (backupCreated && !Directory.Exists(outputRoot.Value) && Directory.Exists(backupRoot.Value))
            {
                try
                {
                    Directory.Move(backupRoot.Value, outputRoot.Value);
                }
                catch (Exception rollbackException)
                {
                    throw new IOException(
                        $"Generated SKILL bundle publication and rollback failed. The previous bundle remains at: {backupRoot}",
                        new AggregateException(publicationException, rollbackException));
                }
            }

            throw;
        }
    }
}
