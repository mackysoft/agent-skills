using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Diffing;
using MackySoft.AgentDistribution.Installation.Inventory;
using MackySoft.AgentDistribution.Installation.Services;
using MackySoft.AgentDistribution.Installation.State;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Transactions;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.AgentDistribution.Skills.Generation;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;
using MackySoft.AgentDistribution.Sources;

namespace MackySoft.AgentDistribution.Tests;

internal static class SkillTestData
{
    internal const string ExpectedCategory = "core";

    internal static readonly string[] ExpectedSkillNames =
    [
        "agent-distribution-plan-apply",
        "agent-distribution-read-project",
        "agent-distribution-troubleshoot",
        "agent-distribution-verify-changes",
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

    internal static SkillInstallRequest CreateInstallRequest (
        HostKind host,
        SkillScopeKind scope,
        string? repositoryRoot,
        string? targetRoot = null)
    {
        var repositoryPath = repositoryRoot is null
            ? null
            : AbsolutePath.Parse(Path.GetFullPath(repositoryRoot));
        return new SkillInstallRequest(
            host,
            scope,
            repositoryPath,
            ResolveTargetPath(repositoryPath, targetRoot));
    }

    internal static AgentTargetRequest CreateAgentTargetRequest (
        HostKind host,
        AgentInstallScopeKind scope,
        string? repositoryRoot,
        string? targetRoot = null)
    {
        var repositoryPath = repositoryRoot is null
            ? null
            : AbsolutePath.Parse(Path.GetFullPath(repositoryRoot));
        return new AgentTargetRequest(
            host,
            scope,
            repositoryPath,
            ResolveTargetPath(repositoryPath, targetRoot));
    }

    private static AbsolutePath? ResolveTargetPath (AbsolutePath? repositoryRoot, string? targetRoot)
    {
        if (targetRoot is null)
        {
            return null;
        }

        if (AbsolutePath.TryParse(targetRoot, out var absoluteTargetRoot, out _))
        {
            return absoluteTargetRoot;
        }

        if (repositoryRoot is null)
        {
            throw new ArgumentException("A relative test target requires a repository root.", nameof(targetRoot));
        }

        return ContainedPath.Create(repositoryRoot, RootRelativePath.Parse(targetRoot)).Target;
    }

    internal static async Task<IReadOnlyList<CanonicalSkillPackage>> GenerateFixturePackagesAsync ()
    {
        var bundle = await GenerateFixtureBundleAsync();
        return bundle.Packages;
    }

    internal static async Task<CanonicalSkillBundle> GenerateFixtureBundleAsync ()
    {
        var service = CreatePackageGenerationService();
        var result = await service.GenerateAllAsync(AbsolutePath.Parse(GetSkillBundleRoot()), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static SkillPackageGenerationService CreatePackageGenerationService ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var bundleDigestCalculator = new SkillBundleDigestCalculator(manifestSerializer);
        var digestCalculator = new PackageContentDigestCalculator();
        var manifestFactory = new SkillManifest.Factory(new SkillManifestDigestCalculator(manifestSerializer));
        var packageFactory = new CanonicalSkillPackage.Factory(
            digestCalculator,
            manifestSerializer);
        return new SkillPackageGenerationService(
            new SkillBundleDefinitionReader(new SkillBundleJsonSerializer()),
            new SkillSourceDefinitionReader(),
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
        IReadOnlyList<PackageTextFile> files)
    {
        var result = CreateCanonicalPackageFactory().CreateCanonical(
            new CanonicalSkillPackageCandidate(manifest, files));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static CanonicalSkillPackage CreatePackageWithDeclaredFrontmatterDigest (
        CanonicalSkillPackage package,
        HostKind host,
        Sha256Digest frontmatterDigest)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(frontmatterDigest);

        var manifestCandidate = CopyManifest(
            package.Manifest,
            hostArtifacts: package.Manifest.HostArtifacts
                .Select(artifact => artifact.Host == host
                    ? new SkillHostArtifactManifest(
                        artifact.Host,
                        artifact.Path,
                        artifact.Digest,
                        frontmatterDigest)
                    : artifact)
                .ToArray());
        var manifest = WithComputedManifestDigest(manifestCandidate);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(file.RelativePath, manifestText)
                : file)
            .ToArray();
        return CreateCanonicalPackage(manifest, files);
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
        manifestSerializer ??= new SkillManifestJsonSerializer();
        return new CanonicalSkillPackage.Factory(
            new PackageContentDigestCalculator(),
            manifestSerializer);
    }

    internal static SkillManifest.Factory CreateManifestFactory (SkillManifestJsonSerializer? manifestSerializer = null)
    {
        manifestSerializer ??= new SkillManifestJsonSerializer();
        return new SkillManifest.Factory(new SkillManifestDigestCalculator(manifestSerializer));
    }

    internal static SkillMaterializationService CreateMaterializationService ()
    {
        return new SkillMaterializationService();
    }

    internal static SkillExportService CreateExportService ()
    {
        return new SkillExportService(CreateMaterializationService());
    }

    internal static SkillInstallService CreateInstallService (ISkillMaterializedPackageWriter? packageWriter = null)
    {
        var installedPackageValidator = CreateInstalledPackageValidator();
        return new SkillInstallService(
            CreateCatalogTargetRootSelector(),
            new SkillMaterializationService(),
            new SkillInstalledTargetStateAnalyzer(
                CreateInstalledManifestReader(),
                installedPackageValidator,
                CreateInstalledPackageIntegrityVerifier()),
            packageWriter ?? CreatePackageWriter(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillUpdateService CreateUpdateService (ISkillMaterializedPackageWriter? packageWriter = null)
    {
        var installedPackageValidator = CreateInstalledPackageValidator();
        return new SkillUpdateService(
            CreateCatalogTargetRootSelector(),
            new SkillMaterializationService(),
            new SkillInstalledTargetStateAnalyzer(
                CreateInstalledManifestReader(),
                installedPackageValidator,
                CreateInstalledPackageIntegrityVerifier()),
            packageWriter ?? CreatePackageWriter(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillUninstallService CreateUninstallService (ISkillInstalledPackageRemover? packageRemover = null)
    {
        var installedPackageValidator = CreateInstalledPackageValidator();
        return new SkillUninstallService(
            CreateCatalogTargetRootSelector(),
            new SkillInstalledTargetStateAnalyzer(
                CreateInstalledManifestReader(),
                installedPackageValidator,
                CreateInstalledPackageIntegrityVerifier()),
            packageRemover ?? CreatePackageRemover(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillPruneService CreatePruneService (ISkillInstalledPackageRemover? packageRemover = null)
    {
        return new SkillPruneService(
            CreateCatalogTargetRootSelector(),
            CreateInstalledManifestReader(),
            CreateInstalledPackageIntegrityVerifier(),
            packageRemover ?? CreatePackageRemover(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillInstallationScanner CreateInstallationScanner ()
    {
        return new SkillInstallationScanner(
            CreateInstalledManifestReader(),
            CreateInstalledPackageValidator());
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

    internal static SkillInstallTargetResolver CreateInstallTargetResolver ()
    {
        return new SkillInstallTargetResolver(CreateUserTargetRootResolver());
    }

    internal static SkillCatalogTargetRootSelector CreateCatalogTargetRootSelector ()
    {
        return new SkillCatalogTargetRootSelector(
            CreateInstallTargetResolver(),
            CreateInstalledManifestReader());
    }

    internal static SkillDoctorService CreateDoctorService ()
    {
        return new SkillDoctorService(CreateTargetStateAnalyzer());
    }

    internal static SkillInstalledTargetStateAnalyzer CreateTargetStateAnalyzer ()
    {
        return new SkillInstalledTargetStateAnalyzer(
            CreateInstalledManifestReader(),
            CreateInstalledPackageValidator(),
            CreateInstalledPackageIntegrityVerifier());
    }

    internal static IReadOnlyList<CanonicalSkillPackage> ReplacePackage (
        IReadOnlyList<CanonicalSkillPackage> packages,
        CanonicalSkillPackage replacement)
    {
        return packages
            .Select(package => string.Equals(package.Manifest.SkillName.Value, replacement.Manifest.SkillName.Value, StringComparison.Ordinal) ? replacement : package)
            .ToArray();
    }

    internal static CanonicalSkillPackage CreatePackageWithUpdatedBody (
        CanonicalSkillPackage package,
        int? skillBundleVersion = null)
    {
        var files = package.Files
            .Select(static file => string.Equals(file.RelativePath.Value, "SKILL.md", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), file.Content + "\nFixture update.\n")
                : file)
            .ToArray();
        var contentDigest = ComputeSkillContentDigest(files);
        var manifestCandidate = CopyManifest(
            package.Manifest,
            skillBundleVersion: skillBundleVersion ?? package.Manifest.SkillBundleVersion.Next().Value,
            contentDigest: contentDigest);
        var manifest = WithComputedManifestDigest(manifestCandidate);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        files = files
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText)
                : file)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    internal static CanonicalSkillPackage CreatePackageWithScripts (
        CanonicalSkillPackage package,
        IReadOnlyList<PackageTextFile> scripts,
        int? skillBundleVersion = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(scripts);

        var scriptSnapshot = scripts.ToArray();
        if (scriptSnapshot.Any(static script => script is null))
        {
            throw new ArgumentException("Scripts must not contain null items.", nameof(scripts));
        }

        var filesWithoutManifest = package.Files
            .Where(static file => !string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal))
            .Concat(scriptSnapshot)
            .ToArray();
        var manifest = WithComputedManifestDigest(CopyManifest(
            package.Manifest,
            skillBundleVersion: skillBundleVersion ?? package.Manifest.SkillBundleVersion.Value,
            contentDigest: ComputeSkillContentDigest(filesWithoutManifest)));
        var manifestFile = new PackageTextFile(
            PackageRelativePath.Parse("agent-skill.json"),
            new SkillManifestJsonSerializer().Serialize(manifest));

        return CreateCanonicalPackage(
            manifest,
            filesWithoutManifest
                .Append(manifestFile)
                .OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal)
                .ToArray());
    }

    internal static CanonicalSkillPackage CreatePackageWithCatalogId (
        CanonicalSkillPackage package,
        AgentDistributionCatalogId catalogId)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(catalogId);

        var manifest = WithComputedManifestDigest(CopyManifest(
            package.Manifest,
            catalogId: catalogId));
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText)
                : file)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    private static Sha256Digest ComputeSkillContentDigest (IEnumerable<PackageTextFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return new PackageContentDigestCalculator().ComputeDigest(files
            .Where(static file => string.Equals(file.RelativePath.Value, "SKILL.md", StringComparison.Ordinal)
                || file.RelativePath.Value.StartsWith("references/", StringComparison.Ordinal)
                || file.RelativePath.Value.StartsWith("scripts/", StringComparison.Ordinal))
            .Select(static file => new PackageContentDigestInputFile(file.RelativePath, file.Content)));
    }

    internal static CanonicalSkillPackage CreateOrdinalSensitivePackage ()
    {
        const string SkillName = "ordinal-culture-contract";
        const string DisplayName = "Ordinal Culture Contract";
        const string Description = "Use this skill to verify ordinal package ordering.";

        var bodyFile = new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Ordinal Culture Contract\n");
        var referenceFiles = new[]
        {
            new PackageTextFile(PackageRelativePath.Parse("references/a.md"), "lowercase reference\n"),
            new PackageTextFile(PackageRelativePath.Parse("references/B.md"), "uppercase reference\n"),
        };
        var digestCalculator = new PackageContentDigestCalculator();
        var contentDigest = digestCalculator.ComputeDigest(
            new[] { new PackageContentDigestInputFile(bodyFile.RelativePath, bodyFile.Content) }
                .Concat(referenceFiles.Select(static file => new PackageContentDigestInputFile(file.RelativePath, file.Content))));
        var metadata = new SkillHostMetadata(new SkillName(SkillName), DisplayName, Description);
        var hostArtifacts = new List<SkillHostArtifactManifest>();
        var hostArtifactFiles = new List<PackageTextFile>();

        foreach (var registration in BuiltInHostCatalog.Registrations)
        {
            var adapter = registration.SkillAdapter;
            var artifacts = adapter.BuildArtifacts(metadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("SKILL.md.frontmatter"), artifacts.Frontmatter);
            var metadataArtifactPath = registration.Skill.MetadataArtifactPath;
            if (metadataArtifactPath is null)
            {
                hostArtifacts.Add(new SkillHostArtifactManifest(
                    registration.Host,
                    null,
                    null,
                    frontmatterDigest));
                continue;
            }

            Assert.NotNull(artifacts.MetadataContent);
            hostArtifacts.Add(new SkillHostArtifactManifest(
                    registration.Host,
                    metadataArtifactPath,
                    digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent),
                    frontmatterDigest));
            hostArtifactFiles.Add(new PackageTextFile(metadataArtifactPath, artifacts.MetadataContent));
        }

        var manifest = WithComputedManifestDigest(new SkillManifestCandidate(
            SkillManifest.CurrentSchemaVersion,
            new SkillBundleVersion(1),
            new AgentDistributionCatalogId("com.mackysoft.agent-distribution"),
            new SkillCategory(ExpectedCategory),
            new SkillName(SkillName),
            DisplayName,
            Description,
            [],
            contentDigest,
            null,
            hostArtifacts));
        var manifestFile = new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), new SkillManifestJsonSerializer().Serialize(manifest));
        var files = new[] { bodyFile, manifestFile }
            .Concat(referenceFiles)
            .Concat(hostArtifactFiles)
            .OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    internal static CanonicalSkillPackage CreatePackageWithUpdatedOpenAiMetadata (CanonicalSkillPackage package)
    {
        var manifestCandidate = CopyManifest(
            package.Manifest,
            skillBundleVersion: package.Manifest.SkillBundleVersion.Next().Value,
            displayName: package.Manifest.DisplayName + " Updated");
        var metadata = new SkillHostMetadata(manifestCandidate.SkillName, manifestCandidate.DisplayName, manifestCandidate.Description);
        var digestCalculator = new PackageContentDigestCalculator();
        string? openAiMetadata = null;
        var hostArtifacts = new List<SkillHostArtifactManifest>();
        foreach (var artifact in manifestCandidate.HostArtifacts.OrderBy(static artifact => artifact.Host))
        {
            var registrationResult = BuiltInHostCatalog.Get(artifact.Host);
            Assert.True(registrationResult.IsSuccess, registrationResult.Failure?.Message);
            var registration = registrationResult.Value!;
            var adapter = registration.SkillAdapter;
            var artifacts = adapter.BuildArtifacts(metadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("SKILL.md.frontmatter"), artifacts.Frontmatter);
            var metadataArtifactPath = registration.Skill.MetadataArtifactPath;
            var metadataDigest = artifacts.MetadataContent is null || metadataArtifactPath is null
                ? null
                : digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent);

            if (registration.Host == HostKind.Codex)
            {
                openAiMetadata = artifacts.MetadataContent;
            }

            hostArtifacts.Add(new SkillHostArtifactManifest(
                registration.Host,
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
                if (string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal))
                {
                    return new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText);
                }

                if (string.Equals(file.RelativePath.Value, "agents/openai.yaml", StringComparison.Ordinal))
                {
                    Assert.NotNull(openAiMetadata);
                    return new PackageTextFile(PackageRelativePath.Parse("agents/openai.yaml"), openAiMetadata!);
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
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText)
                : file)
            .ToArray();

        return CreateCanonicalPackage(manifest, files);
    }

    internal static SkillInstalledPackageValidator CreateInstalledPackageValidator ()
    {
        return new SkillInstalledPackageValidator(
            CreateInstalledManifestReader(),
            new SkillMaterializationService(),
            new SkillInstalledContentDigestVerifier(new PackageContentDigestCalculator()),
            new SkillInstalledFileSetVerifier(),
            new SkillHostMaterializationInspector(new PackageContentDigestCalculator()));
    }

    internal static SkillInstalledPackageIntegrityVerifier CreateInstalledPackageIntegrityVerifier ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillInstalledPackageIntegrityVerifier(
            CreateInstalledManifestReader(),
            manifestSerializer,
            new SkillHostMaterializationInspector(new PackageContentDigestCalculator()),
            new PackageContentDigestCalculator());
    }

    internal static SkillInstalledManifestReader CreateInstalledManifestReader ()
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new SkillInstalledManifestReader(
            manifestSerializer,
            new SkillManifest.Factory(new SkillManifestDigestCalculator(manifestSerializer)));
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
        AgentDistributionCatalogId? catalogId = null,
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
            skillBundleVersion is null ? source.SkillBundleVersion : new SkillBundleVersion(skillBundleVersion.Value),
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
        AgentDistributionCatalogId? catalogId = null,
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
            skillBundleVersion is null ? source.SkillBundleVersion : new SkillBundleVersion(skillBundleVersion.Value),
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
