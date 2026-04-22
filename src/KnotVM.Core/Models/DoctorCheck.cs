namespace KnotVM.Core.Models;

/// <summary>
/// Risultato di un singolo check diagnostico eseguito da knot doctor.
/// </summary>
public record DoctorCheck(
    string  Name,
    bool    Passed,
    bool    IsWarning,
    string? Detail,
    string? Suggestion,
    bool    CanAutoFix
);
