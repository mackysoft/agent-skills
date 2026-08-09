using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Computes a version-independent digest for all v2 skill and agent package files. </summary>
public sealed class AgentSkillsBundleDigestCalculator
{
    private static readonly PackageRelativePath SkillManifestPath = PackageRelativePath.Parse("agent-skill.json");
    private static readonly PackageRelativePath AgentManifestPath = PackageRelativePath.Parse("agent-manifest.json");

    private readonly SkillManifestJsonSerializer skillManifestSerializer;
    private readonly AgentManifestJsonSerializer agentManifestSerializer;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes the calculator. </summary>
    public AgentSkillsBundleDigestCalculator (SkillManifestJsonSerializer skillManifestSerializer, AgentManifestJsonSerializer agentManifestSerializer, SkillDigestCalculator digestCalculator)
    {
        this.skillManifestSerializer = skillManifestSerializer ?? throw new ArgumentNullException(nameof(skillManifestSerializer));
        this.agentManifestSerializer = agentManifestSerializer ?? throw new ArgumentNullException(nameof(agentManifestSerializer));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Computes the mixed package-set digest. </summary>
    public Sha256Digest ComputeDigest (IReadOnlyList<CanonicalSkillPackage> skills, IReadOnlyList<CanonicalAgentPackage> agents)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(agents);
        var files = skills.SelectMany(package => package.Files.Select(file => new SkillDigestInputFile(PackageRelativePath.Parse($"skills/{package.Manifest.SkillName.Value}/{file.RelativePath.Value}"), file.RelativePath == SkillManifestPath ? skillManifestSerializer.SerializeForBundleDigest(package.Manifest) : file.Content)))
            .Concat(agents.SelectMany(package => package.Files.Select(file => new SkillDigestInputFile(PackageRelativePath.Parse($"agents/{package.Manifest.AgentName.Value}/{file.RelativePath.Value}"), file.RelativePath == AgentManifestPath ? agentManifestSerializer.SerializeForBundleDigest(package.Manifest) : file.Content))))
            .ToArray();
        if (files.Length == 0 || files.GroupBy(static file => file.RelativePath).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Mixed bundle digest input must be non-empty and unique.");
        }

        return digestCalculator.ComputeDigest(files);
    }
}
