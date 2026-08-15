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

    /// <summary> Resolves one supported SKILL host for targeting and reporting. </summary>
    public AgentDistributionOperationResult<SkillResolvedHost> ResolveHost (HostKind host)
    {
        var registrationResult = BuiltInHostCatalog.Get(host);
        return registrationResult.IsSuccess
            ? AgentDistributionOperationResult<SkillResolvedHost>.Success(
                new SkillResolvedHost(registrationResult.Value!.Host, registrationResult.Value.Skill))
            : AgentDistributionOperationResult<SkillResolvedHost>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
    }

    /// <summary> Gets all supported SKILL hosts in canonical host order. </summary>
    public IReadOnlyList<SkillResolvedHost> GetSupportedHosts ()
    {
        return BuiltInHostCatalog.Registrations
            .Select(static registration => new SkillResolvedHost(registration.Host, registration.Skill))
            .ToArray();
    }

    /// <summary> Resolves the preferred bundle target root without inspecting installed catalog state. </summary>
    /// <param name="request"> The install request. </param>
    /// <param name="catalogId"> The catalog that owns the resolved bundle target. </param>
    /// <returns> The canonical preferred bundle target, or a structured path-resolution failure. </returns>
    public AgentDistributionOperationResult<SkillResolvedInstallTarget> ResolveTarget (
        SkillInstallRequest request,
        AgentDistributionCatalogId catalogId)
    {
        var candidatesResult = ResolveTargetCandidates(request, catalogId);
        return candidatesResult.IsSuccess
            ? AgentDistributionOperationResult<SkillResolvedInstallTarget>.Success(candidatesResult.Value!.PreferredTarget)
            : AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                candidatesResult.Failure!.Code,
                candidatesResult.Failure.Message);
    }

    internal AgentDistributionOperationResult<SkillInstallTargetCandidates> ResolveTargetCandidates (
        SkillInstallRequest request,
        AgentDistributionCatalogId catalogId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalogId);

        var hostResult = ResolveHost(request.Host);
        if (!hostResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstallTargetCandidates>.FailureResult(
                hostResult.Failure!.Code,
                hostResult.Failure.Message);
        }

        var resolvedHost = hostResult.Value!;
        var descriptor = resolvedHost.Descriptor;
        if (request.TargetRoot is not null)
        {
            var explicitTargetResult = request.Scope == SkillScopeKind.Project
                ? ResolveExplicitProjectTarget(request, resolvedHost)
                : ResolveExplicitUserTarget(request, resolvedHost);
            return CreateCandidateSet(explicitTargetResult);
        }

        var hostRootResult = request.Scope == SkillScopeKind.Project
            ? ResolveDefaultProjectHostRoot(request, descriptor.ProjectDefaultTargetPath)
            : userTargetRootResolver.ResolveDefaultTargetRoot(descriptor);
        if (!hostRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstallTargetCandidates>.FailureResult(
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
            var targetResult = CreateResolvedTarget(resolvedHost, request.Scope, bundleTargetRootResult);
            if (!targetResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillInstallTargetCandidates>.FailureResult(
                    targetResult.Failure!.Code,
                    targetResult.Failure.Message);
            }

            targets.Add(targetResult.Value!);
        }

        return AgentDistributionOperationResult<SkillInstallTargetCandidates>.Success(new SkillInstallTargetCandidates(
            targets,
            hostRootResult.Value!,
            layouts.Contains(SkillBundleTargetRootLayout.CatalogDirectory)));
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveDefaultProjectHostRoot (
        SkillInstallRequest request,
        RootRelativePath projectTargetDirectory)
    {
        var repositoryRoot = request.RepositoryRoot!;
        var result = PackagePathResolver.ResolveUnderRoot(
            repositoryRoot,
            ContainedPath.Create(repositoryRoot, projectTargetDirectory).Target);
        return result;
    }

    private static AgentDistributionOperationResult<SkillResolvedInstallTarget> ResolveExplicitProjectTarget (
        SkillInstallRequest request,
        SkillResolvedHost host)
    {
        var repositoryRoot = request.RepositoryRoot!;
        var targetRootResult = PackagePathResolver.ResolveUnderRoot(repositoryRoot, request.TargetRoot!);
        return CreateResolvedTarget(host, request.Scope, targetRootResult);
    }

    private static AgentDistributionOperationResult<SkillResolvedInstallTarget> ResolveExplicitUserTarget (
        SkillInstallRequest request,
        SkillResolvedHost host)
    {
        var targetRoot = request.TargetRoot!;
        var targetRootResult = PackagePathResolver.ResolveUnderRoot(targetRoot, targetRoot);
        return CreateResolvedTarget(host, request.Scope, targetRootResult);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveDefaultBundleTargetRoot (
        AbsolutePath hostRoot,
        AgentDistributionCatalogId catalogId,
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

    private static AgentDistributionOperationResult<SkillResolvedInstallTarget> CreateResolvedTarget (
        SkillResolvedHost host,
        SkillScopeKind scope,
        AgentDistributionOperationResult<AbsolutePath> targetRootResult)
    {
        return targetRootResult.IsSuccess
            ? AgentDistributionOperationResult<SkillResolvedInstallTarget>.Success(
                new SkillResolvedInstallTarget(host, scope, targetRootResult.Value!))
            : AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                targetRootResult.Failure!.Code,
                targetRootResult.Failure.Message);
    }

    private static AgentDistributionOperationResult<SkillInstallTargetCandidates> CreateCandidateSet (
        AgentDistributionOperationResult<SkillResolvedInstallTarget> targetResult)
    {
        return targetResult.IsSuccess
            ? AgentDistributionOperationResult<SkillInstallTargetCandidates>.Success(
                new SkillInstallTargetCandidates(
                    [targetResult.Value!],
                    defaultHostRoot: null,
                    includesCatalogDirectoryLayout: false))
            : AgentDistributionOperationResult<SkillInstallTargetCandidates>.FailureResult(
                targetResult.Failure!.Code,
                targetResult.Failure.Message);
    }
}
