using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Paths;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Generation;

/// <summary> Reads and generates v2 mixed bundles while preserving v1 skill package contracts. </summary>
internal sealed class AgentSkillsBundleGenerationService
{
    private readonly AgentSkillsBundleDefinitionReader bundleReader;
    private readonly SkillSourceDefinitionReader skillReader;
    private readonly AgentSourceDefinitionReader agentReader;
    private readonly SkillPackageGenerationService skillGenerator;
    private readonly AgentPackageGenerationService agentGenerator;
    private readonly AgentSkillsBundleDigestCalculator bundleDigestCalculator;

    /// <summary> Initializes the mixed generator. </summary>
    public AgentSkillsBundleGenerationService (AgentSkillsBundleDefinitionReader bundleReader, SkillSourceDefinitionReader skillReader, AgentSourceDefinitionReader agentReader, SkillPackageGenerationService skillGenerator, AgentPackageGenerationService agentGenerator, AgentSkillsBundleDigestCalculator bundleDigestCalculator)
    {
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.skillReader = skillReader ?? throw new ArgumentNullException(nameof(skillReader));
        this.agentReader = agentReader ?? throw new ArgumentNullException(nameof(agentReader));
        this.skillGenerator = skillGenerator ?? throw new ArgumentNullException(nameof(skillGenerator));
        this.agentGenerator = agentGenerator ?? throw new ArgumentNullException(nameof(agentGenerator));
        this.bundleDigestCalculator = bundleDigestCalculator ?? throw new ArgumentNullException(nameof(bundleDigestCalculator));
    }

    /// <summary> Reads a complete v2 source snapshot. </summary>
    public async ValueTask<SkillOperationResult<AgentSkillsGenerationSource>> ReadSourceAsync (AbsolutePath bundleRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundleRoot);
        var bundleResult = await bundleReader.ReadAsync(bundleRoot, cancellationToken).ConfigureAwait(false);
        if (!bundleResult.IsSuccess)
        {
            return Failure(bundleResult.Failure!);
        }

        var definitionsRootResult = AuthoredSourcePathResolver.ResolveDirectory(
            bundleRoot,
            RootRelativePath.Parse("definitions"),
            "v2 definitions root");
        if (!definitionsRootResult.IsSuccess)
        {
            return Failure(definitionsRootResult.Failure!);
        }

        var definitionsRoot = definitionsRootResult.Value!;
        var namespaceNamesResult = ReadDefinitionNamespaceNames(definitionsRoot, cancellationToken);
        if (!namespaceNamesResult.IsSuccess)
        {
            return Failure(namespaceNamesResult.Failure!);
        }

