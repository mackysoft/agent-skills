using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Resolves project- and user-scope SKILL bundle target roots. </summary>
public sealed class SkillInstallTargetResolver
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillUserTargetRootResolver userTargetRootResolver;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="userTargetRootResolver"> The user-scope host-root resolver. </param>
    public SkillInstallTargetResolver (
        SkillHostAdapterSet hostAdapters,
        SkillUserTargetRootResolver userTargetRootResolver)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.userTargetRootResolver = userTargetRootResolver ?? throw new ArgumentNullException(nameof(userTargetRootResolver));
    }

    /// <summary> Resolves the preferred bundle target root without inspecting installed catalog state. </summary>
    /// <param name="request"> The install request. </param>
    /// <param name="catalogId"> The catalog that owns the resolved bundle target. </param>
    /// <returns> The canonical preferred bundle target, or a structured path-resolution failure. </returns>
    public SkillOperationResult<SkillResolvedInstallTarget> ResolveTarget (
        SkillInstallRequest request,
        SkillCatalogId catalogId)
    {
        var candidatesResult = ResolveTargetCandidates(request, catalogId);
        return candidatesResult.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(candidatesResult.Value!.PreferredTarget)
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                candidatesResult.Failure!.Code,
                candidatesResult.Failure.Message);
    }

    internal SkillOperationResult<SkillInstallTargetCandidates> ResolveTargetCandidates (
        SkillInstallRequest request,
        SkillCatalogId catalogId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalogId);

        var adapterResult = hostAdapters.GetAdapter(request.Host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallTargetCandidates>.FailureResult(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message);
        }

        var descriptor = adapterResult.Value!.Descriptor;
        if (request.TargetRoot is not null)
        {
            var explicitTargetResult = request.Scope == SkillScopeKind.Project
                ? ResolveExplicitProjectTarget(request, descriptor.Host)
                : ResolveExplicitUserTarget(request, descriptor.Host);
            return CreateCandidateSet(explicitTargetResult);
        }

        var hostRootResult = request.Scope == SkillScopeKind.Project
            ? ResolveDefaultProjectHostRoot(request, descriptor.ProjectDefaultTargetPath)
            : userTargetRootResolver.ResolveDefaultTargetRoot(descriptor);
        if (!hostRootResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallTargetCandidates>.FailureResult(
                hostRootResult.Failure!.Code,
                hostRootResult.Failure.Message);
        }

        var layouts = new[] { descriptor.BundleTargetRootLayout }
            .Concat(descriptor.CompatiblePreviousBundleTargetRootLayouts)
            .ToArray();
        var targets = new List<SkillResolvedInstallTarget>(layouts.Length);
        foreach (var layout in layouts)
        {
            var bundleTargetRootResult = ResolveDefaultBundleTargetRoot(hostRootResult.Value!, catalogId, layout);
            var targetResult = CreateResolvedTarget(descriptor.Host, bundleTargetRootResult);
            if (!targetResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallTargetCandidates>.FailureResult(
                    targetResult.Failure!.Code,
                    targetResult.Failure.Message);
            }

            targets.Add(targetResult.Value!);
        }

        return SkillOperationResult<SkillInstallTargetCandidates>.Success(new SkillInstallTargetCandidates(
            targets,
            hostRootResult.Value!,
            layouts.Contains(SkillBundleTargetRootLayout.CatalogDirectory)));
    }

    private static SkillOperationResult<string> ResolveDefaultProjectHostRoot (
        SkillInstallRequest request,
        string projectTargetDirectory)
    {
        var repositoryRoot = request.RepositoryRoot!;
        return SkillPackagePathBoundary.ResolveUnderRoot(
            repositoryRoot,
            Path.Combine(repositoryRoot, projectTargetDirectory));
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> ResolveExplicitProjectTarget (
        SkillInstallRequest request,
        SkillHostKind host)
    {
        var repositoryRoot = request.RepositoryRoot!;
        var requestedTargetRoot = Path.IsPathRooted(request.TargetRoot!)
            ? request.TargetRoot!
            : Path.Combine(repositoryRoot, request.TargetRoot!);
        var targetRootResult = SkillPackagePathBoundary.ResolveUnderRoot(repositoryRoot, requestedTargetRoot);
        return CreateResolvedTarget(host, targetRootResult);
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> ResolveExplicitUserTarget (
        SkillInstallRequest request,
        SkillHostKind host)
    {
        var targetRootResult = SkillPackagePathBoundary.ResolveUnderRoot(request.TargetRoot!, request.TargetRoot!);
        return CreateResolvedTarget(host, targetRootResult);
    }

    private static SkillOperationResult<string> ResolveDefaultBundleTargetRoot (
        string hostRoot,
        SkillCatalogId catalogId,
        SkillBundleTargetRootLayout layout)
    {
        return layout switch
        {
            SkillBundleTargetRootLayout.Flat => SkillPackagePathBoundary.ResolveUnderRoot(hostRoot, hostRoot),
            SkillBundleTargetRootLayout.CatalogDirectory => SkillPackagePathBoundary.ResolvePackageDirectory(hostRoot, catalogId.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unsupported bundle target-root layout."),
        };
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> CreateResolvedTarget (
        SkillHostKind host,
        SkillOperationResult<string> targetRootResult)
    {
        return targetRootResult.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(
                new SkillResolvedInstallTarget(host, targetRootResult.Value!))
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                targetRootResult.Failure!.Code,
                targetRootResult.Failure.Message);
    }

    private static SkillOperationResult<SkillInstallTargetCandidates> CreateCandidateSet (
        SkillOperationResult<SkillResolvedInstallTarget> targetResult)
    {
        return targetResult.IsSuccess
            ? SkillOperationResult<SkillInstallTargetCandidates>.Success(
                new SkillInstallTargetCandidates(
                    [targetResult.Value!],
                    defaultHostRoot: null,
                    includesCatalogDirectoryLayout: false))
            : SkillOperationResult<SkillInstallTargetCandidates>.FailureResult(
                targetResult.Failure!.Code,
                targetResult.Failure.Message);
    }
}
