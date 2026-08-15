using MackySoft.AgentDistribution.Agents.Generation;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.AgentDistribution.Skills.Generation;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Sources;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles.Generation;

/// <summary> Reads v4 mixed source bundles and generates v3 output while preserving v1 skill package contracts. </summary>
internal sealed class AgentDistributionBundleGenerationService
{
    private readonly AgentDistributionBundleDefinitionReader bundleReader;
    private readonly SkillSourceDefinitionReader skillReader;
    private readonly AgentSourceDefinitionReader agentReader;
    private readonly SkillPackageGenerationService skillGenerator;
    private readonly AgentPackageGenerationService agentGenerator;
    private readonly AgentDistributionBundleDigestCalculator bundleDigestCalculator;

    /// <summary> Initializes the mixed generator. </summary>
    public AgentDistributionBundleGenerationService (AgentDistributionBundleDefinitionReader bundleReader, SkillSourceDefinitionReader skillReader, AgentSourceDefinitionReader agentReader, SkillPackageGenerationService skillGenerator, AgentPackageGenerationService agentGenerator, AgentDistributionBundleDigestCalculator bundleDigestCalculator)
    {
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.skillReader = skillReader ?? throw new ArgumentNullException(nameof(skillReader));
        this.agentReader = agentReader ?? throw new ArgumentNullException(nameof(agentReader));
        this.skillGenerator = skillGenerator ?? throw new ArgumentNullException(nameof(skillGenerator));
        this.agentGenerator = agentGenerator ?? throw new ArgumentNullException(nameof(agentGenerator));
        this.bundleDigestCalculator = bundleDigestCalculator ?? throw new ArgumentNullException(nameof(bundleDigestCalculator));
    }

    /// <summary> Reads a complete v4 source snapshot. </summary>
    public async ValueTask<AgentDistributionOperationResult<AgentDistributionGenerationSource>> ReadSourceAsync (AbsolutePath bundleRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundleRoot);
        var bundleResult = await bundleReader.ReadAsync(bundleRoot, cancellationToken).ConfigureAwait(false);
        if (!bundleResult.IsSuccess)
        {
            return Failure(bundleResult.Failure!);
        }

        var rootEntriesResult = ReadSourceRootEntryNames(bundleRoot, cancellationToken);
        if (!rootEntriesResult.IsSuccess)
        {
            return Failure(rootEntriesResult.Failure!);
        }

        var rootEntries = rootEntriesResult.Value!;
        var unsupportedRootEntry = rootEntries.FirstOrDefault(static name => name is not "bundle.json" and not "agents" and not "skills");
        if (unsupportedRootEntry is not null)
        {
            return AgentDistributionOperationResult<AgentDistributionGenerationSource>.FailureResult(
                AgentDistributionFailureCodes.SourceInvalid,
                $"v4 source bundle root contains an unsupported entry: {unsupportedRootEntry}");
        }

