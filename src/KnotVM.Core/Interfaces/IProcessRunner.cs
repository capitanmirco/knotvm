namespace KnotVM.Core.Interfaces;

/// <summary>
/// Risultato esecuzione processo.
/// </summary>
/// <param name="ExitCode">Codice uscita processo</param>
/// <param name="StandardOutput">Output standard catturato</param>
/// <param name="StandardError">Output errore catturato</param>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Servizio per esecuzione processi con isolamento environment.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Esegue un comando con environment isolato e cattura output.
    /// </summary>
    /// <param name="executablePath">Path eseguibile</param>
    /// <param name="arguments">Argomenti comando</param>
    /// <param name="workingDirectory">Directory lavoro (null = corrente)</param>
    /// <param name="environmentVariables">Variabili environment da sovrascrivere (null = eredita)</param>
    /// <param name="timeoutMilliseconds">Timeout esecuzione (0 = illimitato)</param>
    /// <returns>Risultato esecuzione</returns>
    Task<ProcessResult> RunAsync(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0
    );

    /// <summary>
    /// Esegue un comando con argomenti già tokenizzati e cattura output.
    /// Preferire questo overload quando path o argomenti derivano da input esterno,
    /// per evitare command injection tramite parsing della stringa argomenti.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0
    );

    /// <summary>
    /// Esegue un comando in modo sincrono.
    /// </summary>
    ProcessResult Run(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0
    );

    /// <summary>
    /// Esegue un comando e propaga exit code direttamente.
    /// Utile per `knot run` dove exit code del comando deve essere trasparente.
    /// </summary>
    /// <param name="executablePath">Path eseguibile</param>
    /// <param name="arguments">Argomenti comando</param>
    /// <param name="workingDirectory">Directory lavoro</param>
    /// <param name="environmentVariables">Environment isolato</param>
    /// <returns>Exit code del processo</returns>
    int RunAndPropagateExitCode(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null
    );

    /// <summary>
    /// Esegue un comando passando gli argomenti già tokenizzati.
    /// Utile per preservare correttamente quoting e spazi negli argomenti.
    /// </summary>
    /// <param name="executablePath">Path eseguibile</param>
    /// <param name="arguments">Lista argomenti</param>
    /// <param name="workingDirectory">Directory lavoro</param>
    /// <param name="environmentVariables">Environment isolato</param>
    /// <returns>Exit code del processo</returns>
    int RunAndPropagateExitCode(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null
    );

    /// <summary>
    /// Verifica se un eseguibile esiste ed è accessibile.
    /// </summary>
    bool IsExecutableAccessible(string executablePath);

    /// <summary>
    /// Ottiene la versione di Node.js eseguendo 'node -v'.
    /// Rimuove prefisso 'v' se presente.
    /// </summary>
    /// <param name="nodeExecutablePath">Path node executable</param>
    /// <returns>Versione (es: "20.11.0") o null se fallisce</returns>
    string? GetNodeVersion(string nodeExecutablePath);

    /// <summary>
    /// Trova i processi in esecuzione da un determinato percorso.
    /// </summary>
    /// <param name="executablePath">Path dell'eseguibile da cercare</param>
    /// <returns>Lista di process ID trovati</returns>
    List<int> FindRunningProcesses(string executablePath);
}
