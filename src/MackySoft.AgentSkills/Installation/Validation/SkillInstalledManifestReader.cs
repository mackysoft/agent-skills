using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Reads and validates the shape of an installed <c>agent-skill.json</c> manifest. </summary>
public sealed class SkillInstalledManifestReader
{
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledManifestReader" /> class. </summary>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    public SkillInstalledManifestReader (
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifestValidator manifestValidator)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestValidator = manifestValidator ?? throw new ArgumentNullException(nameof(manifestValidator));
    }

    /// <summary> Reads and shape-validates the required installed manifest from one skill directory. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest or validation failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstalledManifest>> ReadRequiredAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var manifestPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, "agent-skill.json");
        if (!manifestPathResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        var manifestPath = manifestPathResult.Value!;
        if (!File.Exists(manifestPath))
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Target skill directory is missing agent-skill.json: {skillDirectory}");
        }

        var manifestTextResult = await SkillPackageManifestTextReader.ReadUtf8WithoutByteOrderMarkAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (!manifestTextResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                manifestTextResult.Failure!.Code,
                manifestTextResult.Failure.Message);
        }

        var manifestText = manifestTextResult.Value!;
        var manifestResult = manifestSerializer.TryDeserialize(manifestText);
        if (!manifestResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                manifestResult.Failure!.Code,
                $"Target skill manifest is invalid: {manifestPath}");
        }

        var manifest = manifestResult.Value!;
        var validationResult = manifestValidator.ValidateInstalledShape(manifest);
        if (!validationResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                validationResult.Failure!.Code,
                validationResult.Failure.Message);
        }

        if (!string.Equals(Path.GetFileName(skillDirectory), manifest.SkillName, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                SkillFailureCodes.InstallTargetNameCollision,
                $"agent-skill.json skillName must match installed directory name: {manifestPath}");
        }

        return SkillOperationResult<SkillInstalledManifest>.Success(new SkillInstalledManifest(
            manifestPath,
            manifestText,
            manifest));
    }
}
