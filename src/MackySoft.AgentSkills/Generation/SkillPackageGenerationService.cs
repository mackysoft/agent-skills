using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Generation;

/// <summary> Generates canonical host-independent SKILL packages from source definitions. </summary>
public sealed class SkillPackageGenerationService
{
    private readonly SkillSourceDefinitionReader sourceReader;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillDigestCalculator digestCalculator;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestDigestCalculator manifestDigestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillPackageGenerationService" /> class. </summary>
    /// <param name="sourceReader"> The source definition reader. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestDigestCalculator"> The manifest digest calculator. </param>
    public SkillPackageGenerationService (
        SkillSourceDefinitionReader sourceReader,
        SkillHostAdapterSet hostAdapters,
        SkillDigestCalculator digestCalculator,
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifestDigestCalculator manifestDigestCalculator)
    {
        this.sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
    }

    /// <summary> Generates all SKILL packages under one source definitions root. </summary>
    /// <param name="definitionsRoot"> The <c>SkillDefinitions</c> directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The canonical packages or validation failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GenerateAllAsync (
        string definitionsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceResult = await sourceReader.ReadAllAsync(definitionsRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                sourceResult.Failure!.Code,
                sourceResult.Failure.Message);
        }

        if (sourceResult.Value!.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"SkillDefinitions directory does not contain any definitions: {definitionsRoot}");
        }

        var packages = sourceResult.Value!
            .Select(Generate)
            .OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal)
            .ToArray();

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(packages);
    }

    /// <summary> Generates one canonical package from one source definition. </summary>
    /// <param name="definition"> The source definition. </param>
    /// <returns> The canonical package. </returns>
    public CanonicalSkillPackage Generate (SkillSourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bodyFile = SkillPackageFile.Create("SKILL.md", definition.SkillTemplate);
        var referenceFiles = definition.References
            .OrderBy(static reference => reference.FileName, StringComparer.Ordinal)
            .Select(static reference => SkillPackageFile.Create($"references/{reference.FileName}", reference.Template))
            .ToArray();

        var contentDigest = digestCalculator.ComputeDigest(
            new[] { new SkillDigestInputFile(bodyFile.RelativePath, bodyFile.Content) }
                .Concat(referenceFiles.Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content))));

        var hostArtifactOutputs = CreateHostArtifactOutputs(definition.Metadata)
            .OrderBy(static artifact => artifact.Manifest.Host, StringComparer.Ordinal)
            .ToArray();
        var hostArtifacts = hostArtifactOutputs
            .Select(static artifact => artifact.Manifest)
            .ToArray();

        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            definition.Metadata.SkillName,
            definition.Metadata.DisplayName,
            definition.Metadata.Description,
            definition.Metadata.Tier,
            contentDigest,
            string.Empty,
            hostArtifacts);
        manifest = manifestDigestCalculator.WithComputedManifestDigest(manifest);

        var manifestFile = SkillPackageFile.Create("agent-skill.json", manifestSerializer.Serialize(manifest));
        var hostArtifactFiles = hostArtifactOutputs
            .SelectMany(static artifact => artifact.Files)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var files = new[] { bodyFile, manifestFile }
            .Concat(referenceFiles)
            .Concat(hostArtifactFiles)
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return new CanonicalSkillPackage(
            Manifest: manifest,
            Files: files);
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
                    throw new InvalidOperationException($"Host adapter '{adapter.Descriptor.HostKey}' must not emit metadata artifacts.");
                }

                yield return new GeneratedHostArtifactOutput(
                    new SkillHostArtifactManifest(adapter.Descriptor.HostKey, null, null, frontmatterDigest),
                    []);
                continue;
            }

            if (artifacts.MetadataContent is null)
            {
                throw new InvalidOperationException($"Host adapter '{adapter.Descriptor.HostKey}' must emit metadata artifact '{metadataArtifactPath}'.");
            }

            yield return new GeneratedHostArtifactOutput(
                new SkillHostArtifactManifest(
                    adapter.Descriptor.HostKey,
                    metadataArtifactPath,
                    digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent),
                    frontmatterDigest),
                [SkillPackageFile.Create(metadataArtifactPath, artifacts.MetadataContent)]);
        }
    }

    private sealed record GeneratedHostArtifactOutput (
        SkillHostArtifactManifest Manifest,
        IReadOnlyList<SkillPackageFile> Files);
}
