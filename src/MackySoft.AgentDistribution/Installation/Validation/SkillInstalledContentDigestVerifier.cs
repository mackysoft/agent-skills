using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Validation;

/// <summary> Verifies installed host materialization against canonical host-independent content. </summary>
public sealed class SkillInstalledContentDigestVerifier
{
    private readonly PackageContentDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledContentDigestVerifier" /> class. </summary>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledContentDigestVerifier (PackageContentDigestCalculator digestCalculator)
    {
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Checks whether installed files match the canonical content digest. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="package"> The canonical package. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when installed content matches; otherwise <see langword="false" />. </returns>
    public async ValueTask<AgentDistributionOperationResult<bool>> MatchesContentDigestAsync (
        AbsolutePath skillDirectory,
        CanonicalSkillPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        var digestInputs = new List<PackageContentDigestInputFile>();
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(skillBodyResult.Failure!.Code, skillBodyResult.Failure.Message);
        }

        var skillBody = skillBodyResult.Value!;
        if (!skillBody.Exists)
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        digestInputs.Add(new PackageContentDigestInputFile(PackageRelativePath.Parse("SKILL.md"), skillBody.Body));
        foreach (var reference in package.Files
            .Where(static file => SkillContentFileSetPaths.IsSupplementalContentFile(file.RelativePath))
            .OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal))
        {
            var referencePathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, reference.RelativePath);
            if (!referencePathResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(referencePathResult.Failure!.Code, referencePathResult.Failure.Message);
            }

            if (!File.Exists(referencePathResult.Value!.Value))
            {
                return AgentDistributionOperationResult<bool>.Success(false);
            }

            var content = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(referencePathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new PackageContentDigestInputFile(reference.RelativePath, content));
        }

        var actualDigest = digestCalculator.ComputeDigest(digestInputs);
        return AgentDistributionOperationResult<bool>.Success(actualDigest == package.Manifest.ContentDigest);
    }

    private static async ValueTask<AgentDistributionOperationResult<InstalledSkillBody>> ReadInstalledSkillBodyAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, PackageRelativePath.Parse("SKILL.md"));
        if (!skillPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<InstalledSkillBody>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!.Value))
        {
            return AgentDistributionOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var skillText = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return AgentDistributionOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return AgentDistributionOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Present(body));
    }

    private sealed class InstalledSkillBody
    {
        private InstalledSkillBody (
            bool exists,
            string body)
        {
            ArgumentNullException.ThrowIfNull(body);
            if (!exists && body.Length != 0)
            {
                throw new ArgumentException("A missing installed SKILL body must be empty.", nameof(body));
            }

            Exists = exists;
            Body = body;
        }

        public bool Exists { get; }

        public string Body { get; }

        public static InstalledSkillBody Missing { get; } = new(false, string.Empty);

        public static InstalledSkillBody Present (string body)
        {
            return new InstalledSkillBody(true, body);
        }
    }
}
