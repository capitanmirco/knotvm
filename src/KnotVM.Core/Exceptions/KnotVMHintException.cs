using KnotVM.Core.Enums;

namespace KnotVM.Core.Exceptions;

/// <summary>
/// Eccezione per errori KnotVM con suggerimento operativo per l'utente.
/// Formato standard: "[CodeString]: [Message]\nHint: [Hint]"
/// </summary>
public class KnotVMHintException : KnotVMException
{
    /// <summary>
    /// Hint operativo per l'utente su come risolvere il problema.
    /// </summary>
    public string Hint { get; }

    public KnotVMHintException(KnotErrorCode errorCode, string message, string hint)
        : base(errorCode, message)
    {
        Hint = hint;
    }

    public KnotVMHintException(KnotErrorCode errorCode, string message, string hint, Exception innerException)
        : base(errorCode, message, innerException)
    {
        Hint = hint;
    }

    /// <summary>
    /// Ottiene il messaggio formattato completo con codice errore e hint.
    /// </summary>
    public string GetFormattedMessage()
    {
        return $"{CodeString}: {Message}\nHint: {Hint}";
    }
}
