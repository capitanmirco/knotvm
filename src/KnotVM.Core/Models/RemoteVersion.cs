namespace KnotVM.Core.Models;

/// <summary>
/// Rappresenta una versione di Node.js disponibile per il download da repository remoto.
/// </summary>
/// <param name="Version">Versione semver (es: "20.11.0")</param>
/// <param name="Lts">Codename LTS (es: "Iron") o null se non è LTS</param>
/// <param name="Date">Data di rilascio (formato ISO)</param>
/// <param name="Files">Array di file artifact disponibili (es: ["win-x64", "linux-x64", ...])</param>
public record RemoteVersion(
    string Version,
    string? Lts,
    string Date,
    string[] Files
);
