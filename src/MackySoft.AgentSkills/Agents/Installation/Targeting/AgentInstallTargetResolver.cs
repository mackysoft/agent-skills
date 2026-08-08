using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Resolves custom-agent artifact and installation-state targets. </summary>
public sealed class AgentInstallTargetResolver
{
    private readonly AgentHostAdapterSet hostAdapters;
    private readonly AgentUserTargetRootResolver userTargetRootResolver;

    /// <summary> Initializes an agent target resolver. </summary>
    public AgentInstallTargetResolver (AgentHostAdapterSet hostAdapters, AgentUserTargetRootResolver userTargetRootResolver)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.userTargetRootResolver = userTargetRootResolver ?? throw new ArgumentNullException(nameof(userTargetRootResolver));
    }

    /// <summary> Resolves one agent artifact target and its host-unobserved state root. </summary>
    public SkillOperationResult<AgentResolvedTarget> ResolveTarget (AgentTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var adapterResult = hostAdapters.GetAdapter(request.HostId);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<AgentResolvedTarget>.FailureResult(adapterResult.Failure!.Code, adapterResult.Failure.Message);
        }

        var descriptor = adapterResult.Value!.Descriptor;
        return request.ArtifactTargetRoot is not null
            ? ResolveExplicitTarget(request, descriptor)
            : request.Scope == AgentInstallScopeKind.Project
                ? ResolveDefaultProjectTarget(request, descriptor)
                : ResolveDefaultUserTarget(request, descriptor);
    }

    private static SkillOperationResult<AgentResolvedTarget> ResolveDefaultProjectTarget (AgentTargetRequest request, Agents.Hosts.AgentHostDescriptor descriptor)
    {
        var root = request.RepositoryRoot!;
        return CreateResolvedTarget(
            request,
            ResolveProjectRelativePath(root, descriptor.ProjectDefaultArtifactRootPath),
            ResolveProjectRelativePath(root, descriptor.ProjectDefaultStateRootPath));
    }

    private SkillOperationResult<AgentResolvedTarget> ResolveDefaultUserTarget (AgentTargetRequest request, Agents.Hosts.AgentHostDescriptor descriptor)
    {
        var rootsResult = userTargetRootResolver.ResolveDefaultTargetRoots(descriptor);
        return rootsResult.IsSuccess
            ? CreateResolvedTarget(
                request,
                SkillOperationResult<string>.Success(rootsResult.Value!.ArtifactRoot),
                SkillOperationResult<string>.Success(rootsResult.Value.StateRoot))
            : SkillOperationResult<AgentResolvedTarget>.FailureResult(rootsResult.Failure!.Code, rootsResult.Failure.Message);
    }

    private static SkillOperationResult<AgentResolvedTarget> ResolveExplicitTarget (AgentTargetRequest request, Agents.Hosts.AgentHostDescriptor descriptor)
    {
        var artifactTarget = request.ArtifactTargetRoot!;
        var artifactResult = request.Scope == AgentInstallScopeKind.Project
            ? ResolveUnderRoot(request.RepositoryRoot!, artifactTarget)
            : ResolveUnderRoot(artifactTarget, artifactTarget);
        if (!artifactResult.IsSuccess)
        {
            return SkillOperationResult<AgentResolvedTarget>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        var resolvedArtifactTarget = AbsolutePath.Parse(artifactResult.Value!);
        if (!resolvedArtifactTarget.TryGetParent(out var parent))
        {
            return SkillOperationResult<AgentResolvedTarget>.FailureResult(SkillFailureCodes.PathUnsafe, "Explicit agent artifact target must have a parent directory for installation state.");
        }

        var artifactName = Path.GetFileName(resolvedArtifactTarget.Value);
        var stateRelativePath = RootRelativePath.Parse($"{descriptor.ExplicitTargetStateDirectoryName}/{artifactName}");
        var stateTarget = ContainedPath.Create(parent, stateRelativePath).Target;
        var stateResult = request.Scope == AgentInstallScopeKind.Project
            ? ResolveUnderRoot(request.RepositoryRoot!, stateTarget)
            : AgentPathGuard.ValidateContainedPath(ContainedPath.Create(parent, stateRelativePath));
        return CreateResolvedTarget(request, artifactResult, stateResult);
    }

    private static SkillOperationResult<string> ResolveProjectRelativePath (AbsolutePath root, string relativePath)
    {
        var target = ContainedPath.Create(root, RootRelativePath.Parse(relativePath));
        return AgentPathGuard.ValidateContainedPath(target);
    }

    private static SkillOperationResult<string> ResolveUnderRoot (AbsolutePath root, AbsolutePath target)
    {
        return ContainedPath.TryCreate(root, target, out var containedPath, out var failure)
            ? AgentPathGuard.ValidateContainedPath(containedPath)
            : SkillOperationResult<string>.FailureResult(SkillFailureCodes.PathUnsafe, $"Agent path is invalid: {failure.Message}");
    }

    private static SkillOperationResult<AgentResolvedTarget> CreateResolvedTarget (AgentTargetRequest request, SkillOperationResult<string> artifactResult, SkillOperationResult<string> stateResult)
    {
        if (!artifactResult.IsSuccess)
        {
            return SkillOperationResult<AgentResolvedTarget>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        return stateResult.IsSuccess
            ? SkillOperationResult<AgentResolvedTarget>.Success(new AgentResolvedTarget(request.HostId, request.Scope, request.RepositoryRoot?.Value, artifactResult.Value!, stateResult.Value!))
            : SkillOperationResult<AgentResolvedTarget>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
    }
}
