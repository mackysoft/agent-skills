namespace MackySoft.AgentSkills.Bundles;

/// <summary> Publishes a verified staging directory while preserving the previous bundle on commit failure. </summary>
internal static class CanonicalSkillBundleDirectoryPublisher
{
    /// <summary> Swaps a staging directory into the output location and rolls back a displaced output on failure. </summary>
    /// <param name="stagingRoot"> The verified sibling staging directory. </param>
    /// <param name="outputRoot"> The authoritative output directory path. </param>
    /// <param name="backupRoot"> The unique sibling path used while committing. </param>
    internal static void Publish (
        string stagingRoot,
        string outputRoot,
        string backupRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);

        var backupCreated = false;
        if (Directory.Exists(outputRoot))
        {
            Directory.Move(outputRoot, backupRoot);
            backupCreated = true;
        }

        try
        {
            Directory.Move(stagingRoot, outputRoot);
        }
        catch (Exception publicationException)
        {
            if (backupCreated && !Directory.Exists(outputRoot) && Directory.Exists(backupRoot))
            {
                try
                {
                    Directory.Move(backupRoot, outputRoot);
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
