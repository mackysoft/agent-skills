using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Prunes installed managed SKILL packages that no longer exist in a product catalog. </summary>
public sealed class SkillPruneService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;
    private readonly ISkillInstalledPackageRemover packageRemover;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillPruneService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    /// <param name="packageRemover"> The installed package remover. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillPruneService (
        SkillInstallTargetResolver targetResolver,
        SkillInstalledManifestReader installedManifestReader,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier,
        ISkillInstalledPackageRemover packageRemover,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
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
        ArgumentNullException.ThrowIfNull(input.CatalogId);
        ArgumentNullException.ThrowIfNull(input.CurrentCatalogPackages);
        ArgumentNullException.ThrowIfNull(input.TargetRequest);
        cancellationToken.ThrowIfCancellationRequested();

        var catalogIndexResult = CreateCurrentCatalogSkillIndex(input.CatalogId, input.CurrentCatalogPackages);
        if (!catalogIndexResult.IsSuccess)
        {
            return SkillOperationResult<SkillPruneResult>.FailureResult(catalogIndexResult.Failure!.Code, catalogIndexResult.Failure.Message);
        }

        var currentCatalogSkillNames = catalogIndexResult.Value!;
        var targetResult = targetResolver.ResolveTarget(input.TargetRequest);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillPruneResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlansResult = await CreateActionPlansAsync(
                input.CatalogId,
                currentCatalogSkillNames,
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
        string host,
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

            if (actionPlanResult.Value is not null)
            {
                actionPlans.Add(actionPlanResult.Value);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillPruneActionPlan>>.Success(actionPlans);
    }

    private async ValueTask<SkillOperationResult<SkillPruneActionPlan?>> CreateActionPlanAsync (
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> currentCatalogSkillNames,
        string host,
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
            return SkillOperationResult<SkillPruneActionPlan?>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        if (!File.Exists(manifestPathResult.Value!))
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.SkippedUnmanaged,
                    TargetState: CreateTargetState(SkillInstalledTargetStateKind.Unmanaged, SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is not managed by Agent Skills.")),
                skillDirectory,
                ShouldDelete: false));
        }

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return Success(CreateBlockedManifestActionPlan(identity, skillDirectory, installedManifestResult.Failure!));
        }

        var installedManifest = installedManifestResult.Value!.Manifest;
        if (installedManifest.CatalogId != catalogId)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(identity, SkillPruneActionKind.SkippedForeignCatalog),
                skillDirectory,
                ShouldDelete: false));
        }

        if (currentCatalogSkillNames.Contains(installedManifest.SkillName))
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(identity, SkillPruneActionKind.SkippedCurrent),
                skillDirectory,
                ShouldDelete: false));
        }

        return await CreateOrphanActionPlanAsync(installedManifest, host, skillDirectory, identity, force, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SkillOperationResult<SkillPruneActionPlan?>> CreateOrphanActionPlanAsync (
        SkillManifest installedManifest,
        string host,
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
                    TargetState: CreateTargetState(SkillInstalledTargetStateKind.HostConflict, failure)),
                skillDirectory,
                ShouldDelete: false));
        }

        if (failure.Code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedNameCollision,
                    TargetState: CreateTargetState(SkillInstalledTargetStateKind.NameCollision, failure)),
                skillDirectory,
                ShouldDelete: false));
        }

        if (failure.Code == SkillFailureCodes.ManifestInvalid)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedManifestInvalid,
                    TargetState: CreateTargetState(SkillInstalledTargetStateKind.ManifestDrift, failure)),
                skillDirectory,
                ShouldDelete: false));
        }

        if (failure.Code == SkillFailureCodes.InstallTargetManifestDigestMismatch)
        {
            return Success(new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedManifestInvalid,
                    TargetState: CreateTargetState(SkillInstalledTargetStateKind.ManifestDrift, failure)),
                skillDirectory,
                ShouldDelete: false));
        }

        if (!SkillInstalledTargetStateClassifier.TryResolveDriftKind(failure.Code, out var driftKind))
        {
            return SkillOperationResult<SkillPruneActionPlan?>.FailureResult(failure.Code, failure.Message);
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
                SkillBlockedReason.LocalModificationRequiresForce,
                CreateTargetState(driftKind, failure, installedManifest.SkillBundleVersion)),
            skillDirectory,
            ShouldDelete: false));
    }

    private async ValueTask<SkillOperationResult<SkillPruneActionPlan?>> CreateDeleteActionPlanAsync (
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillActionTargetState targetState,
        CancellationToken cancellationToken)
    {
        var fileChangesResult = await diffBuilder.BuildDeletionFileChangesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!fileChangesResult.IsSuccess)
        {
            return SkillOperationResult<SkillPruneActionPlan?>.FailureResult(fileChangesResult.Failure!.Code, fileChangesResult.Failure.Message);
        }

        return Success(new SkillPruneActionPlan(
            new SkillPruneAction(identity, SkillPruneActionKind.Deleted, TargetState: targetState)
            {
                FileChanges = fileChangesResult.Value!.FileChanges,
            },
            skillDirectory,
            ShouldDelete: true,
            TargetSnapshot: fileChangesResult.Value.TargetSnapshot));
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateDeletePreconditionAsync (
        SkillCatalogId catalogId,
        IReadOnlySet<SkillName> currentCatalogSkillNames,
        string host,
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

        if (actionPlanResult.Value?.ShouldDelete != true)
        {
            return SkillOperationResult<bool>.FailureResult(
                ResolveChangedTargetFailureCode(actionPlanResult.Value?.Action.TargetState),
                $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
        }

        return targetSnapshot is null
            ? SkillOperationResult<bool>.Success(true)
            : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, actionPlanResult.Value.Action.TargetState, cancellationToken).ConfigureAwait(false);
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

    private static SkillOperationResult<IReadOnlySet<SkillName>> CreateCurrentCatalogSkillIndex (
        SkillCatalogId catalogId,
        IReadOnlyList<CanonicalSkillPackage> currentCatalogPackages)
    {
        var skillNames = new HashSet<SkillName>();
        foreach (var package in currentCatalogPackages)
        {
            ArgumentNullException.ThrowIfNull(package);
            if (package.Manifest.CatalogId != catalogId)
            {
                return SkillOperationResult<IReadOnlySet<SkillName>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Current catalog package belongs to another catalog: {package.Manifest.SkillName}");
            }

            if (!skillNames.Add(package.Manifest.SkillName))
            {
                return SkillOperationResult<IReadOnlySet<SkillName>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Current catalog contains duplicate SKILL name: {package.Manifest.SkillName}");
            }
        }

        return SkillOperationResult<IReadOnlySet<SkillName>>.Success(skillNames);
    }

    private static SkillPruneActionPlan CreateBlockedManifestActionPlan (
        SkillInstallIdentity identity,
        string skillDirectory,
        SkillFailure failure)
    {
        if (failure.Code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return new SkillPruneActionPlan(
                new SkillPruneAction(
                    identity,
                    SkillPruneActionKind.BlockedNameCollision,
                    TargetState: CreateTargetState(SkillInstalledTargetStateKind.NameCollision, failure)),
                skillDirectory,
                ShouldDelete: false);
        }

        return new SkillPruneActionPlan(
            new SkillPruneAction(
                identity,
                SkillPruneActionKind.BlockedManifestInvalid,
                TargetState: CreateTargetState(SkillInstalledTargetStateKind.ManifestDrift, failure)),
            skillDirectory,
            ShouldDelete: false);
    }

    private static SkillActionTargetState CreateRemovedFromCatalogState (SkillManifest manifest)
    {
        return CreateTargetState(
            SkillInstalledTargetStateKind.RemovedFromCatalog,
            SkillFailure.Create(
                SkillFailureCodes.InstallTargetRemovedFromCatalog,
                $"Installed SKILL package is managed by the catalog but is no longer bundled: {manifest.SkillName}"),
            manifest.SkillBundleVersion);
    }

    private static SkillActionTargetState CreateTargetState (
        SkillInstalledTargetStateKind kind,
        SkillFailureCode code,
        string message)
    {
        return new SkillActionTargetState(SkillActionTargetStateProjection.MapKind(kind), code, message);
    }

    private static SkillActionTargetState CreateTargetState (
        SkillInstalledTargetStateKind kind,
        SkillFailure failure,
        int? installedSkillBundleVersion = null)
    {
        return new SkillActionTargetState(
            SkillActionTargetStateProjection.MapKind(kind),
            failure.Code,
            failure.Message,
            InstalledSkillBundleVersion: installedSkillBundleVersion);
    }

    private static SkillOperationResult<SkillInstallIdentity> CreateIdentityFromDirectory (
        string host,
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

    private static SkillOperationResult<SkillPruneActionPlan?> Success (SkillPruneActionPlan actionPlan)
    {
        return SkillOperationResult<SkillPruneActionPlan?>.Success(actionPlan);
    }

    private sealed record SkillPruneActionPlan (
        SkillPruneAction Action,
        string SkillDirectory,
        bool ShouldDelete,
        SkillActionTargetSnapshot? TargetSnapshot = null);
}
