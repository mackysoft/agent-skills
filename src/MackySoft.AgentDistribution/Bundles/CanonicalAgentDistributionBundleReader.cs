using System.Text.Json;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reads and validates generated v2 mixed bundles. </summary>
public sealed class CanonicalAgentDistributionBundleReader
{
    private readonly AgentDistributionBundleJsonSerializer serializer;
    private readonly CanonicalSkillPackageReader skillReader;
    private readonly CanonicalAgentPackageReader agentReader;
    private readonly AgentDistributionBundleDigestCalculator digestCalculator;

    /// <summary> Initializes the reader. </summary>
    /// <param name="serializer"> The canonical v2 bundle descriptor serializer. </param>
    /// <param name="skillReader"> The canonical generated SKILL package reader. </param>
    /// <param name="agentReader"> The canonical generated agent package reader. </param>
    /// <param name="digestCalculator"> The complete mixed-bundle digest calculator. </param>
    internal CanonicalAgentDistributionBundleReader (
        AgentDistributionBundleJsonSerializer serializer,
        CanonicalSkillPackageReader skillReader,
        CanonicalAgentPackageReader agentReader,
        AgentDistributionBundleDigestCalculator digestCalculator)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.skillReader = skillReader ?? throw new ArgumentNullException(nameof(skillReader));
        this.agentReader = agentReader ?? throw new ArgumentNullException(nameof(agentReader));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Creates the default reader for bundled v2 agent assets. </summary>
    public static CanonicalAgentDistributionBundleReader CreateDefault ()
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var skillReader = new CanonicalSkillPackageReader(
            skillManifestSerializer,
            new SkillManifest.Factory(new SkillManifestDigestCalculator(skillManifestSerializer)),
            new CanonicalSkillPackage.Factory(digestCalculator, skillManifestSerializer));
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            new AgentManifestDigestCalculator(agentManifestSerializer));
        return new CanonicalAgentDistributionBundleReader(
            new AgentDistributionBundleJsonSerializer(),
            skillReader,
            agentReader,
            new AgentDistributionBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator));
    }

    /// <summary> Reads one generated v2 bundle. </summary>
    /// <param name="generatedRoot"> The root containing v2 <c>bundle.json</c>, <c>skills</c>, and <c>agents</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns>The canonical mixed bundle, or a manifest failure.</returns>
    internal async ValueTask<SkillOperationResult<CanonicalAgentDistributionBundle>> ReadAsync (
        AbsolutePath generatedRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generatedRoot);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var root = generatedRoot;
            if (!FileSystemEntryInspector.TryInspect(
                    root,
                    out var rootObservation,
                    out _))
            {
                return Failure(SkillFailureCodes.PathUnsafe, $"Generated v2 bundle root could not be inspected: {root}");
            }

            if (rootObservation.State == FileSystemEntryState.Missing)
            {
                return Failure(SkillFailureCodes.ManifestInvalid, $"Generated v2 bundle directory does not exist: {generatedRoot}");
            }

            if (rootObservation.State != FileSystemEntryState.Directory)
            {
                return Failure(SkillFailureCodes.PathUnsafe, $"Generated v2 bundle root must be a regular directory: {root}");
            }

            var rootValidationResult = ValidateRootEntries(root);
            if (!rootValidationResult.IsSuccess)
            {
                return Failure(rootValidationResult.Failure!.Code, rootValidationResult.Failure.Message);
            }

            var descriptorPathResult = PackagePathResolver.ResolveRegularFile(root, PackageRelativePath.Parse("bundle.json"));
            if (!descriptorPathResult.IsSuccess)
            {
                return Failure(descriptorPathResult.Failure!.Code, descriptorPathResult.Failure.Message);
            }

            var descriptorTextResult = await CanonicalPackageTextReader.ReadAsync(descriptorPathResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!descriptorTextResult.IsSuccess)
            {
                return Failure(descriptorTextResult.Failure!.Code, descriptorTextResult.Failure.Message);
            }

            var descriptorText = descriptorTextResult.Value!;
            var descriptor = serializer.DeserializeDescriptor(descriptorText);
            if (!string.Equals(descriptorText, serializer.SerializeDescriptor(descriptor), StringComparison.Ordinal))
            {
                return Failure(SkillFailureCodes.ManifestInvalid, "Generated v2 bundle.json is not canonical.");
            }

            var skillsRoot = ContainedPath.Create(root, RootRelativePath.Parse("skills")).Target;
            var skillsResult = Directory.Exists(skillsRoot.Value)
                ? await skillReader.ReadAllAsync(skillsRoot, cancellationToken).ConfigureAwait(false)
                : SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success([]);
            if (!skillsResult.IsSuccess)
            {
                return Failure(skillsResult.Failure!.Code, skillsResult.Failure.Message);
            }

            var agentsResult = await agentReader.ReadAllAsync(
                ContainedPath.Create(root, RootRelativePath.Parse("agents")).Target,
                cancellationToken).ConfigureAwait(false);
            if (!agentsResult.IsSuccess)
            {
                return Failure(agentsResult.Failure!.Code, agentsResult.Failure.Message);
            }

            foreach (var skill in skillsResult.Value!)
            {
                if (skill.Manifest.CatalogId != descriptor.CatalogId || skill.Manifest.SkillBundleVersion.Value != descriptor.BundleVersion.Value)
                {
                    return Failure(SkillFailureCodes.ManifestInvalid, "Generated v2 skill package identity does not match bundle.json.");
                }
            }

            foreach (var agent in agentsResult.Value!)
            {
                if (agent.Manifest.CatalogId != descriptor.CatalogId || agent.Manifest.BundleVersion != descriptor.BundleVersion)
                {
                    return Failure(SkillFailureCodes.ManifestInvalid, "Generated v2 agent package identity does not match bundle.json.");
                }
            }

            var availableSkillNames = skillsResult.Value!
                .Select(static package => package.Manifest.SkillName)
                .ToHashSet();
            foreach (var agent in agentsResult.Value!)
            {
                var missingDependencies = agent.Manifest.SkillDependencies
                    .Where(dependency => !availableSkillNames.Contains(dependency))
                    .Select(static dependency => dependency.Value)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                if (missingDependencies.Length != 0)
                {
                    return Failure(
                        SkillFailureCodes.ManifestInvalid,
                        $"Generated v2 agent package references missing skill dependencies for '{agent.Manifest.AgentName.Value}': {string.Join(", ", missingDependencies)}.");
                }
            }

            if ((skillsResult.Value!.Count == 0 && agentsResult.Value!.Count == 0) || digestCalculator.ComputeDigest(skillsResult.Value!, agentsResult.Value!) != descriptor.BundleDigest)
            {
                return Failure(SkillFailureCodes.ManifestInvalid, "Generated v2 bundle digest does not match package files.");
            }

            return SkillOperationResult<CanonicalAgentDistributionBundle>.Success(new CanonicalAgentDistributionBundle(descriptor, skillsResult.Value!, agentsResult.Value!));
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            return Failure(SkillFailureCodes.ManifestInvalid, "Generated v2 bundle is invalid.");
        }
    }

    private static SkillOperationResult<bool> ValidateRootEntries (AbsolutePath root)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(root.Value).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(entryPath);
            if (string.Equals(name, "bundle.json", StringComparison.Ordinal))
            {
                if (!FileSystemEntryInspector.TryInspect(
                        AbsolutePath.Parse(entryPath),
                        out var descriptorObservation,
                        out _)
                    || descriptorObservation.State != FileSystemEntryState.RegularFile)
                {
                    return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.PathUnsafe, "Generated v2 bundle.json must be a regular file.");
                }

                continue;
            }

            if ((string.Equals(name, "skills", StringComparison.Ordinal) || string.Equals(name, "agents", StringComparison.Ordinal))
                && FileSystemEntryInspector.TryInspect(
                    AbsolutePath.Parse(entryPath),
                    out var namespaceObservation,
                    out _)
                && namespaceObservation.State == FileSystemEntryState.Directory)
            {
                continue;
            }

            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Generated v2 bundle contains an unsupported root entry: {name}");
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<CanonicalAgentDistributionBundle> Failure (SkillFailureCode code, string message) => SkillOperationResult<CanonicalAgentDistributionBundle>.FailureResult(code, message);
}
