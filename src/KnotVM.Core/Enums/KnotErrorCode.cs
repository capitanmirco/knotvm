namespace KnotVM.Core.Enums;

/// <summary>
/// Codici errore standardizzati KNOT-*.
/// Ogni codice ha un exit code associato definito in ErrorExitCodeMap.
/// </summary>
public enum KnotErrorCode
{
    /// <summary>
    /// Nessun errore.
    /// </summary>
    None = 0,

    // OS/Platform (10-19)
    
    /// <summary>
    /// KNOT-OS-001: Sistema operativo non supportato.
    /// Exit code: 10
    /// </summary>
    UnsupportedOs = 1001,

    /// <summary>
    /// KNOT-OS-002: Architettura non supportata.
    /// Exit code: 11
    /// </summary>
    UnsupportedArch = 1002,

    // Path/Filesystem (20-29)
    
    /// <summary>
    /// KNOT-PATH-001: Impossibile determinare/creare path base.
    /// Exit code: 20
    /// </summary>
    PathCreationFailed = 2001,

    /// <summary>
    /// KNOT-PERM-001: Permessi insufficienti.
    /// Exit code: 21
    /// </summary>
    InsufficientPermissions = 2002,

    /// <summary>
    /// KNOT-CFG-002: File settings.txt corrotto.
    /// Exit code: 23
    /// </summary>
    CorruptedSettingsFile = 2003,

    /// <summary>
    /// KNOT-PATH-002: Path o file non trovato.
    /// Exit code: 24
    /// </summary>
    PathNotFound = 2004,

    // Artifact/Download (30-39)
    
    /// <summary>
    /// KNOT-API-001: Chiamata API remota fallita (nodejs.org).
    /// Exit code: 30
    /// </summary>
    RemoteApiFailed = 3001,

    /// <summary>
    /// KNOT-ART-001: Artifact Node non disponibile.
    /// Exit code: 31
    /// </summary>
    ArtifactNotAvailable = 3002,

    /// <summary>
    /// KNOT-DL-001: Download fallito dopo retry.
    /// Exit code: 32
    /// </summary>
    DownloadFailed = 3003,

    /// <summary>
    /// KNOT-SEC-001: Checksum SHA256 non corrispondente.
    /// Exit code: 33
    /// </summary>
    ChecksumMismatch = 3004,

    /// <summary>
    /// KNOT-ARC-001: Archivio corrotto/non estraibile.
    /// Exit code: 34
    /// </summary>
    CorruptedArchive = 3005,

    // Installation/Alias (40-49)
    
    /// <summary>
    /// KNOT-INS-001: Installazione/alias non trovata.
    /// Exit code: 40
    /// </summary>
    InstallationNotFound = 4001,

    /// <summary>
    /// KNOT-INS-002: Alias non valido o già esistente.
    /// Exit code: 41
    /// </summary>
    InvalidAlias = 4002,

    /// <summary>
    /// KNOT-RUN-001: Comando non trovato nella versione target.
    /// Exit code: 42
    /// </summary>
    CommandNotFound = 4003,

    /// <summary>
    /// KNOT-INS-003: Installazione fallita.
    /// Exit code: 43
    /// </summary>
    InstallationFailed = 4004,

    // Proxy/Sync (50-59)
    
    /// <summary>
    /// KNOT-PROXY-001: Generazione proxy fallita.
    /// Exit code: 50
    /// </summary>
    ProxyGenerationFailed = 5001,

    /// <summary>
    /// KNOT-SYNC-001: Sync fallita per stato incoerente.
    /// Exit code: 51
    /// </summary>
    SyncFailed = 5002,

    // Concurrency (60-69)
    
    /// <summary>
    /// KNOT-LOCK-001: Lock non acquisibile.
    /// Exit code: 60
    /// </summary>
    LockFailed = 6001,

    // Generico (90-99)
    
    /// <summary>
    /// KNOT-GEN-001: Errore inatteso non classificato.
    /// Exit code: 99
    /// </summary>
    UnexpectedError = 9001
}
