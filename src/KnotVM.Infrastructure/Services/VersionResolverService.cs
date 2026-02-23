using System.Text.RegularExpressions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Servizio principale per la risoluzione intelligente di versioni Node.js.
/// Utilizza un insieme ordinato di strategie per risolvere qualsiasi formato di input
/// a una versione semver completa (es. "20" → "20.11.0", "lts" → "20.11.0").
/// </summary>
/// <remarks>
/// Ordine di precedenza delle strategie:
/// 1. <see cref="VersionResolution.ExactVersionStrategy"/> — semver esatto (18.2.0)
/// 2. <see cref="VersionResolution.AliasStrategy"/> — alias installato localmente
/// 3. <see cref="VersionResolution.MajorVersionStrategy"/> — versione maggiore (20)
/// 4. <see cref="VersionResolution.LtsVersionStrategy"/> — keyword LTS (lts, lts/iron)
/// 5. <see cref="VersionResolution.KeywordStrategy"/> — keyword generiche (latest, current)
/// 6. <see cref="VersionResolution.CodenameStrategy"/> — codename LTS (hydrogen, iron)
/// </remarks>
public class VersionResolverService(IEnumerable<IVersionResolutionStrategy> strategies) : IVersionResolver
{
    private static readonly Regex SemverPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<string> ResolveVersionAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionInput))
        {
            throw new ArgumentException("Il parametro versione non può essere vuoto.", nameof(versionInput));
        }

        var normalized = versionInput.Trim();

        // Trova la prima strategia in grado di gestire l'input
        var strategy = strategies.FirstOrDefault(s => s.CanHandle(normalized));

        if (strategy == null)
        {
            throw new KnotVMException(
                KnotErrorCode.InvalidVersionFormat,
                $"Formato versione '{normalized}' non riconosciuto. " +
                $"Formati supportati: 18.2.0 (semver), 20 (maggiore), lts, lts/iron, hydrogen, latest, current");
        }

        return await strategy.ResolveAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool IsExactVersion(string versionInput)
    {
        return SemverPattern.IsMatch(versionInput.TrimStart('v').Trim());
    }
}
