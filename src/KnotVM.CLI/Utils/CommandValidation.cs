using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;

namespace KnotVM.CLI.Utils;

/// <summary>
/// Utility per validazioni comuni dei command arguments/options.
/// </summary>
public static class CommandValidation
{
    /// <summary>
    /// Verifica che una sola opzione sia selezionata.
    /// Lancia KnotVMException se zero o più opzioni sono attive.
    /// </summary>
    public static void EnsureExactlyOne(string noneSelectedMessage, string multipleSelectedMessage, params bool[] selections)
    {
        if (selections == null || selections.Length == 0)
            throw new ArgumentException("Almeno una selezione deve essere specificata", nameof(selections));

        int selectedCount = selections.Count(s => s);
        if (selectedCount == 0)
        {
            throw new KnotVMException(KnotErrorCode.UnexpectedError, noneSelectedMessage);
        }

        if (selectedCount > 1)
        {
            throw new KnotVMException(KnotErrorCode.UnexpectedError, multipleSelectedMessage);
        }
    }
}
