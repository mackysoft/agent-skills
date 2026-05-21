using System.Globalization;
using System.IO.Compression;
using System.Text;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class SkillExportServiceTests
{
    private static readonly DateTime ZipEntryTimestamp = new(1980, 1, 1, 0, 0, 0);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] Utf8Preamble = Encoding.UTF8.GetPreamble();

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync([package], OpenAiSkillHostAdapter.HostKey, scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var outputPath = scope.GetPath("release.zip");
        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync([package], OpenAiSkillHostAdapter.HostKey, outputPath, SkillExportFormat.Zip, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(File.Exists(outputPath));
        Assert.Empty(Directory.EnumerateFiles(scope.FullPath, ".release.zip.*.tmp"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsExistingSkillDirectoryThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var outputScope = TestDirectories.CreateTempScope("agent-skills-skills", "export-skill-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "export-skill-symlink-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var symlinkPath = Path.Combine(outputScope.FullPath, packages[0].Manifest.SkillName);
        try
        {
            Directory.CreateSymbolicLink(symlinkPath, outsideScope.FullPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, outputScope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_CleansTemporaryFile_WhenCommitFails ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-cleanup");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateExportService();
        var destinationDirectory = scope.CreateDirectory("release.zip");

        var result = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, destinationDirectory, SkillExportFormat.Zip, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.Empty(Directory.EnumerateFiles(scope.FullPath, $".{Path.GetFileName(destinationDirectory)}.*.tmp"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_WritesDeterministicEntries ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-deterministic");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateExportService();

        foreach (var adapter in GetSupportedAdapters())
        {
            var host = adapter.Descriptor.HostKey;
            var firstZip = scope.GetPath($"{host}-first.zip");
            var secondZip = scope.GetPath($"{host}-second.zip");

            var first = await service.ExportAsync(packages, host, firstZip, SkillExportFormat.Zip, CancellationToken.None);
            var second = await service.ExportAsync(packages, host, secondZip, SkillExportFormat.Zip, CancellationToken.None);

            Assert.True(first.IsSuccess, first.Failure?.Message);
            Assert.True(second.IsSuccess, second.Failure?.Message);
            Assert.Equal(await File.ReadAllBytesAsync(firstZip), await File.ReadAllBytesAsync(secondZip));
            AssertFileMapEqual(CreateExpectedExportMap(packages, host), ReadZipExport(firstZip));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_UsesOrdinalEntryOrderingForCultureSensitivePaths ()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-ordinal-culture");
            var package = SkillTestData.CreateOrdinalSensitivePackage();
            var service = SkillTestData.CreateExportService();

            foreach (var adapter in GetSupportedAdapters())
            {
                var zipPath = scope.GetPath($"{adapter.Descriptor.HostKey}.zip");

                var result = await service.ExportAsync([package], adapter.Descriptor.HostKey, zipPath, SkillExportFormat.Zip, CancellationToken.None);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                var entryNames = GetZipEntryNames(zipPath);
                var ordinalNames = entryNames.Order(StringComparer.Ordinal).ToArray();
                Assert.Equal(ordinalNames, entryNames);
                Assert.NotEqual(ordinalNames, entryNames.Order(StringComparer.CurrentCulture).ToArray());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_DirectoryFormat_WritesDeterministicFileSetMatchingZipFormat ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-directory-zip-equivalence");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateExportService();

        foreach (var adapter in GetSupportedAdapters())
        {
            var host = adapter.Descriptor.HostKey;
            var firstDirectoryRoot = scope.GetPath($"{host}-directory-a");
            var secondDirectoryRoot = scope.GetPath($"{host}-directory-b");
            var zipPath = scope.GetPath($"{host}.zip");
            var expectedMap = CreateExpectedExportMap(packages, host);

            var firstDirectory = await service.ExportAsync(packages, host, firstDirectoryRoot, SkillExportFormat.Directory, CancellationToken.None);
            var secondDirectory = await service.ExportAsync(packages, host, secondDirectoryRoot, SkillExportFormat.Directory, CancellationToken.None);
            var zip = await service.ExportAsync(packages, host, zipPath, SkillExportFormat.Zip, CancellationToken.None);

            Assert.True(firstDirectory.IsSuccess, firstDirectory.Failure?.Message);
            Assert.True(secondDirectory.IsSuccess, secondDirectory.Failure?.Message);
            Assert.True(zip.IsSuccess, zip.Failure?.Message);
            AssertFileMapEqual(expectedMap, await ReadDirectoryExportAsync(firstDirectoryRoot, CancellationToken.None));
            AssertByteMapEqual(
                await ReadDirectoryExportBytesAsync(firstDirectoryRoot, CancellationToken.None),
                await ReadDirectoryExportBytesAsync(secondDirectoryRoot, CancellationToken.None));
            AssertFileMapEqual(expectedMap, ReadZipExport(zipPath));
        }
    }

    private static IReadOnlyList<ISkillHostAdapter> GetSupportedAdapters ()
    {
        return SkillTestData.CreateDefaultHostAdapterSet().Adapters;
    }

    private static IReadOnlyDictionary<string, string> CreateExpectedExportMap (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host)
    {
        var service = SkillTestData.CreateMaterializationService();
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            var result = service.Materialize(package, host);
            Assert.True(result.IsSuccess, result.Failure?.Message);

            foreach (var file in result.Value!.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
            {
                files.Add($"{package.Manifest.SkillName}/{file.RelativePath}", file.Content);
            }
        }

        return files;
    }

    private static async ValueTask<IReadOnlyDictionary<string, string>> ReadDirectoryExportAsync (
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(outputRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Assert.DoesNotContain("\r", content, StringComparison.Ordinal);
            files.Add(relativePath, content);
        }

        return files;
    }

    private static async ValueTask<IReadOnlyDictionary<string, byte[]>> ReadDirectoryExportBytesAsync (
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(outputRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Assert.False(HasUtf8Preamble(bytes));
            Assert.DoesNotContain((byte)'\r', bytes);
            _ = StrictUtf8.GetString(bytes);
            files.Add(relativePath, bytes);
        }

        return files;
    }

    private static IReadOnlyDictionary<string, string> ReadZipExport (string zipPath)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        using var archive = ZipFile.OpenRead(zipPath);
        var entryNames = GetZipEntryNames(archive);
        Assert.Equal(entryNames.Order(StringComparer.Ordinal).ToArray(), entryNames);
        Assert.DoesNotContain(entryNames, static entryName => entryName.EndsWith("/", StringComparison.Ordinal));

        foreach (var entry in archive.Entries)
        {
            Assert.Equal(ZipEntryTimestamp, entry.LastWriteTime.DateTime);

            using var entryStream = entry.Open();
            using var memoryStream = new MemoryStream();
            entryStream.CopyTo(memoryStream);

            var bytes = memoryStream.ToArray();
            Assert.False(HasUtf8Preamble(bytes));
            Assert.DoesNotContain((byte)'\r', bytes);

            var content = StrictUtf8.GetString(bytes);
            Assert.DoesNotContain("\r", content, StringComparison.Ordinal);
            files.Add(entry.FullName, content);
        }

        return files;
    }

    private static string[] GetZipEntryNames (string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return GetZipEntryNames(archive);
    }

    private static string[] GetZipEntryNames (ZipArchive archive)
    {
        return archive.Entries.Select(static entry => entry.FullName).ToArray();
    }

    private static bool HasUtf8Preamble (byte[] bytes)
    {
        return Utf8Preamble.Length > 0
            && bytes.Length >= Utf8Preamble.Length
            && bytes.AsSpan(0, Utf8Preamble.Length).SequenceEqual(Utf8Preamble);
    }

    private static void AssertFileMapEqual (
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        var expectedPaths = expected.Keys.Order(StringComparer.Ordinal).ToArray();
        var actualPaths = actual.Keys.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedPaths, actualPaths);

        foreach (var path in expectedPaths)
        {
            Assert.Equal(expected[path], actual[path]);
        }
    }

    private static void AssertByteMapEqual (
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        var expectedPaths = expected.Keys.Order(StringComparer.Ordinal).ToArray();
        var actualPaths = actual.Keys.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedPaths, actualPaths);

        foreach (var path in expectedPaths)
        {
            Assert.Equal(expected[path], actual[path]);
        }
    }
}
