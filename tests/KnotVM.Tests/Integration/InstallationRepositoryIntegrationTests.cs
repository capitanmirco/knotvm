using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Repositories;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Integration;

/// <summary>
/// Test di integrazione per InstallationsRepository + Configuration + FileSystem.
/// Requisito: Prompt 11/12 - validare persistenza settings e interazione componenti.
/// NOTA: Test semplificati senza esecuzione Node reale per massima portabilità.
/// </summary>
public class InstallationRepositoryIntegrationTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly Configuration _config;
    private readonly IInstallationsRepository _repository;
    private readonly Mock<IPlatformService> _platformServiceMock;
    private readonly Mock<IProcessRunner> _processRunnerMock;

    public InstallationRepositoryIntegrationTests()
    {
        // Setup ambiente test isolato usando KNOT_HOME
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"knotvm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempTestDir);

        // Imposta KNOT_HOME per test
        Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, _tempTestDir);
        
        _config = Configuration.Create();
        _config.EnsureDirectoriesExist();
        
        // Setup mock PlatformService per test cross-platform
        _platformServiceMock = new Mock<IPlatformService>();
        _platformServiceMock.Setup(x => x.GetCurrentOs())
            .Returns(OperatingSystem.IsWindows() ? HostOs.Windows : HostOs.Linux);

        _processRunnerMock = new Mock<IProcessRunner>();

        var fileSystemService = new FileSystemService(_platformServiceMock.Object);

        _repository = new LocalInstallationsRepository(
            _config,
            _platformServiceMock.Object,
            _processRunnerMock.Object,
            fileSystemService);
    }

    [Fact]
    public void Repository_GetAll_OnEmptyDirectory_ShouldReturnEmpty()
    {
        // Act
        var installations = _repository.GetAll();

        // Assert
        installations.Should().BeEmpty("nessuna installazione presente");
    }

    [Fact]
    public void Repository_SetActiveInstallation_ShouldPersistToSettingsFile()
    {
        // Arrange
        var alias = "test-alias";

        // Act
        _repository.SetActiveInstallation(alias);

        // Assert - verifica persistenza
        File.Exists(_config.SettingsFile).Should().BeTrue();
        var settingsContent = File.ReadAllText(_config.SettingsFile).Trim();
        settingsContent.Should().Be(alias);
    }

    [Fact]
    public void Repository_SetActiveInstallation_ShouldWriteUtf8WithoutBOM()
    {
        // Arrange
        var alias = "тест-алиас"; // alias con caratteri Unicode

        // Act
        _repository.SetActiveInstallation(alias);

        // Assert - verifica UTF-8 senza BOM
        var bytes = File.ReadAllBytes(_config.SettingsFile);
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        bytes.Take(3).Should().NotEqual(bom, "settings.txt deve essere UTF-8 senza BOM");
        
        // Verifica contenuto corretto
        var content = File.ReadAllText(_config.SettingsFile);
        content.Should().Be(alias);
    }

    [Fact]
    public void Configuration_Integration_ShouldCreateConsistentPaths()
    {
        // Assert - verifica struttura path consistente
        _config.VersionsPath.Should().StartWith(_config.AppDataPath);
        _config.BinPath.Should().StartWith(_config.AppDataPath);
        _config.CachePath.Should().StartWith(_config.AppDataPath);
        _config.TemplatesPath.Should().StartWith(_config.AppDataPath);
        _config.LocksPath.Should().StartWith(_config.AppDataPath);
        
        // Verifica path assoluti
        Path.IsPathRooted(_config.AppDataPath).Should().BeTrue();
        Path.IsPathRooted(_config.SettingsFile).Should().BeTrue();
    }

    [Fact]
    public void Repository_MultipleSetActive_ShouldOverwriteCorrectly()
    {
        // Arrange
        var alias1 = "first";
        var alias2 = "second";
        var alias3 = "third";

        // Act - cambia attivo più volte
        _repository.SetActiveInstallation(alias1);
        var content1 = File.ReadAllText(_config.SettingsFile);
        
        _repository.SetActiveInstallation(alias2);
        var content2 = File.ReadAllText(_config.SettingsFile);
        
        _repository.SetActiveInstallation(alias3);
        var content3 = File.ReadAllText(_config.SettingsFile);

        // Assert
        content1.Should().Be(alias1);
        content2.Should().Be(alias2);
        content3.Should().Be(alias3);
    }

    [Fact]
    public void Configuration_DirectoriesExist_ShouldBeCreatedSuccessfully()
    {
        // Act - già chiamato in constructor
        
        // Assert
        Directory.Exists(_config.AppDataPath).Should().BeTrue();
        Directory.Exists(_config.VersionsPath).Should().BeTrue();
        Directory.Exists(_config.BinPath).Should().BeTrue();
        Directory.Exists(_config.CachePath).Should().BeTrue();
        Directory.Exists(_config.TemplatesPath).Should().BeTrue();
        Directory.Exists(_config.LocksPath).Should().BeTrue();
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
