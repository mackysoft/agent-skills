using System.Text.Json;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reads a generated <c>bundle.json</c> and its complete canonical package set as one integrity boundary. </summary>
public sealed class CanonicalSkillBundleReader
{
    private readonly CanonicalSkillPackageReader packageReader;
    private readonly SkillBundleJsonSerializer bundleSerializer;
    private readonly CanonicalSkillBundle.Factory bundleFactory;

    /// <summary> Initializes a reader with package, descriptor, and bundle integrity contracts. </summary>
    /// <param name="packageReader"> The canonical package reader. </param>
    /// <param name="bundleSerializer"> The canonical bundle descriptor serializer. </param>
    /// <param name="bundleFactory"> The canonical bundle construction boundary. </param>
    public CanonicalSkillBundleReader (
        CanonicalSkillPackageReader packageReader,
        SkillBundleJsonSerializer bundleSerializer,
        CanonicalSkillBundle.Factory bundleFactory)
    {
        this.packageReader = packageReader ?? throw new ArgumentNullException(nameof(packageReader));
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        this.bundleFactory = bundleFactory ?? throw new ArgumentNullException(nameof(bundleFactory));
    }

    /// <summary> Reads and validates one generated canonical bundle directory. </summary>
    /// <param name="packageRoot"> The directory containing <c>bundle.json</c> and package directories. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns> The canonical bundle, or a manifest/path failure. </returns>
    public async ValueTask<SkillOperationResult<CanonicalSkillBundle>> ReadAsync (
        AbsolutePath packageRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPackageRoot = packageRoot;
        if (!FileSystemEntryInspector.TryInspect(
                fullPackageRoot,
                out var packageRootObservation,
                out _))
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL bundle root could not be inspected: {fullPackageRoot}");
        }

        if (packageRootObservation.State == FileSystemEntryState.Missing)
        {
            return Failure($"Generated SKILL bundle directory does not exist: {packageRoot}");
        }

        if (packageRootObservation.State != FileSystemEntryState.Directory)
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL bundle root must be a regular directory: {fullPackageRoot}");
        }

        var rootFileResult = ValidateRootFiles(fullPackageRoot);
        if (!rootFileResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(
                rootFileResult.Failure!.Code,
                rootFileResult.Failure.Message);
        }

        var descriptorPathResult = PackagePathResolver.ResolveRegularFile(fullPackageRoot, PackageRelativePath.Parse("bundle.json"));
        if (!descriptorPathResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(
                descriptorPathResult.Failure!.Code,
                descriptorPathResult.Failure.Message);
        }

        if (!File.Exists(descriptorPathResult.Value!.Value))
        {
            return Failure($"Generated SKILL bundle is missing bundle.json: {fullPackageRoot}");
        }

        var textResult = await SkillBundleFileReader.ReadUtf8WithoutByteOrderMarkAsync(
                descriptorPathResult.Value!,
                SkillFailureCodes.ManifestInvalid,
                cancellationToken)
            .ConfigureAwait(false);
        if (!textResult.IsSuccess)
        {
            return Failure(textResult.Failure!.Message);
        }

        SkillBundleDescriptor descriptor;
        try
        {
            descriptor = bundleSerializer.DeserializeDescriptor(textResult.Value!);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException)
        {
            return Failure("Generated bundle.json is invalid.");
        }

        if (!string.Equals(textResult.Value, bundleSerializer.SerializeDescriptor(descriptor), StringComparison.Ordinal))
        {
            return Failure("Generated bundle.json is not canonical.");
        }

        var packagesResult = await packageReader.ReadAllAsync(fullPackageRoot, cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(
                packagesResult.Failure!.Code,
                packagesResult.Failure.Message);
        }

        return bundleFactory.CreateCanonical(new CanonicalSkillBundleCandidate(descriptor, packagesResult.Value!));
    }

    private static SkillOperationResult<bool> ValidateRootFiles (AbsolutePath packageRoot)
    {
        foreach (var path in Directory.EnumerateFiles(packageRoot.Value).Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            if (!string.Equals(fileName, "bundle.json", StringComparison.Ordinal))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Generated SKILL bundle contains an unsupported root file: {fileName}");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<CanonicalSkillBundle> Failure (string message)
    {
        return SkillOperationResult<CanonicalSkillBundle>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }
}
