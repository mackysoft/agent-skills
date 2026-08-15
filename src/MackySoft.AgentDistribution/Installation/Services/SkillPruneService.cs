using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Diffing;
using MackySoft.AgentDistribution.Installation.Inventory;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Services;

/// <summary> Prunes installed managed SKILL packages that no longer exist in a product catalog. </summary>
public sealed class SkillPruneService
{
    private readonly SkillCatalogTargetRootSelector targetSelector;
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;
    private readonly ISkillInstalledPackageRemover packageRemover;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillPruneService" /> class. </summary>
    /// <param name="targetSelector"> The installed-catalog-aware target selector. </param>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    /// <param name="packageRemover"> The installed package remover. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillPruneService (
        SkillCatalogTargetRootSelector targetSelector,
        SkillInstalledManifestReader installedManifestReader,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier,
        ISkillInstalledPackageRemover packageRemover,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.installedPackageIntegrityVerifier = installedPackageIntegrityVerifier ?? throw new ArgumentNullException(nameof(installedPackageIntegrityVerifier));
        this.packageRemover = packageRemover ?? throw new ArgumentNullException(nameof(packageRemover));
        this.diffBuilder = diffBuilder ?? throw new ArgumentNullException(nameof(diffBuilder));
    }

    /// <summary> Prunes installed managed SKILL packages that no longer exist in the current catalog. </summary>
    /// <param name="input"> The prune input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The prune result or failure. </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillPruneResult>> PruneAsync (
        SkillPruneInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var currentCatalogSkillNames = input.CurrentCatalogPackages
            .Select(static package => package.Manifest.SkillName)
            .ToHashSet();
        var targetSkillNames = input.CurrentCatalogPackages
            .Select(static package => package.Manifest.SkillName)
            .Concat(input.SelectedSkillNames ?? [])
            .Distinct()
            .ToArray();
        var targetResult = await targetSelector.SelectTargetAsync(
                input.TargetRequest,
                input.CatalogId,
                targetSkillNames,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var selectedCategories = CreateSelectedCategorySet(input.SelectedCategories);
        var selectedSkillNames = CreateSelectedSkillNameSet(input.SelectedSkillNames);
        var planningContext = new SkillPrunePlanningContext(
            input,
            target,
            currentCatalogSkillNames,
            selectedCategories);
        var actionPlansResult = await CreateActionPlansAsync(
                planningContext,
                selectedSkillNames,
                cancellationToken)
            .ConfigureAwait(false);
        if (!actionPlansResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneResult>.FailureResult(actionPlansResult.Failure!.Code, actionPlansResult.Failure.Message);
        }

        var actionPlans = actionPlansResult.Value!;
        if (!input.DryRun)
        {
            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!actionPlan.ShouldDelete)
                {
                    continue;
                }

                var preconditionResult = await ValidateDeletePreconditionAsync(
                        planningContext,
                        actionPlan.SkillDirectory,
                        actionPlan.TargetSnapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<SkillPruneResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!actionPlan.ShouldDelete)
                {
                    continue;
                }

                var deleteResult = await packageRemover.DeleteAsync(
                        planningContext.TargetRoot,
                        actionPlan.SkillDirectory,
                        (directory, token) => ValidateDeletePreconditionAsync(
                            planningContext,
                            directory,
                            actionPlan.TargetSnapshot,
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!deleteResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<SkillPruneResult>.FailureResult(deleteResult.Failure!.Code, deleteResult.Failure.Message);
                }
            }
        }

        return AgentDistributionOperationResult<SkillPruneResult>.Success(new SkillPruneResult(
            planningContext.TargetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private ValueTask<AgentDistributionOperationResult<IReadOnlyList<SkillPruneActionPlan>>> CreateActionPlansAsync (
        SkillPrunePlanningContext planningContext,
        IReadOnlySet<SkillName> selectedSkillNames,
        CancellationToken cancellationToken)
    {
        return Directory.Exists(planningContext.TargetRoot.Value)
            ? CreateActionPlansForExistingTargetRootAsync(planningContext, selectedSkillNames, cancellationToken)
            : CreateEmptyActionPlansResult();
    }

    private static ValueTask<AgentDistributionOperationResult<IReadOnlyList<SkillPruneActionPlan>>> CreateEmptyActionPlansResult ()
    {
        return ValueTask.FromResult(AgentDistributionOperationResult<IReadOnlyList<SkillPruneActionPlan>>.Success(Array.Empty<SkillPruneActionPlan>()));
    }

    private async ValueTask<AgentDistributionOperationResult<IReadOnlyList<SkillPruneActionPlan>>> CreateActionPlansForExistingTargetRootAsync (
        SkillPrunePlanningContext planningContext,
        IReadOnlySet<SkillName> selectedSkillNames,
        CancellationToken cancellationToken)
    {
        var actionPlans = new List<SkillPruneActionPlan>();
        foreach (var skillDirectoryValue in Directory.EnumerateDirectories(planningContext.TargetRoot.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var actionPlanResult = await CreateActionPlanForDirectoryAsync(
                    planningContext,
                    selectedSkillNames,
                    skillDirectoryValue,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!actionPlanResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillPruneActionPlan>>.FailureResult(
                    actionPlanResult.Failure!.Code,
                    actionPlanResult.Failure.Message);
            }

            if (actionPlanResult.Value!.ActionPlan is not null)
            {
                actionPlans.Add(actionPlanResult.Value.ActionPlan);
            }
        }

        return AgentDistributionOperationResult<IReadOnlyList<SkillPruneActionPlan>>.Success(actionPlans);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillPruneActionPlanResult>> CreateActionPlanForDirectoryAsync (
        SkillPrunePlanningContext planningContext,
        IReadOnlySet<SkillName> selectedSkillNames,
        string skillDirectoryValue,
        CancellationToken cancellationToken)
    {
        var directoryResult = CreateSelectedSkillDirectory(planningContext, selectedSkillNames, skillDirectoryValue);
        if (!directoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneActionPlanResult>.FailureResult(
                directoryResult.Failure!.Code,
                directoryResult.Failure.Message);
        }

        return directoryResult.Value!.Directory is null
            ? NoAction()
            : await CreateActionPlanAsync(planningContext, directoryResult.Value.Directory, cancellationToken).ConfigureAwait(false);
    }

    private static AgentDistributionOperationResult<SkillPruneDirectorySelection> CreateSelectedSkillDirectory (
        SkillPrunePlanningContext planningContext,
        IReadOnlySet<SkillName> selectedSkillNames,
        string skillDirectoryValue)
    {
        if (!AbsolutePath.TryParse(skillDirectoryValue, out var skillDirectory, out var pathFailure))
        {
            return AgentDistributionOperationResult<SkillPruneDirectorySelection>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Skill directory could not be inspected: {skillDirectoryValue}. {pathFailure.Message}");
        }

        return CreateSelectedSkillDirectory(planningContext, selectedSkillNames, skillDirectory);
    }

    private static AgentDistributionOperationResult<SkillPruneDirectorySelection> CreateSelectedSkillDirectory (
        SkillPrunePlanningContext planningContext,
        IReadOnlySet<SkillName> selectedSkillNames,
        AbsolutePath skillDirectory)
    {
        var skillNameResult = SelectDirectorySkillName(skillDirectory, selectedSkillNames);
        if (!skillNameResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneDirectorySelection>.FailureResult(
                skillNameResult.Failure!.Code,
                skillNameResult.Failure.Message);
        }

        if (skillNameResult.Value!.SkillName is null)
        {
            return AgentDistributionOperationResult<SkillPruneDirectorySelection>.Success(SkillPruneDirectorySelection.Ignored);
        }

        var resolvedSkillDirectoryResult = ResolvePhysicalSkillDirectory(planningContext.TargetRoot, skillDirectory);
        if (!resolvedSkillDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneDirectorySelection>.FailureResult(
                resolvedSkillDirectoryResult.Failure!.Code,
                resolvedSkillDirectoryResult.Failure.Message);
        }

        return SkillPruneDirectory.CreateSelected(
            planningContext,
            resolvedSkillDirectoryResult.Value!,
            skillNameResult.Value.SkillName);
    }

    private static AgentDistributionOperationResult<SkillPruneDirectoryNameSelection> SelectDirectorySkillName (
        AbsolutePath skillDirectory,
        IReadOnlySet<SkillName> selectedSkillNames)
    {
        if (!SkillName.TryCreate(Path.GetFileName(skillDirectory.Value), out var skillName))
        {
            var unsafeNameResult = ValidateIgnoredUnsafeNamedDirectory(skillDirectory);
            return unsafeNameResult.IsSuccess
                ? AgentDistributionOperationResult<SkillPruneDirectoryNameSelection>.Success(SkillPruneDirectoryNameSelection.Ignored)
                : AgentDistributionOperationResult<SkillPruneDirectoryNameSelection>.FailureResult(
                    unsafeNameResult.Failure!.Code,
                    unsafeNameResult.Failure.Message);
        }

        return IsSelectedSkillName(selectedSkillNames, skillName)
            ? AgentDistributionOperationResult<SkillPruneDirectoryNameSelection>.Success(new SkillPruneDirectoryNameSelection(skillName))
            : AgentDistributionOperationResult<SkillPruneDirectoryNameSelection>.Success(SkillPruneDirectoryNameSelection.Ignored);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolvePhysicalSkillDirectory (
        AbsolutePath targetRoot,
        AbsolutePath skillDirectory)
    {
        if (!FileSystemEntryInspector.TryInspect(skillDirectory, out var observation, out var inspectionFailure))
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Skill directory could not be inspected: {skillDirectory}. {inspectionFailure.Message}");
        }

        if (observation.State is FileSystemEntryState.SymbolicLink or FileSystemEntryState.ReparsePoint)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Skill directory must not be a symbolic link: {skillDirectory}");
        }

        var resolvedSkillDirectoryResult = PackagePathResolver.ResolveUnderRoot(targetRoot, skillDirectory);
        if (!resolvedSkillDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                resolvedSkillDirectoryResult.Failure!.Code,
                resolvedSkillDirectoryResult.Failure.Message);
        }

        return AgentDistributionOperationResult<AbsolutePath>.Success(resolvedSkillDirectoryResult.Value!);
    }

    private static AgentDistributionOperationResult<bool> ValidateIgnoredUnsafeNamedDirectory (AbsolutePath skillDirectory)
    {
        var manifestPathResult = PackagePathResolver.ResolveRegularFile(
            skillDirectory,
            PackageRelativePath.Parse("agent-skill.json"));
        if (!manifestPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        return File.Exists(manifestPathResult.Value!.Value)
            ? AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Skill directory name is unsafe: {skillDirectory}")
            : AgentDistributionOperationResult<bool>.Success(true);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillPruneActionPlanResult>> CreateActionPlanAsync (
        SkillPrunePlanningContext planningContext,
        SkillPruneDirectory directory,
        CancellationToken cancellationToken)
    {
        var skillDirectory = directory.SkillDirectory;
        var identity = directory.Identity;
        var manifestPathResult = PackagePathResolver.ResolveRegularFile(
            skillDirectory,
            PackageRelativePath.Parse("agent-skill.json"));
        if (!manifestPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneActionPlanResult>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        if (!File.Exists(manifestPathResult.Value!.Value))
        {
            if (planningContext.SelectedCategories.Count > 0)
            {
                return NoAction();
            }

            return Success(CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.SkippedUnmanaged,
                CreateTargetState(SkillTargetStateKind.Unmanaged, AgentDistributionFailureCodes.InstallTargetUnmanaged, "Skill directory is not managed by Agent Distribution."),
                blockedReason: null));
        }

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return CreateBlockedManifestActionPlan(directory, planningContext.SelectedCategories, installedManifestResult.Failure!);
        }

        var installedManifest = installedManifestResult.Value!.Manifest;
        if (!IsSelectedCategory(planningContext.SelectedCategories, installedManifest.Category))
        {
            return NoAction();
        }

        if (installedManifest.CatalogId != planningContext.CatalogId)
        {
            return Success(CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.SkippedForeignCatalog,
                targetState: null,
                blockedReason: null));
        }

        if (planningContext.CurrentCatalogSkillNames.Contains(installedManifest.SkillName))
        {
            return Success(CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.SkippedCurrent,
                targetState: null,
                blockedReason: null));
        }

        return await CreateOrphanActionPlanAsync(planningContext, directory, installedManifest, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillPruneActionPlanResult>> CreateOrphanActionPlanAsync (
        SkillPrunePlanningContext planningContext,
        SkillPruneDirectory directory,
        SkillManifest installedManifest,
        CancellationToken cancellationToken)
    {
        var integrityResult = await installedPackageIntegrityVerifier
            .VerifyAsync(directory.SkillDirectory, planningContext.Target.Host, cancellationToken)
            .ConfigureAwait(false);
        if (integrityResult.IsSuccess)
        {
            return await CreateDeleteActionPlanAsync(
                    directory,
                    CreateRemovedFromCatalogState(installedManifest),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await CreateOrphanIntegrityFailureActionPlanAsync(
                planningContext,
                directory,
                installedManifest,
                integrityResult.Failure!,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillPruneActionPlanResult>> CreateOrphanIntegrityFailureActionPlanAsync (
        SkillPrunePlanningContext planningContext,
        SkillPruneDirectory directory,
        SkillManifest installedManifest,
        AgentDistributionFailure failure,
        CancellationToken cancellationToken)
    {
        var protectedActionPlan = CreateProtectedOrphanActionPlan(directory, installedManifest, failure);
        if (protectedActionPlan is not null)
        {
            return Success(protectedActionPlan);
        }

        if (!SkillTargetStateClassifier.TryResolveDriftKind(failure.Code, out var driftKind))
        {
            return AgentDistributionOperationResult<SkillPruneActionPlanResult>.FailureResult(failure.Code, failure.Message);
        }

        var targetState = CreateTargetState(driftKind, failure, installedManifest.SkillBundleVersion);
        if (planningContext.Force)
        {
            return await CreateDeleteActionPlanAsync(
                    directory,
                    targetState,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Success(CreateNonDeleteActionPlan(
            directory,
            SkillPruneActionKind.BlockedLocalModification,
            targetState,
            SkillBlockedReason.LocalModificationRequiresForce));
    }

    private static SkillPruneActionPlan? CreateProtectedOrphanActionPlan (
        SkillPruneDirectory directory,
        SkillManifest installedManifest,
        AgentDistributionFailure failure)
    {
        if (failure.Code == AgentDistributionFailureCodes.InstallTargetHostConflict)
        {
            return CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.BlockedHostConflict,
                CreateTargetState(SkillTargetStateKind.HostConflict, failure),
                blockedReason: null);
        }

        if (failure.Code == AgentDistributionFailureCodes.InstallTargetNameCollision)
        {
            return CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.BlockedNameCollision,
                CreateTargetState(SkillTargetStateKind.NameCollision, failure),
                blockedReason: null);
        }

        if (failure.Code == AgentDistributionFailureCodes.ManifestInvalid
            || failure.Code == AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch)
        {
            return CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.BlockedManifestInvalid,
                CreateTargetState(SkillTargetStateKind.ManifestDrift, failure, installedManifest.SkillBundleVersion),
                blockedReason: null);
        }

        return null;
    }

    private async ValueTask<AgentDistributionOperationResult<SkillPruneActionPlanResult>> CreateDeleteActionPlanAsync (
        SkillPruneDirectory directory,
        SkillActionTargetState targetState,
        CancellationToken cancellationToken)
    {
        var fileChangesResult = await diffBuilder.BuildDeletionFileChangesAsync(directory.SkillDirectory, cancellationToken).ConfigureAwait(false);
        if (!fileChangesResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPruneActionPlanResult>.FailureResult(fileChangesResult.Failure!.Code, fileChangesResult.Failure.Message);
        }

        return Success(new SkillPruneActionPlan(
            new SkillPruneAction(
                directory.Identity,
                SkillPruneActionKind.Deleted,
                targetState,
                blockedReason: null,
                fileChangesResult.Value!.FileChanges),
            directory.SkillDirectory,
            shouldDelete: true,
            targetSnapshot: fileChangesResult.Value.TargetSnapshot));
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateDeletePreconditionAsync (
        SkillPrunePlanningContext planningContext,
        AbsolutePath skillDirectory,
        SkillActionTargetSnapshot? targetSnapshot,
        CancellationToken cancellationToken)
    {
        var directoryResult = SkillPruneDirectory.Create(planningContext, skillDirectory);
        if (!directoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var actionPlanResult = await CreateActionPlanAsync(
                planningContext,
                directoryResult.Value!,
                cancellationToken)
            .ConfigureAwait(false);
        if (!actionPlanResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
        }

        if (actionPlanResult.Value!.ActionPlan?.ShouldDelete != true)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                ResolveChangedTargetFailureCode(actionPlanResult.Value.ActionPlan?.Action.TargetState),
                $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
        }

        return targetSnapshot is null
            ? AgentDistributionOperationResult<bool>.Success(true)
            : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, actionPlanResult.Value.ActionPlan.Action.TargetState, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateTargetSnapshotAsync (
        AbsolutePath skillDirectory,
        SkillActionTargetSnapshot expectedSnapshot,
        SkillActionTargetState? targetState,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await diffBuilder.BuildTargetSnapshotAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!snapshotResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(snapshotResult.Failure!.Code, snapshotResult.Failure.Message);
        }

        if (snapshotResult.Value == expectedSnapshot)
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        return AgentDistributionOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(targetState),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private static AgentDistributionOperationResult<SkillPruneActionPlanResult> CreateBlockedManifestActionPlan (
        SkillPruneDirectory directory,
        IReadOnlySet<SkillCategory> selectedCategories,
        AgentDistributionFailure failure)
    {
        if (failure.Code == AgentDistributionFailureCodes.PathUnsafe)
        {
            return AgentDistributionOperationResult<SkillPruneActionPlanResult>.FailureResult(failure.Code, failure.Message);
        }

        if (selectedCategories.Count > 0)
        {
            return NoAction();
        }

        if (failure.Code == AgentDistributionFailureCodes.InstallTargetNameCollision)
        {
            return Success(CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.BlockedNameCollision,
                CreateTargetState(SkillTargetStateKind.NameCollision, failure),
                blockedReason: null));
        }

        if (failure.Code == AgentDistributionFailureCodes.InstallTargetUnmanaged)
        {
            return Success(CreateNonDeleteActionPlan(
                directory,
                SkillPruneActionKind.SkippedUnmanaged,
                CreateTargetState(SkillTargetStateKind.Unmanaged, failure),
                blockedReason: null));
        }

        if (failure.Code != AgentDistributionFailureCodes.ManifestInvalid
            && failure.Code != AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch)
        {
            return AgentDistributionOperationResult<SkillPruneActionPlanResult>.FailureResult(failure.Code, failure.Message);
        }

        return Success(CreateNonDeleteActionPlan(
            directory,
            SkillPruneActionKind.BlockedManifestInvalid,
            CreateTargetState(SkillTargetStateKind.ManifestDrift, failure),
            blockedReason: null));
    }

    private static IReadOnlySet<SkillCategory> CreateSelectedCategorySet (IReadOnlyList<SkillCategory>? selectedCategories)
    {
        if (selectedCategories is null || selectedCategories.Count == 0)
        {
            return new HashSet<SkillCategory>();
        }

        var result = new HashSet<SkillCategory>();
        foreach (var category in selectedCategories)
        {
            result.Add(category);
        }

        return result;
    }

    private static IReadOnlySet<SkillName> CreateSelectedSkillNameSet (IReadOnlyList<SkillName>? selectedSkillNames)
    {
        if (selectedSkillNames is null || selectedSkillNames.Count == 0)
        {
            return new HashSet<SkillName>();
        }

        var result = new HashSet<SkillName>();
        foreach (var skillName in selectedSkillNames)
        {
            result.Add(skillName);
        }

        return result;
    }

    private static bool IsSelectedCategory (
        IReadOnlySet<SkillCategory> selectedCategories,
        SkillCategory category)
    {
        return selectedCategories.Count == 0 || selectedCategories.Contains(category);
    }

    private static bool IsSelectedSkillName (
        IReadOnlySet<SkillName> selectedSkillNames,
        SkillName skillName)
    {
        return selectedSkillNames.Count == 0 || selectedSkillNames.Contains(skillName);
    }

    private static SkillActionTargetState CreateRemovedFromCatalogState (SkillManifest manifest)
    {
        return CreateTargetState(
            SkillTargetStateKind.RemovedFromCatalog,
            AgentDistributionFailure.Create(
                AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog,
                $"Installed SKILL package is managed by the catalog but is no longer bundled: {manifest.SkillName}"),
            manifest.SkillBundleVersion);
    }

    private static SkillActionTargetState CreateTargetState (
        SkillTargetStateKind kind,
        AgentDistributionFailureCode code,
        string message)
    {
        return new SkillActionTargetState(
            kind,
            code,
            message,
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: null);
    }

    private static SkillActionTargetState CreateTargetState (
        SkillTargetStateKind kind,
        AgentDistributionFailure failure,
        SkillBundleVersion? installedSkillBundleVersion = null)
    {
        return new SkillActionTargetState(
            kind,
            failure.Code,
            failure.Message,
            fileSet: null,
            installedSkillBundleVersion?.Value,
            bundledSkillBundleVersion: null);
    }

    private static AgentDistributionFailureCode ResolveChangedTargetFailureCode (SkillActionTargetState? targetState)
    {
        return targetState?.Code ?? AgentDistributionFailureCodes.InstallTargetDigestMismatch;
    }

    private static AgentDistributionOperationResult<SkillPruneActionPlanResult> Success (SkillPruneActionPlan actionPlan)
    {
        return AgentDistributionOperationResult<SkillPruneActionPlanResult>.Success(new SkillPruneActionPlanResult(actionPlan));
    }

    private static AgentDistributionOperationResult<SkillPruneActionPlanResult> NoAction ()
    {
        return AgentDistributionOperationResult<SkillPruneActionPlanResult>.Success(SkillPruneActionPlanResult.NoAction);
    }

    private static SkillPruneActionPlan CreateNonDeleteActionPlan (
        SkillPruneDirectory directory,
        SkillPruneActionKind actionKind,
        SkillActionTargetState? targetState,
        SkillBlockedReason? blockedReason)
    {
        return new SkillPruneActionPlan(
            new SkillPruneAction(directory.Identity, actionKind, targetState, blockedReason, fileChanges: null),
            directory.SkillDirectory,
            shouldDelete: false,
            targetSnapshot: null);
    }

    private sealed class SkillPruneActionPlanResult
    {
        public static SkillPruneActionPlanResult NoAction { get; } = new(null);

        public SkillPruneActionPlanResult (SkillPruneActionPlan? actionPlan)
        {
            ActionPlan = actionPlan;
        }

        public SkillPruneActionPlan? ActionPlan { get; }
    }

    private sealed class SkillPrunePlanningContext
    {
        public SkillPrunePlanningContext (
            SkillPruneInput input,
            SkillResolvedInstallTarget target,
            IReadOnlySet<SkillName> currentCatalogSkillNames,
            IReadOnlySet<SkillCategory> selectedCategories)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            CurrentCatalogSkillNames = new HashSet<SkillName>(currentCatalogSkillNames ?? throw new ArgumentNullException(nameof(currentCatalogSkillNames)));
            SelectedCategories = new HashSet<SkillCategory>(selectedCategories ?? throw new ArgumentNullException(nameof(selectedCategories)));
        }

        public SkillPruneInput Input { get; }

        public SkillResolvedInstallTarget Target { get; }

        public AgentDistributionCatalogId CatalogId => Input.CatalogId;

        public IReadOnlySet<SkillName> CurrentCatalogSkillNames { get; }

        public IReadOnlySet<SkillCategory> SelectedCategories { get; }

        public AbsolutePath TargetRoot => Target.TargetRoot;

        public bool Force => Input.Force;
    }

    private sealed class SkillPruneDirectory
    {
        public SkillPruneDirectory (
            AbsolutePath skillDirectory,
            SkillInstallIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(skillDirectory);
            ArgumentNullException.ThrowIfNull(identity);

            SkillDirectory = skillDirectory;
            Identity = identity;
        }

        public AbsolutePath SkillDirectory { get; }

        public SkillInstallIdentity Identity { get; }

        public static AgentDistributionOperationResult<SkillPruneDirectory> Create (
            SkillPrunePlanningContext planningContext,
            AbsolutePath skillDirectory)
        {
            if (!SkillName.TryCreate(Path.GetFileName(skillDirectory.Value), out var skillName))
            {
                return AgentDistributionOperationResult<SkillPruneDirectory>.FailureResult(
                    AgentDistributionFailureCodes.PathUnsafe,
                    $"Skill directory name is unsafe: {skillDirectory}");
            }

            return AgentDistributionOperationResult<SkillPruneDirectory>.Success(new SkillPruneDirectory(
                skillDirectory,
                new SkillInstallIdentity(
                    planningContext.Target.Host,
                    planningContext.Target.Scope,
                    planningContext.TargetRoot,
                    skillName)));
        }

        public static AgentDistributionOperationResult<SkillPruneDirectorySelection> CreateSelected (
            SkillPrunePlanningContext planningContext,
            AbsolutePath skillDirectory,
            SkillName skillName)
        {
            return AgentDistributionOperationResult<SkillPruneDirectorySelection>.Success(new SkillPruneDirectorySelection(new SkillPruneDirectory(
                skillDirectory,
                new SkillInstallIdentity(
                    planningContext.Target.Host,
                    planningContext.Target.Scope,
                    planningContext.TargetRoot,
                    skillName))));
        }
    }

    private sealed class SkillPruneDirectorySelection
    {
        public static SkillPruneDirectorySelection Ignored { get; } = new(null);

        public SkillPruneDirectorySelection (SkillPruneDirectory? directory)
        {
            Directory = directory;
        }

        public SkillPruneDirectory? Directory { get; }
    }

    private sealed class SkillPruneDirectoryNameSelection
    {
        public static SkillPruneDirectoryNameSelection Ignored { get; } = new(null);

        public SkillPruneDirectoryNameSelection (SkillName? skillName)
        {
            SkillName = skillName;
        }

        public SkillName? SkillName { get; }
    }

    private sealed class SkillPruneActionPlan
    {
        public SkillPruneActionPlan (
            SkillPruneAction action,
            AbsolutePath skillDirectory,
            bool shouldDelete,
            SkillActionTargetSnapshot? targetSnapshot)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ArgumentNullException.ThrowIfNull(skillDirectory);
            if (shouldDelete != (targetSnapshot is not null))
            {
                throw new ArgumentException("A delete action plan must have a target snapshot, and a non-delete plan must not have one.", nameof(targetSnapshot));
            }

            SkillDirectory = skillDirectory;
            ShouldDelete = shouldDelete;
            TargetSnapshot = targetSnapshot;
        }

        public SkillPruneAction Action { get; }

        public AbsolutePath SkillDirectory { get; }

        public bool ShouldDelete { get; }

        public SkillActionTargetSnapshot? TargetSnapshot { get; }
    }
}
