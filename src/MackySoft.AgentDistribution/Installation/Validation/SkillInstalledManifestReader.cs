using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Validation;

/// <summary> Reads and validates the shape of an installed <c>agent-skill.json</c> manifest. </summary>
public sealed class SkillInstalledManifestReader
{
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifest.Factory manifestFactory;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledManifestReader" /> class. </summary>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestFactory"> The canonical manifest construction boundary. </param>
    public SkillInstalledManifestReader (
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifest.Factory manifestFactory)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestFactory = manifestFactory ?? throw new ArgumentNullException(nameof(manifestFactory));
    }

    /// <summary> Reads and shape-validates the required installed manifest from one skill directory. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest or validation failure. </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillInstalledManifest>> ReadRequiredAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var manifestPathResult = PackagePathResolver.ResolveRegularFile(
            skillDirectory,
            PackageRelativePath.Parse("agent-skill.json"));
        if (!manifestPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstalledManifest>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        var manifestPath = manifestPathResult.Value!;
        if (!File.Exists(manifestPath.Value))
        {
            return AgentDistributionOperationResult<SkillInstalledManifest>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetUnmanaged,
                $"Target skill directory is missing agent-skill.json: {skillDirectory}");
        }

        var manifestTextResult = await CanonicalPackageTextReader.ReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (!manifestTextResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstalledManifest>.FailureResult(
                manifestTextResult.Failure!.Code,
                manifestTextResult.Failure.Message);
        }

        var manifestText = manifestTextResult.Value!;
        var manifestResult = manifestSerializer.TryDeserialize(manifestText);
        if (!manifestResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstalledManifest>.FailureResult(
                manifestResult.Failure!.Code,
                $"Target skill manifest is invalid: {manifestPath}");
        }

        var validationResult = manifestFactory.CreateCanonicalFromInstalledShape(manifestResult.Value!);
        if (!validationResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstalledManifest>.FailureResult(
                validationResult.Failure!.Code,
                validationResult.Failure.Message);
        }

        var manifest = validationResult.Value!;
        if (!string.Equals(Path.GetFileName(skillDirectory.Value), manifest.SkillName.Value, StringComparison.Ordinal))
        {
            return AgentDistributionOperationResult<SkillInstalledManifest>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetNameCollision,
                $"agent-skill.json skillName must match installed directory name: {manifestPath}");
        }

        return AgentDistributionOperationResult<SkillInstalledManifest>.Success(new SkillInstalledManifest(
            manifestPath,
            manifestText,
            manifest));
    }
}
