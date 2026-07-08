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
            ["src/MackySoft.AgentSkills.Hosting/MackySoft.AgentSkills.Hosting.csproj"] = new(StringComparer.Ordinal)
            {
                ["IsPackable"] = "true",
                ["PackageId"] = "MackySoft.AgentSkills.Hosting",
                ["Description"] = "Reusable command runtime services for product CLIs that expose Agent Skills workflows.",
                ["PackageTags"] = "agent;skills;cli;hosting;automation",
            },
            ["src/MackySoft.AgentSkills.ConsoleAppFramework/MackySoft.AgentSkills.ConsoleAppFramework.csproj"] = new(StringComparer.Ordinal)
            {
                ["IsPackable"] = "true",
                ["PackageId"] = "MackySoft.AgentSkills.ConsoleAppFramework",
                ["Description"] = "ConsoleAppFramework source integration for product CLIs that expose Agent Skills commands.",
                ["PackageTags"] = "agent;skills;cli;consoleappframework;source",
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
    public void ConsoleAppFramework_package_uses_build_transitive_source_integration ()
    {
        var projectDocument = XDocument.Load(ToRepositoryPath("src/MackySoft.AgentSkills.ConsoleAppFramework/MackySoft.AgentSkills.ConsoleAppFramework.csproj"));
        var removedCompileItems = projectDocument.Descendants("Compile")
            .Select(static element => element.Attribute("Remove")?.Value)
            .Where(static value => value is not null)
            .ToArray();
        var packedItems = projectDocument.Descendants("None")
            .Select(static element => new
            {
                Include = element.Attribute("Include")?.Value,
                Pack = element.Attribute("Pack")?.Value,
                PackagePath = element.Attribute("PackagePath")?.Value,
            })
            .ToArray();
        var propsDocument = XDocument.Load(ToRepositoryPath("src/MackySoft.AgentSkills.ConsoleAppFramework/buildTransitive/MackySoft.AgentSkills.ConsoleAppFramework.props"));

        Assert.Contains("contentFiles/**/*.cs", removedCompileItems);
        Assert.Contains(packedItems, static item =>
            item.Include == "buildTransitive/MackySoft.AgentSkills.ConsoleAppFramework.props"
            && item.Pack == "true"
            && item.PackagePath == "buildTransitive/");
        Assert.Contains(packedItems, static item =>
            item.Include == "contentFiles/cs/any/MackySoft.AgentSkills.ConsoleAppFramework/*.cs"
            && item.Pack == "true"
            && item.PackagePath == "contentFiles/cs/any/MackySoft.AgentSkills.ConsoleAppFramework/");
        Assert.Contains(
            "contentFiles\\cs\\any\\MackySoft.AgentSkills.ConsoleAppFramework\\*.cs",
            propsDocument.ToString(SaveOptions.DisableFormatting),
            StringComparison.Ordinal);
    }

    private static string ToRepositoryPath (string relativePath)
    {
        return Path.Combine(SkillTestData.GetRepositoryRoot(), relativePath);
    }
}
