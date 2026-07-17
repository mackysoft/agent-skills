using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Materialization;

/// <summary> Represents a host-materialized SKILL package. </summary>
public sealed class SkillMaterializedPackage
{
    /// <summary> Initializes one immutable host-materialized package. </summary>
    /// <param name="skillName"> The skill name. </param>
    /// <param name="host"> The host. </param>
    /// <param name="files"> The complete materialized package files. </param>
    public SkillMaterializedPackage (
        SkillName skillName,
        SkillHostKind host,
        IReadOnlyList<SkillPackageFile> files)
    {
        ArgumentNullException.ThrowIfNull(skillName);
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        ArgumentNullException.ThrowIfNull(files);
        var fileSnapshot = files.ToArray();
        if (fileSnapshot.Any(static file => file is null))
        {
            throw new ArgumentException("Materialized package files must not contain null items.", nameof(files));
        }

        var portablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in fileSnapshot)
        {
            if (!portablePaths.Add(file.RelativePath))
            {
                throw new ArgumentException("Materialized package file paths must be unique when case is ignored.", nameof(files));
            }
        }

        SkillName = skillName;
        Host = host;
        Files = Array.AsReadOnly(fileSnapshot
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary> Gets the skill name. </summary>
    public SkillName SkillName { get; }

    /// <summary> Gets the host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the materialized package files in ordinal path order. </summary>
    public IReadOnlyList<SkillPackageFile> Files { get; }
}
