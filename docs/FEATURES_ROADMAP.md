# KnotVM — Feature Roadmap & Implementation Guide

Questo documento descrive le feature candidate per le prossime versioni di KnotVM,
con dettagli architetturali sufficienti a guidare l'implementazione nel rispetto
dei pattern già adottati dal progetto (DI, Strategy, Repository, exit code standardizzati).

---

## Indice

1. [`knot pin` — Scrittura file di versione progetto](#1-knot-pin)
2. [`knot info` — Dettagli installazione](#2-knot-info)
3. [`knot doctor` — Diagnosi ambiente](#3-knot-doctor)
4. [Alpine / musl Linux support](#4-alpine--musl-linux-support)
5. [`knot use --default` — Versione di fallback globale](#5-knot-use---default)
6. [Fish shell proxy (first-class)](#6-fish-shell-proxy-first-class)

---

## 1. `knot pin`

### Descrizione

`knot pin` è l'inverso di `knot use --auto`: invece di *leggere* il file di versione
presente nel progetto, lo *crea o aggiorna* con la versione attiva (o specificata).

```bash
knot pin                          # scrive la versione attiva in .nvmrc
knot pin --alias lts              # usa l'alias "lts" invece della versione attiva
knot pin --format node-version    # scrive in .node-version invece di .nvmrc
knot pin --directory /path/proj   # opera su una directory specifica
```

### Motivazione

Tutti i tool di version management moderni (nvm, fnm, volta) supportano il pinning.
Abbassa il friction per i team: dopo `knot install` + `knot use`, un singolo
`knot pin` documenta la scelta nel repository.

### Implementazione

**Nuovi file:**

| File | Ruolo |
|------|-------|
| `src/KnotVM.CLI/Commands/PinCommand.cs` | Comando CLI |
| `src/KnotVM.Core/Interfaces/IVersionPinService.cs` | Contratto del servizio |
| `src/KnotVM.Infrastructure/Services/VersionPinService.cs` | Implementazione |
| `tests/KnotVM.Tests/CLI/PinCommandTests.cs` | Test del comando |
| `tests/KnotVM.Tests/Infrastructure/VersionPinServiceTests.cs` | Test del servizio |

**`IVersionPinService`:**

```csharp
public interface IVersionPinService
{
    /// <summary>
    /// Scrive la versione nel file di configurazione del progetto.
    /// </summary>
    /// <param name="version">Stringa semver da scrivere (es. "22.14.0").</param>
    /// <param name="format">Formato file: nvmrc | node-version.</param>
    /// <param name="directory">Directory di destinazione (default: cwd).</param>
    Task PinAsync(string version, PinFormat format, string directory);
}

public enum PinFormat { Nvmrc, NodeVersion }
```

**`VersionPinService`:**

```csharp
public class VersionPinService(IFileSystemService fs, IPathService paths) : IVersionPinService
{
    public async Task PinAsync(string version, PinFormat format, string directory)
    {
        var filename = format == PinFormat.NodeVersion ? ".node-version" : ".nvmrc";
        var target   = Path.Combine(directory, filename);
        await fs.WriteAllTextAsync(target, version + Environment.NewLine);
    }
}
```

**`PinCommand`** (pattern analogo a `UseCommand`):

```csharp
public class PinCommand : Command
{
    public PinCommand(IVersionManager vm, IInstallationsRepository repo,
                      IVersionPinService pin) : base("pin", "Scrive la versione nel file di progetto")
    {
        var aliasOpt    = new Option<string?>("--alias");
        var formatOpt   = new Option<string>("--format", () => "nvmrc");
        var dirOpt      = new Option<string?>("--directory");

        this.SetHandler(async (alias, format, dir) =>
        {
            var installation = alias != null
                ? await repo.GetByAliasAsync(alias)
                : await vm.GetActiveInstallationAsync();

            if (installation is null) throw new KnotVMHintException(...);

            var pinFormat = format == "node-version" ? PinFormat.NodeVersion : PinFormat.Nvmrc;
            var directory = dir ?? Directory.GetCurrentDirectory();
            await pin.PinAsync(installation.Version, pinFormat, directory);

            AnsiConsole.MarkupLine($"[green]Pinned[/] {installation.Version} → {filename}");
        }, aliasOpt, formatOpt, dirOpt);
    }
}
```

**Registrazione DI** in `ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IVersionPinService, VersionPinService>();
services.AddSingleton<PinCommand>();
```

**Error codes da aggiungere** in `KnotErrorCode.cs`:

```csharp
PinWriteFailed = 2005,   // impossibile scrivere il file di pin
```

**Completamento shell** — aggiungere `pin` all'elenco comandi nei generator
(`BashCompletionGenerator`, `ZshCompletionGenerator`, ecc.).

---

## 2. `knot info`

### Descrizione

Mostra i metadati completi di un'installazione: alias, versione semver, percorso
su disco, versione npm inclusa, dimensione della cartella, data di installazione.

```bash
knot info lts
knot info 22.14.0
knot info          # mostra la versione attiva
```

Output esempio:

```
╭──────────────────────────────────────────╮
│  lts (attiva)                            │
│  Node.js  22.14.0                        │
│  npm       10.9.2                        │
│  Path      ~/.local/share/node-local/    │
│            versions/lts                  │
│  Dimensione  67.3 MB                     │
│  Installata  2025-01-15                  │
╰──────────────────────────────────────────╯
```

### Implementazione

**Nuovi file:**

| File | Ruolo |
|------|-------|
| `src/KnotVM.CLI/Commands/InfoCommand.cs` | Comando CLI |
| `src/KnotVM.Core/Models/InstallationInfo.cs` | Record dati |
| `src/KnotVM.Core/Interfaces/IInstallationInfoService.cs` | Contratto |
| `src/KnotVM.Infrastructure/Services/InstallationInfoService.cs` | Implementazione |
| `tests/KnotVM.Tests/CLI/InfoCommandTests.cs` | Test |

**`InstallationInfo` record:**

```csharp
public record InstallationInfo(
    string  Alias,
    string  NodeVersion,
    string  NpmVersion,
    string  Path,
    long    DiskUsageBytes,
    DateTimeOffset InstalledAt,
    bool    IsActive
);
```

**`IInstallationInfoService`:**

```csharp
public interface IInstallationInfoService
{
    Task<InstallationInfo> GetInfoAsync(string aliasOrVersion);
}
```

**`InstallationInfoService`** — usa i servizi già esistenti:

```csharp
public class InstallationInfoService(
    IInstallationsRepository repo,
    IProcessRunner           proc,
    IFileSystemService       fs,
    IPathService             paths) : IInstallationInfoService
{
    public async Task<InstallationInfo> GetInfoAsync(string aliasOrVersion)
    {
        var inst = await repo.GetByAliasAsync(aliasOrVersion)
                   ?? throw new KnotVMException(KnotErrorCode.InstallationNotFound, ...);

        var npmVersion  = await proc.RunAsync(npmPath, "--version");
        var diskUsage   = fs.GetDirectorySize(inst.Path);
        var installedAt = fs.GetDirectoryCreationTime(inst.Path);

        return new InstallationInfo(inst.Alias, inst.Version, npmVersion.Trim(),
                                    inst.Path, diskUsage, installedAt, inst.Use);
    }
}
```

Due metodi da aggiungere a `IFileSystemService`:

```csharp
long           GetDirectorySize(string path);
DateTimeOffset GetDirectoryCreationTime(string path);
```

**`InfoCommand`** — usa `Spectre.Console` per il pannello:

```csharp
var panel = new Panel(...)
    .Header(info.IsActive ? $"[green]{info.Alias} (attiva)[/]" : info.Alias)
    .BorderColor(Color.Blue);
AnsiConsole.Write(panel);
```

---

## 3. `knot doctor`

### Descrizione

Diagnostica lo stato dell'ambiente KnotVM ed elenca i problemi trovati con
suggerimenti di risoluzione. Utile per troubleshooting e onboarding.

```bash
knot doctor
knot doctor --fix       # tenta la riparazione automatica dove possibile
```

Output esempio:

```
[✓] KNOT_HOME: ~/.local/share/node-local
[✓] Binari di sistema: node, npm, npx accessibili in PATH
[✓] Versione attiva: lts → 22.14.0
[✗] Proxy desincronizzati: mancano 2 proxy in bin/
    → Esegui: knot sync
[✓] Connettività nodejs.org: OK
[✓] Cache: 3 artifact, 234 MB
[!] Template proxy mancante: bash.template
    → Reinstalla KnotVM con ./install.sh --force
```

### Check da eseguire

| Check | Servizio usato | Fix automatico |
|-------|---------------|---------------|
| KNOT_HOME esiste ed è scrivibile | `IPathService`, `IFileSystemService` | Crea la directory |
| Versione attiva valida (settings.txt non corrotto) | `IVersionManager`, `IInstallationsRepository` | — |
| Proxy in sync con le installazioni | `ISyncService` | `--fix` esegue sync |
| Template proxy presenti | `IFileSystemService` | — |
| Connettività nodejs.org | `IRemoteVersionService` | — |
| Conflitti PATH (altra installazione node globale) | `IProcessRunner` (`which node`) | — |
| Lock file orfani in `locks/` | `ILockManager` | `--fix` li rimuove |
| Versione .NET runtime corretta | `Environment.Version` | — |

### Implementazione

**Nuovi file:**

| File | Ruolo |
|------|-------|
| `src/KnotVM.CLI/Commands/DoctorCommand.cs` | Comando CLI |
| `src/KnotVM.Core/Interfaces/IDoctorService.cs` | Contratto |
| `src/KnotVM.Core/Models/DoctorCheck.cs` | Risultato singolo check |
| `src/KnotVM.Infrastructure/Services/DoctorService.cs` | Implementazione |

**`DoctorCheck` record:**

```csharp
public record DoctorCheck(
    string  Name,
    bool    Passed,
    bool    IsWarning,      // true = avviso non bloccante
    string? Detail,
    string? Suggestion,
    bool    CanAutoFix
);
```

**`IDoctorService`:**

```csharp
public interface IDoctorService
{
    Task<IReadOnlyList<DoctorCheck>> RunAllChecksAsync(CancellationToken ct = default);
    Task<bool> TryAutoFixAsync(DoctorCheck check, CancellationToken ct = default);
}
```

**Rendering** in `DoctorCommand` con Spectre.Console:

```csharp
foreach (var check in checks)
{
    var icon   = check.Passed ? "[green]✓[/]" : (check.IsWarning ? "[yellow]![/]" : "[red]✗[/]");
    var detail = check.Detail is not null ? $"\n    {check.Detail}" : string.Empty;
    AnsiConsole.MarkupLine($"[{icon}] {check.Name}{detail}");
    if (!check.Passed && check.Suggestion is not null)
        AnsiConsole.MarkupLine($"    [dim]→ {check.Suggestion}[/]");
}
```

Exit code: `0` se tutti i check passano, `1` se almeno uno fallisce (non warning).
Questo permette l'uso in CI: `knot doctor || exit 1`.

---

## 4. Alpine / musl Linux support

### Descrizione

Aggiunge il supporto per distribuzioni Linux basate su musl libc
(Alpine Linux, Wolfi, ecc.), attualmente escluse dalla Support Matrix.

Node.js distribuisce artifact separati per musl:
`node-v22.14.0-linux-x64-musl.tar.gz`

### Rilevamento musl

Il rilevamento deve avvenire a runtime in `PlatformService` o in un nuovo
`ILibcDetector`:

```csharp
public interface ILibcDetector
{
    LibcType Detect();
}

public enum LibcType { Glibc, Musl, Unknown }
```

**Strategia di rilevamento** (in ordine di affidabilità):

```csharp
// 1. Controlla /proc/version (Alpine scrive "musl")
// 2. Prova ldd --version e cerca "musl" nell'output
// 3. Controlla esistenza /lib/libc.musl-*.so.1
// 4. Fallback: glibc
```

**Modifica `HostOs`** — aggiungere valore:

```csharp
public enum HostOs { Unknown = 0, Windows = 1, Linux = 2, MacOS = 3, LinuxMusl = 4 }
```

**Modifica `INodeArtifactResolver`** — la risoluzione dell'artifact deve tener
conto di `LinuxMusl`:

```csharp
// Prima
"linux-x64" => "node-{version}-linux-x64.tar.gz"

// Dopo
"linux-x64-musl" => "node-{version}-linux-x64-musl.tar.gz"
```

**Modifica `IPlatformService`** — `GetCurrentOs()` restituisce `LinuxMusl`
se il detector lo rileva su Linux.

**Nota sulle URL**: nodejs.org pubblica musl artifact solo dalla v18.x in poi.
Aggiungere validazione con `KnotErrorCode.ArtifactNotAvailable` per versioni
precedenti su musl.

**Nuovi file:**

| File | Ruolo |
|------|-------|
| `src/KnotVM.Core/Interfaces/ILibcDetector.cs` | Contratto |
| `src/KnotVM.Infrastructure/Services/LibcDetectorService.cs` | Implementazione |
| `tests/KnotVM.Tests/Infrastructure/LibcDetectorServiceTests.cs` | Test |

---

## 5. `knot use --default`

### Descrizione

Imposta una versione di fallback globale che viene usata quando nessuna versione
è stata esplicitamente attivata via `knot use`. Utile in script CI e ambienti
senza un `.nvmrc` di progetto.

```bash
knot use lts --default      # imposta "lts" come default globale
knot use --show-default     # mostra il default corrente
```

### Differenza con la versione attiva

| Concetto | File | Semantica |
|----------|------|-----------|
| Versione attiva | `settings.txt` | Impostata dall'utente con `knot use` |
| Versione default | `default.txt` | Fallback se settings.txt mancante o vuoto |

### Implementazione

**Modifica `IVersionManager`:**

```csharp
Task<string?> GetDefaultAliasAsync();
Task SetDefaultAliasAsync(string alias);
```

**`VersionManager`** — aggiunge lettura/scrittura di `default.txt`
(percorso via `IPathService`).

**Modifica `Configuration`:**

```csharp
public string DefaultFile => Path.Combine(AppDataPath, "default.txt");
```

**Logica di risoluzione** nella catena di boot del CLI: se `settings.txt` è
assente o vuoto, legge `default.txt` e usa quel valore come fallback prima
di sollevare `InstallationNotFound`.

**Modifica `IPathService`** — espone `DefaultFilePath`.

---

## 6. Fish shell proxy (first-class)

### Descrizione

Aggiunge proxy Fish nativi (`.fish`) al posto dei wrapper bash che Fish esegue
in una subshell separata. Il README rimanda questa feature a V2.

Gli script Fish non supportano la sintassi bash (`#!/usr/bin/env bash`),
quindi i proxy attuali non funzionano correttamente su Fish in modalità isolata.

### Proxy Fish nativo

Template `templates/proxy.fish.template`:

```fish
#!/usr/bin/env fish
# KnotVM proxy — {{BINARY_NAME}}
set -x PATH "{{NODE_BIN_DIR}}" $PATH
exec {{BINARY_NAME}} $argv
```

### Implementazione

**Modifica `IProxyGeneratorService`** — aggiungere `Fish` come `ProxyShell`:

```csharp
public enum ProxyShell { Bash, Zsh, Fish, PowerShell, Cmd }
```

**`ProxyGeneratorService`** — selezione del template in base alla shell
rilevata o specificata:

```csharp
var templateName = shell switch {
    ProxyShell.Fish       => "proxy.fish.template",
    ProxyShell.PowerShell => "proxy.ps1.template",
    ProxyShell.Cmd        => "proxy.cmd.template",
    _                     => "proxy.sh.template",
};
```

**Rilevamento shell Fish** in `IPlatformService`:

```csharp
// Legge $SHELL o il processo parent
bool IsFishShell();
```

**Percorso proxy Fish** — i proxy vengono installati in
`~/.config/fish/functions/` con estensione `.fish`, seguendo le convenzioni Fish.

**`SyncService`** — aggiunge sincronizzazione verso la directory Fish functions
quando Fish è rilevato.

**Nuovi file:**

| File | Ruolo |
|------|-------|
| `templates/proxy.fish.template` | Template proxy Fish |
| `tests/KnotVM.Tests/Infrastructure/FishProxyGeneratorTests.cs` | Test |

---

## Priorità suggerita

| # | Feature | Complessità | Impatto utente |
|---|---------|-------------|----------------|
| 1 | `knot pin` | Bassa | Alto — uso quotidiano |
| 2 | `knot info` | Bassa | Medio — troubleshooting |
| 3 | `knot doctor` | Media | Alto — onboarding e CI |
| 4 | Alpine/musl | Media | Medio — target specifico |
| 5 | `--default` | Bassa | Medio — CI/automazione |
| 6 | Fish proxy | Media | Basso — nicchia |

---

## Convenzioni da rispettare

- **DI**: registrare ogni nuovo servizio in `ServiceCollectionExtensions.cs` come `Singleton`.
- **Error codes**: aggiungere nuovi codici in `KnotErrorCode.cs` rispettando i range esistenti.
- **Test**: ogni nuovo servizio deve avere un file `*Tests.cs` corrispondente in `tests/`.
- **Completion**: aggiungere ogni nuovo comando ai 4 generator di shell completion.
- **Exit code 0** solo in caso di successo; usare `KnotVMException` con codice appropriato altrimenti.
