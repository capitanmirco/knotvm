namespace KnotVM.Core.Enums;

/// <summary>
/// Tipo di shell supportata.
/// </summary>
public enum ShellType
{
    /// <summary>
    /// Shell non riconosciuta o non supportata ufficialmente.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// PowerShell (Windows).
    /// </summary>
    PowerShell = 1,

    /// <summary>
    /// CMD / Command Prompt (Windows).
    /// </summary>
    Cmd = 2,

    /// <summary>
    /// Bash (Linux/macOS/Windows WSL).
    /// </summary>
    Bash = 3,

    /// <summary>
    /// Zsh (Linux/macOS).
    /// </summary>
    Zsh = 4,

    /// <summary>
    /// Fish (Linux/macOS).
    /// </summary>
    Fish = 5
}
