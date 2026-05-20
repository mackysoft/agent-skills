using System.Security.Cryptography;
using System.Text;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Computes canonical <c>agent-skill.json</c> manifest digests. </summary>
public sealed class SkillManifestDigestCalculator
{
    private const string DigestPrefix = "sha256:";

    private readonly SkillManifestJsonSerializer manifestSerializer;

    /// <summary> Initializes a new instance of the <see cref="SkillManifestDigestCalculator" /> class. </summary>
    /// <param name="manifestSerializer"> The canonical manifest serializer. </param>
    public SkillManifestDigestCalculator (SkillManifestJsonSerializer manifestSerializer)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
    }

    /// <summary> Computes the manifest digest from canonical JSON with <c>manifestDigest</c> excluded. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The digest in <c>sha256:&lt;lowerhex&gt;</c> form. </returns>
    public string ComputeManifestDigest (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var json = manifestSerializer.SerializeWithoutManifestDigest(manifest);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return DigestPrefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary> Returns a manifest whose <c>manifestDigest</c> matches its canonical content. </summary>
    /// <param name="manifest"> The manifest without a trusted digest. </param>
    /// <returns> The manifest with the computed digest. </returns>
    public SkillManifest WithComputedManifestDigest (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return manifest with
        {
            ManifestDigest = ComputeManifestDigest(manifest),
        };
    }
}
