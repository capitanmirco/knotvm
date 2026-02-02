namespace KnotVM.Core.Interfaces;

using KnotVM.Core.Models;

/// <summary>
/// Repository per gestire le installazioni di Node.js.
/// </summary>
public interface IInstallationsRepository
{
    /// <summary>
    /// Ottiene tutte le installazioni presenti.
    /// </summary>
    /// <returns>Array di installazioni</returns>
    Installation[] GetAll();
}
