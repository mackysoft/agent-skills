using MackySoft.AgentDistribution.Bundles;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class CanonicalSkillBundleDirectoryPublisherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Publish_RestoresExistingOutputWhenStagingMoveFails ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "bundle-publisher-rollback");
        var outputRoot = AbsolutePath.Parse(scope.CreateDirectory("agent-distribution"));
        var oldMarker = scope.WriteFile("agent-distribution/old-bundle.txt", "old bundle\n");
        var missingStagingRoot = AbsolutePath.Parse(scope.GetPath(".agent-distribution.staging.missing"));
        var backupRoot = AbsolutePath.Parse(scope.GetPath(".agent-distribution.backup.test"));

        Assert.Throws<DirectoryNotFoundException>(() =>
            CanonicalSkillBundleDirectoryPublisher.Publish(missingStagingRoot, outputRoot, backupRoot));

        Assert.True(File.Exists(oldMarker));
        Assert.True(Directory.Exists(outputRoot.Value));
        Assert.False(Directory.Exists(backupRoot.Value));
    }
}
