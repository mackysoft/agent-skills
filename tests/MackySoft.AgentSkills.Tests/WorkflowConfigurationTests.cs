namespace MackySoft.AgentSkills.Tests;

public sealed class WorkflowConfigurationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Dotnet_verify_workflow_runs_tests_on_supported_operating_systems ()
    {
        var workflow = ReadNormalizedText(".github/workflows/dotnet-verify.yaml");

        Assert.Contains("name: dotnet-verify", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_call:", workflow, StringComparison.Ordinal);
        Assert.Contains("test:\n    name: test (${{ matrix.os }})", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: ${{ matrix.os }}", workflow, StringComparison.Ordinal);
        Assert.Contains("fail-fast: false", workflow, StringComparison.Ordinal);
        Assert.Contains("          - ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("          - windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("          - macos-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/setup-dotnet@v5", workflow, StringComparison.Ordinal);
        Assert.Contains("run: bash scripts/verify.sh --configuration Release", workflow, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(workflow, "run: bash scripts/verify.sh --configuration Release"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Ci_workflow_verifies_changes_and_packages_without_publishing ()
    {
        var workflow = ReadNormalizedText(".github/workflows/ci.yaml");

        Assert.Contains("name: ci", workflow, StringComparison.Ordinal);
        Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
        Assert.Contains("push:\n    branches:\n      - master", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("test:\n    uses: ./.github/workflows/dotnet-verify.yaml", workflow, StringComparison.Ordinal);
        Assert.Contains("package:\n    name: package\n    needs: test", workflow, StringComparison.Ordinal);
        Assert.Contains("run: bash scripts/verify-packages.sh --configuration Release", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("NuGet/login", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet nuget push", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Mirror packages to GitHub Release", workflow, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Nuget_publish_workflow_uses_release_tag_version_and_publishes_after_package_availability ()
    {
        var workflow = ReadNormalizedText(".github/workflows/nuget-package.yaml");

        Assert.Contains("push:\n    tags:\n      - \"[0-9]*.[0-9]*.[0-9]*\"", workflow, StringComparison.Ordinal);
        Assert.Contains("test:\n    uses: ./.github/workflows/dotnet-verify.yaml", workflow, StringComparison.Ordinal);
        Assert.Contains("package:\n    name: package\n    needs: test", workflow, StringComparison.Ordinal);
        Assert.Contains("packageVersion: ${{ steps.release_source.outputs['package-version'] }}", workflow, StringComparison.Ordinal);
        Assert.Contains("releaseSha: ${{ steps.release_source.outputs['release-sha'] }}", workflow, StringComparison.Ordinal);
        Assert.Contains("fetch-depth: 0", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/release/source-guard@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("tag-name: ${{ github.ref_name }}", workflow, StringComparison.Ordinal);
        Assert.Contains("default-branch: ${{ github.event.repository.default_branch }}", workflow, StringComparison.Ordinal);
        Assert.Contains("expected-release-sha: ${{ env.RELEASE_SHA }}", workflow, StringComparison.Ordinal);
        Assert.Contains("publish:\n    name: publish\n    needs: package", workflow, StringComparison.Ordinal);
        Assert.Contains("PACKAGE_VERSION: ${{ needs.package.outputs.packageVersion }}", workflow, StringComparison.Ordinal);
        Assert.Contains("RELEASE_SHA: ${{ needs.package.outputs.releaseSha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("name: nuget-packages", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Inspect publication state", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/nuget/package-state@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("mode: inspect", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.package_state.outputs['publish-required'] == 'true'", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/nuget/trusted-publish@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("package-glob: artifacts/packages/*.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("nuget-user: ${{ vars.NUGET_USER }}", workflow, StringComparison.Ordinal);
        Assert.Contains("mode: wait", workflow, StringComparison.Ordinal);
        Assert.Contains("max-attempts: 30", workflow, StringComparison.Ordinal);
        Assert.Contains("interval-seconds: 10", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/validate-nuget-package-repository-commit.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Publish packages to nuget.org", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Download published packages for release mirror", workflow, StringComparison.Ordinal);
        Assert.Contains("curl --fail --silent --show-error --location \"${url}\" --output \"${path}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--version \"${PACKAGE_VERSION}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--repository-commit \"${RELEASE_SHA}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages/MackySoft.AgentSkills.${PACKAGE_VERSION}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages/MackySoft.AgentSkills.Cli.${PACKAGE_VERSION}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("--title \"${RELEASE_TAG}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--notes \"\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("bash scripts/validate-release-tag.sh", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("NuGet/login@v1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet nuget push", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("libraryExists", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("cliExists", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--expected-sha", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("DISPATCH_VERSION", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--skip-duplicate", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("- name: Use existing published packages", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("packagesExist=", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("has an incomplete NuGet publication state", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Release tag ${tag_name} must match Directory.Build.props Version", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("- name: Create release tag", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Agent Skills ${PACKAGE_VERSION}", workflow, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(workflow, "uses: mackysoft/actions/release/source-guard@v1"));
        Assert.Equal(2, CountOccurrences(workflow, "uses: mackysoft/actions/nuget/package-state@v1"));
        Assert.Equal(1, CountOccurrences(workflow, "uses: mackysoft/actions/nuget/trusted-publish@v1"));
        Assert.Equal(1, CountOccurrences(workflow, "package_url()"));

        var packageGuardIndex = workflow.IndexOf("- name: Guard release source", StringComparison.Ordinal);
        var packIndex = workflow.IndexOf("- name: Pack and smoke test", StringComparison.Ordinal);
        var publishGuardIndex = workflow.IndexOf("- name: Guard release source", packIndex, StringComparison.Ordinal);
        var validateArtifactsIndex = workflow.IndexOf("- name: Validate package artifacts", StringComparison.Ordinal);
        var inspectIndex = workflow.IndexOf("- name: Inspect publication state", StringComparison.Ordinal);
        var publishIndex = workflow.IndexOf("- name: Publish packages to nuget.org", StringComparison.Ordinal);
        var waitIndex = workflow.IndexOf("- name: Wait for published packages", StringComparison.Ordinal);
        var downloadPublishedIndex = workflow.IndexOf("- name: Download published packages for release mirror", StringComparison.Ordinal);
        var reguardIndex = workflow.IndexOf("- name: Re-guard release source", StringComparison.Ordinal);
        var mirrorIndex = workflow.IndexOf("- name: Mirror packages to GitHub Release", StringComparison.Ordinal);
        Assert.NotEqual(-1, packageGuardIndex);
        Assert.NotEqual(-1, packIndex);
        Assert.NotEqual(-1, publishGuardIndex);
        Assert.NotEqual(-1, validateArtifactsIndex);
        Assert.NotEqual(-1, inspectIndex);
        Assert.NotEqual(-1, publishIndex);
        Assert.NotEqual(-1, waitIndex);
        Assert.NotEqual(-1, downloadPublishedIndex);
        Assert.NotEqual(-1, reguardIndex);
        Assert.NotEqual(-1, mirrorIndex);
        Assert.True(packageGuardIndex < packIndex);
        Assert.True(publishGuardIndex < validateArtifactsIndex);
        Assert.True(validateArtifactsIndex < inspectIndex);
        Assert.True(inspectIndex < publishIndex);
        Assert.True(publishIndex < waitIndex);
        Assert.True(waitIndex < downloadPublishedIndex);
        Assert.True(downloadPublishedIndex < reguardIndex);
        Assert.True(reguardIndex < mirrorIndex);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Release_mirror_script_accepts_empty_notes_and_requires_existing_tag ()
    {
        var script = File.ReadAllText(ToRepositoryPath("scripts/mirror-nuget-package-release.sh"));

        Assert.Contains("release_notes_set=false", script, StringComparison.Ordinal);
        Assert.Contains("release_notes_set=true", script, StringComparison.Ordinal);
        Assert.Contains("\"${release_notes_set}\" != true", script, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--expected-sha", script, StringComparison.Ordinal);
        Assert.DoesNotContain("validate-release-tag.sh", script, StringComparison.Ordinal);
        Assert.DoesNotContain("|| -z \"${release_notes}\"", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Package_verification_script_overrides_project_versions_with_requested_version ()
    {
        var script = File.ReadAllText(ToRepositoryPath("scripts/verify-packages.sh"));

        Assert.Contains("--version <semver>", script, StringComparison.Ordinal);
        Assert.Contains("--repository-commit <sha>", script, StringComparison.Ordinal);
        Assert.Contains("semver_pattern='^(0|[1-9][0-9]*)\\.", script, StringComparison.Ordinal);
        Assert.Contains("-p:Version=\"$package_version\"", script, StringComparison.Ordinal);
        Assert.Contains("-p:PackageVersion=\"$package_version\"", script, StringComparison.Ordinal);
        Assert.Contains("-p:RepositoryCommit=\"$repository_commit\"", script, StringComparison.Ordinal);
        Assert.Contains("validate-nuget-package-repository-commit.sh", script, StringComparison.Ordinal);
        Assert.Contains("MackySoft.AgentSkills.$package_version.nupkg", script, StringComparison.Ordinal);
        Assert.Contains("MackySoft.AgentSkills.Cli.$package_version.nupkg", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Nuget_package_repository_commit_validation_script_checks_nuspec_commit ()
    {
        var script = File.ReadAllText(ToRepositoryPath("scripts/validate-nuget-package-repository-commit.sh"));

        Assert.Contains("--package-id <id> --package-path <path> --expected-commit <sha>", script, StringComparison.Ordinal);
        Assert.Contains("unzip -p \"${package_path}\" \"*.nuspec\"", script, StringComparison.Ordinal);
        Assert.Contains("commit=\"([^\"]+)\"", script, StringComparison.Ordinal);
        Assert.Contains("if [[ \"${package_commit}\" != \"${expected_commit}\" ]]; then", script, StringComparison.Ordinal);
    }

    private static string ToRepositoryPath (string relativePath)
    {
        return Path.Combine(SkillTestData.GetRepositoryRoot(), relativePath);
    }

    private static string ReadNormalizedText (string relativePath)
    {
        return File.ReadAllText(ToRepositoryPath(relativePath)).ReplaceLineEndings("\n");
    }

    private static int CountOccurrences (string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
