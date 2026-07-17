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
        var outputRoot = scope.CreateDirectory("generated");
        var oldMarker = scope.WriteFile("generated/old-bundle.txt", "old bundle\n");
        var missingStagingRoot = scope.GetPath(".generated.staging.missing");
        var backupRoot = scope.GetPath(".generated.backup.test");

        Assert.Throws<DirectoryNotFoundException>(() =>
            CanonicalSkillBundleDirectoryPublisher.Publish(missingStagingRoot, outputRoot, backupRoot));

        Assert.True(File.Exists(oldMarker));
        Assert.True(Directory.Exists(outputRoot));
        Assert.False(Directory.Exists(backupRoot));
    }
}
