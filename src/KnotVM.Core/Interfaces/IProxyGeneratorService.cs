namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per generazione proxy cross-platform da template.
/// </summary>
public interface IProxyGeneratorService
{
    /// <summary>
    /// Genera proxy per un comando generico (node, etc).
    /// Proxy generato con prefisso 'nlocal-' (es. nlocal-node).
    /// </summary>
    /// <param name="commandName">Nome comando</param>
    /// <param name="commandExe">Path relativo eseguibile (es. "node.exe" su Windows, "bin/node" su Unix)</param>
    void GenerateGenericProxy(string commandName, string commandExe);

    /// <summary>
    /// Genera proxy per package manager (npm, yarn, pnpm, etc).
    /// Proxy generato con prefisso 'nlocal-' (es. nlocal-npm).
    /// </summary>
    /// <param name="packageManager">Nome package manager</param>
    /// <param name="scriptPath">Path relativo allo script PM</param>
    void GeneratePackageManagerProxy(string packageManager, string scriptPath);

    /// <summary>
    /// Genera shim C# per node su Windows (override mode).
    /// </summary>
    void GenerateNodeShim();

    /// <summary>
    /// Rimuove un proxy esistente.
    /// </summary>
    /// <param name="proxyName">Nome del proxy da rimuovere</param>
    void RemoveProxy(string proxyName);

    /// <summary>
    /// Rimuove tutti i proxy esistenti.
    /// </summary>
    void RemoveAllProxies();
}
