using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Verifies installed host materialization against canonical host-independent content. </summary>
public sealed class SkillInstalledContentDigestVerifier
{
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledContentDigestVerifier" /> class. </summary>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledContentDigestVerifier (SkillDigestCalculator digestCalculator)
    {
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Checks whether installed files match the canonical content digest. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="package"> The canonical package. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when installed content matches; otherwise <see langword="false" />. </returns>
    public async ValueTask<SkillOperationResult<bool>> MatchesContentDigestAsync (
        string skillDirectory,
        CanonicalSkillPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        var digestInputs = new List<SkillDigestInputFile>();
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(skillBodyResult.Failure!.Code, skillBodyResult.Failure.Message);
        }

        var skillBody = skillBodyResult.Value!;
        if (!skillBody.Exists)
        {
            return SkillOperationResult<bool>.Success(false);
        }

        digestInputs.Add(new SkillDigestInputFile("SKILL.md", skillBody.Body));
        foreach (var reference in package.Files
            .Where(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
        {
            var referencePathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, reference.RelativePath);
            if (!referencePathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(referencePathResult.Failure!.Code, referencePathResult.Failure.Message);
            }

            if (!File.Exists(referencePathResult.Value!))
            {
                return SkillOperationResult<bool>.Success(false);
            }

            var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(referencePathResult.Value!, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new SkillDigestInputFile(reference.RelativePath, content));
        }

        var actualDigest = digestCalculator.ComputeDigest(digestInputs);
        return SkillOperationResult<bool>.Success(actualDigest == package.Manifest.ContentDigest);
    }

    private static async ValueTask<SkillOperationResult<InstalledSkillBody>> ReadInstalledSkillBodyAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, "SKILL.md");
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<InstalledSkillBody>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Present(body));
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
