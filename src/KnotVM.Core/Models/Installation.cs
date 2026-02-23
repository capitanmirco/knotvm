namespace KnotVM.Core.Models;

/// <summary>
/// Rappresenta una singola installazione di Node.js.
/// Corrisponde a una cartella in {KNOT_HOME}/versions/ (es: %APPDATA%\node-local\versions\ su Windows)
/// </summary>
/// <param name="Alias">Alias/nome dell'installazione (nome della cartella). Es: "production", "20.11.0"</param>
/// <param name="Version">Versione semver di Node.js (es: "20.11.0")</param>
/// <param name="Path">Percorso completo della directory di installazione</param>
/// <param name="Use">True se questa installazione è attualmente attiva (in settings.txt)</param>
public record Installation(string Alias, string Version, string Path, bool Use);