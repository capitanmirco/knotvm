using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Rileva e legge versioni Node.js da file di configurazione progetto.
/// </summary>
public interface IVersionFileDetector
{
    /// <summary>
    /// Rileva la versione Node.js dalla directory specificata.
    /// L'ordine di precedenza è: .nvmrc > .node-version > package.json engines.node.
    /// </summary>
    /// <param name="directory">Directory da scansionare</param>
    /// <returns>Versione rilevata, o null se nessun file trovato</returns>
    Task<string?> DetectVersionAsync(string directory);

    /// <summary>
    /// Rileva il contesto di progetto dalla directory specificata con priorità a package.json.
    /// Ordine di precedenza versione: package.json engines.node > .nvmrc > .node-version.
    /// Il nome del progetto viene sempre letto da package.json (campo "name").
    /// </summary>
    /// <param name="directory">Directory da scansionare</param>
    /// <returns>Contesto progetto con versione e nome, mai null (i campi interni possono essere null)</returns>
    Task<ProjectContext> DetectProjectContextAsync(string directory);

    /// <summary>
    /// Legge il nome del progetto dal campo <c>name</c> di un file package.json
    /// e lo normalizza per poter essere usato come alias (rimuove scope @org/, sostituisce caratteri non validi).
    /// </summary>
    /// <param name="filePath">Path al file package.json</param>
    /// <returns>Nome progetto normalizzato, o null se non presente</returns>
    Task<string?> ReadPackageJsonNameAsync(string filePath);

    /// <summary>
    /// Legge la versione node da un file .nvmrc specifico.
    /// </summary>
    /// <param name="filePath">Path al file .nvmrc</param>
    /// <returns>Versione letta (trimmed), o null se il file è vuoto</returns>
    Task<string?> ReadNvmrcAsync(string filePath);

    /// <summary>
    /// Legge la versione node da un file .node-version specifico.
    /// </summary>
    /// <param name="filePath">Path al file .node-version</param>
    /// <returns>Versione letta (trimmed), o null se il file è vuoto</returns>
    Task<string?> ReadNodeVersionAsync(string filePath);

    /// <summary>
    /// Legge la versione node dal campo <c>engines.node</c> di un file package.json.
    /// </summary>
    /// <param name="filePath">Path al file package.json</param>
    /// <returns>Valore di engines.node, o null se non presente</returns>
    Task<string?> ReadPackageJsonEnginesAsync(string filePath);
}