        var hasSkillsNamespace = rootEntries.Contains("skills", StringComparer.Ordinal);
        AgentDistributionOperationResult<IReadOnlyList<SkillSourceDefinition>> skillsResult;
        if (hasSkillsNamespace)
        {
            var namespaceResult = ResolveNamespace(bundleRoot, "skills");
            if (!namespaceResult.IsSuccess)
            {
                return Failure(namespaceResult.Failure!);
            }

            skillsResult = await skillReader.ReadAllAsync(namespaceResult.Value!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            skillsResult = AgentDistributionOperationResult<IReadOnlyList<SkillSourceDefinition>>.Success([]);
        }

        if (!skillsResult.IsSuccess)
        {
            return Failure(skillsResult.Failure!);
        }

        var hasAgentsNamespace = rootEntries.Contains("agents", StringComparer.Ordinal);
        AgentDistributionOperationResult<IReadOnlyList<AgentSourceDefinition>> agentsResult;
        if (hasAgentsNamespace)
        {
            var namespaceResult = ResolveNamespace(bundleRoot, "agents");
            if (!namespaceResult.IsSuccess)
            {
                return Failure(namespaceResult.Failure!);
            }

            agentsResult = await agentReader.ReadAllAsync(namespaceResult.Value!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            agentsResult = AgentDistributionOperationResult<IReadOnlyList<AgentSourceDefinition>>.Success([]);
        }

        if (!agentsResult.IsSuccess)
        {
            return Failure(agentsResult.Failure!);
        }

        if (hasSkillsNamespace && skillsResult.Value!.Count == 0)
        {
            return AgentDistributionOperationResult<AgentDistributionGenerationSource>.FailureResult(
                AgentDistributionFailureCodes.SourceInvalid,
                "The v4 skills namespace must not be empty when it is present.");
        }

        if (hasAgentsNamespace && agentsResult.Value!.Count == 0)
        {
            return AgentDistributionOperationResult<AgentDistributionGenerationSource>.FailureResult(
                AgentDistributionFailureCodes.SourceInvalid,
                "The v4 agents namespace must not be empty when it is present.");
        }

        if (skillsResult.Value!.Count == 0 && agentsResult.Value!.Count == 0)
        {
            return AgentDistributionOperationResult<AgentDistributionGenerationSource>.FailureResult(AgentDistributionFailureCodes.SourceInvalid, "A v4 bundle must contain at least one skill or agent definition.");
        }

        var skillReferences = SkillSourceDependencyReferenceValidator.Validate(skillsResult.Value!);
        if (!skillReferences.IsSuccess)
        {
            return Failure(skillReferences.Failure!);
        }

        var agentDependencies = ValidateAgentSkillDependencies(agentsResult.Value!, skillsResult.Value!);
        return agentDependencies.IsSuccess
            ? AgentDistributionOperationResult<AgentDistributionGenerationSource>.Success(new AgentDistributionGenerationSource(bundleResult.Value!, skillsResult.Value!, agentsResult.Value!))
            : Failure(agentDependencies.Failure!);
    }

    /// <summary> Generates one complete canonical mixed bundle. </summary>
    public CanonicalAgentDistributionBundle Generate (AgentDistributionGenerationSource source, AgentDistributionBundleVersion version)
    {
        var bundle = source.BundleDefinition.BundleVersion == version ? source.BundleDefinition : new AgentDistributionBundleDefinition(source.BundleDefinition.SchemaVersion, source.BundleDefinition.CatalogId, version);
        var skillVersion = new SkillBundleVersion(version.Value);
        var skillBundle = new SkillBundleDefinition(SkillBundleDefinition.CurrentSchemaVersion, bundle.CatalogId, skillVersion);
        var skills = source.Skills.Select(definition => skillGenerator.Generate(skillBundle, definition)).ToArray();
        var agents = source.Agents.Select(definition => agentGenerator.Generate(bundle.CatalogId, bundle.BundleVersion, definition)).ToArray();
        var descriptor = new AgentDistributionBundleDescriptor(AgentDistributionBundleDescriptor.CurrentSchemaVersion, bundle.CatalogId, bundle.BundleVersion, bundleDigestCalculator.ComputeDigest(skills, agents));
        return new CanonicalAgentDistributionBundle(descriptor, skills, agents);
    }

    private static AgentDistributionOperationResult<AgentDistributionGenerationSource> Failure (AgentDistributionFailure failure) => AgentDistributionOperationResult<AgentDistributionGenerationSource>.FailureResult(failure.Code, failure.Message);

    private static AgentDistributionOperationResult<bool> ValidateAgentSkillDependencies (
        IReadOnlyList<AgentSourceDefinition> agents,
        IReadOnlyList<SkillSourceDefinition> skills)
    {
        var knownSkills = skills.Select(static skill => skill.Metadata.SkillName).ToHashSet();
        foreach (var agent in agents.OrderBy(static agent => agent.Metadata.AgentName.Value, StringComparer.Ordinal))
        {
            var missingSkills = agent.Metadata.SkillDependencies
                .Where(skill => !knownSkills.Contains(skill))
                .Select(static skill => skill.Value)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (missingSkills.Length != 0)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.SourceInvalid,
                    $"agent.json references missing skills for '{agent.Metadata.AgentName.Value}': {string.Join(", ", missingSkills)}.");
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveNamespace (
        AbsolutePath definitionsRoot,
        string namespaceName)
    {
        return AuthoredSourcePathResolver.ResolveDirectory(
            definitionsRoot,
            RootRelativePath.Parse(namespaceName),
            $"v4 {namespaceName} namespace");
    }

    private static AgentDistributionOperationResult<IReadOnlyList<string>> ReadSourceRootEntryNames (
        AbsolutePath bundleRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            var names = new List<string>();
            foreach (var path in Directory.EnumerateFileSystemEntries(bundleRoot.Value))
            {
                cancellationToken.ThrowIfCancellationRequested();
                names.Add(Path.GetFileName(path));
            }

            names.Sort(StringComparer.Ordinal);
            return AgentDistributionOperationResult<IReadOnlyList<string>>.Success(Array.AsReadOnly(names.ToArray()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException)
        {
            return AgentDistributionOperationResult<IReadOnlyList<string>>.FailureResult(
                AgentDistributionFailureCodes.SourceInvalid,
                $"The v4 source bundle root could not be read: {exception.Message}");
        }
    }
}
