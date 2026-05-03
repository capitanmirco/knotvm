using System.Diagnostics;
using System.Text;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio esecuzione processi con isolamento environment.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0)
    {
        var startInfo = CreateProcessStartInfo(executablePath, arguments, workingDirectory, environmentVariables);
        return await RunCoreAsync(startInfo, executablePath, timeoutMilliseconds);
    }

    public async Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0)
    {
        var startInfo = CreateProcessStartInfo(executablePath, arguments, workingDirectory, environmentVariables);
        return await RunCoreAsync(startInfo, executablePath, timeoutMilliseconds);
    }

    /// <summary>
    /// Implementazione sincrona diretta: usa WaitForExit() senza passare per RunAsync
    /// per evitare il pattern sync-over-async che può causare deadlock.
    /// </summary>
    public ProcessResult Run(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0)
    {
        var startInfo = CreateProcessStartInfo(executablePath, arguments, workingDirectory, environmentVariables);
        return RunCoreSync(startInfo, executablePath, timeoutMilliseconds);
    }

    public int RunAndPropagateExitCode(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        var startInfo = CreateProcessStartInfo(executablePath, arguments, workingDirectory, environmentVariables);

        // Non redirige output: stdout/stderr passano direttamente al terminale
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;
        startInfo.UseShellExecute = false;

        using var process = Process.Start(startInfo);
        process?.WaitForExit();

        return process?.ExitCode ?? -1;
    }

    public int RunAndPropagateExitCode(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        var startInfo = CreateProcessStartInfo(executablePath, arguments, workingDirectory, environmentVariables);

        // Non redirige output: stdout/stderr passano direttamente al terminale
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;
        startInfo.UseShellExecute = false;

        using var process = Process.Start(startInfo);
        process?.WaitForExit();

        return process?.ExitCode ?? -1;
    }

    public bool IsExecutableAccessible(string executablePath)
    {
        try
        {
            return File.Exists(executablePath) &&
                   new FileInfo(executablePath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public string? GetNodeVersion(string nodeExecutablePath)
    {
        if (!IsExecutableAccessible(nodeExecutablePath))
            return null;

        try
        {
            var result = Run(nodeExecutablePath, "-v", timeoutMilliseconds: 5000);

            if (result.ExitCode != 0)
                return null;

            var version = result.StandardOutput.Trim();

            // Rimuovi prefisso 'v' se presente
            if (version.StartsWith('v'))
                version = version.Substring(1);

            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
    }

    public List<int> FindRunningProcesses(string executablePath)
    {
        var processIds = new List<int>();

        try
        {
            // Normalizza il path per il confronto
            var normalizedPath = Path.GetFullPath(executablePath).ToLowerInvariant();

            // Ottieni tutti i processi con lo stesso nome dell'eseguibile
            var processName = Path.GetFileNameWithoutExtension(executablePath);
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                try
                {
                    // Confronta il path del processo con quello cercato
                    var processPath = process.MainModule?.FileName;
                    if (processPath != null)
                    {
                        var normalizedProcessPath = Path.GetFullPath(processPath).ToLowerInvariant();
                        if (normalizedProcessPath == normalizedPath)
                        {
                            processIds.Add(process.Id);
                        }
                    }
                }
                catch
                {
                    // Ignora errori di accesso (processo di sistema, ecc.)
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // Ignora errori generali - ritorna lista vuota
        }

        return processIds;
    }

    // ── Core execution helpers ────────────────────────────────────────────────

    /// <summary>
    /// Esecuzione asincrona tramite WaitForExitAsync: non blocca thread-pool thread
    /// per tutta la durata del processo figlio.
    /// </summary>
    private static async Task<ProcessResult> RunCoreAsync(
        ProcessStartInfo startInfo,
        string executablePath,
        int timeoutMilliseconds)
    {
        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder  = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (timeoutMilliseconds > 0)
        {
            using var cts = new CancellationTokenSource(timeoutMilliseconds);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                KillSafely(process);
                throw new TimeoutException($"Processo '{executablePath}' timeout dopo {timeoutMilliseconds}ms");
            }
        }
        else
        {
            await process.WaitForExitAsync();
        }

        return new ProcessResult(
            process.ExitCode,
            outputBuilder.ToString().TrimEnd(),
            errorBuilder.ToString().TrimEnd());
    }

    /// <summary>
    /// Esecuzione sincrona diretta: usa WaitForExit() senza passare per Task.
    /// Evita il pattern sync-over-async e il rischio di deadlock associato.
    /// </summary>
    private static ProcessResult RunCoreSync(
        ProcessStartInfo startInfo,
        string executablePath,
        int timeoutMilliseconds)
    {
        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder  = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool completed;
        if (timeoutMilliseconds > 0)
        {
            completed = process.WaitForExit(timeoutMilliseconds);
        }
        else
        {
            process.WaitForExit();
            completed = true;
        }

        if (!completed)
        {
            KillSafely(process);
            throw new TimeoutException($"Processo '{executablePath}' timeout dopo {timeoutMilliseconds}ms");
        }

        // ROB-09: WaitForExit() senza argomenti è già implicito nel ramo else sopra
        // o garantito dalla terminazione. La chiamata qui era ridondante.

        return new ProcessResult(
            process.ExitCode,
            outputBuilder.ToString().TrimEnd(),
            errorBuilder.ToString().TrimEnd());
    }

    /// <summary>
    /// Termina il processo ignorando <see cref="InvalidOperationException"/>
    /// (processo già uscito) che è la condizione attesa, non un errore reale.
    /// </summary>
    private static void KillSafely(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Il processo è già terminato prima che riuscissimo a killarlo — ok.
        }
    }

    // ── StartInfo factories ───────────────────────────────────────────────────

    private static ProcessStartInfo CreateProcessStartInfo(
        string executablePath,
        string arguments,
        string? workingDirectory,
        Dictionary<string, string>? environmentVariables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ApplyEnvironmentVariables(startInfo, environmentVariables);
        return startInfo;
    }

    private static ProcessStartInfo CreateProcessStartInfo(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        Dictionary<string, string>? environmentVariables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        ApplyEnvironmentVariables(startInfo, environmentVariables);
        return startInfo;
    }

    private static void ApplyEnvironmentVariables(
        ProcessStartInfo startInfo,
        Dictionary<string, string>? environmentVariables)
    {
        if (environmentVariables == null) return;

        foreach (var kvp in environmentVariables)
        {
            if (startInfo.Environment.ContainsKey(kvp.Key))
                startInfo.Environment[kvp.Key] = kvp.Value;
            else
                startInfo.Environment.Add(kvp.Key, kvp.Value);
        }
    }
}
