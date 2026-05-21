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
        Assert.Contains("tag_name=\"${GITHUB_REF_NAME}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("semver_pattern='^(0|[1-9][0-9]*)\\.", workflow, StringComparison.Ordinal);
        Assert.Contains("if [[ ! \"${tag_name}\" =~ ${semver_pattern} ]]; then", workflow, StringComparison.Ordinal);
        Assert.Contains("package_version=\"${tag_name}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("releaseSha: ${{ steps.source.outputs.releaseSha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("echo \"RELEASE_SHA=${release_sha}\" >> \"${GITHUB_ENV}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("fetch-depth: 0", workflow, StringComparison.Ordinal);
        Assert.Contains("git fetch origin \"${DEFAULT_BRANCH}:refs/remotes/origin/${DEFAULT_BRANCH}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("release_sha=\"$(git rev-list -n 1 \"refs/tags/${TAG_NAME}\")\"", workflow, StringComparison.Ordinal);
        Assert.Contains("checkout_sha=\"$(git rev-parse HEAD)\"", workflow, StringComparison.Ordinal);
        Assert.Contains("if [[ \"${checkout_sha}\" != \"${release_sha}\" ]]; then", workflow, StringComparison.Ordinal);
        Assert.Contains("git merge-base --is-ancestor \"${release_sha}\" \"origin/${DEFAULT_BRANCH}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("publish:\n    name: publish\n    needs: package", workflow, StringComparison.Ordinal);
        Assert.Contains("PACKAGE_VERSION: ${{ needs.package.outputs.packageVersion }}", workflow, StringComparison.Ordinal);
        Assert.Contains("RELEASE_SHA: ${{ needs.package.outputs.releaseSha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("name: nuget-packages", workflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/validate-release-tag.sh --tag-name \"${GITHUB_REF_NAME}\" --expected-sha \"${RELEASE_SHA}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Download and validate existing published packages", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.state.outputs.libraryExists == 'true' || steps.state.outputs.cliExists == 'true'", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/validate-nuget-package-repository-commit.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.state.outputs.libraryExists != 'true' || steps.state.outputs.cliExists != 'true'", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Publish library to nuget.org", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Publish CLI to nuget.org", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Download published packages for release mirror", workflow, StringComparison.Ordinal);
        Assert.Contains("curl --fail --silent --show-error --location \"${url}\" --output \"${path}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--version \"${PACKAGE_VERSION}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--repository-commit \"${RELEASE_SHA}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages/MackySoft.AgentSkills.${PACKAGE_VERSION}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages/MackySoft.AgentSkills.Cli.${PACKAGE_VERSION}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("libraryPackageUrl=${library_package_url}", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet nuget push \\", workflow, StringComparison.Ordinal);
        Assert.Contains("--title \"${RELEASE_TAG}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--notes \"\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--expected-sha \"${RELEASE_SHA}\"", workflow, StringComparison.Ordinal);
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
        Assert.Equal(1, CountOccurrences(workflow, "package_url()"));

        var validateSourceIndex = workflow.IndexOf("- name: Validate release source", StringComparison.Ordinal);
        var packIndex = workflow.IndexOf("- name: Pack and smoke test", StringComparison.Ordinal);
        var validatePublishTagIndex = workflow.IndexOf("- name: Validate publish tag", StringComparison.Ordinal);
        var publishLibraryIndex = workflow.IndexOf("- name: Publish library to nuget.org", StringComparison.Ordinal);
        var publishCliIndex = workflow.IndexOf("- name: Publish CLI to nuget.org", StringComparison.Ordinal);
        var waitIndex = workflow.IndexOf("- name: Wait for published packages", StringComparison.Ordinal);
        var downloadPublishedIndex = workflow.IndexOf("- name: Download published packages for release mirror", StringComparison.Ordinal);
        var mirrorIndex = workflow.IndexOf("- name: Mirror packages to GitHub Release", StringComparison.Ordinal);
        Assert.NotEqual(-1, validateSourceIndex);
        Assert.NotEqual(-1, packIndex);
        Assert.NotEqual(-1, validatePublishTagIndex);
        Assert.NotEqual(-1, publishLibraryIndex);
        Assert.NotEqual(-1, publishCliIndex);
        Assert.NotEqual(-1, waitIndex);
        Assert.NotEqual(-1, downloadPublishedIndex);
        Assert.NotEqual(-1, mirrorIndex);
        Assert.True(validateSourceIndex < packIndex);
        Assert.True(validatePublishTagIndex < publishLibraryIndex);
        Assert.True(publishLibraryIndex < publishCliIndex);
        Assert.True(publishCliIndex < waitIndex);
        Assert.True(waitIndex < downloadPublishedIndex);
        Assert.True(downloadPublishedIndex < mirrorIndex);
        Assert.True(waitIndex < mirrorIndex);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Release_mirror_script_accepts_empty_notes_and_requires_existing_tag ()
    {
        var script = File.ReadAllText(ToRepositoryPath("scripts/mirror-nuget-package-release.sh"));

        Assert.Contains("release_notes_set=false", script, StringComparison.Ordinal);
        Assert.Contains("release_notes_set=true", script, StringComparison.Ordinal);
        Assert.Contains("\"${release_notes_set}\" != true", script, StringComparison.Ordinal);
        Assert.Contains("--expected-sha <sha>", script, StringComparison.Ordinal);
        Assert.Contains("validate-release-tag.sh", script, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", script, StringComparison.Ordinal);
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
    public void Release_tag_validation_script_requires_expected_sha ()
    {
        var script = File.ReadAllText(ToRepositoryPath("scripts/validate-release-tag.sh"));

        Assert.Contains("--tag-name <tag> --expected-sha <sha>", script, StringComparison.Ordinal);
        Assert.Contains("git fetch --force \"${remote}\" \"refs/tags/${tag_name}:refs/tags/${tag_name}\"", script, StringComparison.Ordinal);
        Assert.Contains("tag_sha=\"$(git rev-list -n 1 \"refs/tags/${tag_name}\")\"", script, StringComparison.Ordinal);
        Assert.Contains("if [[ \"${tag_sha}\" != \"${expected_sha}\" ]]; then", script, StringComparison.Ordinal);
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
