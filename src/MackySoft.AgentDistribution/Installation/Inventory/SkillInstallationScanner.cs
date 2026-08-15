using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Inventory;

/// <summary> Scans installed SKILL manifests under one bundle target root. </summary>
public sealed class SkillInstallationScanner
{
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillInstalledPackageValidator installedPackageValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallationScanner" /> class. </summary>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="installedPackageValidator"> The installed package validator. </param>
    public SkillInstallationScanner (
        SkillInstalledManifestReader installedManifestReader,
        SkillInstalledPackageValidator installedPackageValidator)
    {
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.installedPackageValidator = installedPackageValidator ?? throw new ArgumentNullException(nameof(installedPackageValidator));
    }

    /// <summary> Scans installed SKILL manifests. </summary>
    /// <param name="packages"> The canonical packages used for digest verification. </param>
    /// <param name="target"> The resolved host, scope, and bundle target root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed skill list, or a structured failure for unsafe path use, manifest problems, or installed target drift. </returns>
    public async ValueTask<AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>> ScanAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillResolvedInstallTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var fullTargetRoot = target.TargetRoot;
        if (!Directory.Exists(fullTargetRoot.Value))
        {
            return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(Array.Empty<SkillInstalledSkill>());
        }

        var packageByName = packages.ToDictionary(static package => package.Manifest.SkillName);
        var installedSkills = new List<SkillInstalledSkill>();
        foreach (var skillDirectoryValue in Directory.EnumerateDirectories(fullTargetRoot.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectory = AbsolutePath.Parse(skillDirectoryValue);
            var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(fullTargetRoot, skillDirectory);
            if (!skillDirectoryResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    skillDirectoryResult.Failure!.Code,
                    skillDirectoryResult.Failure.Message);
            }

            var resolvedSkillDirectory = skillDirectoryResult.Value!;
            var manifestPathResult = PackagePathResolver.ResolveRegularFile(
                resolvedSkillDirectory,
                PackageRelativePath.Parse("agent-skill.json"));
            if (!manifestPathResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    manifestPathResult.Failure!.Code,
                    manifestPathResult.Failure.Message);
            }

            if (!File.Exists(manifestPathResult.Value!.Value))
            {
                continue;
            }

            var installedManifestResult = await installedManifestReader.ReadRequiredAsync(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
            if (!installedManifestResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    installedManifestResult.Failure!.Code,
                    installedManifestResult.Failure.Message);
            }

            var manifest = installedManifestResult.Value!.Manifest;
            if (!packageByName.TryGetValue(manifest.SkillName, out var package))
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetUnmanaged,
                    $"Installed SKILL is not part of the canonical package set: {manifest.SkillName}");
            }

            var validationResult = await installedPackageValidator.ValidateAsync(package, resolvedSkillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    validationResult.Failure!.Code,
                    validationResult.Failure.Message);
            }

            installedSkills.Add(new SkillInstalledSkill(
                new SkillInstallIdentity(target.Host, target.Scope, fullTargetRoot, manifest.SkillName),
                resolvedSkillDirectory,
                validationResult.Value!));
        }

        return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(installedSkills);
    }
}
