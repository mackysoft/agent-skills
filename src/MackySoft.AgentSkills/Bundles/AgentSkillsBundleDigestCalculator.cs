using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Computes a version-independent digest for all v2 skill and agent package files. </summary>
public sealed class AgentSkillsBundleDigestCalculator
{
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
        var files = skills.SelectMany(package => package.Files.Select(file => new SkillDigestInputFile($"skills/{package.Manifest.SkillName.Value}/{file.RelativePath}", file.RelativePath == "agent-skill.json" ? skillManifestSerializer.SerializeForBundleDigest(package.Manifest) : file.Content)))
            .Concat(agents.SelectMany(package => package.Files.Select(file => new SkillDigestInputFile($"agents/{package.Manifest.AgentName.Value}/{file.RelativePath}", file.RelativePath == "agent-manifest.json" ? agentManifestSerializer.SerializeForBundleDigest(package.Manifest) : file.Content))))
            .ToArray();
        if (files.Length == 0 || files.GroupBy(static file => file.RelativePath, StringComparer.Ordinal).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Mixed bundle digest input must be non-empty and unique.");
        }

        return digestCalculator.ComputeDigest(files);
    }
}
