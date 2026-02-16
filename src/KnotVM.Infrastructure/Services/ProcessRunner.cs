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
        
        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = timeoutMilliseconds == 0
            ? await Task.Run(() => { process.WaitForExit(); return true; })
            : await Task.Run(() => process.WaitForExit(timeoutMilliseconds));

        if (!completed)
        {
            try { process.Kill(); } catch { /* Ignore */ }
            throw new TimeoutException($"Processo {executablePath} timeout dopo {timeoutMilliseconds}ms");
        }

        return new ProcessResult(
            process.ExitCode,
            outputBuilder.ToString().TrimEnd(),
            errorBuilder.ToString().TrimEnd()
        );
    }

    public ProcessResult Run(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMilliseconds = 0)
    {
        return RunAsync(executablePath, arguments, workingDirectory, environmentVariables, timeoutMilliseconds)
            .GetAwaiter()
            .GetResult();
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

        // Applica environment variables isolato se specificato
        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                if (startInfo.Environment.ContainsKey(kvp.Key))
                    startInfo.Environment[kvp.Key] = kvp.Value;
                else
                    startInfo.Environment.Add(kvp.Key, kvp.Value);
            }
        }

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
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Applica environment variables isolato se specificato
        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                if (startInfo.Environment.ContainsKey(kvp.Key))
                    startInfo.Environment[kvp.Key] = kvp.Value;
                else
                    startInfo.Environment.Add(kvp.Key, kvp.Value);
            }
        }

        return startInfo;
    }
}
