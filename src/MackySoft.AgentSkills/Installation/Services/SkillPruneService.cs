using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Inventory;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Services;

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
    public async ValueTask<SkillOperationResult<SkillPruneResult>> PruneAsync (
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
            return SkillOperationResult<SkillPruneResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var selectedCategories = CreateSelectedCategorySet(input.SelectedCategories);
        var selectedSkillNames = CreateSelectedSkillNameSet(input.SelectedSkillNames);
        var actionPlansResult = await CreateActionPlansAsync(
                input.CatalogId,
                currentCatalogSkillNames,
                selectedCategories,
                selectedSkillNames,
                target.Host,
                input.TargetRequest.Scope,
                targetRoot,
                input.Force,
                cancellationToken)
            .ConfigureAwait(false);
        if (!actionPlansResult.IsSuccess)
        {
            return SkillOperationResult<SkillPruneResult>.FailureResult(actionPlansResult.Failure!.Code, actionPlansResult.Failure.Message);
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
                        input.CatalogId,
                        currentCatalogSkillNames,
                        selectedCategories,
                        target.Host,
                        input.TargetRequest.Scope,
                        targetRoot,
                        actionPlan.SkillDirectory,
                        actionPlan.TargetSnapshot,
                        input.Force,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return SkillOperationResult<SkillPruneResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
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
                        targetRoot,
                        actionPlan.SkillDirectory,
                        (directory, token) => ValidateDeletePreconditionAsync(
                            input.CatalogId,
                            currentCatalogSkillNames,
                            selectedCategories,
                            target.Host,
                            input.TargetRequest.Scope,
                            targetRoot,
                            directory,
                            actionPlan.TargetSnapshot,
                            input.Force,
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!deleteResult.IsSuccess)
                {
                    return SkillOperationResult<SkillPruneResult>.FailureResult(deleteResult.Failure!.Code, deleteResult.Failure.Message);
                }
            }
        }

        return SkillOperationResult<SkillPruneResult>.Success(new SkillPruneResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>> CreateActionPlansAsync (
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> currentCatalogSkillNames,
        IReadOnlySet<SkillCategory> selectedCategories,
        IReadOnlySet<SkillName> selectedSkillNames,
        SkillHostKind host,
        SkillScopeKind scope,
        string targetRoot,
        bool force,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(targetRoot))
        {
            return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.Success(Array.Empty<SkillPruneActionPlan>());
        }

        var actionPlans = new List<SkillPruneActionPlan>();
        foreach (var skillDirectory in Directory.EnumerateDirectories(targetRoot).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SkillName.TryCreate(Path.GetFileName(skillDirectory), out var skillName))
            {
                var manifestPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(
                    targetRoot,
                    skillDirectory,
                    "agent-skill.json");
                if (!manifestPathResult.IsSuccess)
                {
                    return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.FailureResult(
                        manifestPathResult.Failure!.Code,
                        manifestPathResult.Failure.Message);
                }

                if (File.Exists(manifestPathResult.Value!))
                {
                    return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.FailureResult(
                        SkillFailureCodes.PathUnsafe,
                        $"Skill directory name is unsafe: {skillDirectory}");
                }

                continue;
            }

            if (!IsSelectedSkillName(selectedSkillNames, skillName))
            {
                continue;
            }

            if (IsSymbolicLinkOrReparsePoint(skillDirectory))
            {
                return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Skill directory must not be a symbolic link: {skillDirectory}");
            }

            var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, skillDirectory);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.FailureResult(
                    skillDirectoryResult.Failure!.Code,
                    skillDirectoryResult.Failure.Message);
            }

            var resolvedSkillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(host, scope, targetRoot, skillName);
            var actionPlanResult = await CreateActionPlanAsync(
                    catalogId,
                    currentCatalogSkillNames,
                    selectedCategories,
                    host,
                    resolvedSkillDirectory,
                    identity,
                    force,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!actionPlanResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.FailureResult(
                    actionPlanResult.Failure!.Code,
                    actionPlanResult.Failure.Message);
            }

            if (actionPlanResult.Value!.ActionPlan is not null)
            {
                actionPlans.Add(actionPlanResult.Value.ActionPlan);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.Success(actionPlans);
    }

    private async ValueTask<SkillOperationResult<SkillPruneActionPlanResult>> CreateActionPlanAsync (
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> currentCatalogSkillNames,
        IReadOnlySet<SkillCategory> selectedCategories,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        bool force,
        CancellationToken cancellationToken)
    {
        var manifestPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(
            identity.TargetRoot,
            skillDirectory,
            "agent-skill.json");
        if (!manifestPathResult.IsSuccess)
        {
            return SkillOperationResult<SkillPruneActionPlanResult>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        if (!File.Exists(manifestPathResult.Value!))
        {
            if (selectedCategories.Count > 0)
            {
                return NoAction();
            }

            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.SkippedUnmanaged,
                    CreateTargetState(SkillTargetStateKind.Unmanaged, SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is not managed by Agent Skills."),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return CreateBlockedManifestActionPlan(identity, skillDirectory, selectedCategories, installedManifestResult.Failure!);
        }

        var installedManifest = installedManifestResult.Value!.Manifest;
        if (!IsSelectedCategory(selectedCategories, installedManifest.Category))
        {
            return NoAction();
        }

        if (installedManifest.CatalogId != catalogId)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(identity, SkillPruneActionKind.SkippedForeignCatalog, targetState: null, blockedReason: null, fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (currentCatalogSkillNames.Contains(installedManifest.SkillName))
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(identity, SkillPruneActionKind.SkippedCurrent, targetState: null, blockedReason: null, fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        return await CreateOrphanActionPlanAsync(installedManifest, host, skillDirectory, identity, force, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SkillOperationResult<SkillPruneActionPlanResult>> CreateOrphanActionPlanAsync (
        SkillManifest installedManifest,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        bool force,
        CancellationToken cancellationToken)
    {
        var integrityResult = await installedPackageIntegrityVerifier.VerifyAsync(skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (integrityResult.IsSuccess)
        {
            return await CreateDeleteActionPlanAsync(
                    skillDirectory,
                    identity,
                    CreateRemovedFromCatalogState(installedManifest),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var failure = integrityResult.Failure!;
        if (failure.Code == SkillFailureCodes.InstallTargetHostConflict)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedHostConflict,
                    CreateTargetState(SkillTargetStateKind.HostConflict, failure),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (failure.Code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedNameCollision,
                    CreateTargetState(SkillTargetStateKind.NameCollision, failure),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (failure.Code == SkillFailureCodes.ManifestInvalid)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedManifestInvalid,
                    CreateTargetState(SkillTargetStateKind.ManifestDrift, failure, installedManifest.SkillBundleVersion),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (failure.Code == SkillFailureCodes.InstallTargetManifestDigestMismatch)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedManifestInvalid,
                    CreateTargetState(SkillTargetStateKind.ManifestDrift, failure, installedManifest.SkillBundleVersion),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (!SkillTargetStateClassifier.TryResolveDriftKind(failure.Code, out var driftKind))
        {
            return SkillOperationResult<SkillPruneActionPlanResult>.FailureResult(failure.Code, failure.Message);
        }

        if (force)
        {
            return await CreateDeleteActionPlanAsync(
                    skillDirectory,
                    identity,
                    CreateTargetState(driftKind, failure, installedManifest.SkillBundleVersion),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Success(new SkillPruneActionPlan(
            new SkillPruneAction(
                identity,
                SkillPruneActionKind.BlockedLocalModification,
                CreateTargetState(driftKind, failure, installedManifest.SkillBundleVersion),
                SkillBlockedReason.LocalModificationRequiresForce,
                fileChanges: null),
            skillDirectory,
            shouldDelete: false,
            targetSnapshot: null));
    }

    private async ValueTask<SkillOperationResult<SkillPruneActionPlanResult>> CreateDeleteActionPlanAsync (
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillActionTargetState targetState,
        CancellationToken cancellationToken)
    {
        var fileChangesResult = await diffBuilder.BuildDeletionFileChangesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!fileChangesResult.IsSuccess)
        {
            return SkillOperationResult<SkillPruneActionPlanResult>.FailureResult(fileChangesResult.Failure!.Code, fileChangesResult.Failure.Message);
        }

        return Success(new SkillPruneActionPlan(
            new SkillPruneAction(
                identity,
                SkillPruneActionKind.Deleted,
                targetState,
                blockedReason: null,
                fileChangesResult.Value!.FileChanges),
            skillDirectory,
            shouldDelete: true,
            targetSnapshot: fileChangesResult.Value.TargetSnapshot));
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateDeletePreconditionAsync (
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> currentCatalogSkillNames,
        IReadOnlySet<SkillCategory> selectedCategories,
        SkillHostKind host,
        SkillScopeKind scope,
        string targetRoot,
        string skillDirectory,
        SkillActionTargetSnapshot? targetSnapshot,
        bool force,
        CancellationToken cancellationToken)
    {
        var identityResult = CreateIdentityFromDirectory(host, scope, targetRoot, skillDirectory);
        if (!identityResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(identityResult.Failure!.Code, identityResult.Failure.Message);
        }

        var actionPlanResult = await CreateActionPlanAsync(
                catalogId,
                currentCatalogSkillNames,
                selectedCategories,
                host,
                skillDirectory,
                identityResult.Value!,
                force,
                cancellationToken)
            .ConfigureAwait(false);
        if (!actionPlanResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
        }

        if (actionPlanResult.Value!.ActionPlan?.ShouldDelete != true)
        {
            return SkillOperationResult<bool>.FailureResult(
                ResolveChangedTargetFailureCode(actionPlanResult.Value.ActionPlan?.Action.TargetState),
                $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
        }

        return targetSnapshot is null
            ? SkillOperationResult<bool>.Success(true)
            : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, actionPlanResult.Value.ActionPlan.Action.TargetState, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateTargetSnapshotAsync (
        string skillDirectory,
        SkillActionTargetSnapshot expectedSnapshot,
        SkillActionTargetState? targetState,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await diffBuilder.BuildTargetSnapshotAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!snapshotResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(snapshotResult.Failure!.Code, snapshotResult.Failure.Message);
        }

        if (snapshotResult.Value == expectedSnapshot)
        {
            return SkillOperationResult<bool>.Success(true);
        }

        return SkillOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(targetState),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private static SkillOperationResult<SkillPruneActionPlanResult> CreateBlockedManifestActionPlan (
        SkillInstallIdentity identity,
        string skillDirectory,
        IReadOnlySet<SkillCategory> selectedCategories,
        SkillFailure failure)
    {
        if (failure.Code == SkillFailureCodes.PathUnsafe)
        {
            return SkillOperationResult<SkillPruneActionPlanResult>.FailureResult(failure.Code, failure.Message);
        }

        if (selectedCategories.Count > 0)
        {
            return NoAction();
        }

        if (failure.Code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedNameCollision,
                    CreateTargetState(SkillTargetStateKind.NameCollision, failure),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (failure.Code == SkillFailureCodes.InstallTargetUnmanaged)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.SkippedUnmanaged,
                    CreateTargetState(SkillTargetStateKind.Unmanaged, failure),
                    blockedReason: null,
                    fileChanges: null),
                skillDirectory,
                shouldDelete: false,
                targetSnapshot: null));
        }

        if (failure.Code != SkillFailureCodes.ManifestInvalid
            && failure.Code != SkillFailureCodes.InstallTargetManifestDigestMismatch)
        {
            return SkillOperationResult<SkillPruneActionPlanResult>.FailureResult(failure.Code, failure.Message);
        }

        return Success(new SkillPruneActionPlan(
            new SkillPruneAction(
                identity,
                SkillPruneActionKind.BlockedManifestInvalid,
                CreateTargetState(SkillTargetStateKind.ManifestDrift, failure),
                blockedReason: null,
                fileChanges: null),
            skillDirectory,
            shouldDelete: false,
            targetSnapshot: null));
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
            SkillFailure.Create(
                SkillFailureCodes.InstallTargetRemovedFromCatalog,
                $"Installed SKILL package is managed by the catalog but is no longer bundled: {manifest.SkillName}"),
            manifest.SkillBundleVersion);
    }

    private static SkillActionTargetState CreateTargetState (
        SkillTargetStateKind kind,
        SkillFailureCode code,
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
        SkillFailure failure,
        int? installedSkillBundleVersion = null)
    {
        return new SkillActionTargetState(
            kind,
            failure.Code,
            failure.Message,
            fileSet: null,
            installedSkillBundleVersion,
            bundledSkillBundleVersion: null);
    }

    private static SkillOperationResult<SkillInstallIdentity> CreateIdentityFromDirectory (
        SkillHostKind host,
        SkillScopeKind scope,
        string targetRoot,
        string skillDirectory)
    {
        if (!SkillName.TryCreate(Path.GetFileName(skillDirectory), out var skillName))
        {
            return SkillOperationResult<SkillInstallIdentity>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Skill directory name is unsafe: {skillDirectory}");
        }

        var resolvedTargetRoot = string.IsNullOrEmpty(targetRoot)
            ? Path.GetDirectoryName(skillDirectory) ?? string.Empty
            : targetRoot;
        if (string.IsNullOrWhiteSpace(resolvedTargetRoot))
        {
            return SkillOperationResult<SkillInstallIdentity>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Skill directory parent could not be resolved: {skillDirectory}");
        }

        return SkillOperationResult<SkillInstallIdentity>.Success(new SkillInstallIdentity(host, scope, resolvedTargetRoot, skillName));
    }

    private static SkillFailureCode ResolveChangedTargetFailureCode (SkillActionTargetState? targetState)
    {
        return targetState?.Code ?? SkillFailureCodes.InstallTargetDigestMismatch;
    }

    private static bool IsSymbolicLinkOrReparsePoint (string path)
    {
        var directory = new DirectoryInfo(path);
        return directory.LinkTarget is not null
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    private static SkillOperationResult<SkillPruneActionPlanResult> Success (SkillPruneActionPlan actionPlan)
    {
        return SkillOperationResult<SkillPruneActionPlanResult>.Success(new SkillPruneActionPlanResult(actionPlan));
    }

    private static SkillOperationResult<SkillPruneActionPlanResult> NoAction ()
    {
        return SkillOperationResult<SkillPruneActionPlanResult>.Success(new SkillPruneActionPlanResult(null));
    }

    private sealed class SkillPruneActionPlanResult
    {
        public SkillPruneActionPlanResult (SkillPruneActionPlan? actionPlan)
        {
            ActionPlan = actionPlan;
        }

        public SkillPruneActionPlan? ActionPlan { get; }
    }

    private sealed class SkillPruneActionPlan
    {
        public SkillPruneActionPlan (
            SkillPruneAction action,
            string skillDirectory,
            bool shouldDelete,
            SkillActionTargetSnapshot? targetSnapshot)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
            if (shouldDelete != (targetSnapshot is not null))
            {
                throw new ArgumentException("A delete action plan must have a target snapshot, and a non-delete plan must not have one.", nameof(targetSnapshot));
            }

            SkillDirectory = skillDirectory;
            ShouldDelete = shouldDelete;
            TargetSnapshot = targetSnapshot;
        }

        public SkillPruneAction Action { get; }

        public string SkillDirectory { get; }

        public bool ShouldDelete { get; }

        public SkillActionTargetSnapshot? TargetSnapshot { get; }
    }
}
