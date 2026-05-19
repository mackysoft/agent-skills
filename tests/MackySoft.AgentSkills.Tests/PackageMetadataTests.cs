using System.Xml.Linq;

namespace MackySoft.AgentSkills.Tests;

public sealed class PackageMetadataTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Packable_projects_declare_expected_package_metadata ()
    {
        var expectedMetadataByProject = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["src/MackySoft.AgentSkills/MackySoft.AgentSkills.csproj"] = new(StringComparer.Ordinal)
            {
                ["IsPackable"] = "true",
                ["PackageId"] = "MackySoft.AgentSkills",
                ["Description"] = "Reusable .NET services for building, materializing, installing, exporting, and diagnosing agent skill packages.",
                ["PackageTags"] = "agent;skills;cli;automation",
            },
            ["src/MackySoft.AgentSkills.Cli/MackySoft.AgentSkills.Cli.csproj"] = new(StringComparer.Ordinal)
            {
                ["IsPackable"] = "true",
                ["PackAsTool"] = "true",
                ["ToolCommandName"] = "agent-skills",
                ["PackageId"] = "MackySoft.AgentSkills.Cli",
                ["Description"] = ".NET CLI tool for generating canonical agent skill packages from product-owned skill definitions.",
                ["PackageTags"] = "agent;skills;cli;tool;generator",
            },
        };

        foreach ((string projectPath, Dictionary<string, string> expectedMetadata) in expectedMetadataByProject)
        {
            var document = XDocument.Load(ToRepositoryPath(projectPath));
            foreach ((string propertyName, string expectedValue) in expectedMetadata)
            {
                var element = document.Descendants(propertyName).SingleOrDefault();
                Assert.NotNull(element);
                Assert.Equal(expectedValue, element!.Value);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Central_package_metadata_declares_repository_package_version ()
    {
        var document = XDocument.Load(ToRepositoryPath("Directory.Build.props"));
        var version = document.Descendants("Version").SingleOrDefault()?.Value;
        Assert.NotNull(version);
        Assert.Matches(@"^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$", version);

        var expectedProperties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PackageVersion"] = "$(Version)",
            ["Authors"] = "Hiroya Aramaki",
            ["Company"] = "MackySoft",
            ["RepositoryUrl"] = "https://github.com/mackysoft/agent-skills",
            ["RepositoryType"] = "git",
            ["PackageLicenseFile"] = "LICENSE",
            ["PackageReadmeFile"] = "README.md",
        };

        foreach ((string propertyName, string expectedValue) in expectedProperties)
        {
            var element = document.Descendants(propertyName).SingleOrDefault();
            Assert.NotNull(element);
            Assert.Equal(expectedValue, element!.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Central_package_metadata_includes_readme_and_license_files ()
    {
        var document = XDocument.Load(ToRepositoryPath("Directory.Build.props"));
        var packageFiles = document
            .Descendants("None")
            .Select(static element => new
            {
                Include = element.Attribute("Include")?.Value,
                Pack = element.Attribute("Pack")?.Value,
                PackagePath = element.Attribute("PackagePath")?.Value,
            })
            .ToArray();

        Assert.Contains(packageFiles, static item =>
            item.Include == "$(MSBuildThisFileDirectory)LICENSE"
            && item.Pack == "true"
            && item.PackagePath == string.Empty);
        Assert.Contains(packageFiles, static item =>
            item.Include == "$(MSBuildThisFileDirectory)README.md"
            && item.Pack == "true"
            && item.PackagePath == string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Nuget_publish_workflow_uses_repository_version_and_tags_after_package_availability ()
    {
        var workflow = File.ReadAllText(ToRepositoryPath(".github/workflows/nuget-package.yaml"));

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("package_version=\"${package_versions[0]}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("NuGet publish must be dispatched from the default branch", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("DISPATCH_VERSION", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--skip-duplicate", workflow, StringComparison.Ordinal);
        Assert.Contains("libraryPackageUrl=${library_package_url}", workflow, StringComparison.Ordinal);
        Assert.Equal(2, workflow.Split("uses: actions/setup-dotnet@v5", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, workflow.Split("package_url()", StringSplitOptions.None).Length - 1);

        var waitIndex = workflow.IndexOf("- name: Wait for published packages", StringComparison.Ordinal);
        var tagIndex = workflow.IndexOf("- name: Create release tag", StringComparison.Ordinal);
        Assert.NotEqual(-1, waitIndex);
        Assert.NotEqual(-1, tagIndex);
        Assert.True(waitIndex < tagIndex);
    }

    private static string ToRepositoryPath (string relativePath)
    {
        return Path.Combine(SkillTestData.GetRepositoryRoot(), relativePath);
    }
}