        var namespaceNames = namespaceNamesResult.Value!;
        var unsupportedNamespace = namespaceNames.FirstOrDefault(static name => name is not "agents" and not "skills");
        if (unsupportedNamespace is not null)
        {
            return SkillOperationResult<AgentSkillsGenerationSource>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"v2 definitions root contains an unsupported entry: {unsupportedNamespace}");
        }

        var hasSkillsNamespace = namespaceNames.Contains("skills", StringComparer.Ordinal);
        SkillOperationResult<IReadOnlyList<SkillSourceDefinition>> skillsResult;
        if (hasSkillsNamespace)
        {
            var namespaceResult = ResolveNamespace(definitionsRoot, "skills");
            if (!namespaceResult.IsSuccess)
            {
                return Failure(namespaceResult.Failure!);
            }

            skillsResult = await skillReader.ReadAllAsync(namespaceResult.Value!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            skillsResult = SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>.Success([]);
        }

        if (!skillsResult.IsSuccess)
        {
            return Failure(skillsResult.Failure!);
        }

        var hasAgentsNamespace = namespaceNames.Contains("agents", StringComparer.Ordinal);
        SkillOperationResult<IReadOnlyList<AgentSourceDefinition>> agentsResult;
        if (hasAgentsNamespace)
        {
            var namespaceResult = ResolveNamespace(definitionsRoot, "agents");
            if (!namespaceResult.IsSuccess)
            {
                return Failure(namespaceResult.Failure!);
            }

            agentsResult = await agentReader.ReadAllAsync(namespaceResult.Value!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            agentsResult = SkillOperationResult<IReadOnlyList<AgentSourceDefinition>>.Success([]);
        }

        if (!agentsResult.IsSuccess)
        {
            return Failure(agentsResult.Failure!);
        }

        if (hasSkillsNamespace && skillsResult.Value!.Count == 0)
        {
            return SkillOperationResult<AgentSkillsGenerationSource>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                "The v2 skills namespace must not be empty when it is present.");
        }

        if (hasAgentsNamespace && agentsResult.Value!.Count == 0)
        {
            return SkillOperationResult<AgentSkillsGenerationSource>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                "The v2 agents namespace must not be empty when it is present.");
        }

        if (skillsResult.Value!.Count == 0 && agentsResult.Value!.Count == 0)
        {
            return SkillOperationResult<AgentSkillsGenerationSource>.FailureResult(SkillFailureCodes.SourceInvalid, "A v2 bundle must contain at least one skill or agent definition.");
        }

        var skillReferences = SkillSourceDependencyReferenceValidator.Validate(skillsResult.Value!);
        if (!skillReferences.IsSuccess)
        {
            return Failure(skillReferences.Failure!);
        }

        var agentDependencies = ValidateAgentSkillDependencies(agentsResult.Value!, skillsResult.Value!);
        return agentDependencies.IsSuccess
            ? SkillOperationResult<AgentSkillsGenerationSource>.Success(new AgentSkillsGenerationSource(bundleResult.Value!, skillsResult.Value!, agentsResult.Value!))
            : Failure(agentDependencies.Failure!);
    }

    /// <summary> Generates one complete canonical mixed bundle. </summary>
    public CanonicalAgentSkillsBundle Generate (AgentSkillsGenerationSource source, AgentSkillsBundleVersion version)
    {
        var bundle = source.BundleDefinition.BundleVersion == version ? source.BundleDefinition : new AgentSkillsBundleDefinition(source.BundleDefinition.SchemaVersion, source.BundleDefinition.CatalogId, version);
        var skillVersion = new SkillBundleVersion(version.Value);
        var skillBundle = new SkillBundleDefinition(SkillBundleDefinition.CurrentSchemaVersion, bundle.CatalogId, skillVersion);
        var skills = source.Skills.Select(definition => skillGenerator.Generate(skillBundle, definition)).ToArray();
        var agents = source.Agents.Select(definition => agentGenerator.Generate(bundle, definition)).ToArray();
        var descriptor = new AgentSkillsBundleDescriptor(AgentSkillsBundleDefinition.CurrentSchemaVersion, bundle.CatalogId, bundle.BundleVersion, bundleDigestCalculator.ComputeDigest(skills, agents));
        return new CanonicalAgentSkillsBundle(descriptor, skills, agents);
    }

    private static SkillOperationResult<AgentSkillsGenerationSource> Failure (SkillFailure failure) => SkillOperationResult<AgentSkillsGenerationSource>.FailureResult(failure.Code, failure.Message);

    private static SkillOperationResult<bool> ValidateAgentSkillDependencies (
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
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"agent.json references missing skills for '{agent.Metadata.AgentName.Value}': {string.Join(", ", missingSkills)}.");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<AbsolutePath> ResolveNamespace (
        AbsolutePath definitionsRoot,
        string namespaceName)
    {
        return AuthoredSourcePathResolver.ResolveDirectory(
            definitionsRoot,
            RootRelativePath.Parse(namespaceName),
            $"v2 {namespaceName} namespace");
    }

    private static SkillOperationResult<IReadOnlyList<string>> ReadDefinitionNamespaceNames (
        AbsolutePath definitionsRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            var names = new List<string>();
            foreach (var path in Directory.EnumerateFileSystemEntries(definitionsRoot.Value))
            {
                cancellationToken.ThrowIfCancellationRequested();
                names.Add(Path.GetFileName(path));
            }

            names.Sort(StringComparer.Ordinal);
            return SkillOperationResult<IReadOnlyList<string>>.Success(Array.AsReadOnly(names.ToArray()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException)
        {
            return SkillOperationResult<IReadOnlyList<string>>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"The v2 definitions root could not be read: {exception.Message}");
        }
    }
}
