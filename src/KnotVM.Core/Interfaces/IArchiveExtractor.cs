using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per estrazione archivi .zip (Windows), .tar.gz/.tar.xz (Linux/macOS).
/// </summary>
public interface IArchiveExtractor
{
    /// <summary>
    /// Estrae archivio nella directory di destinazione.
    /// Windows: usa ZipFile.ExtractToDirectory
    /// Linux/macOS: usa tar command-line (.tar.gz, .tar.xz)
    /// </summary>
    /// <param name="archivePath">Path archivio (.zip o .tar.gz)</param>
    /// <param name="destinationDirectory">Directory destinazione (creata se non esiste)</param>
    /// <param name="preservePermissions">Se true, preserva permessi Unix (Linux/macOS only)</param>
    /// <param name="cancellationToken">Token cancellazione</param>
    /// <returns>Risultato estrazione</returns>
    Task<ExtractionResult> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        bool preservePermissions = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Verifica se un file è un archivio valido.
    /// </summary>
    /// <param name="archivePath">Path archivio</param>
    /// <returns>True se archivio valido e supportato</returns>
    bool IsValidArchive(string archivePath);

    /// <summary>
    /// Ottiene lista file in archivio senza estrarre.
    /// </summary>
    /// <param name="archivePath">Path archivio</param>
    /// <returns>Lista path relativi file in archivio</returns>
    Task<string[]> ListArchiveContentsAsync(string archivePath);
}
