using MackySoft.AgentSkills.Dependencies;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Represents one generated bundle descriptor and its complete canonical package set. </summary>
public sealed class CanonicalSkillBundle
{
    /// <summary> Initializes one immutable canonical bundle from a fully validated candidate. </summary>
    /// <param name="candidate"> The candidate whose descriptor and complete package set agree. </param>
    private CanonicalSkillBundle (CanonicalSkillBundleCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Descriptor = candidate.Descriptor;
        Packages = Array.AsReadOnly(candidate.Packages
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary> Gets the runtime bundle descriptor. </summary>
    public SkillBundleDescriptor Descriptor { get; }

    /// <summary> Gets an immutable snapshot of the complete canonical package set. </summary>
    public IReadOnlyList<CanonicalSkillPackage> Packages { get; }

    /// <summary> Validates complete bundle candidates and creates canonical bundle snapshots. </summary>
    public sealed class Factory
    {
        private readonly SkillBundleDigestCalculator digestCalculator;

        /// <summary> Initializes the canonical bundle construction boundary. </summary>
        /// <param name="digestCalculator"> The canonical package-set digest calculator. </param>
        public Factory (SkillBundleDigestCalculator digestCalculator)
        {
            this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        }

        /// <summary> Validates one complete candidate and creates its canonical bundle snapshot. </summary>
        internal SkillOperationResult<CanonicalSkillBundle> CreateCanonical (CanonicalSkillBundleCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            var validationResult = Validate(candidate.Descriptor, candidate.Packages);
            return validationResult.IsSuccess
                ? SkillOperationResult<CanonicalSkillBundle>.Success(new CanonicalSkillBundle(candidate))
                : BundleFailure(validationResult.Failure!.Message);
        }

        private SkillOperationResult<bool> Validate (
            SkillBundleDescriptor descriptor,
            IReadOnlyList<CanonicalSkillPackage> packages)
        {
            if (packages.Count == 0)
            {
                return ValidationFailure("Canonical SKILL bundle must contain at least one package.");
            }

            var skillNames = new HashSet<SkillName>();
            foreach (var package in packages)
            {
                if (!skillNames.Add(package.Manifest.SkillName))
                {
                    return ValidationFailure($"Canonical SKILL bundle package names must be unique: {package.Manifest.SkillName.Value}");
                }

                if (package.Manifest.CatalogId != descriptor.CatalogId)
                {
                    return ValidationFailure($"Generated SKILL package catalogId does not match bundle.json: {package.Manifest.SkillName.Value}");
                }

                if (package.Manifest.SkillBundleVersion != descriptor.SkillBundleVersion)
                {
                    return ValidationFailure($"Generated SKILL package skillBundleVersion does not match bundle.json: {package.Manifest.SkillName.Value}");
                }
            }

            var packageIndex = packages.ToDictionary(static package => package.Manifest.SkillName);
            var dependencyResult = SkillDependencyGraphValidator.Validate(
                packageIndex.ToDictionary(
                    static item => item.Key,
                    static item => item.Value.Manifest.Dependencies),
                SkillFailureCodes.ManifestInvalid,
                "Generated SKILL bundle");
            if (!dependencyResult.IsSuccess)
            {
                return ValidationFailure(dependencyResult.Failure!.Message);
            }

            Sha256Digest actualDigest;
            try
            {
                actualDigest = digestCalculator.ComputeDigest(packages);
            }
            catch (ArgumentException ex)
            {
                return ValidationFailure(ex.Message);
            }

            if (actualDigest != descriptor.BundleDigest)
            {
                return ValidationFailure("Generated bundle.json bundleDigest does not match canonical package files.");
            }

            return SkillOperationResult<bool>.Success(true);
        }

        private static SkillOperationResult<CanonicalSkillBundle> BundleFailure (string message)
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
        }

        private static SkillOperationResult<bool> ValidationFailure (string message)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
        }
    }
}
