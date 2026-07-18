using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Inventory;

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
    public async ValueTask<SkillOperationResult<SkillResolvedInstallTarget>> SelectTargetAsync (
        SkillInstallRequest request,
        SkillCatalogId catalogId,
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
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                candidatesResult.Failure!.Code,
                candidatesResult.Failure.Message);
        }

        var candidates = candidatesResult.Value!;
        if (candidates.DefaultHostRoot is null)
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.Success(candidates.PreferredTarget);
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
                return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
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
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                SkillFailureCodes.InstallTargetRootConflict,
                $"SKILL catalog '{catalogId.Value}' is installed under multiple compatible target roots: {string.Join(", ", matchingTargets.Select(static target => target.TargetRoot))}");
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
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
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
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                siblingCollisionResult.Failure!.Code,
                siblingCollisionResult.Failure.Message);
        }

        return SkillOperationResult<SkillResolvedInstallTarget>.Success(activeTarget);
    }

    private static SkillOperationResult<bool> ValidateActiveCatalogDirectoryRoot (
        SkillInstallTargetCandidates candidates,
        SkillCatalogId catalogId,
        SkillResolvedInstallTarget activeTarget)
    {
        var hostRoot = candidates.DefaultHostRoot;
        if (!candidates.IncludesCatalogDirectoryLayout
            || hostRoot is null
            || IsSamePath(hostRoot, activeTarget.TargetRoot)
            || !Directory.Exists(activeTarget.TargetRoot))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        return ContainsSkillRootMarker(activeTarget.TargetRoot)
            ? SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetRootConflict,
                $"Preferred target root for catalog '{catalogId.Value}' is already occupied by a flat SKILL: {activeTarget.TargetRoot}")
            : SkillOperationResult<bool>.Success(false);
    }

    private static SkillOperationResult<bool> ValidateNoSiblingCatalogSkillNameCollision (
        SkillInstallTargetCandidates candidates,
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> selectedSkillNames,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hostRoot = candidates.DefaultHostRoot;
        if (!candidates.IncludesCatalogDirectoryLayout
            || hostRoot is null
            || selectedSkillNames.Count == 0
            || !Directory.Exists(hostRoot))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        string[] siblingRoots;
        try
        {
            siblingRoots = Directory.GetDirectories(hostRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetReadFailed,
                $"Could not inspect sibling SKILL catalog roots: {hostRoot}. {ex.Message}");
        }

        var candidateRoots = candidates.Targets
            .Select(static target => target.TargetRoot)
            .ToArray();
        foreach (var siblingRoot in siblingRoots.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidateRoots.Any(candidateRoot => IsSamePath(candidateRoot, siblingRoot)))
            {
                continue;
            }

            var siblingRootResult = SkillPackagePathBoundary.ResolveUnderRoot(hostRoot, siblingRoot);
            if (!siblingRootResult.IsSuccess)
            {
                foreach (var selectedSkillName in selectedSkillNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Directory.Exists(Path.Combine(siblingRoot, selectedSkillName.Value)))
                    {
                        return SkillOperationResult<bool>.FailureResult(
                            siblingRootResult.Failure!.Code,
                            siblingRootResult.Failure.Message);
                    }
                }

                continue;
            }

            var resolvedSiblingRoot = siblingRootResult.Value!;
            if (candidateRoots.Any(candidateRoot => IsSamePath(candidateRoot, resolvedSiblingRoot)))
            {
                continue;
            }

            if (ContainsSkillRootMarker(resolvedSiblingRoot))
            {
                continue;
            }

            foreach (var selectedSkillName in selectedSkillNames.OrderBy(static name => name.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(
                    resolvedSiblingRoot,
                    selectedSkillName.Value);
                if (!skillDirectoryResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(
                        skillDirectoryResult.Failure!.Code,
                        skillDirectoryResult.Failure.Message);
                }

                if (!Directory.Exists(skillDirectoryResult.Value!))
                {
                    continue;
                }

                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.InstallTargetNameCollision,
                    $"SKILL name '{selectedSkillName.Value}' is already present under another catalog directory while selecting catalog '{catalogId.Value}': {skillDirectoryResult.Value}");
            }
        }

        return SkillOperationResult<bool>.Success(false);
    }

    private static bool ContainsSkillRootMarker (string candidateRoot)
    {
        return File.Exists(Path.Combine(candidateRoot, "agent-skill.json"))
            || File.Exists(Path.Combine(candidateRoot, "SKILL.md"));
    }

    private async ValueTask<SkillOperationResult<bool>> ContainsCatalogOrSelectedTargetAsync (
        string targetRoot,
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> selectedSkillNames,
        IReadOnlyList<string> candidateRoots,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(targetRoot))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        string[] skillDirectories;
        try
        {
            skillDirectories = Directory.GetDirectories(targetRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetReadFailed,
                $"Could not inspect compatible SKILL target root: {targetRoot}. {ex.Message}");
        }

        foreach (var skillDirectory in skillDirectories.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(skillDirectory));
            var isSelectedName = SkillName.TryCreate(directoryName, out var skillName)
                && selectedSkillNames.Contains(skillName);
            var isAnotherCandidateRoot = candidateRoots.Any(candidateRoot =>
                !IsSamePath(candidateRoot, targetRoot) && IsSamePath(candidateRoot, skillDirectory));

            var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, skillDirectory);
            if (!skillDirectoryResult.IsSuccess)
            {
                if (isSelectedName && !isAnotherCandidateRoot)
                {
                    return SkillOperationResult<bool>.Success(true);
                }

                continue;
            }

            var resolvedSkillDirectory = skillDirectoryResult.Value!;
            isAnotherCandidateRoot = isAnotherCandidateRoot || candidateRoots.Any(candidateRoot =>
                !IsSamePath(candidateRoot, targetRoot) && IsSamePath(candidateRoot, resolvedSkillDirectory));
            var manifestPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(
                targetRoot,
                resolvedSkillDirectory,
                "agent-skill.json");
            var hasManifest = manifestPathResult.IsSuccess && File.Exists(manifestPathResult.Value!);
            if (hasManifest)
            {
                var manifestResult = await installedManifestReader
                    .ReadRequiredAsync(resolvedSkillDirectory, cancellationToken)
                    .ConfigureAwait(false);
                if (manifestResult.IsSuccess && manifestResult.Value!.Manifest.CatalogId == catalogId)
                {
                    return SkillOperationResult<bool>.Success(true);
                }

                if (isSelectedName)
                {
                    return SkillOperationResult<bool>.Success(true);
                }
            }

            if (isSelectedName && !isAnotherCandidateRoot)
            {
                return SkillOperationResult<bool>.Success(true);
            }
        }

        return SkillOperationResult<bool>.Success(false);
    }

    private static bool IsSamePath (
        string left,
        string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            comparison);
    }
}
