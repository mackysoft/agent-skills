using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Hosts.Contracts;
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

namespace MackySoft.AgentSkills.Tests;

internal static class SkillTestData
{
    internal const string ExpectedCategory = "core";

    internal static readonly string[] ExpectedSkillNames =
    [
        "agent-skills-plan-apply",
        "agent-skills-read-project",
        "agent-skills-troubleshoot",
        "agent-skills-verify-changes",
    ];

    internal static string GetSkillBundleRoot ()
    {
        return Path.Combine(GetRepositoryRoot(), "tests", "Fixtures", "SkillBundle");
    }

    internal static string GetDefinitionsRoot ()
    {
        return Path.Combine(GetSkillBundleRoot(), "definitions");
    }

    internal static string GetGeneratedSkillsRoot ()
    {
        return Path.Combine(GetSkillBundleRoot(), "generated");
    }

    internal static string GetRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Fixtures", "SkillBundle", "bundle.json");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tests/Fixtures/SkillBundle from the test output directory.");
    }

    internal static async Task<IReadOnlyList<CanonicalSkillPackage>> GenerateFixturePackagesAsync ()
    {
        var bundle = await GenerateFixtureBundleAsync();
        return bundle.Packages;
    }

    internal static async Task<CanonicalSkillBundle> GenerateFixtureBundleAsync ()
    {
        var service = CreatePackageGenerationService();
        var result = await service.GenerateAllAsync(GetSkillBundleRoot(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static SkillHostAdapterSet CreateDefaultHostAdapterSet ()
    {
        return new SkillHostAdapterSet();
    }

    internal static SkillPackageGenerationService CreatePackageGenerationService ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var bundleDigestCalculator = new SkillBundleDigestCalculator(manifestSerializer);
        var hostAdapters = CreateDefaultHostAdapterSet();
        var digestCalculator = new SkillDigestCalculator();
        var manifestFactory = new SkillManifest.Factory(
            hostAdapters,
            new SkillManifestDigestCalculator(manifestSerializer));
        var packageFactory = new CanonicalSkillPackage.Factory(
            hostAdapters,
            digestCalculator,
            manifestSerializer);
        return new SkillPackageGenerationService(
            new SkillBundleDefinitionReader(new SkillBundleJsonSerializer()),
            new SkillSourceDefinitionReader(),
            hostAdapters,
            digestCalculator,
            manifestSerializer,
            manifestFactory,
            packageFactory,
            bundleDigestCalculator,
            new CanonicalSkillBundle.Factory(bundleDigestCalculator));
    }

    internal static CanonicalSkillPackageReader CreatePackageReader ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new CanonicalSkillPackageReader(
            manifestSerializer,
            CreateManifestFactory(manifestSerializer),
            CreateCanonicalPackageFactory(manifestSerializer));
    }

    internal static CanonicalSkillPackageWriter CreateCanonicalPackageWriter ()
    {
        return new CanonicalSkillPackageWriter();
    }

    internal static CanonicalSkillPackage CreateCanonicalPackage (
        SkillManifest manifest,
        IReadOnlyList<SkillPackageFile> files)
    {
        var result = CreateCanonicalPackageFactory().CreateCanonical(
            new CanonicalSkillPackageCandidate(manifest, files));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static CanonicalSkillBundle CreateCanonicalBundle (
        SkillBundleDescriptor descriptor,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var result = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(manifestSerializer))
            .CreateCanonical(new CanonicalSkillBundleCandidate(descriptor, packages));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    private static CanonicalSkillPackage.Factory CreateCanonicalPackageFactory (
        SkillManifestJsonSerializer? manifestSerializer = null)
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        manifestSerializer ??= new SkillManifestJsonSerializer();
        return new CanonicalSkillPackage.Factory(
            hostAdapters,
            new SkillDigestCalculator(),
            manifestSerializer);
    }

    internal static SkillManifest.Factory CreateManifestFactory (SkillManifestJsonSerializer? manifestSerializer = null)
    {
        manifestSerializer ??= new SkillManifestJsonSerializer();
        return new SkillManifest.Factory(
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

    internal static SkillInstallService CreateInstallService (ISkillMaterializedPackageWriter? packageWriter = null)
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillInstallService(
            new SkillInstallTargetResolver(hostAdapters, CreateUserTargetRootResolver()),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            packageWriter ?? CreatePackageWriter(),
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

    internal static SkillUninstallService CreateUninstallService (ISkillInstalledPackageRemover? packageRemover = null)
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillUninstallService(
            new SkillInstallTargetResolver(hostAdapters, CreateUserTargetRootResolver()),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            packageRemover ?? CreatePackageRemover(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillPruneService CreatePruneService (ISkillInstalledPackageRemover? packageRemover = null)
    {
        var hostAdapters = CreateDefaultHostAdapterSet();
        return new SkillPruneService(
            new SkillInstallTargetResolver(hostAdapters, CreateUserTargetRootResolver()),
            CreateInstalledManifestReader(hostAdapters),
            CreateInstalledPackageIntegrityVerifier(hostAdapters),
            packageRemover ?? CreatePackageRemover(),
            new SkillMaterializedPackageDiffBuilder());
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
                ? new SkillPackageFile("SKILL.md", file.Content + "\nFixture update.\n")
                : file)
            .ToArray();
        var contentDigest = new SkillDigestCalculator().ComputeDigest(files
            .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));
        var manifestCandidate = CopyManifest(
            package.Manifest,
            skillBundleVersion: package.Manifest.SkillBundleVersion + 1,
            contentDigest: contentDigest);
        var manifest = WithComputedManifestDigest(manifestCandidate);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        files = files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile("agent-skill.json", manifestText)
                : file)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    internal static CanonicalSkillPackage CreateOrdinalSensitivePackage ()
    {
        const string SkillName = "ordinal-culture-contract";
        const string DisplayName = "Ordinal Culture Contract";
        const string Description = "Use this skill to verify ordinal package ordering.";

        var bodyFile = new SkillPackageFile("SKILL.md", "# Ordinal Culture Contract\n");
        var referenceFiles = new[]
        {
            new SkillPackageFile("references/a.md", "lowercase reference\n"),
            new SkillPackageFile("references/B.md", "uppercase reference\n"),
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
            var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
            if (metadataArtifactPath is null)
            {
                hostArtifacts.Add(new SkillHostArtifactManifest(
                    adapter.Descriptor.Host,
                    null,
                    null,
                    frontmatterDigest));
                continue;
            }

            Assert.NotNull(artifacts.MetadataContent);
            hostArtifacts.Add(new SkillHostArtifactManifest(
                adapter.Descriptor.Host,
                metadataArtifactPath,
                digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent),
                frontmatterDigest));
            hostArtifactFiles.Add(new SkillPackageFile(metadataArtifactPath, artifacts.MetadataContent));
        }

        var manifest = WithComputedManifestDigest(new SkillManifestCandidate(
            SkillManifest.CurrentSchemaVersion,
            1,
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillCategory(ExpectedCategory),
            new SkillName(SkillName),
            DisplayName,
            Description,
            [],
            contentDigest,
            null,
            hostArtifacts));
        var manifestFile = new SkillPackageFile("agent-skill.json", new SkillManifestJsonSerializer().Serialize(manifest));
        var files = new[] { bodyFile, manifestFile }
            .Concat(referenceFiles)
            .Concat(hostArtifactFiles)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    internal static CanonicalSkillPackage CreatePackageWithUpdatedOpenAiMetadata (CanonicalSkillPackage package)
    {
        var manifestCandidate = CopyManifest(
            package.Manifest,
            skillBundleVersion: package.Manifest.SkillBundleVersion + 1,
            displayName: package.Manifest.DisplayName + " Updated");
        var metadata = new SkillHostMetadata(manifestCandidate.SkillName, manifestCandidate.DisplayName, manifestCandidate.Description);
        var hostAdapters = CreateDefaultHostAdapterSet();
        var digestCalculator = new SkillDigestCalculator();
        string? openAiMetadata = null;
        var hostArtifacts = new List<SkillHostArtifactManifest>();
        foreach (var artifact in manifestCandidate.HostArtifacts.OrderBy(static artifact => artifact.Host))
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

            if (adapter.Descriptor.Host == SkillHostKind.OpenAi)
            {
                openAiMetadata = artifacts.MetadataContent;
            }

            hostArtifacts.Add(new SkillHostArtifactManifest(
                adapter.Descriptor.Host,
                metadataArtifactPath,
                metadataDigest,
                frontmatterDigest));
        }

        manifestCandidate = CopyManifest(manifestCandidate, hostArtifacts: hostArtifacts);
        var manifest = WithComputedManifestDigest(manifestCandidate);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file =>
            {
                if (string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal))
                {
                    return new SkillPackageFile("agent-skill.json", manifestText);
                }

                if (string.Equals(file.RelativePath, "agents/openai.yaml", StringComparison.Ordinal))
                {
                    Assert.NotNull(openAiMetadata);
                    return new SkillPackageFile("agents/openai.yaml", openAiMetadata!);
                }

                return file;
            })
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    internal static CanonicalSkillPackage CreatePackageWithSkillBundleVersion (
        CanonicalSkillPackage package,
        int skillBundleVersion)
    {
        var manifest = WithComputedManifestDigest(CopyManifest(
            package.Manifest,
            skillBundleVersion: skillBundleVersion));
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile("agent-skill.json", manifestText)
                : file)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
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
            new SkillHostMaterializationInspector(hostAdapters, new SkillDigestCalculator()),
            new SkillDigestCalculator());
    }

    internal static SkillInstalledManifestReader CreateInstalledManifestReader (SkillHostAdapterSet hostAdapters)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillInstalledManifestReader(
            manifestSerializer,
            new SkillManifest.Factory(hostAdapters, new SkillManifestDigestCalculator(manifestSerializer)));
    }

    internal static void TamperManifestDigest (string manifestPath)
    {
        var manifestText = File.ReadAllText(manifestPath);
        var manifest = new SkillManifestJsonSerializer().Deserialize(manifestText);
        var manifestDigest = manifest.ManifestDigest!.ToString();
        var replacementDigest = string.Equals(manifestDigest, new string('f', 64), StringComparison.Ordinal)
            ? new string('0', 64)
            : new string('f', 64);
        File.WriteAllText(manifestPath, manifestText.Replace(manifestDigest, replacementDigest, StringComparison.Ordinal));
    }

    internal static void WriteNameCollisionManifest (string targetRoot, CanonicalSkillPackage package)
    {
        var skillDirectory = Path.Combine(targetRoot, package.Manifest.SkillName.Value);
        Directory.CreateDirectory(skillDirectory);
        var manifest = WithComputedManifestDigest(CopyManifest(
            package.Manifest,
            skillName: new SkillName(package.Manifest.SkillName.Value + "-collision")));
        File.WriteAllText(Path.Combine(skillDirectory, "agent-skill.json"), new SkillManifestJsonSerializer().Serialize(manifest));
    }

    internal static SkillManifest WithComputedManifestDigest (SkillManifest manifest)
    {
        return WithComputedManifestDigest(new SkillManifestCandidate(
            manifest.SchemaVersion,
            manifest.SkillBundleVersion,
            manifest.CatalogId,
            manifest.Category,
            manifest.SkillName,
            manifest.DisplayName,
            manifest.Description,
            manifest.Dependencies,
            manifest.ContentDigest,
            null,
            manifest.HostArtifacts));
    }

    internal static SkillManifest WithComputedManifestDigest (SkillManifestCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var withoutDeclaredDigest = new SkillManifestCandidate(
            candidate.SchemaVersion,
            candidate.SkillBundleVersion,
            candidate.CatalogId,
            candidate.Category,
            candidate.SkillName,
            candidate.DisplayName,
            candidate.Description,
            candidate.Dependencies,
            candidate.ContentDigest,
            null,
            candidate.HostArtifacts);
        var result = CreateManifestFactory().CreateCanonical(withoutDeclaredDigest);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static SkillManifestCandidate CopyManifest (
        SkillManifest source,
        int? schemaVersion = null,
        int? skillBundleVersion = null,
        SkillCatalogId? catalogId = null,
        SkillCategory? category = null,
        SkillName? skillName = null,
        string? displayName = null,
        string? description = null,
        IReadOnlyList<SkillName>? dependencies = null,
        Sha256Digest? contentDigest = null,
        Sha256Digest? manifestDigest = null,
        IReadOnlyList<SkillHostArtifactManifest>? hostArtifacts = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new SkillManifestCandidate(
            schemaVersion ?? source.SchemaVersion,
            skillBundleVersion ?? source.SkillBundleVersion,
            catalogId ?? source.CatalogId,
            category ?? source.Category,
            skillName ?? source.SkillName,
            displayName ?? source.DisplayName,
            description ?? source.Description,
            dependencies ?? source.Dependencies,
            contentDigest ?? source.ContentDigest,
            manifestDigest ?? source.ManifestDigest,
            hostArtifacts ?? source.HostArtifacts);
    }

    internal static SkillManifestCandidate CopyManifest (
        SkillManifestCandidate source,
        int? schemaVersion = null,
        int? skillBundleVersion = null,
        SkillCatalogId? catalogId = null,
        SkillCategory? category = null,
        SkillName? skillName = null,
        string? displayName = null,
        string? description = null,
        IReadOnlyList<SkillName>? dependencies = null,
        Sha256Digest? contentDigest = null,
        Sha256Digest? manifestDigest = null,
        IReadOnlyList<SkillHostArtifactManifest>? hostArtifacts = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new SkillManifestCandidate(
            schemaVersion ?? source.SchemaVersion,
            skillBundleVersion ?? source.SkillBundleVersion,
            catalogId ?? source.CatalogId,
            category ?? source.Category,
            skillName ?? source.SkillName,
            displayName ?? source.DisplayName,
            description ?? source.Description,
            dependencies ?? source.Dependencies,
            contentDigest ?? source.ContentDigest,
            manifestDigest ?? source.ManifestDigest,
            hostArtifacts ?? source.HostArtifacts);
    }

}
