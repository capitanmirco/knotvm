using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio lock file cross-platform usando FileStream.
/// </summary>
public class LockManager : ILockManager
{
    private readonly IPathService _pathService;
    private readonly IFileSystemService _fileSystem;
    private readonly Dictionary<string, FileStream> _activeLocks = new();
    private readonly object _lockObject = new();

    public LockManager(IPathService pathService, IFileSystemService fileSystem)
    {
        _pathService = pathService;
        _fileSystem = fileSystem;
    }

    public IDisposable AcquireLock(string lockName, int timeoutSeconds = 30)
    {
        ValidateLockName(lockName);
        if (timeoutSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Il timeout non può essere negativo");

        var lockFilePath = GetLockFilePath(lockName);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        FileStream? lockStream = null;
        var singleAttempt = timeoutSeconds == 0;

        while (singleAttempt || DateTime.UtcNow < deadline)
        {
            try
            {
                _fileSystem.EnsureDirectoryExists(_pathService.GetLocksPath());

                // Apri file con lock esclusivo (FileShare.None)
                lockStream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None, // Lock esclusivo
                    bufferSize: 4096,
                    FileOptions.DeleteOnClose
                );

                // Scrivi timestamp nel lock file
                using var writer = new StreamWriter(lockStream, leaveOpen: true);
                writer.WriteLine($"Locked at: {DateTime.UtcNow:O}");
                writer.WriteLine($"Process ID: {Environment.ProcessId}");
                writer.Flush();

                lock (_lockObject)
                {
                    _activeLocks[lockName] = lockStream;
                }

                return new LockHandle(lockName, lockStream, this);
            }
            catch (IOException)
            {
                // Lock già acquisito da altro processo
                lockStream?.Dispose();
                if (singleAttempt)
                    break;

                Thread.Sleep(100); // Retry dopo 100ms
            }
            finally
            {
                singleAttempt = false;
            }
        }

        throw new KnotVMException(
            KnotErrorCode.LockFailed,
            $"Impossibile acquisire lock '{lockName}' entro {timeoutSeconds} secondi"
        );
    }

    public bool TryAcquireLock(string lockName, out IDisposable? lockHandle)
    {
        ValidateLockName(lockName);

        try
        {
            lockHandle = AcquireLock(lockName, timeoutSeconds: 0);
            return true;
        }
        catch
        {
            lockHandle = null;
            return false;
        }
    }

    public bool IsLocked(string lockName)
    {
        ValidateLockName(lockName);

        lock (_lockObject)
        {
            if (_activeLocks.ContainsKey(lockName))
                return true;
        }

        // Verifica se file lock esiste su disco
        var lockFilePath = GetLockFilePath(lockName);
        if (!_fileSystem.FileExists(lockFilePath))
            return false;

        // Tenta di aprire per vedere se locked
        try
        {
            using var stream = new FileStream(
                lockFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None
            );
            return false; // Se aperto, non è locked
        }
        catch (IOException)
        {
            return true; // Se IOException, è locked
        }
        catch
        {
            return false;
        }
    }

    public void ForceReleaseLock(string lockName)
    {
        ValidateLockName(lockName);

        lock (_lockObject)
        {
            if (_activeLocks.TryGetValue(lockName, out var stream))
            {
                stream.Dispose();
                _activeLocks.Remove(lockName);
            }
        }

        var lockFilePath = GetLockFilePath(lockName);
        _fileSystem.DeleteFileIfExists(lockFilePath);
    }

    public void CleanupStaleLocks(int maxAgeHours = 24)
    {
        var locksPath = _pathService.GetLocksPath();
        if (!_fileSystem.DirectoryExists(locksPath))
            return;

        var lockFiles = _fileSystem.GetFiles(locksPath, "*.lock");
        var cutoffTime = DateTime.UtcNow.AddHours(-maxAgeHours);

        foreach (var lockFile in lockFiles)
        {
            try
            {
                var lastWrite = _fileSystem.GetFileLastWriteTime(lockFile);
                if (lastWrite < cutoffTime)
                {
                    // Lock file vecchio, verifica se ancora attivo
                    try
                    {
                        using var stream = new FileStream(
                            lockFile,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.None
                        );
                        // Se apriamo con successo, non è locked → eliminabile
                        stream.Close();
                        _fileSystem.DeleteFileIfExists(lockFile);
                    }
                    catch (IOException)
                    {
                        // Locked da processo attivo, skip
                    }
                }
            }
            catch
            {
                // Ignora errori su singoli file
            }
        }
    }

    private string GetLockFilePath(string lockName)
    {
        return Path.Combine(_pathService.GetLocksPath(), $"{lockName}.lock");
    }

    private void ValidateLockName(string lockName)
    {
        if (string.IsNullOrWhiteSpace(lockName))
            throw new ArgumentException("Nome lock non può essere vuoto", nameof(lockName));

        if (lockName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Nome lock contiene caratteri non validi", nameof(lockName));
    }

    internal void ReleaseLock(string lockName, FileStream stream)
    {
        lock (_lockObject)
        {
            if (_activeLocks.TryGetValue(lockName, out var activeStream) && activeStream == stream)
            {
                _activeLocks.Remove(lockName);
            }
        }

        stream.Dispose();
    }

    private class LockHandle : IDisposable
    {
        private readonly string _lockName;
        private readonly FileStream _stream;
        private readonly LockManager _manager;
        private bool _disposed;

        public LockHandle(string lockName, FileStream stream, LockManager manager)
        {
            _lockName = lockName;
            _stream = stream;
            _manager = manager;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _manager.ReleaseLock(_lockName, _stream);
        }
    }
}
