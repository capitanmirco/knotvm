namespace KnotVM.Core.Enums;

/// <summary>
/// Architettura processore host.
/// </summary>
public enum HostArch
{
    /// <summary>
    /// Architettura non riconosciuta o non supportata.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// x64 / AMD64 / x86_64 (64-bit Intel/AMD).
    /// </summary>
    X64 = 1,

    /// <summary>
    /// ARM64 / AArch64 (64-bit ARM).
    /// Include Apple Silicon (M1/M2/M3/M4).
    /// </summary>
    Arm64 = 2,

    /// <summary>
    /// x86 / i386 / i686 (32-bit Intel/AMD) - solo Windows.
    /// </summary>
    X86 = 3
}
