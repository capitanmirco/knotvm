using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using Xunit;

namespace KnotVM.Tests.Core;

/// <summary>
/// Test Configuration path handling
/// Requisito: Prompt 11/12 - test anti-regressione path/encoding
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void Create_ShouldReturnValidConfiguration()
    {
        // Act
        var config = Configuration.Create();
        
        // Assert
        config.Should().NotBeNull();
        config.AppDataPath.Should().NotBeNullOrWhiteSpace();
        config.VersionsPath.Should().NotBeNullOrWhiteSpace();
        config.BinPath.Should().NotBeNullOrWhiteSpace();
        config.CachePath.Should().NotBeNullOrWhiteSpace();
        config.SettingsFile.Should().NotBeNullOrWhiteSpace();
        config.TemplatesPath.Should().NotBeNullOrWhiteSpace();
        config.LocksPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Configuration_PathsShouldBeSubdirectories()
    {
        // Arrange
        var config = Configuration.Create();
        
        // Assert
        config.VersionsPath.Should().StartWith(config.AppDataPath);
        config.BinPath.Should().StartWith(config.AppDataPath);
        config.CachePath.Should().StartWith(config.AppDataPath);
        config.TemplatesPath.Should().StartWith(config.AppDataPath);
        config.LocksPath.Should().StartWith(config.AppDataPath);
        Path.GetDirectoryName(config.SettingsFile).Should().Be(config.AppDataPath);
    }

    [Fact]
    public void Configuration_ShouldHandleKnotHomeEnvVar()
    {
        // Arrange
        var customPath = Path.Combine(Path.GetTempPath(), "knot-test-custom");
        var originalValue = Environment.GetEnvironmentVariable(Configuration.KnotHomeEnvVar);
        
        try
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, customPath);
            
            // Act
            var config = Configuration.Create();
            
            // Assert
            config.AppDataPath.Should().Be(customPath);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, originalValue);
        }
    }

    [Theory]
    [InlineData("C:\\Users\\Test User\\AppData\\Roaming\\node-local")]
    [InlineData("C:\\Program Files (x86)\\KnotVM")]
    [InlineData("/home/test user/.local/share/node-local")]
    [InlineData("/Users/test user/Library/Application Support/node-local")]
    public void Configuration_ShouldHandlePathsWithSpaces(string pathWithSpaces)
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable(Configuration.KnotHomeEnvVar);
        
        try
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, pathWithSpaces);
            
            // Act
            var config = Configuration.Create();
            
            // Assert
            config.AppDataPath.Should().Be(pathWithSpaces);
            config.VersionsPath.Should().StartWith(pathWithSpaces);
            config.BinPath.Should().StartWith(pathWithSpaces);
            
            // Verifica che i path siano validi anche con spazi
            Path.IsPathRooted(config.AppDataPath).Should().BeTrue();
            Path.IsPathRooted(config.VersionsPath).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, originalValue);
        }
    }

    [Fact]
    public void Configuration_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var unicodePath = Path.Combine(Path.GetTempPath(), "knot-тест-مэмب-测试");
        var originalValue = Environment.GetEnvironmentVariable(Configuration.KnotHomeEnvVar);
        
        try
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, unicodePath);
            
            // Act
            var config = Configuration.Create();
            
            // Assert
            config.AppDataPath.Should().Be(unicodePath);
            
            // Verifica encoding UTF-8
            var bytes = System.Text.Encoding.UTF8.GetBytes(config.AppDataPath);
            var decoded = System.Text.Encoding.UTF8.GetString(bytes);
            decoded.Should().Be(unicodePath, "Path con Unicode deve preservare encoding");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, originalValue);
        }
    }

    [Fact]
    public void EnsureDirectoriesExist_ShouldCreateDirectories()
    {
        // Arrange
        var tempBase = Path.Combine(Path.GetTempPath(), $"knot-test-{Guid.NewGuid()}");
        var originalValue = Environment.GetEnvironmentVariable(Configuration.KnotHomeEnvVar);
        
        try
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, tempBase);
            var config = Configuration.Create();
            
            // Act
            config.EnsureDirectoriesExist();
            
            // Assert
            Directory.Exists(config.AppDataPath).Should().BeTrue();
            Directory.Exists(config.VersionsPath).Should().BeTrue();
            Directory.Exists(config.BinPath).Should().BeTrue();
            Directory.Exists(config.CachePath).Should().BeTrue();
            Directory.Exists(config.TemplatesPath).Should().BeTrue();
            Directory.Exists(config.LocksPath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, originalValue);
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void DefaultDirectoryName_ShouldBeNodeLocal()
    {
        // Assert
        Configuration.DefaultDirectoryName.Should().Be("node-local");
    }

    [Fact]
    public void Configuration_SubpathsShouldUseCorrectNames()
    {
        // Arrange
        var config = Configuration.Create();
        
        // Assert
        Path.GetFileName(config.VersionsPath).Should().Be("versions");
        Path.GetFileName(config.BinPath).Should().Be("bin");
        Path.GetFileName(config.CachePath).Should().Be("cache");
        Path.GetFileName(config.TemplatesPath).Should().Be("templates");
        Path.GetFileName(config.LocksPath).Should().Be("locks");
        Path.GetFileName(config.SettingsFile).Should().Be("settings.txt");
    }

    [Fact]
    public void Configuration_ShouldHandleRelativePaths()
    {
        // Arrange
        var relativePath = "./relative-knot-test";
        var originalValue = Environment.GetEnvironmentVariable(Configuration.KnotHomeEnvVar);
        
        try
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, relativePath);
            
            // Act
            var config = Configuration.Create();
            
            // Assert
            // Path dovrebbe essere il relativo
            config.AppDataPath.Should().Be(relativePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Configuration.KnotHomeEnvVar, originalValue);
        }
    }

    [Fact]
    public void EnsureDirectoriesExist_ShouldWrapInvalidPathError()
    {
        // Arrange
        var invalidPath = "\0invalid-path";
        var separator = Path.DirectorySeparatorChar;
        var config = new Configuration(
            AppDataPath: invalidPath,
            VersionsPath: $"{invalidPath}{separator}versions",
            BinPath: $"{invalidPath}{separator}bin",
            CachePath: $"{invalidPath}{separator}cache",
            SettingsFile: $"{invalidPath}{separator}settings.txt",
            TemplatesPath: $"{invalidPath}{separator}templates",
            LocksPath: $"{invalidPath}{separator}locks"
        );

        // Act
        Action act = () => config.EnsureDirectoriesExist();

        // Assert
        var ex = act.Should().Throw<KnotVMException>().Which;
        ex.ErrorCode.Should().Be(KnotErrorCode.PathCreationFailed);
    }
}
