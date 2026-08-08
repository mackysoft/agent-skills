using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class SkillPathBoundaryFoundationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveUnderRoot_WithSameRootAndDescendant_PreservesContainmentContract ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-paths", "foundation-containment");
        var descendant = scope.GetPath("nested/file.txt");

        var rootResult = SkillPackagePathBoundary.ResolveUnderRoot(scope.FullPath, scope.FullPath);
        var descendantResult = SkillPackagePathBoundary.ResolveUnderRoot(scope.FullPath, descendant);

        Assert.True(rootResult.IsSuccess, rootResult.Failure?.Message);
        Assert.True(descendantResult.IsSuccess, descendantResult.Failure?.Message);
        Assert.EndsWith(Path.GetFileName(scope.FullPath), rootResult.Value, StringComparison.Ordinal);
        Assert.EndsWith("nested/file.txt", descendantResult.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolvePackagePaths_WithCreatedStagingRoot_PreservesNestedContainmentContract ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-paths", "foundation-package-containment");
        var stagingRoot = scope.CreateDirectory(".generated.staging.operation");

        var directoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(stagingRoot, "example-skill");
        Assert.True(directoryResult.IsSuccess, directoryResult.Failure?.Message);

        Directory.CreateDirectory(directoryResult.Value!);
        var fileResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(
            stagingRoot,
            directoryResult.Value!,
            "SKILL.md");

        Assert.True(fileResult.IsSuccess, fileResult.Failure?.Message);
    }
}
