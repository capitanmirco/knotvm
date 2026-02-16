namespace KnotVM.CLI.Utils;

/// <summary>
/// Scope che converte Ctrl+C in CancellationToken cancellato.
/// </summary>
public sealed class ConsoleCancellationScope : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private bool _disposed;

    public ConsoleCancellationScope(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            _cancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += _cancelHandler;
    }

    public CancellationToken Token => _cancellationTokenSource.Token;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= _cancelHandler;
        _cancellationTokenSource.Dispose();
    }
}
