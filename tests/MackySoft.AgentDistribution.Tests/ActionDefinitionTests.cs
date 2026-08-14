using System.Diagnostics;

namespace MackySoft.AgentDistribution.Tests;

public sealed class ActionDefinitionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAction_BuildsSourceIntoRunnerTemporaryOutput ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution", "action-verify");
        var sourceRoot = scope.CreateDirectory("agent-distribution");
        scope.WriteFile("agent-distribution/bundle.json", "{}\n");
        var temporaryRoot = scope.CreateDirectory("runner-temp");
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
            output=""
            while [[ "$#" -gt 0 ]]; do
              if [[ "$1" == "--output" ]]; then
                output="$2"
                break
              fi
              shift
            done
            mkdir -p "${output}"
            printf 'generated\n' > "${output}/bundle.json"
            """);
        File.SetUnixFileMode(
            fakeDotnet,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var environment = new Dictionary<string, string>
        {
            ["AGENT_DISTRIBUTION_SOURCE"] = "agent-distribution",
            ["DOTNET_LOG"] = dotnetLog,
            ["PATH"] = fakeBin + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty),
            ["RUNNER_TEMP"] = temporaryRoot,
        };

        await RunProcessAsync("bash", [GetActionScriptPath()], scope.FullPath, environment);

        var invocations = File.ReadAllLines(dotnetLog);
        Assert.Single(invocations);
        Assert.StartsWith("run --project ", invocations[0], StringComparison.Ordinal);
        Assert.Contains("/src/MackySoft.AgentDistribution.Cli/MackySoft.AgentDistribution.Cli.csproj --configuration Release -- build --source ", invocations[0], StringComparison.Ordinal);
        Assert.EndsWith($" --output {Path.Combine(temporaryRoot, "agent-distribution")}", invocations[0], StringComparison.Ordinal);
        Assert.Equal("generated\n", File.ReadAllText(Path.Combine(temporaryRoot, "agent-distribution", "bundle.json")));
        Assert.False(Directory.Exists(Path.Combine(sourceRoot, "generated")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifyAction_UsesSdkThatCanBuildTheBundledCli ()
    {
        var actionDefinition = File.ReadAllText(Path.Combine(GetActionDirectory(), "action.yaml"));

        Assert.Contains("dotnet-version: 10.0.x", actionDefinition, StringComparison.Ordinal);
    }

    private static string GetActionScriptPath ()
    {
        return Path.Combine(GetActionDirectory(), "build-source.sh");
    }

    private static string GetActionDirectory ()
    {
        return Path.Combine(
            SkillTestData.GetRepositoryRoot(),
            "actions",
            "verify");
    }

    private static async Task RunProcessAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
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
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await standardOutput;
        var error = await standardError;
        Assert.True(process.ExitCode == 0, $"{fileName} failed with exit code {process.ExitCode}: {error}");
    }
}
