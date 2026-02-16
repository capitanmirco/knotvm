using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Infrastructure.Services;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

/// <summary>
/// Test platform detection (OS, arch)
/// Requisito: Prompt 11/12 - test detection piattaforma
/// </summary>
public class PlatformServiceTests
{
    private readonly PlatformService _platformService;

    public PlatformServiceTests()
    {
        _platformService = new PlatformService();
    }

    [Fact]
    public void GetCurrentOs_ShouldReturnValidOS()
    {
        // Act
        var os = _platformService.GetCurrentOs();
        
        // Assert
        os.Should().BeOneOf(HostOs.Windows, HostOs.Linux, HostOs.MacOS);
    }

    [Fact]
    public void GetCurrentArch_ShouldReturnValidArch()
    {
        // Act
        var arch = _platformService.GetCurrentArch();
        
        // Assert
        arch.Should().BeOneOf(HostArch.X64, HostArch.Arm64, HostArch.X86);
    }

    [Fact]
    public void IsOsSupported_ShouldReturnTrueForCurrentPlatform()
    {
        // Act
        var isSupported = _platformService.IsOsSupported();
        
        // Assert
        // Se il test gira, l'OS corrente deve essere supportato
        isSupported.Should().BeTrue("OS corrente deve essere supportato");
    }

    [Fact]
    public void IsArchSupported_ShouldReturnTrueForCurrentPlatform()
    {
        // Act  
        var isSupported = _platformService.IsArchSupported();
        
        // Assert
        // Se il test gira, l'arch corrente deve essere supportata
        isSupported.Should().BeTrue("Architettura corrente deve essere supportata");
    }

    [Fact]
    public void GetDefaultShell_Windows_ShouldReturnPowerShell()
    {
        // Arrange
        var os = _platformService.GetCurrentOs();
        
        // Act
        var shell = _platformService.GetDefaultShell();
        
        // Assert
        if (os == HostOs.Windows)
        {
            shell.Should().Be(ShellType.PowerShell);
        }
        else if (os == HostOs.Linux)
        {
            shell.Should().Be(ShellType.Bash);
        }
        else if (os == HostOs.MacOS)
        {
            shell.Should().Be(ShellType.Zsh);
        }
    }

    [Fact]
    public void GetExecutableExtension_Windows_ShouldReturnExe()
    {
        // Arrange
        var os = _platformService.GetCurrentOs();
        
        // Act
        var extension = _platformService.GetExecutableExtension();
        
        // Assert
        if (os == HostOs.Windows)
        {
            extension.Should().Be(".exe");
        }
        else
        {
            extension.Should().BeEmpty("Unix non usa estensione eseguibili");
        }
    }

    [Fact]
    public void GetNodeArtifactIdentifier_ShouldReturnValidFormat()
    {
        // Act
        var identifier = _platformService.GetNodeArtifactIdentifier();
        
        // Assert
        identifier.Should().NotBeNullOrEmpty();
        identifier.Should().MatchRegex(@"^(win|linux|darwin)-(x64|arm64|x86)$",
            "Identifier deve seguire formato 'os-arch'");
    }

    [Fact]
    public void IsPathCaseSensitive_Windows_ShouldReturnFalse()
    {
        // Arrange
        var os = _platformService.GetCurrentOs();
        
        // Act
        var isCaseSensitive = _platformService.IsPathCaseSensitive();
        
        // Assert
        if (os == HostOs.Windows)
        {
            isCaseSensitive.Should().BeFalse("Windows usa path case-insensitive");
        }
        else
        {
            isCaseSensitive.Should().BeTrue("Unix usa path case-sensitive");
        }
    }
}
