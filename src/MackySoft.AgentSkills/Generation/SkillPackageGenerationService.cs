using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.FileSystem;
using MackySoft.AgentSkills.Shared.Text;
using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Generation;

/// <summary> Generates canonical host-independent SKILL packages from source definitions. </summary>
public sealed class SkillPackageGenerationService
{
    private readonly SkillBundleDefinitionReader bundleReader;
    private readonly SkillSourceDefinitionReader sourceReader;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillDigestCalculator digestCalculator;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifest.Factory manifestFactory;
    private readonly CanonicalSkillPackage.Factory packageFactory;
    private readonly SkillBundleDigestCalculator bundleDigestCalculator;
    private readonly CanonicalSkillBundle.Factory bundleFactory;

    /// <summary> Initializes a new instance of the <see cref="SkillPackageGenerationService" /> class. </summary>
    /// <param name="bundleReader"> The authored bundle definition reader. </param>
    /// <param name="sourceReader"> The source skill definition reader. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestFactory"> The canonical manifest construction boundary. </param>
    /// <param name="packageFactory"> The canonical package construction boundary. </param>
    /// <param name="bundleDigestCalculator"> The version-independent bundle digest calculator. </param>
    /// <param name="bundleFactory"> The canonical bundle construction boundary. </param>
    public SkillPackageGenerationService (
        SkillBundleDefinitionReader bundleReader,
        SkillSourceDefinitionReader sourceReader,
        SkillHostAdapterSet hostAdapters,
        SkillDigestCalculator digestCalculator,
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifest.Factory manifestFactory,
        CanonicalSkillPackage.Factory packageFactory,
        SkillBundleDigestCalculator bundleDigestCalculator,
        CanonicalSkillBundle.Factory bundleFactory)
    {
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestFactory = manifestFactory ?? throw new ArgumentNullException(nameof(manifestFactory));
        this.packageFactory = packageFactory ?? throw new ArgumentNullException(nameof(packageFactory));
        this.bundleDigestCalculator = bundleDigestCalculator ?? throw new ArgumentNullException(nameof(bundleDigestCalculator));
        this.bundleFactory = bundleFactory ?? throw new ArgumentNullException(nameof(bundleFactory));
    }

    /// <summary> Generates a complete canonical bundle from one fixed-layout source bundle root. </summary>
    /// <param name="bundleRoot"> The root containing authored <c>bundle.json</c> and <c>definitions</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The generated descriptor and complete canonical package set, or a source validation failure. </returns>
    internal async ValueTask<SkillOperationResult<CanonicalSkillBundle>> GenerateAllAsync (
        string bundleRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceResult = await ReadSourceAsync(bundleRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return GenerationFailure(sourceResult.Failure!);
        }

        var source = sourceResult.Value!;
        return SkillOperationResult<CanonicalSkillBundle>.Success(
            GenerateAll(source, source.BundleDefinition.SkillBundleVersion));
    }

    /// <summary> Reads one validated source snapshot without generating package output. </summary>
    /// <param name="bundleRoot"> The root containing authored <c>bundle.json</c> and <c>definitions</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access. </param>
    /// <returns> The validated source snapshot, or a source validation failure. </returns>
    internal async ValueTask<SkillOperationResult<SkillPackageGenerationSource>> ReadSourceAsync (
        string bundleRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var bundleResult = await bundleReader.ReadAsync(bundleRoot, cancellationToken).ConfigureAwait(false);
        if (!bundleResult.IsSuccess)
        {
            return SourceFailure(bundleResult.Failure!);
        }

        var fullBundleRoot = Path.GetFullPath(bundleRoot);
        var definitionsRootResult = SkillPathBoundary.ResolveUnderRoot(
            fullBundleRoot,
            Path.Combine(fullBundleRoot, "definitions"),
            SkillFailureCodes.SourceInvalid,
            "SKILL bundle definitions path");
        if (!definitionsRootResult.IsSuccess)
        {
            return SourceFailure(definitionsRootResult.Failure!);
        }

        var definitionsRoot = definitionsRootResult.Value!;
        var sourceResult = await sourceReader.ReadAllAsync(definitionsRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return SourceFailure(sourceResult.Failure!);
        }

        if (sourceResult.Value!.Count == 0)
        {
            return SkillOperationResult<SkillPackageGenerationSource>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"SKILL definitions directory does not contain any definitions: {definitionsRoot}");
        }

