using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Resolves project- and user-scope SKILL target roots. </summary>
public sealed class SkillInstallTargetResolver
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillUserTargetRootResolver userTargetRootResolver;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="userTargetRootResolver"> The user-scope target root resolver. </param>
    public SkillInstallTargetResolver (
        SkillHostAdapterSet hostAdapters,
        SkillUserTargetRootResolver userTargetRootResolver)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.userTargetRootResolver = userTargetRootResolver ?? throw new ArgumentNullException(nameof(userTargetRootResolver));
    }

    /// <summary> Resolves the target root for one install request. </summary>
    /// <param name="request"> The install request. </param>
    /// <returns> The canonical host target, or a structured failure for an unavailable user target or unsafe path use. </returns>
    public SkillOperationResult<SkillResolvedInstallTarget> ResolveTarget (SkillInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = hostAdapters.GetAdapter(request.Host).Value!.Descriptor;
        return request.Scope == SkillScopeKind.Project
            ? ResolveProjectTarget(request, descriptor.Host, descriptor.ProjectDefaultTargetPath)
            : ResolveUserTarget(request, descriptor);
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> ResolveProjectTarget (
        SkillInstallRequest request,
        SkillHostKind host,
        string projectTargetDirectory)
    {
        var repositoryRoot = request.RepositoryRoot!;
        var targetRoot = request.TargetRoot is null
            ? Path.Combine(repositoryRoot, projectTargetDirectory)
            : Path.IsPathRooted(request.TargetRoot)
                ? request.TargetRoot
                : Path.Combine(repositoryRoot, request.TargetRoot);

        var resolvedTargetRoot = SkillPackagePathBoundary.ResolveUnderRoot(repositoryRoot, targetRoot);
        return resolvedTargetRoot.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(host, resolvedTargetRoot.Value!))
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(resolvedTargetRoot.Failure!.Code, resolvedTargetRoot.Failure.Message);
    }

    private SkillOperationResult<SkillResolvedInstallTarget> ResolveUserTarget (
        SkillInstallRequest request,
        SkillHostDescriptor descriptor)
    {
        if (request.TargetRoot is not null)
        {
            var explicitTargetResult = SkillPackagePathBoundary.ResolveUnderRoot(request.TargetRoot, request.TargetRoot);
            return explicitTargetResult.IsSuccess
                ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(descriptor.Host, explicitTargetResult.Value!))
                : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(explicitTargetResult.Failure!.Code, explicitTargetResult.Failure.Message);
        }

        var defaultTargetResult = userTargetRootResolver.ResolveDefaultTargetRoot(descriptor);
        if (!defaultTargetResult.IsSuccess)
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(defaultTargetResult.Failure!.Code, defaultTargetResult.Failure.Message);
        }

        var targetRoot = defaultTargetResult.Value!;
        var resolvedTargetRoot = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, targetRoot);
        return resolvedTargetRoot.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(descriptor.Host, resolvedTargetRoot.Value!))
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(resolvedTargetRoot.Failure!.Code, resolvedTargetRoot.Failure.Message);
    }
}
