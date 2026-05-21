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
        Assert.Contains("uses: mackysoft/actions/setup-dotnet-cache@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("restore-command: dotnet restore AgentSkills.slnx", workflow, StringComparison.Ordinal);
        Assert.Contains("run: bash scripts/verify.sh --no-restore --configuration Release", workflow, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(workflow, "run: bash scripts/verify.sh --no-restore --configuration Release"));
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
        Assert.Contains("uses: mackysoft/actions/setup-dotnet-cache@v1", workflow, StringComparison.Ordinal);
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
        Assert.Contains("uses: mackysoft/actions/setup-dotnet-cache@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/resolve-release-version@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("allow-prerelease: true", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/validate-release-source@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("releaseSha: ${{ steps.source.outputs.release-sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("fetch-depth: 0", workflow, StringComparison.Ordinal);
        Assert.Contains("publish:\n    name: publish\n    needs: package", workflow, StringComparison.Ordinal);
        Assert.Contains("PACKAGE_VERSION: ${{ needs.package.outputs.packageVersion }}", workflow, StringComparison.Ordinal);
        Assert.Contains("RELEASE_SHA: ${{ needs.package.outputs.releaseSha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("name: nuget-packages", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Validate publish source", workflow, StringComparison.Ordinal);
        Assert.Contains("release-sha: ${{ env.RELEASE_SHA }}", workflow, StringComparison.Ordinal);
        Assert.Contains("require-head-match: true", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/inspect-nuget-package-state@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("fail-on-partial: false", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Map publication state", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Download and validate existing published packages", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.state.outputs.libraryExists == 'true' || steps.state.outputs.cliExists == 'true'", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/validate-nuget-package-repository-commit.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.publication.outputs.publish-required == 'true'", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/publish-nuget-package@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/wait-nuget-packages@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Download published packages for release mirror", workflow, StringComparison.Ordinal);
        Assert.Contains("curl --fail --silent --show-error --location \"${url}\" --output \"${path}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--version \"${{ steps.version.outputs.package-version }}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--repository-commit \"${{ steps.source.outputs.release-sha }}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages/MackySoft.AgentSkills.${PACKAGE_VERSION}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages/MackySoft.AgentSkills.Cli.${PACKAGE_VERSION}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("libraryPackageUrl=${library_package_url}", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: mackysoft/actions/mirror-github-release-assets@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("update-existing-release: false", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("DISPATCH_VERSION", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("NuGet/login@v1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet nuget push", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("validate-release-tag.sh", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("mirror-nuget-package-release.sh", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("- name: Use existing published packages", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("packagesExist=", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("has an incomplete NuGet publication state", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Release tag ${tag_name} must match Directory.Build.props Version", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("- name: Create release tag", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Agent Skills ${PACKAGE_VERSION}", workflow, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(workflow, "package_url()"));

        var validateSourceIndex = workflow.IndexOf("- name: Validate release source", StringComparison.Ordinal);
        var packIndex = workflow.IndexOf("- name: Pack and smoke test", StringComparison.Ordinal);
        var validatePublishTagIndex = workflow.IndexOf("- name: Validate publish tag", StringComparison.Ordinal);
        var validatePublishSourceIndex = workflow.IndexOf("- name: Validate publish source", StringComparison.Ordinal);
        var inspectIndex = workflow.IndexOf("- name: Inspect publication state", StringComparison.Ordinal);
        var downloadExistingIndex = workflow.IndexOf("- name: Download and validate existing published packages", StringComparison.Ordinal);
        var publishIndex = workflow.IndexOf("- name: Publish to nuget.org", StringComparison.Ordinal);
        var waitIndex = workflow.IndexOf("- name: Wait for published packages", StringComparison.Ordinal);
        var downloadPublishedIndex = workflow.IndexOf("- name: Download published packages for release mirror", StringComparison.Ordinal);
        var mirrorIndex = workflow.IndexOf("- name: Mirror packages to GitHub Release", StringComparison.Ordinal);
        Assert.NotEqual(-1, validateSourceIndex);
        Assert.NotEqual(-1, packIndex);
        Assert.NotEqual(-1, validatePublishTagIndex);
        Assert.NotEqual(-1, validatePublishSourceIndex);
        Assert.NotEqual(-1, inspectIndex);
        Assert.NotEqual(-1, downloadExistingIndex);
        Assert.NotEqual(-1, publishIndex);
        Assert.NotEqual(-1, waitIndex);
        Assert.NotEqual(-1, downloadPublishedIndex);
        Assert.NotEqual(-1, mirrorIndex);
        Assert.True(validateSourceIndex < packIndex);
        Assert.True(validatePublishTagIndex < validatePublishSourceIndex);
        Assert.True(validatePublishSourceIndex < inspectIndex);
        Assert.True(inspectIndex < downloadExistingIndex);
        Assert.True(downloadExistingIndex < publishIndex);
        Assert.True(publishIndex < waitIndex);
        Assert.True(waitIndex < downloadPublishedIndex);
        Assert.True(downloadPublishedIndex < mirrorIndex);
        Assert.True(waitIndex < mirrorIndex);
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

    [Fact]
    [Trait("Size", "Small")]
    public void Code_quality_uses_editorconfig_without_diagnostic_filter ()
    {
        var script = File.ReadAllText(ToRepositoryPath("scripts/code-quality.sh"));

        Assert.Contains("dotnet format \"$command\" \"$solution\"", script, StringComparison.Ordinal);
        Assert.Contains("run_dotnet_format style --verbosity minimal --no-restore", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--diagnostics", script, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostics=(", script, StringComparison.Ordinal);
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
