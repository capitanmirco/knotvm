namespace KnotVM.Core.Exceptions;

/// <summary>
/// Eccezione base per tutte le eccezioni specifiche di KnotVM.
/// </summary>
public class KnotVMException : Exception
{
    public KnotVMException()
    {
    }

    public KnotVMException(string message) : base(message)
    {
    }

    public KnotVMException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
