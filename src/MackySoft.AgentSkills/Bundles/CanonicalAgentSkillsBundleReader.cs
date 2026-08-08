using System.Text.Json;
using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reads and validates generated v2 mixed bundles. </summary>
public sealed class CanonicalAgentSkillsBundleReader
{
    private readonly AgentSkillsBundleJsonSerializer serializer;
    private readonly CanonicalSkillPackageReader skillReader;
    private readonly CanonicalAgentPackageReader agentReader;
    private readonly AgentSkillsBundleDigestCalculator digestCalculator;

    /// <summary> Initializes the reader. </summary>
    /// <param name="serializer"> The canonical v2 bundle descriptor serializer. </param>
    /// <param name="skillReader"> The canonical generated SKILL package reader. </param>
    /// <param name="agentReader"> The canonical generated agent package reader. </param>
    /// <param name="digestCalculator"> The complete mixed-bundle digest calculator. </param>
    internal CanonicalAgentSkillsBundleReader (
        AgentSkillsBundleJsonSerializer serializer,
        CanonicalSkillPackageReader skillReader,
        CanonicalAgentPackageReader agentReader,
        AgentSkillsBundleDigestCalculator digestCalculator)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.skillReader = skillReader ?? throw new ArgumentNullException(nameof(skillReader));
        this.agentReader = agentReader ?? throw new ArgumentNullException(nameof(agentReader));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Creates the default reader for bundled v2 agent assets. </summary>
    public static CanonicalAgentSkillsBundleReader CreateDefault ()
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var skillHosts = new SkillHostAdapterSet();
        var skillReader = new CanonicalSkillPackageReader(
            skillManifestSerializer,
            new SkillManifest.Factory(skillHosts, new SkillManifestDigestCalculator(skillManifestSerializer)),
            new CanonicalSkillPackage.Factory(skillHosts, digestCalculator, skillManifestSerializer));
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            new AgentManifestDigestCalculator(agentManifestSerializer));
        return new CanonicalAgentSkillsBundleReader(
            new AgentSkillsBundleJsonSerializer(),
            skillReader,
            agentReader,
            new AgentSkillsBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator));
    }

    /// <summary> Reads one generated v2 bundle. </summary>
    /// <param name="generatedRoot"> The root containing v2 <c>bundle.json</c>, <c>skills</c>, and <c>agents</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns>The canonical mixed bundle, or a manifest failure.</returns>
    internal async ValueTask<SkillOperationResult<CanonicalAgentSkillsBundle>> ReadAsync (
        string generatedRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedRoot);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(generatedRoot))
        {
            return Failure(SkillFailureCodes.ManifestInvalid, $"Generated v2 bundle directory does not exist: {generatedRoot}");
        }

        try
        {
            var root = Path.GetFullPath(generatedRoot);
            if (!SkillPackageFileSystemEntryGuard.IsDirectory(root))
            {
                return Failure(SkillFailureCodes.PathUnsafe, $"Generated v2 bundle root must be a regular directory: {root}");
            }

            var rootValidationResult = ValidateRootEntries(root);
            if (!rootValidationResult.IsSuccess)
            {
                return Failure(rootValidationResult.Failure!.Code, rootValidationResult.Failure.Message);
            }

            var descriptorPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(root, "bundle.json");
            if (!descriptorPathResult.IsSuccess)
            {
                return Failure(descriptorPathResult.Failure!.Code, descriptorPathResult.Failure.Message);
            }

            var descriptorTextResult = await SkillPackageTextFileReader.ReadAsync(descriptorPathResult.Value!, cancellationToken).ConfigureAwait(false);
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

            var skillsRoot = Path.Combine(root, "skills");
            var skillsResult = Directory.Exists(skillsRoot)
                ? await skillReader.ReadAllAsync(skillsRoot, cancellationToken).ConfigureAwait(false)
                : SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success([]);
            if (!skillsResult.IsSuccess)
            {
                return Failure(skillsResult.Failure!.Code, skillsResult.Failure.Message);
            }

            var agentsResult = await agentReader.ReadAllAsync(Path.Combine(root, "agents"), cancellationToken).ConfigureAwait(false);
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

            return SkillOperationResult<CanonicalAgentSkillsBundle>.Success(new CanonicalAgentSkillsBundle(descriptor, skillsResult.Value!, agentsResult.Value!));
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            return Failure(SkillFailureCodes.ManifestInvalid, "Generated v2 bundle is invalid.");
        }
    }

    private static SkillOperationResult<bool> ValidateRootEntries (string root)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(entryPath);
            if (string.Equals(name, "bundle.json", StringComparison.Ordinal))
            {
                if (!SkillPackageFileSystemEntryGuard.IsRegularFile(entryPath))
                {
                    return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.PathUnsafe, "Generated v2 bundle.json must be a regular file.");
                }

                continue;
            }

            if ((string.Equals(name, "skills", StringComparison.Ordinal) || string.Equals(name, "agents", StringComparison.Ordinal))
                && SkillPackageFileSystemEntryGuard.IsDirectory(entryPath))
            {
                continue;
            }

            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Generated v2 bundle contains an unsupported root entry: {name}");
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<CanonicalAgentSkillsBundle> Failure (SkillFailureCode code, string message) => SkillOperationResult<CanonicalAgentSkillsBundle>.FailureResult(code, message);
}
