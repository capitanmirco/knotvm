namespace KnotVM.Core.Enums;

/// <summary>
/// Tipo di proxy da generare.
/// </summary>
public enum ProxyType
{
    /// <summary>
    /// Proxy generico per comandi Node.js (node stesso).
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Proxy per package manager (npm, yarn, pnpm, ecc.).
    /// Include logica di auto-sync dopo install/uninstall globale.
    /// </summary>
    PackageManager = 1,

    /// <summary>
    /// Shim compilato C# per node.exe (Windows).
    /// </summary>
    Shim = 2
}
