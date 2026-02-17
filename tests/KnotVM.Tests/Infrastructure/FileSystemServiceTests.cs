using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class FileSystemServiceTests
{
    private readonly Mock<IPlatformService> _platformMock;
    private readonly FileSystemService _sut;
    private readonly string _testDir;

    public FileSystemServiceTests()
    {
        _platformMock = new Mock<IPlatformService>();
        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);

        _sut = new FileSystemService(_platformMock.Object);
        _testDir = Path.Combine(Path.GetTempPath(), $"knotvm-test-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_CreatesDirectory()
    {
        // Arrange
        var testPath = Path.Combine(_testDir, "test-directory");

        // Act
        _sut.EnsureDirectoryExists(testPath);

        // Assert
        Directory.Exists(testPath).Should().BeTrue();

        // Cleanup
        Directory.Delete(testPath);
    }

    [Fact]
    public void DirectoryExists_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);

        // Act
        var result = _sut.DirectoryExists(_testDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_WithNonExistingDirectory_ReturnsFalse()
    {
        // Act
        var result = _sut.DirectoryExists("/non/existing/directory");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);
        var testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test");

        // Act
        var result = _sut.FileExists(testFile);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ReturnsFalse()
    {
        // Act
        var result = _sut.FileExists("/non/existing/file.txt");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void WriteAllTextSafe_CreatesFileWithContent()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);
        var testFile = Path.Combine(_testDir, "test.txt");

        // Act
        _sut.WriteAllTextSafe(testFile, "test content");

        // Assert
        File.Exists(testFile).Should().BeTrue();
        File.ReadAllText(testFile).Should().Be("test content");
    }

    [Fact]
    public void ReadAllTextSafe_ReadsFileContent()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);
        var testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        var result = _sut.ReadAllTextSafe(testFile);

        // Assert
        result.Should().Be("test content");
    }

    [Fact]
    public void DeleteFileIfExists_RemovesExistingFile()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);
        var testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test");

        // Act
        _sut.DeleteFileIfExists(testFile);

        // Assert
        File.Exists(testFile).Should().BeFalse();
    }

    [Fact]
    public void DeleteFileIfExists_WithNonExistingFile_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _sut.DeleteFileIfExists("/non/existing/file.txt");
        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteDirectoryIfExists_RemovesDirectory()
    {
        // Arrange
        var testPath = Path.Combine(_testDir, "test-dir");
        Directory.CreateDirectory(testPath);

        // Act
        _sut.DeleteDirectoryIfExists(testPath, recursive: true);

        // Assert
        Directory.Exists(testPath).Should().BeFalse();
    }

    [Fact]
    public void GetFileLastWriteTime_ReturnsCorrectTime()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);
        var testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test");
        var expectedTime = File.GetLastWriteTime(testFile);

        // Act
        var result = _sut.GetFileLastWriteTime(testFile);

        // Assert
        result.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
    }
}
