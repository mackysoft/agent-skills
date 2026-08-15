using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Targeting;

/// <summary> Resolves custom-agent artifact and installation-state targets. </summary>
public sealed class AgentInstallTargetResolver
{
    private readonly AgentUserTargetRootResolver userTargetRootResolver;

    /// <summary> Initializes an agent target resolver. </summary>
    public AgentInstallTargetResolver (AgentUserTargetRootResolver userTargetRootResolver)
    {
        this.userTargetRootResolver = userTargetRootResolver ?? throw new ArgumentNullException(nameof(userTargetRootResolver));
    }

    /// <summary> Resolves one agent artifact target and its host-unobserved state root. </summary>
    public AgentDistributionOperationResult<AgentResolvedTarget> ResolveTarget (AgentTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var registrationResult = BuiltInHostCatalog.Get(request.HostId);
        if (!registrationResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentResolvedTarget>.FailureResult(registrationResult.Failure!.Code, registrationResult.Failure.Message);
        }

        var descriptor = registrationResult.Value!.AgentTargetPolicy;
        return request.ArtifactTargetRoot is not null
            ? ResolveExplicitTarget(request, descriptor)
            : request.Scope == AgentInstallScopeKind.Project
                ? ResolveDefaultProjectTarget(request, descriptor)
                : ResolveDefaultUserTarget(request, descriptor);
    }

    private static AgentDistributionOperationResult<AgentResolvedTarget> ResolveDefaultProjectTarget (AgentTargetRequest request, AgentHostTargetPolicy descriptor)
    {
        var root = request.RepositoryRoot!;
        return CreateResolvedTarget(
            request,
            ResolveProjectRelativePath(root, descriptor.ProjectDefaultArtifactRootPath),
            ResolveProjectRelativePath(root, descriptor.ProjectDefaultStateRootPath));
    }

    private AgentDistributionOperationResult<AgentResolvedTarget> ResolveDefaultUserTarget (AgentTargetRequest request, AgentHostTargetPolicy descriptor)
    {
        var rootsResult = userTargetRootResolver.ResolveDefaultTargetRoots(descriptor);
        return rootsResult.IsSuccess
            ? CreateResolvedTarget(
                request,
                AgentDistributionOperationResult<AbsolutePath>.Success(rootsResult.Value!.ArtifactRoot),
                AgentDistributionOperationResult<AbsolutePath>.Success(rootsResult.Value.StateRoot))
            : AgentDistributionOperationResult<AgentResolvedTarget>.FailureResult(rootsResult.Failure!.Code, rootsResult.Failure.Message);
    }

    private static AgentDistributionOperationResult<AgentResolvedTarget> ResolveExplicitTarget (AgentTargetRequest request, AgentHostTargetPolicy descriptor)
    {
        var artifactTarget = request.ArtifactTargetRoot!;
        var artifactResult = request.Scope == AgentInstallScopeKind.Project
            ? ResolveUnderRoot(request.RepositoryRoot!, artifactTarget)
            : ResolveUnderRoot(artifactTarget, artifactTarget);
        if (!artifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentResolvedTarget>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        var resolvedArtifactTarget = artifactResult.Value!;
        if (!resolvedArtifactTarget.TryGetParent(out var parent))
        {
            return AgentDistributionOperationResult<AgentResolvedTarget>.FailureResult(AgentDistributionFailureCodes.PathUnsafe, "Explicit agent artifact target must have a parent directory for installation state.");
        }

        var artifactName = Path.GetFileName(resolvedArtifactTarget.Value);
        var stateDirectory = ContainedPath.Create(parent, descriptor.ExplicitTargetStateDirectory).Target;
        var stateTarget = ContainedPath.Create(stateDirectory, RootRelativePath.Parse(artifactName)).Target;
        if (!ContainedPath.TryCreate(parent, stateTarget, out var containedStateTarget, out _))
        {
            throw new InvalidOperationException("The typed explicit-state path must remain contained by its established parent.");
        }

        var stateResult = request.Scope == AgentInstallScopeKind.Project
            ? ResolveUnderRoot(request.RepositoryRoot!, stateTarget)
            : AgentPathGuard.Validate(containedStateTarget);

        return CreateResolvedTarget(request, artifactResult, stateResult);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveProjectRelativePath (AbsolutePath root, RootRelativePath relativePath)
    {
        var target = ContainedPath.Create(root, relativePath);
        return AgentPathGuard.Validate(target);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveUnderRoot (AbsolutePath root, AbsolutePath target)
    {
        return ContainedPath.TryCreate(root, target, out var containedPath, out var failure)
            ? AgentPathGuard.Validate(containedPath)
            : AgentDistributionOperationResult<AbsolutePath>.FailureResult(AgentDistributionFailureCodes.PathUnsafe, $"Agent path is invalid: {failure.Message}");
    }

    private static AgentDistributionOperationResult<AgentResolvedTarget> CreateResolvedTarget (AgentTargetRequest request, AgentDistributionOperationResult<AbsolutePath> artifactResult, AgentDistributionOperationResult<AbsolutePath> stateResult)
    {
        if (!artifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentResolvedTarget>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        return stateResult.IsSuccess
            ? AgentDistributionOperationResult<AgentResolvedTarget>.Success(new AgentResolvedTarget(
                request.HostId,
                request.Scope,
                request.RepositoryRoot,
                artifactResult.Value!,
                stateResult.Value!))
            : AgentDistributionOperationResult<AgentResolvedTarget>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
    }
}
