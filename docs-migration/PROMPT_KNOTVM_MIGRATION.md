# Prompt Master: Porting Cross-Platform `node-local` -> `knot` (.NET 8)

## Uso Obbligatorio di Questo File
1. Questo file e la fonte primaria e vincolante per tutta la migrazione.
2. Ogni sotto-prompt operativo deve essere eseguito solo dopo aver riletto questo file.
3. Se un sotto-prompt confligge con questo file, prevale sempre questo file.
4. Se una decisione non e specificata, l'agent deve:
   - proporre 2-3 opzioni con tradeoff,
   - scegliere la piu sicura per parity e stabilita,
   - registrare la decisione in `docs/CSHARP_MIGRATION.md` (Decision Log).

## Missione
Portare `node-local` (PowerShell, Windows-only) in `knot` (C#, .NET 8) con supporto reale a:
- Windows
- Linux
- macOS

Obiettivo v1:
- parity funzionale dei comandi core,
- isolamento installazioni per alias,
- gestione versione attiva,
- sync comandi globali,
- script lifecycle cross-platform,
- alta affidabilita e riduzione regressioni.

## Stato di Partenza Verificato (Source of Truth)
Questa repository e la codebase sorgente PowerShell (`node-local`).

File runtime principali da cui derivare il comportamento:
- `node-local.ps1` (router comandi)
- `lib/argparser.ps1` (parsing flag)
- `lib/core.ps1`, `lib/installation.ps1`, `lib/versions.ps1`, `lib/sync.ps1`, `lib/run.ps1`, `lib/remote.ps1`, `lib/cache.ps1`, `lib/security.ps1`, `lib/modes.ps1`
- `lib/classes/*.ps1` (`ConfigurationClass`, `Installations`, `InstallationClass`, `Network`, `Proxies`, `Proxy`)
- `templates/*.template` (5 template attuali, orientati soprattutto a Windows)
- `install.ps1`, `update.ps1`, `uninstall.ps1`

Nota: `upgrade` e `downgrade` sono presenti ma non instradati nel runtime principale.

## Scope V1 e Non-Scope
### Scope V1 obbligatorio
- `list`, `list-remote`, `install`, `use`, `remove`, `rename`, `run`, `sync`, `cache`, `help`, `version`
- mode `isolated` e `override`
- lifecycle install/update/uninstall su Windows/Linux/macOS
- test automatici + CI matrix multi-OS
- comportamento funzionale `isolated/override` uguale su tutti gli OS (Windows, Linux, macOS)

### Non-scope V1 (salvo richiesta esplicita)
- flussi interattivi complessi `upgrade`/`downgrade`
- supporto shell non POSIX avanzate (fish/tcsh) come prima classe
- firma GPG completa delle release Node (si usa SHA256 ufficiale; GPG puo essere V2)
- distro Linux musl/Alpine (supporto rinviato a V2)

## Vincoli Architetturali
1. Linguaggio/runtime: C# + .NET 8.
2. Persistenza stato: filesystem (no DB).
3. Nessun requisito admin per installazione utente.
4. Nessun symlink/junction come meccanismo principale.
5. Output user-facing in italiano.
6. Encoding stato: UTF-8 senza BOM.

## Matrice Supporto OS/Arch (V1)
### OS supportati
- Windows 10/11 e Windows Server moderni
- Linux glibc (Ubuntu/Debian/Fedora e compatibili)
- macOS 13+

Vincolo esplicito Linux V1:
- supporto solo distro glibc
- Alpine/musl non supportate in V1 (errore esplicito con hint)

### Arch supportate (minimo)
- Windows: x64, arm64, x86
- Linux: x64, arm64
- macOS: x64, arm64

Se arch non supportata: errore esplicito e suggerimento operativo.

## Path Strategy Cross-Platform
Supporta override tramite env var `KNOT_HOME` (utile per test/CI e ambienti avanzati).
Se `KNOT_HOME` non e impostata, usa default OS-specific:

- Windows: `%APPDATA%\\node-local`
- Linux: `$HOME/.local/share/node-local`
- macOS: `$HOME/Library/Application Support/node-local`

Sotto-struttura comune:
- `<Base>/bin`
- `<Base>/versions`
- `<Base>/cache`
- `<Base>/settings.txt`
- `<Base>/mode.txt`
- `<Base>/templates`
- `<Base>/locks`

Compatibilita retroattiva:
- su Windows mantieni piena compatibilita con dati esistenti `%APPDATA%\\node-local`.

## Shell Ufficiali Supportate (V1)
- Windows: PowerShell, CMD
- Linux: bash, zsh
- macOS: bash, zsh

Qualsiasi shell diversa da quelle sopra e best-effort, non garantita in V1.

## Contratti Dati/Stato
### `settings.txt`
- contiene alias attivo (nome installazione), trim + rimozione BOM in lettura.
- scrittura sempre UTF-8 no BOM.

### `mode.txt`
- valori ammessi: `isolated`, `override`.
- fallback se mancante/valore invalido: `isolated`.

### Alias
- regex: `^[a-zA-Z0-9_-]+$`
- lunghezza: 1..50
- case-insensitive check per collisioni filesystem-sensitive/insensitive
- riservati da bloccare: `node`, `npm`, `npx`, `knot`

## Architettura C# Target (consigliata)
Progetti:
- `KnotVM.CLI`
- `KnotVM.Core`
- `KnotVM.Infrastructure`
- `KnotVM.Tests`

### Core
- Modelli: `Installation`, `RemoteVersion`, `InstallationPrepareResult`, `ProxyGenerationResult`, `CacheEntry`
- Enum: `OperatingMode`, `ProxyType`, `ShellType`, `HostOs`
- Interfacce:
  - `IPlatformService`
  - `IPathService`
  - `IInstallationManager`
  - `IVersionManager`
  - `IProxyManager`
  - `IRemoteApiClient`
  - `IDownloadService`
  - `ISecurityService`
  - `ICacheService`
  - `IFileSystemService`
  - `IProcessRunner`

### Infrastructure
- Implementazioni concrete OS-aware, senza if/switch sparsi nel dominio.
- Strategia raccomandata:
  - `PlatformService` per detection OS/arch/shell default.
  - `ArtifactResolver` per URL/pattern artifact Node per OS/arch.
  - `ArchiveExtractor` con supporto zip e tar.*.

### CLI
- Parser robusto (`System.CommandLine` o equivalente).
- Handler piccoli che delegano ai service.
- Error handling centralizzato con exit code standardizzati.

## Comandi e Parity (Target `knot`)
### Comandi obbligatori
- `knot list`
- `knot list-remote [--lts] [--all] [--limit <n>]`
- `knot install <version> [--alias <name>] [--latest] [--latest-lts]`
- `knot use <installation>`
- `knot remove <installation>`
- `knot rename --from <old> --to <new>`
- `knot run "<comando completo>" --with-version <installation>`
- `knot sync [--force]`
- `knot cache --list | --clear | --clean`
- `knot version` e `knot --version`
- `knot help` e `knot --help`
- `knot --isolated` e `knot --override`

### Parsing regole
- `install`: mutua esclusione tra `<version>`, `--latest`, `--latest-lts`.
- `run`: comando completo + `--with-version` obbligatorio.
- `cache`: una sola azione tra `--list|--clear|--clean`.

## Artifact Node per OS
### Principio
Non hardcodare un solo pattern cieco. Definire candidate list per OS/arch e tentare in ordine.

### Candidate tipici
- Windows: `node-v{version}-win-{arch}.zip`
- Linux: `node-v{version}-linux-{arch}.tar.xz`
- macOS: `node-v{version}-darwin-{arch}.tar.gz` (fallback a `.tar.xz` se necessario)

Base URL:
- `https://nodejs.org/dist/v{version}/`

Checksum:
- `SHASUMS256.txt` nella stessa cartella versione.

## Sicurezza e Integrita
1. Download con retry esponenziale (3 tentativi minimi).
2. Verifica SHA256 obbligatoria per artifact installati.
3. Verifica integrita archivio (zip/tar).
4. Cache salvataggio solo dopo verifica riuscita.
5. In caso di checksum mismatch: errore bloccante.

## Estrazione/Installazione OS-aware
### Windows
- formato zip
- eseguibile node: `node.exe`
- comandi PM: spesso `.cmd` in root installazione

### Linux/macOS
- formato tar.*
- eseguibile node: `bin/node`
- comandi PM: `bin/npm`, `bin/npx`, ecc.
- preserva permessi executable durante estrazione e copia.

## Risoluzione Comandi (`run`) per OS
### Windows ordine
1. `<versionPath>\\<cmd>.exe`
2. `<versionPath>\\<cmd>.cmd`
3. `<versionPath>\\node_modules\\.bin\\<cmd>.cmd`
4. `<versionPath>\\node_modules\\.bin\\<cmd>`

### Linux/macOS ordine
1. `<versionPath>/bin/<cmd>`
2. `<versionPath>/<cmd>`
3. `<versionPath>/lib/node_modules/.bin/<cmd>`
4. `<versionPath>/node_modules/.bin/<cmd>`

Per tutti gli OS:
- override temporaneo env (`PATH`, `NODE_PATH`) in `try/finally`
- restore garantito
- exit code propagato

## Strategia Proxy Cross-Platform
### Obiettivo
Mantenere modello template-driven evitando logica ad hoc fragile.

### Template richiesti
Mantieni i 5 template esistenti e introduci template Unix dedicati:
- `generic-proxy.sh.template`
- `package-manager.sh.template`

### Regole generazione
- Windows:
  - proxy `.cmd` per command e package manager
  - shim `.exe` per `node` dove necessario
  - encoding `.cmd`: ASCII
- Linux/macOS:
  - script senza estensione in `<Base>/bin`
  - shebang `#!/usr/bin/env bash`
  - `chmod +x`
  - encoding UTF-8 no BOM, line ending LF

### Mode naming
- `isolated`: prefisso `nlocal-`
- `override`: nomi standard (`node`, `npm`, `npx`, ...)

Vincolo di parity cross-OS:
- su Linux/macOS la semantica di `isolated` e `override` deve essere identica a Windows.

### Sync semantics
- `sync` normale: rigenera dinamici
- `sync --force`: rigenera tutto

## Package Manager e Isolamento
Package manager minimi:
- npm, yarn, yarnpkg, pnpm, ni, nun, nup, bun

Regole:
1. rileva install/remove globale e lancia auto-sync post-comando se successo.
2. preserva exit code del package manager.
3. non bloccare installazione se PM opzionale non presente nella versione.

## Lifecycle Scripts per OS
### Windows
- `install.ps1`, `update.ps1`, `uninstall.ps1`
- update PATH user-scope in modo idempotente

### Linux/macOS
- `install.sh`, `update.sh`, `uninstall.sh`
- update PATH via marker block su file shell rc (idempotente):
  - bash: `~/.bashrc`
  - zsh: `~/.zshrc`
- no modifica system-wide

Regola di sicurezza:
- uninstall deve offrire scelta: rimuovere solo bin/config runtime o anche versions/cache.

## Messaggi e UX
- lingua italiana per messaggi utente
- livelli: info/warn/error/success consistenti
- hint operativi sempre presenti per errori comuni

## Preflight Obbligatorio (all'avvio CLI e nei lifecycle script)
Prima di operazioni mutanti (`install`, `use`, `sync`, `remove`, `rename`, `update`) esegui questi check:
1. OS supportato (`Windows|Linux|macOS`).
2. Arch supportata per l'OS corrente.
3. Linux: verifica libc `glibc`; se `musl`, blocca con errore esplicito.
4. Verifica permessi di lettura/scrittura su home path knot.
5. Verifica integrita minima file stato (`settings.txt`, `mode.txt`) e fallback sicuri.
6. Verifica che i path con spazi siano gestiti con quoting corretto (soprattutto macOS).

## Catalogo Errori Standardizzati (OS-Specific)
Formato obbligatorio messaggio errore:
- `CodiceErrore: Messaggio umano`
- `Hint: Azione consigliata`

Codici minimi obbligatori:
1. `KNOT-OS-001`: sistema operativo non supportato.
2. `KNOT-OS-002`: architettura non supportata su OS corrente.
3. `KNOT-LNX-001`: Linux musl/Alpine non supportato in V1 (richiesta glibc).
4. `KNOT-SHELL-001`: shell non ufficialmente supportata in V1.
5. `KNOT-PATH-001`: impossibile determinare/creare path base.
6. `KNOT-PERM-001`: permessi insufficienti su cartella/file richiesti.
7. `KNOT-CFG-001`: file `mode.txt` invalido (fallback a `isolated` + warning).
8. `KNOT-CFG-002`: file `settings.txt` corrotto/non leggibile.
9. `KNOT-ART-001`: artifact Node non disponibile per versione/OS/arch.
10. `KNOT-DL-001`: download fallito dopo retry.
11. `KNOT-SEC-001`: checksum SHA256 non corrispondente (errore bloccante).
12. `KNOT-ARC-001`: archivio corrotto/non estraibile.
13. `KNOT-INS-001`: installazione/alias non trovata.
14. `KNOT-INS-002`: alias non valido o gia esistente.
15. `KNOT-RUN-001`: comando non trovato nella versione target.
16. `KNOT-PROXY-001`: generazione proxy fallita.
17. `KNOT-SYNC-001`: sync fallita per stato incoerente.
18. `KNOT-LOCK-001`: lock attivo/scaduto non acquisibile.

Regole UX errore:
1. Ogni errore deve includere codice, messaggio in italiano e hint.
2. Gli errori bloccanti devono avere exit code diverso da 0.
3. Per warning non bloccanti (es. fallback mode) usare codice e continuare in sicurezza.

## Mappa Exit Code Standard (obbligatoria)
Regole generali:
1. `0` = successo (anche con warning recoverable).
2. Ogni errore bloccante deve avere un exit code stabile e deterministico.
3. Nessuna collisione tra codici exit definiti sotto.
4. I test devono verificare codice errore e relativo exit code.

Mappa:
- `KNOT-OS-001` -> `10`
- `KNOT-OS-002` -> `11`
- `KNOT-LNX-001` -> `12`
- `KNOT-SHELL-001` -> `13`
- `KNOT-PATH-001` -> `20`
- `KNOT-PERM-001` -> `21`
- `KNOT-CFG-001` -> `0` (warning con fallback a `isolated`)
- `KNOT-CFG-002` -> `23`
- `KNOT-ART-001` -> `30`
- `KNOT-DL-001` -> `31`
- `KNOT-SEC-001` -> `32`
- `KNOT-ARC-001` -> `33`
- `KNOT-INS-001` -> `40`
- `KNOT-INS-002` -> `41`
- `KNOT-RUN-001` -> `42`
- `KNOT-PROXY-001` -> `50`
- `KNOT-SYNC-001` -> `51`
- `KNOT-LOCK-001` -> `60`
- `KNOT-GEN-001` (errore inatteso non classificato) -> `99`

## Guardrail Aggiuntivi (Raccomandati)
1. Lock file con timeout e recupero lock stantio (evita deadlock).
2. Aggiornamento PATH idempotente con marker block e rollback su errore.
3. Operazioni destructive (`remove`, `cache --clear`, uninstall) con conferma esplicita o `--yes`.
4. Nessuna cancellazione ricorsiva fuori da `KNOT_HOME`/path base atteso.
5. Logging diagnostico opzionale (`--verbose`) separato dai messaggi user-facing.

## Strategia Review/Ottimizzazione (obbligatoria)
1. Fine di ogni step: review mirata su file toccati + dipendenze dirette.
2. Ogni 2-3 step: review ampia cross-modulo.
3. Fine migrazione: review globale con ottimizzazioni safe e testate.
4. Priorita: parity e stabilita prima di refactor aggressivi.

## Test Strategy Multi-OS
### Unit test
- parser e validazioni
- path resolution OS-aware
- alias validation
- artifact resolution OS/arch
- checksum parsing e compare
- template rendering e naming mode-specific

### Integration test
- install/use/list/sync/run/cache per OS
- rename/remove casi attivi/non attivi
- mode switch e rigenerazione proxy
- lifecycle scripts idempotenti

### CI matrix minima
- `windows-latest`
- `ubuntu-latest`
- `macos-latest`

Ogni job:
- `dotnet build`
- `dotnet test`
- smoke CLI (`--help`, `list`, `version`)

## Definition of Done
Completato solo se:
1. Comandi V1 parity implementati su tutti gli OS target.
2. Test unit/integration verdi nella matrix CI.
3. Lifecycle scripts funzionanti e idempotenti su ogni OS.
4. Documentazione aggiornata (`docs/CSHARP_MIGRATION.md`) con:
   - mapping PS -> C#
   - decision log
   - gap v2 espliciti

## Formato Output Finale Richiesto all'Agent
Rispondi sempre con:
1. File modificati
2. Comportamenti parity coperti
3. Differenze OS-specific implementate
4. Gap rinviati a v2
5. Rischi residui
6. Prossimi step consigliati
