namespace KnotVM.Core.Common;

/// <summary>
/// Utility centralizzata per naming dei proxy in modalità isolated.
/// </summary>
public static class ProxyNaming
{
    /// <summary>
    /// Prefisso usato per tutti i proxy isolated mode.
    /// </summary>
    public const string IsolatedPrefix = "nlocal-";

    /// <summary>
    /// Costruisce il nome completo del proxy per un comando.
    /// Esempio: node -> nlocal-node.
    /// </summary>
    public static string BuildIsolatedProxyName(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return $"{IsolatedPrefix}{commandName}";
    }
}
