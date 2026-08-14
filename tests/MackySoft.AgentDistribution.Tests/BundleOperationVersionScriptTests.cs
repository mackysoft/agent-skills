using System.Diagnostics;

namespace MackySoft.AgentDistribution.Tests;

public sealed class BundleOperationVersionScriptTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyRelease_WhenCurrentVersionIsTarget_ReturnsTargetVersion ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-release", "verify-release-target");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.name", "Test User"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.email", "test@example.com"], scope.FullPath);
        WriteBundle(scope, 3);
        await RunProcessAsync("git", ["add", "agent-distribution/bundle.json"], scope.FullPath);
        await RunProcessAsync("git", ["commit", "--quiet", "-m", "base"], scope.FullPath);
        var baseRef = (await RunProcessAsync("git", ["rev-parse", "HEAD"], scope.FullPath)).StandardOutput.Trim();
        WriteBundle(scope, 4);

        var result = await RunProcessAsync(
            "bash",
            [GetScriptPath(), "--operation", "verify-release", "--source", "agent-distribution", "--base-ref", baseRef],
            scope.FullPath);

        Assert.Equal("4", result.StandardOutput.Trim());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyRelease_WhenCurrentVersionIsStillBase_Fails ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-release", "verify-release-base");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.name", "Test User"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.email", "test@example.com"], scope.FullPath);
        WriteBundle(scope, 3);
        await RunProcessAsync("git", ["add", "agent-distribution/bundle.json"], scope.FullPath);
        await RunProcessAsync("git", ["commit", "--quiet", "-m", "base"], scope.FullPath);
        var baseRef = (await RunProcessAsync("git", ["rev-parse", "HEAD"], scope.FullPath)).StandardOutput.Trim();

        var result = await RunProcessAsync(
            "bash",
            [GetScriptPath(), "--operation", "verify-release", "--source", "agent-distribution", "--base-ref", baseRef],
            scope.FullPath,
            requireSuccess: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("must equal release target 4", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WhenCurrentVersionMatchesBase_ReturnsPreservedVersion ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-release", "resolve-sync-current");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.name", "Test User"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.email", "test@example.com"], scope.FullPath);
        WriteBundle(scope, 3);
        await RunProcessAsync("git", ["add", "agent-distribution/bundle.json"], scope.FullPath);
        await RunProcessAsync("git", ["commit", "--quiet", "-m", "base"], scope.FullPath);
        var baseRef = (await RunProcessAsync("git", ["rev-parse", "HEAD"], scope.FullPath)).StandardOutput.Trim();

        var result = await RunProcessAsync(
            "bash",
            [GetScriptPath(), "--operation", "verify", "--source", "agent-distribution", "--base-ref", baseRef],
            scope.FullPath);

        Assert.Equal("3", result.StandardOutput.Trim());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WhenCurrentVersionChanges_Fails ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-release", "resolve-sync-changed");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.name", "Test User"], scope.FullPath);
        await RunProcessAsync("git", ["config", "user.email", "test@example.com"], scope.FullPath);
        WriteBundle(scope, 3);
        await RunProcessAsync("git", ["add", "agent-distribution/bundle.json"], scope.FullPath);
        await RunProcessAsync("git", ["commit", "--quiet", "-m", "base"], scope.FullPath);
        var baseRef = (await RunProcessAsync("git", ["rev-parse", "HEAD"], scope.FullPath)).StandardOutput.Trim();
        WriteBundle(scope, 4);

        var result = await RunProcessAsync(
            "bash",
            [GetScriptPath(), "--operation", "verify", "--source", "agent-distribution", "--base-ref", baseRef],
            scope.FullPath,
            requireSuccess: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("must preserve the base bundle version 3", result.StandardError, StringComparison.Ordinal);
    }

    private static void WriteBundle (TestDirectoryScope scope, int bundleVersion)
    {
        scope.WriteFile(
            "agent-distribution/bundle.json",
            $$"""
            {
              "schemaVersion": 4,
              "catalogId": "com.mackysoft.agent-distribution.tests",
              "bundleVersion": {{bundleVersion}}
            }
            """ + "\n");
    }

    private static string GetScriptPath ()
    {
        return Path.Combine(
            SkillTestData.GetRepositoryRoot(),
            "scripts",
            "resolve-bundle-operation-version.sh");
    }

    private static async Task<ProcessResult> RunProcessAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool requireSuccess = true)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = new ProcessResult(process.ExitCode, await standardOutput, await standardError);
        if (requireSuccess)
        {
            Assert.True(result.ExitCode == 0, $"{fileName} failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        return result;
    }

    private sealed record ProcessResult (int ExitCode, string StandardOutput, string StandardError);
}
