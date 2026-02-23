using System.Runtime.InteropServices;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio platform detection usando RuntimeInformation.
/// </summary>
public class PlatformService : IPlatformService
{
    private readonly HostOs _currentOs;
    private readonly HostArch _currentArch;

    public PlatformService()
    {
        _currentOs = DetectOs();
        _currentArch = DetectArch();
    }

    public HostOs GetCurrentOs() => _currentOs;

    public HostArch GetCurrentArch() => _currentArch;

    public ShellType GetDefaultShell()
    {
        return _currentOs switch
        {
            HostOs.Windows => ShellType.PowerShell,
            HostOs.Linux => ShellType.Bash,
            HostOs.MacOS => ShellType.Zsh,
            _ => throw new PlatformNotSupportedException($"OS {_currentOs} non supportato")
        };
    }

    public bool IsOsSupported()
    {
        return _currentOs is HostOs.Windows or HostOs.Linux or HostOs.MacOS;
    }

    public bool IsArchSupported()
    {
        // Windows: x64, arm64
        // Linux: x64, arm64
        // macOS: x64, arm64 (Apple Silicon)
        return (_currentOs, _currentArch) switch
        {
            (HostOs.Windows, HostArch.X64) => true,
            (HostOs.Windows, HostArch.Arm64) => true,
            (HostOs.Linux, HostArch.X64) => true,
            (HostOs.Linux, HostArch.Arm64) => true,
            (HostOs.MacOS, HostArch.X64) => true,
            (HostOs.MacOS, HostArch.Arm64) => true, // Apple Silicon M1/M2/M3/M4
            _ => false
        };
    }

    public string GetNodeArtifactIdentifier()
    {
        var osStr = _currentOs switch
        {
            HostOs.Windows => "win",
            HostOs.Linux => "linux",
            HostOs.MacOS => "darwin",
            _ => throw new PlatformNotSupportedException($"OS {_currentOs} non supportato")
        };

        var archStr = _currentArch switch
        {
            HostArch.X64 => "x64",
            HostArch.Arm64 => "arm64",
            HostArch.X86 => "x86",
            _ => throw new PlatformNotSupportedException($"Architettura {_currentArch} non supportata")
        };

        return $"{osStr}-{archStr}";
    }

    public string GetExecutableExtension()
    {
        return _currentOs == HostOs.Windows ? ".exe" : string.Empty;
    }

    public bool IsPathCaseSensitive()
    {
        // Windows: case-insensitive
        // Linux/macOS: case-sensitive (macOS può essere configurato diversamente, ma default è sensitive)
        return _currentOs != HostOs.Windows;
    }

    private static HostOs DetectOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return HostOs.Windows;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return HostOs.Linux;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return HostOs.MacOS;

        throw new PlatformNotSupportedException("Sistema operativo non supportato");
    }

    private static HostArch DetectArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => HostArch.X64,
            Architecture.Arm64 => HostArch.Arm64,
            Architecture.X86 => HostArch.X86,
            _ => throw new PlatformNotSupportedException(
                $"Architettura {RuntimeInformation.ProcessArchitecture} non supportata")
        };
    }
}
