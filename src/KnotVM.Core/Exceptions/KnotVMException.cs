using KnotVM.Core.Enums;
using KnotVM.Core.Common;

namespace KnotVM.Core.Exceptions;

/// <summary>
/// Eccezione base per tutte le eccezioni specifiche di KnotVM.
/// Include codice errore standardizzato e exit code associato.
/// </summary>
public class KnotVMException : Exception
{
    /// <summary>
    /// Codice errore standardizzato KNOT-*.
    /// </summary>
    public KnotErrorCode ErrorCode { get; }

    /// <summary>
    /// Exit code associato al codice errore.
    /// </summary>
    public int ExitCode => ErrorExitCodeMap.GetExitCode(ErrorCode);

    /// <summary>
    /// Codice stringa formattato (es: "KNOT-OS-001").
    /// </summary>
    public string CodeString => ErrorExitCodeMap.GetCodeString(ErrorCode);

    public KnotVMException() : base()
    {
        ErrorCode = KnotErrorCode.UnexpectedError;
    }

    public KnotVMException(string message) : base(message)
    {
        ErrorCode = KnotErrorCode.UnexpectedError;
    }

    public KnotVMException(string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = KnotErrorCode.UnexpectedError;
    }

    public KnotVMException(KnotErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public KnotVMException(KnotErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
