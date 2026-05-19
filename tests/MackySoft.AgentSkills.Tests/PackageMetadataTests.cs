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
            ["tools/MackySoft.AgentSkills.Builder/MackySoft.AgentSkills.Builder.csproj"] = new(StringComparer.Ordinal)
            {
                ["IsPackable"] = "true",
                ["PackAsTool"] = "true",
                ["ToolCommandName"] = "agent-skills",
                ["PackageId"] = "MackySoft.AgentSkills.Builder",
                ["Description"] = ".NET tool for generating canonical agent skill packages from product-owned skill definitions.",
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
    public void Central_package_metadata_targets_initial_nuget_version ()
    {
        var document = XDocument.Load(ToRepositoryPath("Directory.Build.props"));
        var expectedProperties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Version"] = "0.1.0",
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

    private static string ToRepositoryPath (string relativePath)
    {
        return Path.Combine(SkillTestData.GetRepositoryRoot(), relativePath);
    }
}
