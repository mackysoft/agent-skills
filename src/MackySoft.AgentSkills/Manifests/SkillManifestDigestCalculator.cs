using System.Text;
using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Computes canonical <c>agent-skill.json</c> manifest digests. </summary>
public sealed class SkillManifestDigestCalculator
{
    private readonly SkillManifestJsonSerializer manifestSerializer;

    /// <summary> Initializes a new instance of the <see cref="SkillManifestDigestCalculator" /> class. </summary>
    /// <param name="manifestSerializer"> The canonical manifest serializer. </param>
    public SkillManifestDigestCalculator (SkillManifestJsonSerializer manifestSerializer)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
    }

    /// <summary> Computes the manifest digest from canonical JSON with <c>manifestDigest</c> excluded. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    public Sha256Digest ComputeManifestDigest (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var json = manifestSerializer.SerializeWithoutManifestDigest(manifest);
        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(json));
    }

    /// <summary> Computes the canonical digest for a manifest candidate before it becomes a canonical manifest. </summary>
    /// <param name="candidate"> The field-valid manifest candidate. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    internal Sha256Digest ComputeManifestDigest (SkillManifestCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var json = manifestSerializer.SerializeWithoutManifestDigest(candidate);
        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(json));
    }
}
