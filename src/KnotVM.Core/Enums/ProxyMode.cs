namespace KnotVM.Core.Enums;

/// <summary>
/// Modalità di operazione proxy.
/// KnotVM usa solo modalità Isolated: tutti i proxy hanno prefisso 'nlocal-' (es. nlocal-node, nlocal-npm).
/// Questo evita conflitti con installazioni Node globali del sistema.
/// </summary>
public enum ProxyMode
{
    /// <summary>
    /// Isolato: proxy con prefisso (es. nlocal-node, nlocal-npm).
    /// Non interferisce con installazioni Node globali del sistema.
    /// </summary>
    Isolated
}
