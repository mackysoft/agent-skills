using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Targeting;

/// <summary> Resolves project- and user-scope SKILL bundle target roots. </summary>
public sealed class SkillInstallTargetResolver
{
    private readonly SkillUserTargetRootResolver userTargetRootResolver;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="userTargetRootResolver"> The user-scope host-root resolver. </param>
    public SkillInstallTargetResolver (SkillUserTargetRootResolver userTargetRootResolver)
    {
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

        var registrationResult = HostRegistration.Get(request.Host);
        if (!registrationResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallTargetCandidates>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
        }

        var registration = registrationResult.Value!;
        var descriptor = registration.Skill;
        if (request.TargetRoot is not null)
        {
            var explicitTargetResult = request.Scope == SkillScopeKind.Project
                ? ResolveExplicitProjectTarget(request, registration.Host)
                : ResolveExplicitUserTarget(request, registration.Host);
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
            var targetResult = CreateResolvedTarget(registration.Host, bundleTargetRootResult);
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

    private static SkillOperationResult<AbsolutePath> ResolveDefaultProjectHostRoot (
        SkillInstallRequest request,
        RootRelativePath projectTargetDirectory)
    {
        var repositoryRoot = request.RepositoryRoot!;
        var result = PackagePathResolver.ResolveUnderRoot(
            repositoryRoot,
            ContainedPath.Create(repositoryRoot, projectTargetDirectory).Target);
        return result;
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> ResolveExplicitProjectTarget (
        SkillInstallRequest request,
        HostKind host)
    {
        var repositoryRoot = request.RepositoryRoot!;
        var targetRootResult = PackagePathResolver.ResolveUnderRoot(repositoryRoot, request.TargetRoot!);
        return CreateResolvedTarget(host, targetRootResult);
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> ResolveExplicitUserTarget (
        SkillInstallRequest request,
        HostKind host)
    {
        var targetRoot = request.TargetRoot!;
        var targetRootResult = PackagePathResolver.ResolveUnderRoot(targetRoot, targetRoot);
        return CreateResolvedTarget(host, targetRootResult);
    }

    private static SkillOperationResult<AbsolutePath> ResolveDefaultBundleTargetRoot (
        AbsolutePath hostRoot,
        SkillCatalogId catalogId,
        SkillBundleTargetRootLayout layout)
    {
        return layout switch
        {
            SkillBundleTargetRootLayout.Flat => PackagePathResolver.ResolveUnderRoot(hostRoot, hostRoot),
            SkillBundleTargetRootLayout.CatalogDirectory => PackagePathResolver.ResolveUnderRoot(
                hostRoot,
                ContainedPath.Create(hostRoot, RootRelativePath.Parse(catalogId.Value)).Target),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unsupported bundle target-root layout."),
        };
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> CreateResolvedTarget (
        HostKind host,
        SkillOperationResult<AbsolutePath> targetRootResult)
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
