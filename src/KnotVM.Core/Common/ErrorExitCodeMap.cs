using KnotVM.Core.Enums;

namespace KnotVM.Core.Common;

/// <summary>
/// Mapping centralizzato KnotErrorCode -> Exit Code.
/// Garantisce che ogni errore abbia un exit code deterministico e unico.
/// </summary>
public static class ErrorExitCodeMap
{
    private static readonly Dictionary<KnotErrorCode, int> _map = new()
    {
        // Success
        { KnotErrorCode.None, 0 },

        // OS/Platform (10-19)
        { KnotErrorCode.UnsupportedOs, 10 },
        { KnotErrorCode.UnsupportedArch, 11 },

        // Path/Filesystem (20-29)
        { KnotErrorCode.PathCreationFailed, 20 },
        { KnotErrorCode.InsufficientPermissions, 21 },
        { KnotErrorCode.CorruptedSettingsFile, 23 },
        { KnotErrorCode.PathNotFound, 24 },

        // Artifact/Download (30-39)
        { KnotErrorCode.RemoteApiFailed, 30 },
        { KnotErrorCode.ArtifactNotAvailable, 31 },
        { KnotErrorCode.DownloadFailed, 32 },
        { KnotErrorCode.ChecksumMismatch, 33 },
        { KnotErrorCode.CorruptedArchive, 34 },

        // Installation/Alias (40-49)
        { KnotErrorCode.InstallationNotFound, 40 },
        { KnotErrorCode.InvalidAlias, 41 },
        { KnotErrorCode.CommandNotFound, 42 },
        { KnotErrorCode.InstallationFailed, 43 },

        // Proxy/Sync (50-59)
        { KnotErrorCode.ProxyGenerationFailed, 50 },
        { KnotErrorCode.SyncFailed, 51 },

        // Concurrency (60-69)
        { KnotErrorCode.LockFailed, 60 },

        // Version File (70-79)
        { KnotErrorCode.VersionFileNotFound, 70 },
        { KnotErrorCode.InvalidVersionFormat, 71 },

        // Generico (90-99)
        { KnotErrorCode.UnexpectedError, 99 }
    };

    /// <summary>
    /// Ottiene l'exit code per un dato KnotErrorCode.
    /// </summary>
    /// <param name="errorCode">Codice errore</param>
    /// <returns>Exit code</returns>
    public static int GetExitCode(KnotErrorCode errorCode)
    {
        return _map.TryGetValue(errorCode, out var exitCode) ? exitCode : 99;
    }

    /// <summary>
    /// Verifica che il mapping sia completo e senza collisioni.
    /// Usato nei test per garantire integrità.
    /// </summary>
    /// <returns>True se mapping valido</returns>
    public static bool ValidateMapping()
    {
        // Verifica che tutti i KnotErrorCode (tranne None) siano mappati
        var allCodes = Enum.GetValues<KnotErrorCode>().Where(c => c != KnotErrorCode.None);
        var mappedCodes = _map.Keys.Where(k => k != KnotErrorCode.None);

        if (!allCodes.All(c => mappedCodes.Contains(c)))
        {
            return false; // Codice non mappato
        }

        // Verifica che non ci siano collisioni (exit code duplicati per errori diversi)
        // Escludo exit code 0 che può essere usato per warning multipli
        var exitCodes = _map.Where(kv => kv.Value != 0).Select(kv => kv.Value);
        return exitCodes.Count() == exitCodes.Distinct().Count();
    }

    /// <summary>
    /// Ottiene il codice stringa formattato (es: "KNOT-OS-001").
    /// </summary>
    public static string GetCodeString(KnotErrorCode errorCode)
    {
        return errorCode switch
        {
            KnotErrorCode.None => "",
            KnotErrorCode.UnsupportedOs => "KNOT-OS-001",
            KnotErrorCode.UnsupportedArch => "KNOT-OS-002",
            KnotErrorCode.PathCreationFailed => "KNOT-PATH-001",
            KnotErrorCode.InsufficientPermissions => "KNOT-PERM-001",
            KnotErrorCode.CorruptedSettingsFile => "KNOT-CFG-002",
            KnotErrorCode.PathNotFound => "KNOT-PATH-002",
            KnotErrorCode.RemoteApiFailed => "KNOT-API-001",
            KnotErrorCode.ArtifactNotAvailable => "KNOT-ART-001",
            KnotErrorCode.DownloadFailed => "KNOT-DL-001",
            KnotErrorCode.ChecksumMismatch => "KNOT-SEC-001",
            KnotErrorCode.CorruptedArchive => "KNOT-ARC-001",
            KnotErrorCode.InstallationNotFound => "KNOT-INS-001",
            KnotErrorCode.InvalidAlias => "KNOT-INS-002",
            KnotErrorCode.CommandNotFound => "KNOT-RUN-001",
            KnotErrorCode.InstallationFailed => "KNOT-INS-003",
            KnotErrorCode.ProxyGenerationFailed => "KNOT-PROXY-001",
            KnotErrorCode.SyncFailed => "KNOT-SYNC-001",
            KnotErrorCode.LockFailed => "KNOT-LOCK-001",
            KnotErrorCode.UnexpectedError => "KNOT-GEN-001",
            _ => "KNOT-UNKNOWN"
        };
    }
}
