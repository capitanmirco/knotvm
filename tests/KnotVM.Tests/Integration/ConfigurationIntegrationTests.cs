using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Xunit;

namespace KnotVM.Tests.Integration;

/// <summary>
/// Test di integrazione per Configuration + PathService + FileSystemService.
/// Requisito: Prompt 11/12 - validare interazione tra servizi multipli.
/// </summary>
public class ConfigurationIntegrationTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly IFileSystemService _fileSystem;
    private readonly Configuration _config;

    public ConfigurationIntegrationTests()
    {
        // Setup ambiente test isolato usando KNOT_HOME
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"knotvm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempTestDir);
        
        // Imposta KNOT_HOME per usare temp directory
        Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, _tempTestDir);
        
        // Crea servizi con dipendenze reali
        var platformService = new PlatformService();
        _fileSystem = new FileSystemService(platformService);
        _config = Configuration.Create();
    }

    [Fact]
    public void Configuration_Creation_ShouldCreateAllRequiredDirectories()
    {
        // Arrange & Act
        _config.EnsureDirectoriesExist();

        // Assert
        Directory.Exists(_config.VersionsPath).Should().BeTrue();
        Directory.Exists(_config.BinPath).Should().BeTrue();
        Directory.Exists(_config.CachePath).Should().BeTrue();
        Directory.Exists(_config.TemplatesPath).Should().BeTrue();
        Directory.Exists(_config.LocksPath).Should().BeTrue();
    }

    [Fact]
    public void Configuration_WithKnotHome_ShouldUseEnvironmentVariable()
    {
        // Assert - KNOT_HOME influenza il path base
        _config.AppDataPath.Should().StartWith(_tempTestDir);
    }

    [Fact]
    public void Configuration_ShouldHaveCorrectSubdirectories()
    {
        // Assert
        _config.VersionsPath.Should().Be(Path.Combine(_config.AppDataPath, "versions"));
        _config.BinPath.Should().Be(Path.Combine(_config.AppDataPath, "bin"));
        _config.CachePath.Should().Be(Path.Combine(_config.AppDataPath, "cache"));
        _config.TemplatesPath.Should().Be(Path.Combine(_config.AppDataPath, "templates"));
        _config.LocksPath.Should().Be(Path.Combine(_config.AppDataPath, "locks"));
    }

    [Fact]
    public void FileSystem_WriteAndReadUtf8_ShouldPreserveEncoding()
    {
        // Arrange
        _fileSystem.EnsureDirectoryExists(_config.AppDataPath);
        var testFile = Path.Combine(_config.AppDataPath, "utf8-test.txt");
        var content = "Test encoding: тест, مرحبا, 测试, 🚀";

        // Act
        _fileSystem.WriteAllTextSafe(testFile, content);
        var readContent = _fileSystem.ReadAllTextSafe(testFile);

        // Assert
        readContent.Should().Be(content);
        
        // Verifica UTF-8 senza BOM
        var bytes = File.ReadAllBytes(testFile);
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        bytes.Take(3).Should().NotEqual(bom, "file deve essere UTF-8 senza BOM");
    }

    [Fact]
    public void FileSystem_DirectoryOperations_ShouldBeIdempotent()
    {
        // Arrange
        var testDir = Path.Combine(_config.AppDataPath, "idempotent-test");

        // Act - chiama più volte EnsureDirectoryExists
        _fileSystem.EnsureDirectoryExists(testDir);
        _fileSystem.EnsureDirectoryExists(testDir);
        _fileSystem.EnsureDirectoryExists(testDir);

        // Assert
        Directory.Exists(testDir).Should().BeTrue();
    }

    public void Dispose()
    {
        // Cleanup env var e filesystem
        Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, null);
        
        if (Directory.Exists(_tempTestDir))
        {
            Directory.Delete(_tempTestDir, recursive: true);
        }
    }
}
