using System.Text;
using MackySoft.AgentDistribution.Digests;

namespace MackySoft.AgentDistribution.Agents.Manifests;

/// <summary> Computes the digest of an agent manifest excluding the digest field itself. </summary>
public sealed class AgentManifestDigestCalculator
{
    private readonly AgentManifestJsonSerializer serializer;

    /// <summary> Initializes the calculator. </summary>
    public AgentManifestDigestCalculator (AgentManifestJsonSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary> Computes the manifest digest. </summary>
    public Sha256Digest ComputeManifestDigest (AgentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(serializer.SerializeWithoutManifestDigest(manifest)));
    }
}
