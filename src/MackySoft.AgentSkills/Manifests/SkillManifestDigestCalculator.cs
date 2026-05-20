using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Computes canonical <c>agent-skill.json</c> manifest digests. </summary>
public sealed class SkillManifestDigestCalculator
{
    private const string ManifestPath = "agent-skill.json";

    private readonly SkillDigestCalculator digestCalculator;
    private readonly SkillManifestJsonSerializer manifestSerializer;

    /// <summary> Initializes a new instance of the <see cref="SkillManifestDigestCalculator" /> class. </summary>
    /// <param name="digestCalculator"> The deterministic digest calculator. </param>
    /// <param name="manifestSerializer"> The canonical manifest serializer. </param>
    public SkillManifestDigestCalculator (
        SkillDigestCalculator digestCalculator,
        SkillManifestJsonSerializer manifestSerializer)
    {
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
    }

    /// <summary> Computes the manifest digest from canonical JSON with <c>manifestDigest</c> excluded. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The digest in <c>sha256:&lt;lowerhex&gt;</c> form. </returns>
    public string ComputeManifestDigest (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return digestCalculator.ComputeSingleFileDigest(
            ManifestPath,
            manifestSerializer.SerializeWithoutManifestDigest(manifest));
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
