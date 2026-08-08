using MackySoft.AgentSkills.Bundles;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class CanonicalSkillBundleDirectoryPublisherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Publish_RestoresExistingOutputWhenStagingMoveFails ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "bundle-publisher-rollback");
        var outputRoot = AbsolutePath.Parse(scope.CreateDirectory("generated"));
        var oldMarker = scope.WriteFile("generated/old-bundle.txt", "old bundle\n");
        var missingStagingRoot = AbsolutePath.Parse(scope.GetPath(".generated.staging.missing"));
        var backupRoot = AbsolutePath.Parse(scope.GetPath(".generated.backup.test"));

        Assert.Throws<DirectoryNotFoundException>(() =>
            CanonicalSkillBundleDirectoryPublisher.Publish(missingStagingRoot, outputRoot, backupRoot));

        Assert.True(File.Exists(oldMarker));
        Assert.True(Directory.Exists(outputRoot.Value));
        Assert.False(Directory.Exists(backupRoot.Value));
    }
}
