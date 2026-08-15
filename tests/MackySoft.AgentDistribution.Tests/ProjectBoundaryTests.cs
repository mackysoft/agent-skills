using System.Xml.Linq;
using MackySoft.AgentDistribution.Hosts.Registration;

namespace MackySoft.AgentDistribution.Tests;

public sealed class ProjectBoundaryTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("src/MackySoft.AgentDistribution/MackySoft.AgentDistribution.csproj")]
    [InlineData("src/MackySoft.AgentDistribution.Hosting/MackySoft.AgentDistribution.Hosting.csproj")]
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
    [InlineData("Names", "MackySoft.AgentDistribution.Bundles")]
    [InlineData("Names", "MackySoft.AgentDistribution.Catalogs")]
    [InlineData("Names", "MackySoft.AgentDistribution.Categories")]
    [InlineData("Names", "MackySoft.AgentDistribution.Commands")]
    [InlineData("Names", "MackySoft.AgentDistribution.Dependencies")]
    [InlineData("Names", "MackySoft.AgentDistribution.Digests")]
    [InlineData("Names", "MackySoft.AgentDistribution.Distribution")]
    [InlineData("Names", "MackySoft.AgentDistribution.Doctor")]
    [InlineData("Names", "MackySoft.AgentDistribution.Hosts")]
    [InlineData("Names", "MackySoft.AgentDistribution.Installation")]
    [InlineData("Names", "MackySoft.AgentDistribution.Materialization")]
    [InlineData("Names", "MackySoft.AgentDistribution.OperationReports")]
    [InlineData("Names", "MackySoft.AgentDistribution.Packaging")]
    [InlineData("Names", "MackySoft.AgentDistribution.Selection")]
    [InlineData("Names", "MackySoft.AgentDistribution.Serialization")]
    [InlineData("Names", "MackySoft.AgentDistribution.Sources")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Bundles")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Catalogs")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Categories")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Commands")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Digests")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Distribution")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Doctor")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Hosts")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Installation")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Materialization")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.OperationReports")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Packaging")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Selection")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Serialization")]
    [InlineData("Dependencies", "MackySoft.AgentDistribution.Sources")]
    [InlineData("Agents", "MackySoft.AgentDistribution.Bundles")]
    [InlineData("Agents", "MackySoft.AgentDistribution.Distribution")]
    [InlineData("Skills", "MackySoft.AgentDistribution.Bundles")]
    [InlineData("Skills", "MackySoft.AgentDistribution.Distribution")]
    [InlineData("Distribution", "MackySoft.AgentDistribution.Installation")]
    [InlineData("Installation", "MackySoft.AgentDistribution.Doctor")]
    [InlineData("Installation/Validation", "MackySoft.AgentDistribution.Hosts.Registration")]
    [InlineData("Materialization", "MackySoft.AgentDistribution.Installation")]
    [InlineData("Packaging", "MackySoft.AgentDistribution.Bundles")]
    [InlineData("Packaging", "MackySoft.AgentDistribution.Installation")]
    [InlineData("Packaging", "MackySoft.AgentDistribution.Materialization")]
    [InlineData("Packaging", "MackySoft.AgentDistribution.Distribution")]
    [InlineData("Packaging", "MackySoft.AgentDistribution.Doctor")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Doctor")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Contracts")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Diffing")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Inventory")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Requests")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Results")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Services")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.State")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Transactions")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Installation.Validation")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Materialization")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Packaging")]
    [InlineData("Commands", "MackySoft.AgentDistribution.Sources")]
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
            ["MackySoft.AgentDistribution.Commands", "Commands."]);
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

    [Fact]
    [Trait("Size", "Small")]
    public void HostContractDirectory_DoesNotReferenceConcreteHostImplementations ()
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", "Contracts");

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, GetConcreteHostImplementationReferences());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostRegistrationDirectory_DoesNotOwnConcreteHostArtifacts ()
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", "Registration");

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, GetConcreteHostArtifactReferences());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostContractFilesExceptHostKinds_DoNotOwnConcreteHostArtifacts ()
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", "Contracts");
        var forbiddenArtifacts = GetConcreteHostArtifactReferences();

        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(static filePath => Path.GetFileName(filePath) is not "HostKind.cs")
            .SelectMany(filePath => forbiddenArtifacts
                .Where(artifact => File.ReadAllText(filePath).Contains(artifact, StringComparison.Ordinal))
                .Select(artifact => $"{Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/')} contains {artifact}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
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
            ["MackySoft.AgentDistribution.Sources"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostDirectory_DoesNotReferenceAgentOrSkillSourceNamespaces ()
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts");

        AssertDirectoryDoesNotContainAny(
            sourceRoot,
            directoryPath,
            [
                "MackySoft.AgentDistribution.Agents.Sources",
                "MackySoft.AgentDistribution.Sources",
            ]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("ClaudeCode", "Codex")]
    [InlineData("ClaudeCode", "GitHubCopilot")]
    [InlineData("Codex", "ClaudeCode")]
    [InlineData("Codex", "GitHubCopilot")]
    [InlineData("GitHubCopilot", "ClaudeCode")]
    [InlineData("GitHubCopilot", "Codex")]
    public void ConcreteHostDirectory_DoesNotReferenceSiblingConcreteHostImplementation (
        string hostDirectoryName,
        string siblingHostDirectoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", hostDirectoryName);

        AssertDirectoryDoesNotContainAny(
            sourceRoot,
            directoryPath,
            [$"MackySoft.AgentDistribution.Hosts.{siblingHostDirectoryName}", $"{siblingHostDirectoryName}SkillHostAdapter"]);
    }

    private static string GetSourceRoot ()
    {
        return Path.Combine(SkillTestData.GetRepositoryRoot(), "src", "MackySoft.AgentDistribution");
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

    public static TheoryData<string, string> BoundarySubdirectoryForbiddenNamespaceCases ()
    {
        var data = new TheoryData<string, string>();

        AddForbiddenNamespaceCases(
            data,
            "Packaging/Paths",
            [
                "MackySoft.AgentDistribution.Distribution",
                "MackySoft.AgentDistribution.Doctor",
                "MackySoft.AgentDistribution.Skills.Generation",
                "MackySoft.AgentDistribution.Hosts",
                "MackySoft.AgentDistribution.Installation",
                "MackySoft.AgentDistribution.Materialization",
                "MackySoft.AgentDistribution.Sources",
            ]);

        AddForbiddenNamespaceCases(
            data,
            "Skills/Packaging/Canonical",
            [
                "MackySoft.AgentDistribution.Distribution",
                "MackySoft.AgentDistribution.Doctor",
                "MackySoft.AgentDistribution.Skills.Generation",
                "MackySoft.AgentDistribution.Installation",
                "MackySoft.AgentDistribution.Materialization",
                "MackySoft.AgentDistribution.Sources",
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
                "MackySoft.AgentDistribution.Skills.Manifests",
                "MackySoft.AgentDistribution.Packaging",
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
        return BuiltInHostCatalog.Registrations
            .SelectMany(static registration =>
            {
                var types = new[] { registration.SkillAdapter.GetType(), registration.AgentArtifactAdapter.GetType() };
                return types.SelectMany(static type => new[] { type.Namespace!, type.Name });
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetConcreteHostArtifactReferences ()
    {
        return BuiltInHostCatalog.Registrations
            .SelectMany(static registration =>
            {
                var descriptor = registration.Skill;
                var references = new List<string>
                {
                    Vocabulary.GetText(registration.Host),
                    descriptor.ReloadGuidance,
                    descriptor.ProjectDefaultTargetPath.Value,
                };

                var userTargetRootPolicy = descriptor.UserTargetRootPolicy;
                references.Add(userTargetRootPolicy.HomeRelativeDirectory.Value);

                if (!string.IsNullOrWhiteSpace(userTargetRootPolicy.EnvironmentVariableName))
                {
                    references.Add(userTargetRootPolicy.EnvironmentVariableName);
                }

                if (userTargetRootPolicy.EnvironmentVariableChildDirectory is not null
                    && !string.Equals(userTargetRootPolicy.EnvironmentVariableChildDirectory.Value, "skills", StringComparison.Ordinal))
                {
                    references.Add(userTargetRootPolicy.EnvironmentVariableChildDirectory.Value);
                }

                if (descriptor.MetadataArtifactPath is not null)
                {
                    references.Add(descriptor.MetadataArtifactPath.Value);
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
            .Select(static directoryName => $"MackySoft.AgentDistribution.Installation.{directoryName}")
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
