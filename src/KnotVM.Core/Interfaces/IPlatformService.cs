using KnotVM.Core.Enums;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per detection e informazioni sulla piattaforma corrente.
/// </summary>
public interface IPlatformService
{
    /// <summary>
    /// Ottiene il sistema operativo corrente.
    /// </summary>
    HostOs GetCurrentOs();

    /// <summary>
    /// Ottiene l'architettura processore corrente.
    /// </summary>
    HostArch GetCurrentArch();

    /// <summary>
    /// Ottiene la shell di default per l'OS corrente.
    /// </summary>
    ShellType GetDefaultShell();

    /// <summary>
    /// Verifica se l'OS corrente è supportato.
    /// </summary>
    /// <returns>True se supportato</returns>
    bool IsOsSupported();

    /// <summary>
    /// Verifica se l'architettura corrente è supportata per l'OS corrente.
    /// </summary>
    /// <returns>True se supportata</returns>
    bool IsArchSupported();

    /// <summary>
    /// Ottiene la stringa identificativa OS/arch per artifact Node.js.
    /// Es: "win-x64", "linux-x64", "darwin-arm64"
    /// </summary>
    string GetNodeArtifactIdentifier();

    /// <summary>
    /// Ottiene l'estensione file per eseguibili sull'OS corrente.
    /// Windows: ".exe", Linux/macOS: ""
    /// </summary>
    string GetExecutableExtension();

    /// <summary>
    /// Verifica se l'OS corrente usa path case-sensitive.
    /// Windows: false, Linux/macOS: true
    /// </summary>
    bool IsPathCaseSensitive();
}
