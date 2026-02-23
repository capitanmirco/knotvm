using KnotVM.Core.Enums;
using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per risoluzione URL artifact Node.js per OS/arch.
/// </summary>
public interface INodeArtifactResolver
{
    /// <summary>
    /// Ottiene URL download artifact per versione e piattaforma corrente.
    /// Es: https://nodejs.org/dist/v20.11.0/node-v20.11.0-win-x64.zip
    /// </summary>
    /// <param name="version">Versione Node.js (es: "20.11.0")</param>
    /// <param name="os">Sistema operativo target (null = corrente)</param>
    /// <param name="arch">Architettura target (null = corrente)</param>
    /// <returns>URL download artifact</returns>
    string GetArtifactDownloadUrl(string version, HostOs? os = null, HostArch? arch = null);

    /// <summary>
    /// Ottiene URL file SHASUMS256.txt per verifica checksum.
    /// Es: https://nodejs.org/dist/v20.11.0/SHASUMS256.txt
    /// </summary>
    /// <param name="version">Versione Node.js</param>
    /// <returns>URL file checksum</returns>
    string GetChecksumFileUrl(string version);

    /// <summary>
    /// Ottiene nome file artifact per versione e piattaforma.
    /// Es: "node-v20.11.0-win-x64.zip"
    /// </summary>
    /// <param name="version">Versione Node.js</param>
    /// <param name="os">Sistema operativo (null = corrente)</param>
    /// <param name="arch">Architettura (null = corrente)</param>
    /// <returns>Nome file artifact</returns>
    string GetArtifactFileName(string version, HostOs? os = null, HostArch? arch = null);

    /// <summary>
    /// Verifica se artifact è disponibile per OS/arch specificati.
    /// </summary>
    /// <param name="remoteVersion">Versione remota con lista files</param>
    /// <param name="os">Sistema operativo (null = corrente)</param>
    /// <param name="arch">Architettura (null = corrente)</param>
    /// <returns>True se disponibile</returns>
    bool IsArtifactAvailable(RemoteVersion remoteVersion, HostOs? os = null, HostArch? arch = null);

    /// <summary>
    /// Ottiene estensione archivio per OS.
    /// Windows: ".zip", Linux/macOS: ".tar.gz"
    /// </summary>
    string GetArtifactExtension(HostOs? os = null);
}
