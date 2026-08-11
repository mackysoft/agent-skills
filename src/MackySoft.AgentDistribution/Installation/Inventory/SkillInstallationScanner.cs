using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
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
    /// <param name="targetRoot"> The bundle target root. </param>
    /// <param name="host"> The host used for install identity. </param>
    /// <param name="scope"> The install scope used for install identity. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed skill list, or a structured failure for invalid input, unsupported host, unsafe path use, manifest problems, or installed target drift. </returns>
    public async ValueTask<AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>> ScanAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string targetRoot,
        HostKind host,
        SkillScopeKind scope = SkillScopeKind.Project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (scope is not SkillScopeKind.Project and not SkillScopeKind.User)
        {
            return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                AgentDistributionFailureCodes.InputInvalid,
                $"Unsupported SKILL install scope: {scope}");
        }

        var registrationResult = HostRegistration.Get(host);
        if (!registrationResult.IsSuccess)
        {
            return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
        }

        var registeredHost = registrationResult.Value!.Host;
        var fullTargetRoot = AbsolutePath.Parse(Path.GetFullPath(targetRoot));
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

            var validationResult = await installedPackageValidator.ValidateAsync(package, resolvedSkillDirectory, registeredHost, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    validationResult.Failure!.Code,
                    validationResult.Failure.Message);
            }

            installedSkills.Add(new SkillInstalledSkill(
                new SkillInstallIdentity(registeredHost, scope, fullTargetRoot, manifest.SkillName),
                resolvedSkillDirectory,
                validationResult.Value!));
        }

        return AgentDistributionOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(installedSkills);
    }
}
