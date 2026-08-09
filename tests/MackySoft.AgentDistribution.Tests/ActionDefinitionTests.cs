using System.Diagnostics;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests;

public sealed class ActionDefinitionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAction_WhenBundleRequiresSynchronization_DoesNotWrite ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "action-verify");
        scope.CreateDirectory("skills");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);

        var fakeBin = scope.CreateDirectory("fake-bin");
        var dotnetLog = scope.GetPath("dotnet.log");
        var fakeDotnet = scope.WriteFile(
            "fake-bin/dotnet",
            """
            #!/usr/bin/env bash
            set -euo pipefail
            printf '%s\n' "$*" >> "${DOTNET_LOG}"
            if [[ "$*" == "tool restore" ]]; then
              exit 0
            fi
            if [[ "$*" == *" --check" ]]; then
              exit 1
            fi
            mkdir -p "${ACTION_BUNDLE_ROOT}/generated"
            """);
        File.SetUnixFileMode(
            fakeDotnet,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var environment = CreateActionEnvironment(scope, fakeBin, dotnetLog);
        var exitCode = await RunProcessAsync(
            "bash",
            [GetRunnerPath(), "verify"],
            scope.FullPath,
            environment,
            requireSuccess: false);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "tool restore",
                "tool run agent-distribution -- build --root ./skills --check",
            ],
            File.ReadAllLines(dotnetLog));
        Assert.False(Directory.Exists(scope.GetPath("skills/generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SyncAction_WhenIgnoredGeneratedOutputRequiresSynchronization_ReportsChanged ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "action-sync-changed");
        var bundleRoot = scope.CreateDirectory("skills");
        scope.WriteFile("skills/bundle.json", "{}\n");
        scope.WriteFile(".gitignore", "skills/generated/\n");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);
        await RunProcessAsync("git", ["switch", "-c", "main"], scope.FullPath);
        var remotePath = scope.CreateDirectory("remote.git");
        await RunProcessAsync("git", ["init", "--bare", "--quiet", remotePath], scope.FullPath);
        await RunProcessAsync("git", ["remote", "add", "origin", remotePath], scope.FullPath);

        var fakeBin = scope.CreateDirectory("fake-bin");
        var dotnetLog = scope.GetPath("dotnet.log");
        var fakeDotnet = scope.WriteFile(
            "fake-bin/dotnet",
            """
            #!/usr/bin/env bash
            set -euo pipefail
            printf '%s\n' "$*" >> "${DOTNET_LOG}"
            if [[ "$*" == "tool restore" ]]; then
              exit 0
            fi
            if [[ "$*" == *" --check" ]]; then
              exit 1
            fi
            mkdir -p "${ACTION_BUNDLE_ROOT}/generated"
            printf 'generated\n' > "${ACTION_BUNDLE_ROOT}/generated/result.txt"
            """);
        File.SetUnixFileMode(
            fakeDotnet,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var outputPath = scope.GetPath("github-output.txt");
        var environment = CreateActionEnvironment(scope, fakeBin, dotnetLog, outputPath);
        environment["GITHUB_REF"] = "refs/heads/main";
        environment["AGENT_DISTRIBUTION_BUNDLE_VERSION"] = "2";

        await RunProcessAsync("bash", [GetRunnerPath(), "sync"], scope.FullPath, environment);

        Assert.Equal("changed=true\n", File.ReadAllText(outputPath).ReplaceLineEndings("\n"));
        Assert.Equal(
            [
                "tool restore",
                "tool run agent-distribution -- build --root ./skills --bundle-version 2 --check",
                "tool run agent-distribution -- build --root ./skills --bundle-version 2",
            ],
            File.ReadAllLines(dotnetLog));
        Assert.True(File.Exists(scope.GetPath("skills/generated/result.txt")));
        await RunProcessAsync("git", ["check-ignore", "--no-index", "--quiet", "skills/generated/result.txt"], scope.FullPath);
        Assert.Equal(
            "chore(agent-distribution): sync generated bundle\n",
            await RunProcessForOutputAsync(
                "git",
                ["--git-dir", remotePath, "log", "-1", "--format=%s", "refs/heads/main"],
                scope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SyncAction_WhenBundleIsCurrent_ReportsUnchanged ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "action-sync-current");
        scope.CreateDirectory("skills");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);

        var fakeBin = scope.CreateDirectory("fake-bin");
        var dotnetLog = scope.GetPath("dotnet.log");
        var fakeDotnet = scope.WriteFile(
            "fake-bin/dotnet",
            """
            #!/usr/bin/env bash
            set -euo pipefail
            printf '%s\n' "$*" >> "${DOTNET_LOG}"
            """);
        File.SetUnixFileMode(
            fakeDotnet,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var outputPath = scope.GetPath("github-output.txt");
        var environment = CreateActionEnvironment(scope, fakeBin, dotnetLog, outputPath);

        await RunProcessAsync("bash", [GetRunnerPath(), "sync"], scope.FullPath, environment);

        Assert.Equal("changed=false\n", File.ReadAllText(outputPath).ReplaceLineEndings("\n"));
        Assert.Equal(
            [
                "tool restore",
                "tool run agent-distribution -- build --root ./skills --check",
            ],
            File.ReadAllLines(dotnetLog));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SyncAction_WhenAnotherChangeIsStaged_DoesNotGenerateOrCommit ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "action-sync-staged");
        scope.CreateDirectory("skills");
        scope.WriteFile("notes.txt", "staged user change\n");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);
        await RunProcessAsync("git", ["add", "notes.txt"], scope.FullPath);

        var fakeBin = scope.CreateDirectory("fake-bin");
        var dotnetLog = scope.GetPath("dotnet.log");
        var fakeDotnet = scope.WriteFile(
            "fake-bin/dotnet",
            """
            #!/usr/bin/env bash
            set -euo pipefail
            printf '%s\n' "$*" >> "${DOTNET_LOG}"
            if [[ "$*" == "tool restore" ]]; then
              exit 0
            fi
            if [[ "$*" == *" --check" ]]; then
              exit 1
            fi
            mkdir -p "${ACTION_BUNDLE_ROOT}/generated"
            """);
        File.SetUnixFileMode(
            fakeDotnet,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var outputPath = scope.GetPath("github-output.txt");
        var environment = CreateActionEnvironment(scope, fakeBin, dotnetLog, outputPath);

        var exitCode = await RunProcessAsync(
            "bash",
            [GetRunnerPath(), "sync"],
            scope.FullPath,
            environment,
            requireSuccess: false);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "tool restore",
                "tool run agent-distribution -- build --root ./skills --check",
            ],
            File.ReadAllLines(dotnetLog));
        Assert.False(Directory.Exists(scope.GetPath("skills/generated")));
        Assert.Equal("notes.txt\n", await RunProcessForOutputAsync("git", ["diff", "--cached", "--name-only"], scope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SyncAction_WhenRunFromPullRequestRef_DoesNotGenerateOrPush ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "action-sync-pull-request");
        scope.CreateDirectory("skills");
        await RunProcessAsync("git", ["init", "--quiet"], scope.FullPath);

        var fakeBin = scope.CreateDirectory("fake-bin");
        var dotnetLog = scope.GetPath("dotnet.log");
        var fakeDotnet = scope.WriteFile(
            "fake-bin/dotnet",
            """
            #!/usr/bin/env bash
            set -euo pipefail
            printf '%s\n' "$*" >> "${DOTNET_LOG}"
            if [[ "$*" == "tool restore" ]]; then
              exit 0
            fi
            if [[ "$*" == *" --check" ]]; then
              exit 1
            fi
            mkdir -p "${ACTION_BUNDLE_ROOT}/generated"
            """);
        File.SetUnixFileMode(
            fakeDotnet,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var outputPath = scope.GetPath("github-output.txt");
        var environment = CreateActionEnvironment(scope, fakeBin, dotnetLog, outputPath);
        environment["GITHUB_REF"] = "refs/pull/17/merge";

        var exitCode = await RunProcessAsync(
            "bash",
            [GetRunnerPath(), "sync"],
            scope.FullPath,
            environment,
            requireSuccess: false);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "tool restore",
                "tool run agent-distribution -- build --root ./skills --check",
            ],
            File.ReadAllLines(dotnetLog));
        Assert.False(Directory.Exists(scope.GetPath("skills/generated")));
        Assert.False(File.Exists(outputPath));
    }

    private static string GetRunnerPath ()
    {
        return Path.Combine(
            SkillTestData.GetRepositoryRoot(),
            "actions",
            "run-bundle-operation.sh");
    }

    private static Dictionary<string, string> CreateActionEnvironment (
        TestDirectoryScope scope,
        string fakeBin,
        string dotnetLog,
        string? outputPath = null)
    {
        var environment = new Dictionary<string, string>
        {
            ["ACTION_BUNDLE_ROOT"] = scope.GetPath("skills"),
            ["AGENT_DISTRIBUTION_ROOT"] = "skills",
            ["DOTNET_LOG"] = dotnetLog,
            ["PATH"] = fakeBin + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty),
        };
        if (outputPath is not null)
        {
            environment["GITHUB_OUTPUT"] = outputPath;
        }

        return environment;
    }

    private static async Task<string> RunProcessForOutputAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await standardOutput;
        var error = await standardError;
        Assert.True(process.ExitCode == 0, $"{fileName} failed with exit code {process.ExitCode}: {error}");
        return output.ReplaceLineEndings("\n");
    }

    private static async Task<int> RunProcessAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null,
        bool requireSuccess = true)
    {
        var startInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await standardOutput;
        var error = await standardError;
        if (requireSuccess)
        {
            Assert.True(process.ExitCode == 0, $"{fileName} failed with exit code {process.ExitCode}: {error}");
        }

        return process.ExitCode;
    }

    private static ProcessStartInfo CreateProcessStartInfo (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
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

        return startInfo;
    }
}
