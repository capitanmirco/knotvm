using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class VersionFileDetectorServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly VersionFileDetectorService _sut;
    private const string TestDir = "/test/project";

    public VersionFileDetectorServiceTests()
    {
        _fileSystemMock = new Mock<IFileSystemService>();
        _sut = new VersionFileDetectorService(_fileSystemMock.Object);
    }

    // ─── DetectVersionAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task DetectVersionAsync_WithNvmrc_ReturnsVersion()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc"))).Returns("18.2.0\n");

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().Be("18.2.0");
    }

    [Fact]
    public async Task DetectVersionAsync_WithNodeVersion_ReturnsVersion()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(false);
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".node-version"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".node-version"))).Returns("20.11.0");

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task DetectVersionAsync_WithPackageJson_ReturnsEnginesNode()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(false);
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".node-version"))).Returns(false);
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, "package.json")))
            .Returns("{\"engines\":{\"node\":\">=18.0.0\"}}");

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().Be(">=18.0.0");
    }

    [Fact]
    public async Task DetectVersionAsync_NoFiles_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectVersionAsync_NvmrcAndNodeVersion_PrefersNvmrc()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc"))).Returns("18.0.0");
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".node-version"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".node-version"))).Returns("20.0.0");

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().Be("18.0.0");
        _fileSystemMock.Verify(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".node-version")), Times.Never);
    }

    [Fact]
    public async Task DetectVersionAsync_NvmrcAndPackageJson_PrefersNvmrc()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc"))).Returns("18.0.0");
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(true);

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().Be("18.0.0");
        _fileSystemMock.Verify(x => x.ReadAllTextSafe(Path.Combine(TestDir, "package.json")), Times.Never);
    }

    [Fact]
    public async Task DetectVersionAsync_NodeVersionAndPackageJson_PrefersNodeVersion()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(false);
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".node-version"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".node-version"))).Returns("20.0.0");
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(true);

        // Act
        var result = await _sut.DetectVersionAsync(TestDir);

        // Assert
        result.Should().Be("20.0.0");
        _fileSystemMock.Verify(x => x.ReadAllTextSafe(Path.Combine(TestDir, "package.json")), Times.Never);
    }

    // ─── ReadNvmrcAsync ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("18.2.0")]
    [InlineData("lts/hydrogen")]
    [InlineData("20")]
    [InlineData("lts/*")]
    [InlineData("lts")]
    public async Task ReadNvmrcAsync_VariousFormats_ReturnsTrimedValue(string version)
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.nvmrc")).Returns($"  {version}  \n");

        // Act
        var result = await _sut.ReadNvmrcAsync("/test/.nvmrc");

        // Assert
        result.Should().Be(version);
    }

    [Fact]
    public async Task ReadNvmrcAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.nvmrc")).Returns("   \n  ");

        // Act
        var result = await _sut.ReadNvmrcAsync("/test/.nvmrc");

        // Assert
        result.Should().BeNull();
    }

    // ─── ReadNodeVersionAsync ────────────────────────────────────────────────

    [Theory]
    [InlineData("20.11.0")]
    [InlineData("18.19.0")]
    [InlineData("lts")]
    public async Task ReadNodeVersionAsync_ValidContent_ReturnsTrimmedValue(string version)
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.node-version")).Returns($"{version}\r\n");

        // Act
        var result = await _sut.ReadNodeVersionAsync("/test/.node-version");

        // Assert
        result.Should().Be(version);
    }

    [Fact]
    public async Task ReadNodeVersionAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.node-version")).Returns("  ");

        // Act
        var result = await _sut.ReadNodeVersionAsync("/test/.node-version");

        // Assert
        result.Should().BeNull();
    }

    // ─── ReadPackageJsonEnginesAsync ─────────────────────────────────────────

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_WithEnginesNode_ReturnsValue()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"my-app\",\"engines\":{\"node\":\">=18.0.0\"}}");

        // Act
        var result = await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        result.Should().Be(">=18.0.0");
    }

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_WithCaretRange_ReturnsValue()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"engines\":{\"node\":\"^18.0.0\"}}");

        // Act
        var result = await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        result.Should().Be("^18.0.0");
    }

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_WithTildeRange_ReturnsValue()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"engines\":{\"node\":\"~18.0.0\"}}");

        // Act
        var result = await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        result.Should().Be("~18.0.0");
    }

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_WithoutEnginesField_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"my-app\",\"version\":\"1.0.0\"}");

        // Act
        var result = await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_WithEnginesButNoNode_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"engines\":{\"npm\":\">=8.0.0\"}}");

        // Act
        var result = await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json")).Returns("  ");

        // Act
        var result = await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        result.Should().BeNull();
    }

    // ─── DetectProjectContextAsync ───────────────────────────────────────────

    [Fact]
    public async Task DetectProjectContextAsync_WithPackageJson_PrioritizesPackageJsonVersion()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, "package.json")))
            .Returns("{\"name\":\"my-app\",\"engines\":{\"node\":\"20.11.0\"}}");
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc"))).Returns("18.0.0");

        // Act
        var result = await _sut.DetectProjectContextAsync(TestDir);

        // Assert
        result.Version.Should().Be("20.11.0");
        result.ProjectName.Should().Be("my-app");
        // .nvmrc should NOT override package.json version
        _fileSystemMock.Verify(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc")), Times.Never);
    }

    [Fact]
    public async Task DetectProjectContextAsync_PackageJsonNoEngines_FallsBackToNvmrc()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, "package.json")))
            .Returns("{\"name\":\"my-app\"}");
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc"))).Returns("18.0.0");

        // Act
        var result = await _sut.DetectProjectContextAsync(TestDir);

        // Assert
        result.Version.Should().Be("18.0.0");
        result.ProjectName.Should().Be("my-app");
    }

    [Fact]
    public async Task DetectProjectContextAsync_PackageJsonNoEngines_FallsBackToNodeVersionFile()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, "package.json")))
            .Returns("{\"name\":\"my-app\"}");
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(false);
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".node-version"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".node-version"))).Returns("20.0.0");

        // Act
        var result = await _sut.DetectProjectContextAsync(TestDir);

        // Assert
        result.Version.Should().Be("20.0.0");
        result.ProjectName.Should().Be("my-app");
    }

    [Fact]
    public async Task DetectProjectContextAsync_NoFiles_ReturnsNullContext()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.DetectProjectContextAsync(TestDir);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().BeNull();
        result.ProjectName.Should().BeNull();
    }

    [Fact]
    public async Task DetectProjectContextAsync_OnlyNvmrc_ReturnsVersionNullName()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, "package.json"))).Returns(false);
        _fileSystemMock.Setup(x => x.FileExists(Path.Combine(TestDir, ".nvmrc"))).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(Path.Combine(TestDir, ".nvmrc"))).Returns("20.0.0");

        // Act
        var result = await _sut.DetectProjectContextAsync(TestDir);

        // Assert
        result.Version.Should().Be("20.0.0");
        result.ProjectName.Should().BeNull();
    }

    // ─── ReadPackageJsonNameAsync ────────────────────────────────────────────

    [Fact]
    public async Task ReadPackageJsonNameAsync_SimpleName_ReturnsName()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"my-app\"}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result.Should().Be("my-app");
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_ScopedPackage_RemovesScope()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"@my-org/my-app\"}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result.Should().Be("my-app");
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_WithDots_ReplacesWithDash()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"my.app.name\"}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result.Should().Be("my-app-name");
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_WithSpaces_ReplacesWithDash()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"my app name\"}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result.Should().Be("my-app-name");
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_LongName_TruncatesToFiftyChars()
    {
        // Arrange
        var longName = new string('a', 60);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns($"{{\"name\":\"{longName}\"}}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result!.Length.Should().BeLessOrEqualTo(50);
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_NoNameField_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"version\":\"1.0.0\"}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_ScopedPackageWithDots_SanitizesCorrectly()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"@acme/my.project-v2\"}");

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert
        result.Should().Be("my-project-v2");
    }

    // ─── v-prefix stripping ──────────────────────────────────────────────────

    [Theory]
    [InlineData("v18.2.0", "18.2.0")]
    [InlineData("V20.11.0", "20.11.0")]
    [InlineData("v20", "20")]
    public async Task ReadNvmrcAsync_WithVPrefix_StripsPrefixForCompatibility(string rawContent, string expected)
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.nvmrc")).Returns(rawContent + "\n");

        // Act
        var result = await _sut.ReadNvmrcAsync("/test/.nvmrc");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ReadNvmrcAsync_LtsAlias_NotAffectedByVPrefixLogic()
    {
        // "lts/hydrogen" starts with 'l', not 'v', should be returned as-is
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.nvmrc")).Returns("lts/hydrogen\n");

        var result = await _sut.ReadNvmrcAsync("/test/.nvmrc");

        result.Should().Be("lts/hydrogen");
    }

    [Theory]
    [InlineData("v18.2.0", "18.2.0")]
    [InlineData("V20.11.0", "20.11.0")]
    public async Task ReadNodeVersionAsync_WithVPrefix_StripsPrefixForCompatibility(string rawContent, string expected)
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/.node-version")).Returns(rawContent);

        // Act
        var result = await _sut.ReadNodeVersionAsync("/test/.node-version");

        // Assert
        result.Should().Be(expected);
    }

    // ─── malformed JSON ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReadPackageJsonEnginesAsync_MalformedJson_ThrowsInvalidVersionFormat()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"engines\":{\"node\":\"18.0.0\""); // malformed – missing closing braces

        // Act
        var act = async () => await _sut.ReadPackageJsonEnginesAsync("/test/package.json");

        // Assert
        await act.Should().ThrowAsync<KnotVMException>()
            .Where(ex => ex.ErrorCode == KnotErrorCode.InvalidVersionFormat);
    }

    [Fact]
    public async Task ReadPackageJsonNameAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.ReadAllTextSafe("/test/package.json"))
            .Returns("{\"name\":\"my-app\""); // malformed – missing closing brace

        // Act
        var result = await _sut.ReadPackageJsonNameAsync("/test/package.json");

        // Assert – name read failure is non-critical, should not throw
        result.Should().BeNull();
    }
}