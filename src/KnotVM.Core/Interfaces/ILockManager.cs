namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per gestione lock file cross-platform.
/// Previene operazioni concorrenti che potrebbero corrompere lo stato.
/// </summary>
public interface ILockManager
{
    /// <summary>
    /// Acquisisce un lock per nome operazione.
    /// </summary>
    /// <param name="lockName">Nome lock (es: "install", "use", "remove")</param>
    /// <param name="timeoutSeconds">Timeout acquisizione lock in secondi (0 = tentativo immediato non bloccante)</param>
    /// <returns>IDisposable che rilascia il lock quando disposed</returns>
    /// <exception cref="KnotVM.Core.Exceptions.KnotVMException">Se lock non acquisibile entro timeout</exception>
    IDisposable AcquireLock(string lockName, int timeoutSeconds = 30);

    /// <summary>
    /// Tenta di acquisire un lock senza bloccare.
    /// </summary>
    /// <param name="lockName">Nome lock</param>
    /// <param name="lockHandle">Handle IDisposable se acquisito</param>
    /// <returns>True se lock acquisito</returns>
    bool TryAcquireLock(string lockName, out IDisposable? lockHandle);

    /// <summary>
    /// Verifica se un lock è attualmente attivo.
    /// </summary>
    /// <param name="lockName">Nome lock</param>
    /// <returns>True se lock esistente</returns>
    bool IsLocked(string lockName);

    /// <summary>
    /// Forza il rilascio di un lock (usare con cautela).
    /// </summary>
    /// <param name="lockName">Nome lock</param>
    void ForceReleaseLock(string lockName);

    /// <summary>
    /// Pulisce lock file orfani (più vecchi di X ore).
    /// </summary>
    /// <param name="maxAgeHours">Età massima lock in ore (default: 24)</param>
    void CleanupStaleLocks(int maxAgeHours = 24);
}
