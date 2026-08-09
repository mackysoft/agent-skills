using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Materialization;

/// <summary> Materializes canonical SKILL packages for supported hosts. </summary>
public sealed class SkillMaterializationService
{
    /// <summary> Materializes one canonical package for one host. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="host"> The target host. </param>
    /// <returns> The materialized package or unsupported-host failure. </returns>
    public SkillOperationResult<SkillMaterializedPackage> Materialize (
        CanonicalSkillPackage package,
        HostKind host)
    {
        ArgumentNullException.ThrowIfNull(package);

        var registrationResult = HostRegistration.Get(host);
        if (!registrationResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackage>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
        }

        var metadata = new Hosts.Contracts.SkillHostMetadata(
            package.Manifest.SkillName,
            package.Manifest.DisplayName,
            package.Manifest.Description);

        var registration = registrationResult.Value!;
        var adapter = registration.SkillAdapter;
        var artifacts = adapter.BuildArtifacts(metadata);
        var metadataArtifactPath = registration.Skill.MetadataArtifactPath;
        var files = new List<PackageTextFile>();
        var hostArtifactFilePaths = package.Manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .OfType<PackageRelativePath>()
            .ToHashSet();

        foreach (var file in package.Files)
        {
            if (file.RelativePath == PackageRelativePath.Parse("SKILL.md"))
            {
                files.Add(new PackageTextFile(file.RelativePath, artifacts.Frontmatter + "\n" + file.Content));
                continue;
            }

            if (hostArtifactFilePaths.Contains(file.RelativePath))
            {
                continue;
            }

            files.Add(file);
        }

        if (metadataArtifactPath is null)
        {
            if (artifacts.MetadataContent is not null)
            {
                throw new InvalidOperationException($"Host adapter '{Vocabulary.GetText(registration.Host)}' must not emit metadata artifacts.");
            }
        }
        else
        {
            if (artifacts.MetadataContent is null)
            {
                throw new InvalidOperationException($"Host adapter '{Vocabulary.GetText(registration.Host)}' must emit metadata artifact '{metadataArtifactPath}'.");
            }

            files.Add(new PackageTextFile(metadataArtifactPath, artifacts.MetadataContent));
        }

        return SkillOperationResult<SkillMaterializedPackage>.Success(new SkillMaterializedPackage(
            package.Manifest.SkillName,
            registration.Host,
            files.OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal).ToArray()));
    }
}
