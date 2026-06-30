using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Inventory;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Transactions;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests;

internal static class SkillTestData
{
    internal static readonly string[] ExpectedSkillNames =
    [
        "agent-skills-plan-apply",
        "agent-skills-read-project",
        "agent-skills-troubleshoot",
        "agent-skills-verify-changes",
    ];

    internal static string GetDefinitionsRoot ()
    {
        return Path.Combine(GetRepositoryRoot(), "tests", "Fixtures", "SkillDefinitions");
    }

    internal static string GetGeneratedSkillsRoot ()
    {
        return Path.Combine(GetRepositoryRoot(), "tests", "Fixtures", "generated");
    }

    internal static string GetRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Fixtures", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tests/Fixtures/SkillDefinitions from the test output directory.");
    }

    internal static async Task<IReadOnlyList<CanonicalSkillPackage>> GenerateFixturePackagesAsync ()
    {
        var service = CreatePackageGenerationService();
        var result = await service.GenerateAllAsync(GetDefinitionsRoot(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static SkillHostAdapterSet CreateDefaultHostAdapterSet ()
    {
        return new SkillHostAdapterSet(
        [
            new ClaudeSkillHostAdapter(),
            new CopilotSkillHostAdapter(),
            new OpenAiSkillHostAdapter(),
        ]);
    }

    internal static SkillPackageGenerationService CreatePackageGenerationService ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillPackageGenerationService(
            new SkillSourceDefinitionReader(),
            CreateDefaultHostAdapterSet(),
            new SkillDigestCalculator(),
            manifestSerializer,
            new SkillManifestDigestCalculator(manifestSerializer));
    }

    internal static CanonicalSkillPackageReader CreatePackageReader ()
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        var manifestSerializer = new SkillManifestJsonSerializer();
        var manifestDigestCalculator = new SkillManifestDigestCalculator(manifestSerializer);
        return new CanonicalSkillPackageReader(
            hostAdapters,
            new SkillDigestCalculator(),
            manifestSerializer,
            new SkillManifestValidator(hostAdapters, manifestDigestCalculator));
    }

    internal static SkillManifestValidator CreateManifestValidator ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillManifestValidator(
            CreateDefaultHostAdapterSet(),
            new SkillManifestDigestCalculator(manifestSerializer));
    }

    internal static SkillMaterializationService CreateMaterializationService ()
    {
        return new SkillMaterializationService(CreateDefaultHostAdapterSet());
    }

    internal static SkillExportService CreateExportService ()
    {
        return new SkillExportService(CreateMaterializationService());
    }

    internal static SkillInstallService CreateInstallService ()
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillInstallService(
            new SkillInstallTargetResolver(hostAdapters, CreateUserTargetRootResolver()),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            CreatePackageWriter(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillUpdateService CreateUpdateService (ISkillMaterializedPackageWriter? packageWriter = null)
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillUpdateService(
            new SkillInstallTargetResolver(hostAdapters, CreateUserTargetRootResolver()),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            packageWriter ?? CreatePackageWriter(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillUninstallService CreateUninstallService ()
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillUninstallService(
            new SkillInstallTargetResolver(hostAdapters, CreateUserTargetRootResolver()),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            CreatePackageRemover());
    }

    internal static SkillInstallationScanner CreateInstallationScanner ()
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        return new SkillInstallationScanner(
            hostAdapters,
            CreateInstalledManifestReader(hostAdapters),
            CreateInstalledPackageValidator(hostAdapters));
    }

    internal static SkillMaterializedPackageWriter CreatePackageWriter ()
    {
        return new SkillMaterializedPackageWriter(new SkillPackageDirectoryOperations());
    }

    internal static SkillInstalledPackageRemover CreatePackageRemover ()
    {
        return new SkillInstalledPackageRemover(new SkillPackageDirectoryOperations());
    }

    internal static SkillUserTargetRootResolver CreateUserTargetRootResolver ()
    {
        return new SkillUserTargetRootResolver(
            static () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable);
    }

    internal static SkillDoctorService CreateDoctorService ()
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        return new SkillDoctorService(
            hostAdapters,
            CreateTargetStateAnalyzer(hostAdapters));
    }

    internal static SkillInstalledTargetStateAnalyzer CreateTargetStateAnalyzer (SkillHostAdapterSet? hostAdapters = null)
    {
        hostAdapters ??= CreateDefaultHostAdapterSet();
        return new SkillInstalledTargetStateAnalyzer(CreateInstalledPackageValidator(hostAdapters), CreateInstalledPackageIntegrityVerifier(hostAdapters));
    }

    internal static IReadOnlyList<CanonicalSkillPackage> ReplacePackage (
        IReadOnlyList<CanonicalSkillPackage> packages,
        CanonicalSkillPackage replacement)
    {
        return packages
            .Select(package => string.Equals(package.Manifest.SkillName.Value, replacement.Manifest.SkillName.Value, StringComparison.Ordinal) ? replacement : package)
            .ToArray();
    }

    internal static CanonicalSkillPackage CreatePackageWithUpdatedBody (CanonicalSkillPackage package)
    {
        var files = package.Files
            .Select(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                ? SkillPackageFile.Create("SKILL.md", file.Content + "\nFixture update.\n")
                : file)
            .ToArray();
        var contentDigest = new SkillDigestCalculator().ComputeDigest(files
            .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));
        var manifest = package.Manifest with
        {
            ContentDigest = contentDigest,
        };
        manifest = WithComputedManifestDigest(manifest);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        files = files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? SkillPackageFile.Create("agent-skill.json", manifestText)
                : file)
            .ToArray();

        return package with
        {
            Manifest = manifest,
            Files = files,
        };
    }

    internal static CanonicalSkillPackage CreateOrdinalSensitivePackage ()
    {
        const string SkillName = "ordinal-culture-contract";
        const string DisplayName = "Ordinal Culture Contract";
        const string Description = "Use this skill to verify ordinal package ordering.";

        var bodyFile = SkillPackageFile.Create("SKILL.md", "# Ordinal Culture Contract\n");
        var referenceFiles = new[]
        {
            SkillPackageFile.Create("references/a.md", "lowercase reference\n"),
            SkillPackageFile.Create("references/B.md", "uppercase reference\n"),
        };
        var digestCalculator = new SkillDigestCalculator();
        var contentDigest = digestCalculator.ComputeDigest(
            new[] { new SkillDigestInputFile(bodyFile.RelativePath, bodyFile.Content) }
                .Concat(referenceFiles.Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content))));
        var metadata = new SkillHostMetadata(new SkillName(SkillName), DisplayName, Description);
        var hostArtifacts = new List<SkillHostArtifactManifest>();
        var hostArtifactFiles = new List<SkillPackageFile>();

        foreach (var adapter in CreateDefaultHostAdapterSet().Adapters)
        {
            var artifacts = adapter.BuildArtifacts(metadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", artifacts.Frontmatter);
            if (adapter.MetadataArtifactPath is null)
            {
                hostArtifacts.Add(new SkillHostArtifactManifest(
                    adapter.Descriptor.HostKey,
                    null,
                    null,
                    frontmatterDigest));
                continue;
            }

            Assert.NotNull(artifacts.MetadataContent);
            hostArtifacts.Add(new SkillHostArtifactManifest(
                adapter.Descriptor.HostKey,
                adapter.MetadataArtifactPath,
                digestCalculator.ComputeSingleFileDigest(adapter.MetadataArtifactPath, artifacts.MetadataContent),
                frontmatterDigest));
            hostArtifactFiles.Add(SkillPackageFile.Create(adapter.MetadataArtifactPath, artifacts.MetadataContent));
        }

        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName(SkillName),
            DisplayName,
            Description,
            [],
            new SkillTier("basic"),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            contentDigest,
            string.Empty,
            hostArtifacts);
        manifest = WithComputedManifestDigest(manifest);
        var manifestFile = SkillPackageFile.Create("agent-skill.json", new SkillManifestJsonSerializer().Serialize(manifest));
        var files = new[] { bodyFile, manifestFile }
            .Concat(referenceFiles)
            .Concat(hostArtifactFiles)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return new CanonicalSkillPackage(manifest, files);
    }

    internal static CanonicalSkillPackage CreatePackageWithUpdatedOpenAiMetadata (CanonicalSkillPackage package)
    {
        var manifest = package.Manifest with
        {
            DisplayName = package.Manifest.DisplayName + " Updated",
        };
        var metadata = new SkillHostMetadata(manifest.SkillName, manifest.DisplayName, manifest.Description);
        var hostAdapters = CreateDefaultHostAdapterSet();
        var digestCalculator = new SkillDigestCalculator();
        string? openAiMetadata = null;
        var hostArtifacts = new List<SkillHostArtifactManifest>();
        foreach (var artifact in manifest.HostArtifacts.OrderBy(static artifact => artifact.Host, StringComparer.Ordinal))
        {
            var adapterResult = hostAdapters.GetAdapter(artifact.Host);
            Assert.True(adapterResult.IsSuccess, adapterResult.Failure?.Message);
            var adapter = adapterResult.Value!;
            var artifacts = adapter.BuildArtifacts(metadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", artifacts.Frontmatter);
            var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
            var metadataDigest = artifacts.MetadataContent is null || metadataArtifactPath is null
                ? null
                : digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent);

            if (string.Equals(adapter.Descriptor.HostKey, OpenAiSkillHostAdapter.HostKey, StringComparison.Ordinal))
            {
                openAiMetadata = artifacts.MetadataContent;
            }

            hostArtifacts.Add(new SkillHostArtifactManifest(
                adapter.Descriptor.HostKey,
                metadataArtifactPath,
                metadataDigest,
                frontmatterDigest));
        }

        manifest = manifest with
        {
            HostArtifacts = hostArtifacts.ToArray(),
        };
        manifest = WithComputedManifestDigest(manifest);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file =>
            {
                if (string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal))
                {
                    return SkillPackageFile.Create("agent-skill.json", manifestText);
                }

                if (string.Equals(file.RelativePath, "agents/openai.yaml", StringComparison.Ordinal))
                {
                    Assert.NotNull(openAiMetadata);
                    return SkillPackageFile.Create("agents/openai.yaml", openAiMetadata!);
                }

                return file;
            })
            .ToArray();

        return package with
        {
            Manifest = manifest,
            Files = files,
        };
    }

    internal static CanonicalSkillPackage WithFileEnumerationCallback (
        CanonicalSkillPackage package,
        Action callback)
    {
        return package with
        {
            Files = new CallbackPackageFileList(package.Files, callback),
        };
    }

    internal static SkillInstalledPackageValidator CreateInstalledPackageValidator (SkillHostAdapterSet hostAdapters)
    {
        return new SkillInstalledPackageValidator(
            CreateInstalledManifestReader(hostAdapters),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledContentDigestVerifier(new SkillDigestCalculator()),
            new SkillInstalledFileSetVerifier(),
            new SkillHostMaterializationInspector(hostAdapters, new SkillDigestCalculator()));
    }

    internal static SkillInstalledPackageIntegrityVerifier CreateInstalledPackageIntegrityVerifier (SkillHostAdapterSet hostAdapters)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillInstalledPackageIntegrityVerifier(
            CreateInstalledManifestReader(hostAdapters),
            manifestSerializer,
            new SkillManifestDigestCalculator(manifestSerializer),
            new SkillHostMaterializationInspector(hostAdapters, new SkillDigestCalculator()),
            new SkillDigestCalculator());
    }

    internal static SkillInstalledManifestReader CreateInstalledManifestReader (SkillHostAdapterSet hostAdapters)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillInstalledManifestReader(
            manifestSerializer,
            new SkillManifestValidator(hostAdapters, new SkillManifestDigestCalculator(manifestSerializer)));
    }

    internal static void TamperManifestDigest (string manifestPath)
    {
        var manifestText = File.ReadAllText(manifestPath);
        var manifest = new SkillManifestJsonSerializer().Deserialize(manifestText);
        var replacementDigest = string.Equals(manifest.ManifestDigest, new string('f', 64), StringComparison.Ordinal)
            ? new string('0', 64)
            : new string('f', 64);
        File.WriteAllText(manifestPath, manifestText.Replace(manifest.ManifestDigest, replacementDigest, StringComparison.Ordinal));
    }

    internal static void WriteNameCollisionManifest (string targetRoot, CanonicalSkillPackage package)
    {
        var skillDirectory = Path.Combine(targetRoot, package.Manifest.SkillName.Value);
        Directory.CreateDirectory(skillDirectory);
        var manifest = WithComputedManifestDigest(package.Manifest with
        {
            SkillName = new SkillName(package.Manifest.SkillName.Value + "-collision"),
        });
        File.WriteAllText(Path.Combine(skillDirectory, "agent-skill.json"), new SkillManifestJsonSerializer().Serialize(manifest));
    }

    internal static SkillManifest WithComputedManifestDigest (SkillManifest manifest)
    {
        var serializer = new SkillManifestJsonSerializer();
        return new SkillManifestDigestCalculator(serializer)
            .WithComputedManifestDigest(manifest);
    }

    private sealed class CallbackPackageFileList : IReadOnlyList<SkillPackageFile>
    {
        private readonly IReadOnlyList<SkillPackageFile> files;
        private readonly Action callback;

        private bool invoked;

        internal CallbackPackageFileList (
            IReadOnlyList<SkillPackageFile> files,
            Action callback)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public SkillPackageFile this[int index] => files[index];

        public int Count => files.Count;

        public IEnumerator<SkillPackageFile> GetEnumerator ()
        {
            if (!invoked)
            {
                invoked = true;
                callback();
            }

            return files.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return GetEnumerator();
        }
    }
}
