using FluentAssertions;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;

namespace KnotVM.Tests.Infrastructure;

public class ArchiveExtractorTests
{
    [Fact]
    public void IsValidArchive_ShouldAcceptTarXz()
    {
        var extractor = CreateExtractor(
            fileExists: _ => true,
            runAsync: (_, _) => Task.FromResult(new ProcessResult(0, string.Empty, string.Empty)));

        var isValid = extractor.IsValidArchive("node-v20.11.0-linux-x64.tar.xz");

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ListArchiveContentsAsync_TarXz_ShouldUseTarTJf()
    {
        var extractor = CreateExtractor(
            fileExists: _ => true,
            runAsync: (_, args) => Task.FromResult(new ProcessResult(0, "a\nb\n", string.Empty)));

        var result = await extractor.ListArchiveContentsAsync("archive.tar.xz");

        result.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public async Task ExtractAsync_TarXz_ShouldSucceedWhenTarReturnsZero()
    {
        var extractor = CreateExtractor(
            fileExists: _ => true,
            runAsync: (_, _) => Task.FromResult(new ProcessResult(0, string.Empty, string.Empty)));

        var result = await extractor.ExtractAsync("archive.tar.xz", "/tmp/extract");

        result.Success.Should().BeTrue();
    }

    private static ArchiveExtractor CreateExtractor(
        Func<string, bool> fileExists,
        Func<string, string, Task<ProcessResult>> runAsync)
    {
        var platformMock = new Mock<IPlatformService>();
        var fileSystemMock = new Mock<IFileSystemService>();
        var processRunnerMock = new Mock<IProcessRunner>();

        fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns((string path) => fileExists(path));
        fileSystemMock.Setup(x => x.EnsureDirectoryExists(It.IsAny<string>()));
        fileSystemMock.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Array.Empty<string>());
        fileSystemMock.Setup(x => x.GetDirectories(It.IsAny<string>()))
            .Returns(Array.Empty<string>());

        processRunnerMock.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<int>()))
            .Returns((string exe, string args, string? workingDirectory, Dictionary<string, string>? environment, int timeoutMilliseconds) => runAsync(exe, args));

        return new ArchiveExtractor(platformMock.Object, fileSystemMock.Object, processRunnerMock.Object);
    }
}
