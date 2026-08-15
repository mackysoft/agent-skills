using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Inventory;

/// <summary> Selects the one compatible bundle target root that already owns a catalog, or its preferred current root. </summary>
public sealed class SkillCatalogTargetRootSelector
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillInstalledManifestReader installedManifestReader;

    /// <summary> Initializes a new instance of the <see cref="SkillCatalogTargetRootSelector" /> class. </summary>
    /// <param name="targetResolver"> The host target candidate resolver. </param>
    /// <param name="installedManifestReader"> The installed manifest reader used to identify catalog ownership. </param>
    public SkillCatalogTargetRootSelector (
        SkillInstallTargetResolver targetResolver,
        SkillInstalledManifestReader installedManifestReader)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
    }

    /// <summary> Selects the active bundle target root for one catalog operation. </summary>
    /// <param name="request"> The host target request. </param>
    /// <param name="catalogId"> The catalog that owns the operation. </param>
    /// <param name="selectedSkillNames"> The exact skill names that the operation may access. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The explicit root, the single compatible root containing the catalog, or the preferred current root when the
    /// catalog is not installed. Returns a structured failure when one safe root cannot be selected or a sibling
    /// catalog already contains a selected SKILL name.
    /// </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillResolvedInstallTarget>> SelectTargetAsync (
        SkillInstallRequest request,
        AgentDistributionCatalogId catalogId,
        IReadOnlyList<SkillName> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalogId);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var selectedNames = new HashSet<SkillName>();
        foreach (var skillName in selectedSkillNames)
        {
            if (skillName is null)
            {
                throw new ArgumentException("Selected SKILL names must not contain null items.", nameof(selectedSkillNames));
            }

            selectedNames.Add(skillName);
        }

        var candidatesResult = targetResolver.ResolveTargetCandidates(request, catalogId);
        if (!candidatesResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                candidatesResult.Failure!.Code,
                candidatesResult.Failure.Message);
        }

        var candidates = candidatesResult.Value!;
        if (candidates.DefaultHostRoot is null)
        {
            return AgentDistributionOperationResult<SkillResolvedInstallTarget>.Success(candidates.PreferredTarget);
        }

        var candidateRoots = candidates.Targets
            .Select(static target => target.TargetRoot)
            .ToArray();
        var matchingTargets = new List<SkillResolvedInstallTarget>();
        foreach (var target in candidates.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var containsCatalogResult = await ContainsCatalogOrSelectedTargetAsync(
                    target.TargetRoot,
                    catalogId,
                    selectedNames,
                    candidateRoots,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!containsCatalogResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                    containsCatalogResult.Failure!.Code,
                    containsCatalogResult.Failure.Message);
            }

            if (containsCatalogResult.Value)
            {
                matchingTargets.Add(target);
            }
        }

        if (matchingTargets.Count > 1)
        {
            return AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetRootConflict,
                $"SKILL catalog '{catalogId.Value}' is installed under multiple compatible target roots: {string.Join(", ", matchingTargets.Select(static target => target.TargetRoot.Value))}");
        }

        var activeTarget = matchingTargets.Count == 1
            ? matchingTargets[0]
            : candidates.PreferredTarget;
        var activeTargetShapeResult = ValidateActiveCatalogDirectoryRoot(
            candidates,
            catalogId,
            activeTarget);
        if (!activeTargetShapeResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                activeTargetShapeResult.Failure!.Code,
                activeTargetShapeResult.Failure.Message);
        }

        var siblingCollisionResult = ValidateNoSiblingCatalogSkillNameCollision(
            candidates,
            catalogId,
            selectedNames,
            cancellationToken);
        if (!siblingCollisionResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillResolvedInstallTarget>.FailureResult(
                siblingCollisionResult.Failure!.Code,
                siblingCollisionResult.Failure.Message);
        }

        return AgentDistributionOperationResult<SkillResolvedInstallTarget>.Success(activeTarget);
    }

    private static AgentDistributionOperationResult<bool> ValidateActiveCatalogDirectoryRoot (
        SkillInstallTargetCandidates candidates,
        AgentDistributionCatalogId catalogId,
        SkillResolvedInstallTarget activeTarget)
    {
        var hostRoot = candidates.DefaultHostRoot;
        if (!candidates.IncludesCatalogDirectoryLayout
            || hostRoot is null
            || hostRoot.IsSameAs(activeTarget.TargetRoot)
            || !Directory.Exists(activeTarget.TargetRoot.Value))
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        return ContainsSkillRootMarker(activeTarget.TargetRoot)
            ? AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetRootConflict,
                $"Preferred target root for catalog '{catalogId.Value}' is already occupied by a flat SKILL: {activeTarget.TargetRoot}")
            : AgentDistributionOperationResult<bool>.Success(false);
    }

    private static AgentDistributionOperationResult<bool> ValidateNoSiblingCatalogSkillNameCollision (
        SkillInstallTargetCandidates candidates,
        AgentDistributionCatalogId catalogId,
        IReadOnlySet<SkillName> selectedSkillNames,
        CancellationToken cancellationToken)
    {
        return SiblingCatalogInspection.Validate(candidates, catalogId, selectedSkillNames, cancellationToken);
    }

    private static bool ContainsSkillRootMarker (AbsolutePath candidateRoot)
    {
        var manifestPath = ContainedPath.Create(candidateRoot, RootRelativePath.Parse("agent-skill.json")).Target;
        var skillBodyPath = ContainedPath.Create(candidateRoot, RootRelativePath.Parse("SKILL.md")).Target;
        return File.Exists(manifestPath.Value)
            || File.Exists(skillBodyPath.Value);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ContainsCatalogOrSelectedTargetAsync (
        AbsolutePath targetRoot,
        AgentDistributionCatalogId catalogId,
        IReadOnlySet<SkillName> selectedSkillNames,
        IReadOnlyList<AbsolutePath> candidateRoots,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(targetRoot.Value))
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        AbsolutePath[] skillDirectories;
        try
        {
            skillDirectories = Directory.GetDirectories(targetRoot.Value)
                .Select(AbsolutePath.Parse)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetReadFailed,
                $"Could not inspect compatible SKILL target root: {targetRoot}. {ex.Message}");
        }

        foreach (var skillDirectory in skillDirectories.OrderBy(static path => path.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(skillDirectory.Value));
            var isSelectedName = SkillName.TryCreate(directoryName, out var skillName)
                && selectedSkillNames.Contains(skillName);
            var isAnotherCandidateRoot = candidateRoots.Any(candidateRoot =>
                !candidateRoot.IsSameAs(targetRoot) && candidateRoot.IsSameAs(skillDirectory));

            var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(targetRoot, skillDirectory);
            if (!skillDirectoryResult.IsSuccess)
            {
                if (isSelectedName && !isAnotherCandidateRoot)
                {
                    return AgentDistributionOperationResult<bool>.Success(true);
                }

                continue;
            }

            var resolvedSkillDirectory = skillDirectoryResult.Value!;
            isAnotherCandidateRoot = isAnotherCandidateRoot || candidateRoots.Any(candidateRoot =>
                !candidateRoot.IsSameAs(targetRoot) && candidateRoot.IsSameAs(resolvedSkillDirectory));
            var manifestPathResult = PackagePathResolver.ResolveRegularFile(
                resolvedSkillDirectory,
                PackageRelativePath.Parse("agent-skill.json"));
            var hasManifest = manifestPathResult.IsSuccess && File.Exists(manifestPathResult.Value!.Value);
            if (hasManifest)
            {
                var manifestResult = await installedManifestReader
                    .ReadRequiredAsync(resolvedSkillDirectory, cancellationToken)
                    .ConfigureAwait(false);
                if (manifestResult.IsSuccess && manifestResult.Value!.Manifest.CatalogId == catalogId)
                {
                    return AgentDistributionOperationResult<bool>.Success(true);
                }

                if (isSelectedName)
                {
                    return AgentDistributionOperationResult<bool>.Success(true);
                }
            }

            if (isSelectedName && !isAnotherCandidateRoot)
            {
                return AgentDistributionOperationResult<bool>.Success(true);
            }
        }

        return AgentDistributionOperationResult<bool>.Success(false);
    }

    private sealed class SiblingCatalogInspection
    {
        private AgentDistributionCatalogId CatalogId { get; init; } = null!;

        private AbsolutePath HostRoot { get; init; } = null!;

        private IReadOnlyList<AbsolutePath> CandidateRoots { get; init; } = null!;

        private IReadOnlyList<AbsolutePath> PhysicalCandidateRoots { get; init; } = null!;

        private IReadOnlySet<SkillName> SelectedSkillNames { get; init; } = null!;

        private IReadOnlyList<AbsolutePath> SiblingRoots { get; init; } = null!;

        public static AgentDistributionOperationResult<bool> Validate (
            SkillInstallTargetCandidates candidates,
            AgentDistributionCatalogId catalogId,
            IReadOnlySet<SkillName> selectedSkillNames,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hostRoot = candidates.DefaultHostRoot;
            if (!candidates.IncludesCatalogDirectoryLayout
                || hostRoot is null
                || selectedSkillNames.Count == 0
                || !Directory.Exists(hostRoot.Value))
            {
                return AgentDistributionOperationResult<bool>.Success(false);
            }

            var inspectionResult = Create(candidates, catalogId, selectedSkillNames, hostRoot);
            return inspectionResult.IsSuccess
                ? inspectionResult.Value!.Validate(cancellationToken)
                : AgentDistributionOperationResult<bool>.FailureResult(
                    inspectionResult.Failure!.Code,
                    inspectionResult.Failure.Message);
        }

        private static AgentDistributionOperationResult<SiblingCatalogInspection> Create (
            SkillInstallTargetCandidates candidates,
            AgentDistributionCatalogId catalogId,
            IReadOnlySet<SkillName> selectedSkillNames,
            AbsolutePath hostRoot)
        {
            var siblingRootsResult = EnumerateSiblingCatalogRoots(hostRoot);
            if (!siblingRootsResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SiblingCatalogInspection>.FailureResult(
                    siblingRootsResult.Failure!.Code,
                    siblingRootsResult.Failure.Message);
            }

            var candidateRoots = candidates.Targets.Select(static target => target.TargetRoot).ToArray();
            var physicalCandidateRootsResult = ResolvePhysicalCandidateRoots(hostRoot, candidateRoots);
            if (!physicalCandidateRootsResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SiblingCatalogInspection>.FailureResult(
                    physicalCandidateRootsResult.Failure!.Code,
                    physicalCandidateRootsResult.Failure.Message);
            }

            return AgentDistributionOperationResult<SiblingCatalogInspection>.Success(new SiblingCatalogInspection
            {
                CatalogId = catalogId,
                CandidateRoots = candidateRoots,
                HostRoot = hostRoot,
                PhysicalCandidateRoots = physicalCandidateRootsResult.Value!,
                SelectedSkillNames = selectedSkillNames,
                SiblingRoots = siblingRootsResult.Value!,
            });
        }

        public AgentDistributionOperationResult<bool> Validate (CancellationToken cancellationToken)
        {
            foreach (var siblingRoot in SiblingRoots.OrderBy(static path => path.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CandidateRoots.Any(candidateRoot => candidateRoot.IsSameAs(siblingRoot)))
                {
                    continue;
                }

                var collisionResult = ValidateSiblingCatalog(siblingRoot, cancellationToken);
                if (!collisionResult.IsSuccess || collisionResult.Value)
                {
                    return collisionResult;
                }
            }

            return AgentDistributionOperationResult<bool>.Success(false);
        }

        private AgentDistributionOperationResult<bool> ValidateSiblingCatalog (
            AbsolutePath siblingRoot,
            CancellationToken cancellationToken)
        {
            var siblingRootResult = PackagePathResolver.ResolveUnderRoot(HostRoot, siblingRoot);
            if (!siblingRootResult.IsSuccess)
            {
                return HasSelectedSkillDirectory(siblingRoot, cancellationToken)
                    ? AgentDistributionOperationResult<bool>.FailureResult(
                        siblingRootResult.Failure!.Code,
                        siblingRootResult.Failure.Message)
                    : AgentDistributionOperationResult<bool>.Success(false);
            }

            var resolvedSiblingRoot = siblingRootResult.Value!;
            var physicalSiblingRootResult = ResolvePhysicalSiblingRoot(resolvedSiblingRoot);
            if (!physicalSiblingRootResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    physicalSiblingRootResult.Failure!.Code,
                    physicalSiblingRootResult.Failure.Message);
            }

            if (PhysicalCandidateRoots.Any(candidateRoot => candidateRoot.IsSameAs(physicalSiblingRootResult.Value!))
                || ContainsSkillRootMarker(resolvedSiblingRoot))
            {
                return AgentDistributionOperationResult<bool>.Success(false);
            }

            return FindSelectedSkillNameCollision(resolvedSiblingRoot, cancellationToken);
        }

        private bool HasSelectedSkillDirectory (AbsolutePath siblingRoot, CancellationToken cancellationToken)
        {
            foreach (var selectedSkillName in SelectedSkillNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selectedSkillPath = ContainedPath.Create(
                    siblingRoot,
                    RootRelativePath.Parse(selectedSkillName.Value)).Target;
                if (Directory.Exists(selectedSkillPath.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private AgentDistributionOperationResult<AbsolutePath> ResolvePhysicalSiblingRoot (AbsolutePath resolvedSiblingRoot)
        {
            if (!ContainedPath.TryCreate(
                    HostRoot,
                    resolvedSiblingRoot,
                    out var containedSiblingRoot,
                    out var siblingContainmentFailure))
            {
                return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                    AgentDistributionFailureCodes.PathUnsafe,
                    $"Sibling SKILL catalog root escaped its host root: {siblingContainmentFailure.Message}");
            }

            if (!PhysicalPathResolver.TryResolve(
                    containedSiblingRoot,
                    SymbolicLinkHandling.Follow,
                    MissingPathHandling.Reject,
                    out var siblingResolution,
                    out var siblingFailure))
            {
                return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                    AgentDistributionFailureCodes.PathUnsafe,
                    $"Sibling SKILL catalog root could not be resolved: {siblingFailure.Message}");
            }

            return AgentDistributionOperationResult<AbsolutePath>.Success(siblingResolution.ResolvedPath.Target);
        }

        private AgentDistributionOperationResult<bool> FindSelectedSkillNameCollision (
            AbsolutePath resolvedSiblingRoot,
            CancellationToken cancellationToken)
        {
            foreach (var selectedSkillName in SelectedSkillNames.OrderBy(static name => name.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var skillDirectoryPath = ContainedPath.Create(
                    resolvedSiblingRoot,
                    RootRelativePath.Parse(selectedSkillName.Value)).Target;
                var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(resolvedSiblingRoot, skillDirectoryPath);
                if (!skillDirectoryResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(
                        skillDirectoryResult.Failure!.Code,
                        skillDirectoryResult.Failure.Message);
                }

                if (Directory.Exists(skillDirectoryResult.Value!.Value))
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(
                        AgentDistributionFailureCodes.InstallTargetNameCollision,
                        $"SKILL name '{selectedSkillName.Value}' is already present under another catalog directory while selecting catalog '{CatalogId.Value}': {skillDirectoryResult.Value}");
                }
            }

            return AgentDistributionOperationResult<bool>.Success(false);
        }

        private static AgentDistributionOperationResult<AbsolutePath[]> EnumerateSiblingCatalogRoots (AbsolutePath hostRoot)
        {
            AbsolutePath[] siblingRoots;
            try
            {
                siblingRoots = Directory.GetDirectories(hostRoot.Value)
                    .Select(AbsolutePath.Parse)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return AgentDistributionOperationResult<AbsolutePath[]>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetReadFailed,
                    $"Could not inspect sibling SKILL catalog roots: {hostRoot}. {ex.Message}");
            }

            return AgentDistributionOperationResult<AbsolutePath[]>.Success(siblingRoots);
        }

        private static AgentDistributionOperationResult<IReadOnlyList<AbsolutePath>> ResolvePhysicalCandidateRoots (
            AbsolutePath hostRoot,
            IReadOnlyList<AbsolutePath> candidateRoots)
        {
            var physicalCandidateRoots = new List<AbsolutePath>(candidateRoots.Count);
            foreach (var candidateRoot in candidateRoots)
            {
                var candidateRootResult = ResolvePhysicalCandidateRoot(hostRoot, candidateRoot);
                if (!candidateRootResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<IReadOnlyList<AbsolutePath>>.FailureResult(
                        candidateRootResult.Failure!.Code,
                        candidateRootResult.Failure.Message);
                }

                physicalCandidateRoots.Add(candidateRootResult.Value!);
            }

            return AgentDistributionOperationResult<IReadOnlyList<AbsolutePath>>.Success(physicalCandidateRoots);
        }

        private static AgentDistributionOperationResult<AbsolutePath> ResolvePhysicalCandidateRoot (
            AbsolutePath hostRoot,
            AbsolutePath candidateRoot)
        {
            if (!ContainedPath.TryCreate(hostRoot, candidateRoot, out var containedCandidateRoot, out var containmentFailure))
            {
                return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                    AgentDistributionFailureCodes.PathUnsafe,
                    $"SKILL target candidate is outside its host root: {containmentFailure.Message}");
            }

            if (!PhysicalPathResolver.TryResolve(
                    containedCandidateRoot,
                    SymbolicLinkHandling.Follow,
                    MissingPathHandling.AllowMissingTail,
                    out var candidateResolution,
                    out var candidateFailure))
            {
                return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                    AgentDistributionFailureCodes.PathUnsafe,
                    $"SKILL target candidate could not be resolved: {candidateFailure.Message}");
            }

            return AgentDistributionOperationResult<AbsolutePath>.Success(candidateResolution.ResolvedPath.Target);
        }
    }

}
