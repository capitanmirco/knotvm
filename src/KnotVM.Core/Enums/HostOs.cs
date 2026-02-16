namespace KnotVM.Core.Enums;

/// <summary>
/// Sistema operativo host.
/// </summary>
public enum HostOs
{
    /// <summary>
    /// Sistema operativo non riconosciuto o non supportato.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Windows 10/11 o Windows Server moderni.
    /// </summary>
    Windows = 1,

    /// <summary>
    /// Linux (Ubuntu, Debian, Fedora, ecc.).
    /// </summary>
    Linux = 2,

    /// <summary>
    /// macOS 13+ (Ventura o superiore).
    /// </summary>
    MacOS = 3
}