        var dependencyReferenceResult = SkillSourceDependencyReferenceValidator.Validate(sourceResult.Value);
        if (!dependencyReferenceResult.IsSuccess)
        {
            return SourceFailure(dependencyReferenceResult.Failure!);
        }

        return SkillOperationResult<SkillPackageGenerationSource>.Success(
            new SkillPackageGenerationSource(bundleResult.Value!, sourceResult.Value));
    }

    /// <summary> Generates one canonical bundle version from a previously validated source snapshot. </summary>
    /// <param name="source"> The source snapshot whose content and bundle identity remain fixed. </param>
    /// <param name="skillBundleVersion"> The positive release version stamped into every generated package. </param>
    /// <returns> The complete canonical bundle for the requested release version. </returns>
    internal CanonicalSkillBundle GenerateAll (
        SkillPackageGenerationSource source,
        SkillBundleVersion skillBundleVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(skillBundleVersion);

        var authoredBundle = source.BundleDefinition;
        var bundle = authoredBundle.SkillBundleVersion == skillBundleVersion
            ? authoredBundle
            : new SkillBundleDefinition(authoredBundle.SchemaVersion, authoredBundle.CatalogId, skillBundleVersion);
        var packages = source.Definitions
            .Select(definition => Generate(bundle, definition))
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray();
        var descriptor = new SkillBundleDescriptor(
            SkillBundleDefinition.CurrentSchemaVersion,
            bundle.CatalogId,
            bundle.SkillBundleVersion,
            bundleDigestCalculator.ComputeDigest(packages));

        var bundleResult = bundleFactory.CreateCanonical(new CanonicalSkillBundleCandidate(descriptor, packages));
        if (!bundleResult.IsSuccess)
        {
            throw new InvalidOperationException($"Generated SKILL bundle violated its canonical contract: {bundleResult.Failure!.Message}");
        }

        return bundleResult.Value!;
    }

    /// <summary> Generates one canonical package from one source definition. </summary>
    /// <param name="bundle"> The authored bundle identity and release version to stamp into the package. </param>
    /// <param name="definition"> The source definition. </param>
    /// <returns> The canonical package. </returns>
    internal CanonicalSkillPackage Generate (
        SkillBundleDefinition bundle,
        SkillSourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(definition);

        var bodyFile = new SkillPackageFile("SKILL.md", CreateSkillBody(definition));
        var referenceFiles = definition.References
            .OrderBy(static reference => reference.FileName, StringComparer.Ordinal)
            .Select(static reference => new SkillPackageFile($"references/{reference.FileName}", reference.Template))
            .ToArray();

        var contentDigest = digestCalculator.ComputeDigest(
            new[] { new SkillDigestInputFile(bodyFile.RelativePath, bodyFile.Content) }
                .Concat(referenceFiles.Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content))));

        var hostArtifactOutputs = CreateHostArtifactOutputs(definition.Metadata)
            .OrderBy(static artifact => artifact.Manifest.Host)
            .ToArray();
        var hostArtifacts = hostArtifactOutputs
            .Select(static artifact => artifact.Manifest)
            .ToArray();

        var manifestCandidate = new SkillManifestCandidate(
            SkillManifest.CurrentSchemaVersion,
            bundle.SkillBundleVersion,
            bundle.CatalogId,
            definition.Metadata.Category,
            definition.Metadata.SkillName,
            definition.Metadata.DisplayName,
            definition.Metadata.Description,
            definition.Metadata.Dependencies.OrderBy(static dependency => dependency.Value, StringComparer.Ordinal).ToArray(),
            contentDigest,
            null,
            hostArtifacts);
        var manifestResult = manifestFactory.CreateCanonical(manifestCandidate);
        if (!manifestResult.IsSuccess)
        {
            throw new InvalidOperationException($"Generated SKILL manifest violated its canonical contract: {manifestResult.Failure!.Message}");
        }

        var manifest = manifestResult.Value!;

        var manifestFile = new SkillPackageFile("agent-skill.json", manifestSerializer.Serialize(manifest));
        var hostArtifactFiles = hostArtifactOutputs
            .SelectMany(static artifact => artifact.Files)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var files = new[] { bodyFile, manifestFile }
            .Concat(referenceFiles)
            .Concat(hostArtifactFiles)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();

        var packageResult = packageFactory.CreateCanonical(new CanonicalSkillPackageCandidate(manifest, files));
        if (!packageResult.IsSuccess)
        {
            throw new InvalidOperationException($"Generated SKILL package violated its canonical contract: {packageResult.Failure!.Message}");
        }

        return packageResult.Value!;
    }

    private static SkillOperationResult<CanonicalSkillBundle> GenerationFailure (SkillFailure failure)
    {
        return SkillOperationResult<CanonicalSkillBundle>.FailureResult(failure.Code, failure.Message);
    }

    private static SkillOperationResult<SkillPackageGenerationSource> SourceFailure (SkillFailure failure)
    {
        return SkillOperationResult<SkillPackageGenerationSource>.FailureResult(failure.Code, failure.Message);
    }

    private static string CreateSkillBody (SkillSourceDefinition definition)
    {
        var body = SkillTextNormalizer.NormalizeToLf(definition.SkillTemplate).TrimStart('\n');
        return $"# {definition.Metadata.SkillName.Value}\n\n{body}";
    }

    private IEnumerable<GeneratedHostArtifactOutput> CreateHostArtifactOutputs (SkillSourceMetadata metadata)
    {
        var hostMetadata = new SkillHostMetadata(metadata.SkillName, metadata.DisplayName, metadata.Description);
        foreach (var adapter in hostAdapters.Adapters)
        {
            var artifacts = adapter.BuildArtifacts(hostMetadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", artifacts.Frontmatter);
            var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;

            if (metadataArtifactPath is null)
            {
                if (artifacts.MetadataContent is not null)
                {
                    throw new InvalidOperationException($"Host adapter '{ContractLiteralCodec.ToValue(adapter.Descriptor.Host)}' must not emit metadata artifacts.");
                }

                yield return new GeneratedHostArtifactOutput(
                    new SkillHostArtifactManifest(adapter.Descriptor.Host, null, null, frontmatterDigest),
                    []);
                continue;
            }

            if (artifacts.MetadataContent is null)
            {
                throw new InvalidOperationException($"Host adapter '{ContractLiteralCodec.ToValue(adapter.Descriptor.Host)}' must emit metadata artifact '{metadataArtifactPath}'.");
            }

            yield return new GeneratedHostArtifactOutput(
                new SkillHostArtifactManifest(
                    adapter.Descriptor.Host,
                    metadataArtifactPath,
                    digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent),
                    frontmatterDigest),
                [new SkillPackageFile(metadataArtifactPath, artifacts.MetadataContent)]);
        }
    }

    private sealed class GeneratedHostArtifactOutput
    {
        internal GeneratedHostArtifactOutput (
            SkillHostArtifactManifest manifest,
            IReadOnlyList<SkillPackageFile> files)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            ArgumentNullException.ThrowIfNull(files);
            if (files.Any(static file => file is null))
            {
                throw new ArgumentException("Generated host artifact output must not contain null files.", nameof(files));
            }

            Files = Array.AsReadOnly(files.ToArray());
        }

        internal SkillHostArtifactManifest Manifest { get; }

        internal IReadOnlyList<SkillPackageFile> Files { get; }
    }
}
