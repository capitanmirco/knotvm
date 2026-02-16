using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio risoluzione artifact Node.js per OS/arch.
/// </summary>
public class NodeArtifactResolver : INodeArtifactResolver
{
    private const string NodeDistBaseUrl = "https://nodejs.org/dist";

    private readonly IPlatformService _platform;

    public NodeArtifactResolver(IPlatformService platform)
    {
        _platform = platform;
    }

    public string GetArtifactDownloadUrl(string version, HostOs? os = null, HostArch? arch = null)
    {
        var targetOs = os ?? _platform.GetCurrentOs();
        var targetArch = arch ?? _platform.GetCurrentArch();
        var fileName = GetArtifactFileName(version, targetOs, targetArch);
        var versionTag = version.StartsWith('v') ? version : $"v{version}";

        return $"{NodeDistBaseUrl}/{versionTag}/{fileName}";
    }

    public string GetChecksumFileUrl(string version)
    {
        var versionTag = version.StartsWith('v') ? version : $"v{version}";
        return $"{NodeDistBaseUrl}/{versionTag}/SHASUMS256.txt";
    }

    public string GetArtifactFileName(string version, HostOs? os = null, HostArch? arch = null)
    {
        var targetOs = os ?? _platform.GetCurrentOs();
        var targetArch = arch ?? _platform.GetCurrentArch();
        var versionTag = version.StartsWith('v') ? version : $"v{version}";

        var osIdentifier = targetOs switch
        {
            HostOs.Windows => "win",
            HostOs.Linux => "linux",
            HostOs.MacOS => "darwin",
            _ => throw new PlatformNotSupportedException($"OS {targetOs} non supportato")
        };

        var archIdentifier = targetArch switch
        {
            HostArch.X64 => "x64",
            HostArch.Arm64 => "arm64",
            HostArch.X86 => "x86",
            _ => throw new PlatformNotSupportedException($"Arch {targetArch} non supportato")
        };

        var extension = GetArtifactExtension(targetOs);

        return $"node-{versionTag}-{osIdentifier}-{archIdentifier}{extension}";
    }

    public bool IsArtifactAvailable(RemoteVersion remoteVersion, HostOs? os = null, HostArch? arch = null)
    {
        var targetOs = os ?? _platform.GetCurrentOs();
        var targetArch = arch ?? _platform.GetCurrentArch();

        var osIdentifier = targetOs switch
        {
            HostOs.Windows => "win",
            HostOs.Linux => "linux",
            HostOs.MacOS => "darwin",
            _ => throw new PlatformNotSupportedException($"OS {targetOs} non supportato")
        };

        var archIdentifier = targetArch switch
        {
            HostArch.X64 => "x64",
            HostArch.Arm64 => "arm64",
            HostArch.X86 => "x86",
            _ => throw new PlatformNotSupportedException($"Arch {targetArch} non supportato")
        };

        var platformString = $"{osIdentifier}-{archIdentifier}";

        return remoteVersion.Files.Any(f => 
            f.Equals(platformString, StringComparison.OrdinalIgnoreCase)
        );
    }

    public string GetArtifactExtension(HostOs? os = null)
    {
        var targetOs = os ?? _platform.GetCurrentOs();

        return targetOs switch
        {
            HostOs.Windows => ".zip",
            HostOs.Linux => ".tar.xz",
            HostOs.MacOS => ".tar.gz",
            _ => throw new PlatformNotSupportedException($"OS {targetOs} non supportato")
        };
    }
}
