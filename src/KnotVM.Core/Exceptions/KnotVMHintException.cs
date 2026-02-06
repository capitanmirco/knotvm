namespace KnotVM.Core.Exceptions;

/// <summary>
/// Eccezione base per errori KnotVM con suggerimento per l'utente.
/// </summary>
public class KnotVMHintException : KnotVMException
{
    /// <summary>
    /// Hint per l'utente su come risolvere il problema.
    /// </summary>
    public string Hint { get; }

    public KnotVMHintException(string message, string hint) : base(message)
    {
        Hint = hint;
    }

    public KnotVMHintException(string message, string hint, Exception innerException)
        : base(message, innerException)
    {
        Hint = hint;
    }
}
