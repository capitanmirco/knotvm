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
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("Command name non può essere vuoto", nameof(commandName));

        return $"{IsolatedPrefix}{commandName}";
    }
}
