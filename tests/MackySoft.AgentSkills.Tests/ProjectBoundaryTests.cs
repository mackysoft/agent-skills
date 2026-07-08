using System.Xml.Linq;

namespace MackySoft.AgentSkills.Tests;

public sealed class ProjectBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AgentSkillsProject_DoesNotReferenceInfrastructureOrContracts ()
    {
        var projectPath = Path.Combine(GetSourceRoot(), "MackySoft.AgentSkills.csproj");
        var document = XDocument.Load(projectPath);

        var references = document.Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => value is not null)
            .ToArray();

        Assert.DoesNotContain(references, static reference => reference!.Contains("MackySoft.AgentSkills.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(references, static reference => reference!.Contains("MackySoft.AgentSkills.Contracts", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("src/MackySoft.AgentSkills/MackySoft.AgentSkills.csproj")]
    [InlineData("src/MackySoft.AgentSkills.Hosting/MackySoft.AgentSkills.Hosting.csproj")]
    public void NonConsoleAppFrameworkProjects_DoNotReferenceConsoleAppFramework (string relativeProjectPath)
    {
        var projectPath = Path.Combine(SkillTestData.GetRepositoryRoot(), relativeProjectPath);
        var document = XDocument.Load(projectPath);

        var packageReferences = document.Descendants("PackageReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => value is not null)
            .ToArray();

        Assert.DoesNotContain(packageReferences, static reference => string.Equals(reference, "ConsoleAppFramework", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Names", "MackySoft.AgentSkills.Catalogs")]
    [InlineData("Names", "MackySoft.AgentSkills.Commands")]
    [InlineData("Names", "MackySoft.AgentSkills.Dependencies")]
    [InlineData("Names", "MackySoft.AgentSkills.Digests")]
    [InlineData("Names", "MackySoft.AgentSkills.Distribution")]
    [InlineData("Names", "MackySoft.AgentSkills.Doctor")]
    [InlineData("Names", "MackySoft.AgentSkills.Generation")]
    [InlineData("Names", "MackySoft.AgentSkills.Hosts")]
    [InlineData("Names", "MackySoft.AgentSkills.Installation")]
    [InlineData("Names", "MackySoft.AgentSkills.Manifests")]
    [InlineData("Names", "MackySoft.AgentSkills.Materialization")]
    [InlineData("Names", "MackySoft.AgentSkills.OperationReports")]
    [InlineData("Names", "MackySoft.AgentSkills.Packaging")]
    [InlineData("Names", "MackySoft.AgentSkills.Selection")]
    [InlineData("Names", "MackySoft.AgentSkills.Serialization")]
    [InlineData("Names", "MackySoft.AgentSkills.Sources")]
    [InlineData("Names", "MackySoft.AgentSkills.Tiers")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Catalogs")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Commands")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Digests")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Distribution")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Doctor")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Generation")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Hosts")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Installation")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Manifests")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Materialization")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.OperationReports")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Packaging")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Selection")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Serialization")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Sources")]
    [InlineData("Dependencies", "MackySoft.AgentSkills.Tiers")]
    [InlineData("Distribution", "MackySoft.AgentSkills.Installation")]
    [InlineData("Installation", "MackySoft.AgentSkills.Doctor")]
    [InlineData("Materialization", "MackySoft.AgentSkills.Installation")]
    [InlineData("Packaging", "MackySoft.AgentSkills.Installation")]
    [InlineData("Packaging", "MackySoft.AgentSkills.Materialization")]
    [InlineData("Packaging", "MackySoft.AgentSkills.Distribution")]
    [InlineData("Packaging", "MackySoft.AgentSkills.Doctor")]
    [InlineData("Commands", "MackySoft.AgentSkills.Doctor")]
    [InlineData("Commands", "MackySoft.AgentSkills.Generation")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Contracts")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Diffing")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Inventory")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Requests")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Results")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Services")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.State")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Transactions")]
    [InlineData("Commands", "MackySoft.AgentSkills.Installation.Validation")]
    [InlineData("Commands", "MackySoft.AgentSkills.Materialization")]
    [InlineData("Commands", "MackySoft.AgentSkills.Packaging")]
    [InlineData("Commands", "MackySoft.AgentSkills.Sources")]
    public void Directory_DoesNotReferenceForbiddenNamespace (
        string directoryName,
        string forbiddenNamespace)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(filePath => File.ReadAllText(filePath).Contains(forbiddenNamespace, StringComparison.Ordinal))
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(CommandReverseDependencyDirectoryCases))]
    public void NonCommandDirectory_DoesNotReferenceCommandNamespace (string directoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        AssertDirectoryDoesNotContainAny(
            sourceRoot,
            directoryPath,
            ["MackySoft.AgentSkills.Commands", "Commands."]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(BoundaryRootDirectoryCases))]
    public void BoundaryRootDirectory_ContainsOnlyExpectedSubdirectories (
        string relativeDirectory,
        string[] expectedDirectoryNames)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = CombineSourcePath(sourceRoot, relativeDirectory);

        var directSourceFiles = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var actualDirectoryNames = Directory.EnumerateDirectories(directoryPath)
            .Select(Path.GetFileName)
            .Where(static directoryName => !string.IsNullOrWhiteSpace(directoryName))
            .Select(static directoryName => directoryName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(directSourceFiles);
        Assert.Equal(expectedDirectoryNames.Order(StringComparer.Ordinal), actualDirectoryNames);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(BoundarySubdirectoryForbiddenNamespaceCases))]
    public void BoundarySubdirectory_DoesNotReferenceForbiddenNamespace (
        string relativeDirectory,
        string forbiddenNamespace)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = CombineSourcePath(sourceRoot, relativeDirectory);

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, [forbiddenNamespace]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(NonHostConcreteHostArtifactCases))]
    public void NonHostDirectory_DoesNotReferenceConcreteHostArtifacts (
        string directoryName,
        string concreteHostArtifact)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(filePath => File.ReadAllText(filePath).Contains(concreteHostArtifact, StringComparison.Ordinal))
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(HostAgnosticSourceDirectoryCases))]
    public void NonHostDirectory_DoesNotReferenceConcreteHostImplementations (string directoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, GetConcreteHostImplementationReferences());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Contracts")]
    [InlineData("Registration")]
    public void HostInfrastructureDirectory_DoesNotReferenceConcreteHostImplementations (string directoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", directoryName);

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, GetConcreteHostImplementationReferences());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Contracts")]
    [InlineData("Registration")]
    public void HostInfrastructureDirectory_DoesNotReferenceConcreteHostArtifacts (string directoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", directoryName);

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, GetConcreteHostArtifactReferences());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostContractDirectory_DoesNotReferenceSourceNamespace ()
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", "Contracts");

        AssertDirectoryDoesNotContainAny(
            sourceRoot,
            directoryPath,
            ["MackySoft.AgentSkills.Sources"]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Claude", "ClaudeSkillHostAdapter.cs")]
    [InlineData("Copilot", "CopilotSkillHostAdapter.cs")]
    [InlineData("OpenAi", "OpenAiSkillHostAdapter.cs")]
    public void ConcreteHostImplementation_IsLocatedUnderConcreteHostDirectory (
        string hostDirectoryName,
        string fileName)
    {
        var sourceRoot = GetSourceRoot();
        var hostsRoot = Path.Combine(sourceRoot, "Hosts");
        var expectedPath = Path.GetFullPath(Path.Combine(hostsRoot, hostDirectoryName, fileName));

        Assert.True(File.Exists(expectedPath), $"Expected concrete host implementation file: {expectedPath}");

        var misplacedFiles = Directory.EnumerateFiles(hostsRoot, fileName, SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(filePath => !string.Equals(filePath, expectedPath, StringComparison.Ordinal))
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(misplacedFiles);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Claude", "Copilot")]
    [InlineData("Claude", "OpenAi")]
    [InlineData("Copilot", "Claude")]
    [InlineData("Copilot", "OpenAi")]
    [InlineData("OpenAi", "Claude")]
    [InlineData("OpenAi", "Copilot")]
    public void ConcreteHostDirectory_DoesNotReferenceSiblingConcreteHostImplementation (
        string hostDirectoryName,
        string siblingHostDirectoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", hostDirectoryName);

        AssertDirectoryDoesNotContainAny(
            sourceRoot,
            directoryPath,
            [$"MackySoft.AgentSkills.Hosts.{siblingHostDirectoryName}", $"{siblingHostDirectoryName}SkillHostAdapter"]);
    }

    private static string GetSourceRoot ()
    {
        return Path.Combine(SkillTestData.GetRepositoryRoot(), "src", "MackySoft.AgentSkills");
    }

    public static TheoryData<string, string> NonHostConcreteHostArtifactCases ()
    {
        var data = new TheoryData<string, string>();
        foreach (var directoryName in GetHostAgnosticSourceDirectoryNames())
        {
            foreach (var artifactReference in GetConcreteHostArtifactReferences())
            {
                data.Add(directoryName, artifactReference);
            }
        }

        return data;
    }

    public static TheoryData<string> HostAgnosticSourceDirectoryCases ()
    {
        var data = new TheoryData<string>();
        foreach (var directoryName in GetHostAgnosticSourceDirectoryNames())
        {
            data.Add(directoryName);
        }

        return data;
    }

    public static TheoryData<string> CommandReverseDependencyDirectoryCases ()
    {
        var data = new TheoryData<string>();
        foreach (var directoryName in GetSourceDirectoryNamesExcept("Commands"))
        {
            data.Add(directoryName);
        }

        return data;
    }

    public static TheoryData<string, string[]> BoundaryRootDirectoryCases ()
    {
        var data = new TheoryData<string, string[]>
        {
            {
                "Installation",
                [
                    "Contracts",
                    "Diffing",
                    "Inventory",
                    "Requests",
                    "Results",
                    "Services",
                    "State",
                    "Targeting",
                    "Transactions",
                    "Validation",
                ]
            },
            {
                "OperationReports",
                [
                    "Contracts",
                    "Literals",
                    "Projection",
                ]
            },
            {
                "Packaging",
                [
                    "Canonical",
                    "FileSystem",
                ]
            },
        };

        return data;
    }

    public static TheoryData<string, string> BoundarySubdirectoryForbiddenNamespaceCases ()
    {
        var data = new TheoryData<string, string>();

        AddForbiddenNamespaceCases(
            data,
            "Packaging/FileSystem",
            [
                "MackySoft.AgentSkills.Distribution",
                "MackySoft.AgentSkills.Doctor",
                "MackySoft.AgentSkills.Generation",
                "MackySoft.AgentSkills.Hosts",
                "MackySoft.AgentSkills.Installation",
                "MackySoft.AgentSkills.Materialization",
                "MackySoft.AgentSkills.Sources",
            ]);

        AddForbiddenNamespaceCases(
            data,
            "Packaging/Canonical",
            [
                "MackySoft.AgentSkills.Distribution",
                "MackySoft.AgentSkills.Doctor",
                "MackySoft.AgentSkills.Generation",
                "MackySoft.AgentSkills.Installation",
                "MackySoft.AgentSkills.Materialization",
                "MackySoft.AgentSkills.Sources",
            ]);

        AddForbiddenNamespaceCases(
            data,
            "Installation/Contracts",
            GetInstallationSubnamespaceReferencesExcept("Contracts"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Diffing",
            GetInstallationSubnamespaceReferencesExcept("Diffing", "Results"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Inventory",
            GetInstallationSubnamespaceReferencesExcept("Inventory", "Targeting", "Validation"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Requests",
            GetInstallationSubnamespaceReferencesExcept("Requests", "Targeting"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Results",
            GetInstallationSubnamespaceReferencesExcept("Results", "Targeting"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/State",
            GetInstallationSubnamespaceReferencesExcept("State", "Validation"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Targeting",
            GetInstallationSubnamespaceReferencesExcept("Targeting"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Transactions",
            GetInstallationSubnamespaceReferencesExcept("Transactions", "Contracts"));

        AddForbiddenNamespaceCases(
            data,
            "Installation/Validation",
            GetInstallationSubnamespaceReferencesExcept("Validation"));

        AddForbiddenNamespaceCases(
            data,
            "OperationReports/Contracts",
            [
                "MackySoft.AgentSkills.Distribution",
                "MackySoft.AgentSkills.Doctor",
                "MackySoft.AgentSkills.Hosts",
                "MackySoft.AgentSkills.Installation",
                "MackySoft.AgentSkills.Manifests",
                "MackySoft.AgentSkills.Packaging",
                "MackySoft.AgentSkills.Shared",
            ]);

        return data;
    }

    private static string[] GetHostAgnosticSourceDirectoryNames ()
    {
        return GetSourceDirectoryNamesExcept("Hosts");
    }

    private static string[] GetSourceDirectoryNamesExcept (params string[] excludedDirectoryNames)
    {
        var excluded = new HashSet<string>(excludedDirectoryNames, StringComparer.Ordinal)
        {
            "bin",
            "obj",
            "SkillDefinitions",
        };

        var sourceRoot = GetSourceRoot();
        return Directory.EnumerateDirectories(sourceRoot)
            .Select(Path.GetFileName)
            .Where(static directoryName => !string.IsNullOrWhiteSpace(directoryName))
            .Select(static directoryName => directoryName!)
            .Where(directoryName => !excluded.Contains(directoryName))
            .Where(directoryName => Directory.EnumerateFiles(Path.Combine(sourceRoot, directoryName), "*.cs", SearchOption.AllDirectories).Any())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetConcreteHostImplementationReferences ()
    {
        return SkillTestData.CreateDefaultHostAdapterSet()
            .Adapters
            .SelectMany(static adapter =>
            {
                var type = adapter.GetType();
                return new[] { type.Namespace!, type.Name };
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetConcreteHostArtifactReferences ()
    {
        return SkillTestData.CreateDefaultHostAdapterSet()
            .Adapters
            .SelectMany(static adapter =>
            {
                var descriptor = adapter.Descriptor;
                var references = new List<string>
                {
                    $"{adapter.GetType().Name}.HostKey",
                    descriptor.HostKey,
                    descriptor.ReloadGuidance,
                };

                if (descriptor.SupportsProjectScope)
                {
                    references.Add(descriptor.ProjectDefaultTargetPath!);
                }

                if (descriptor.SupportsUserScope)
                {
                    var userTargetRootPolicy = descriptor.UserTargetRootPolicy!;
                    references.Add(descriptor.UserDefaultTargetPath!);
                    references.Add(userTargetRootPolicy.HomeRelativeDirectory);

                    if (!string.IsNullOrWhiteSpace(userTargetRootPolicy.EnvironmentVariableName))
                    {
                        references.Add(userTargetRootPolicy.EnvironmentVariableName);
                    }

                    if (!string.IsNullOrWhiteSpace(userTargetRootPolicy.EnvironmentVariableChildDirectory)
                        && !string.Equals(userTargetRootPolicy.EnvironmentVariableChildDirectory, "skills", StringComparison.Ordinal))
                    {
                        references.Add(userTargetRootPolicy.EnvironmentVariableChildDirectory);
                    }
                }

                if (descriptor.MetadataArtifactPath is not null)
                {
                    references.Add(descriptor.MetadataArtifactPath);
                }

                return references;
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string CombineSourcePath (string sourceRoot, string relativeDirectory)
    {
        return Path.Combine([sourceRoot, .. relativeDirectory.Split('/')]);
    }

    private static void AddForbiddenNamespaceCases (
        TheoryData<string, string> data,
        string relativeDirectory,
        IReadOnlyList<string> forbiddenNamespaces)
    {
        foreach (var forbiddenNamespace in forbiddenNamespaces)
        {
            data.Add(relativeDirectory, forbiddenNamespace);
        }
    }

    private static string[] GetInstallationSubnamespaceReferencesExcept (params string[] allowedSubnamespaceNames)
    {
        var allowedSubnamespaces = new HashSet<string>(allowedSubnamespaceNames, StringComparer.Ordinal);
        return GetInstallationBoundarySubdirectoryNames()
            .Where(directoryName => !allowedSubnamespaces.Contains(directoryName))
            .Select(static directoryName => $"MackySoft.AgentSkills.Installation.{directoryName}")
            .ToArray();
    }

    private static string[] GetInstallationBoundarySubdirectoryNames ()
    {
        return
        [
            "Contracts",
            "Diffing",
            "Inventory",
            "Requests",
            "Results",
            "Services",
            "State",
            "Targeting",
            "Transactions",
            "Validation",
        ];
    }

    private static void AssertDirectoryDoesNotContainAny (
        string sourceRoot,
        string directoryPath,
        IReadOnlyList<string> forbiddenTexts)
    {
        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .SelectMany(filePath => forbiddenTexts
                .Where(forbiddenText => File.ReadAllText(filePath).Contains(forbiddenText, StringComparison.Ordinal))
                .Select(forbiddenText => $"{Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/')} contains {forbiddenText}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }
}
