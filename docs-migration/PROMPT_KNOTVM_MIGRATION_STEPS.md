# Sotto-prompt Sequenziali per Migrazione `knot` Cross-Platform

Usa questi prompt in ordine (`1 -> 12`).
Ogni prompt va eseguito in sessione separata, ma sempre con riferimento al master prompt.

## Regole Globali
1. Riferimento obbligatorio a `PROMPT_KNOTVM_MIGRATION.md` per ogni step.
2. Se emerge conflitto, prevale sempre il master prompt.
3. A fine step applica review mirata e test rilevanti.
4. A fine step 4, 8, 11 esegui review ampia cross-modulo.

## Vincoli Cross-Platform Confermati (obbligatori)
1. Path di default OS-native:
   - Windows: `%APPDATA%\\node-local`
   - Linux: `$HOME/.local/share/node-local`
   - macOS: `$HOME/Library/Application Support/node-local`
2. Linux V1: supporto solo distro glibc; Alpine/musl fuori scope V1.
3. Shell ufficiali V1:
   - Windows: PowerShell, CMD
   - Linux/macOS: bash, zsh
4. Modalita `isolated` e `override`: comportamento identico su Windows/Linux/macOS.

## Error Handling Standardizzato (obbligatorio)
1. Implementa codici errore `KNOT-*` come definiti nel master prompt.
2. Ogni errore utente deve avere: `codice + messaggio italiano + hint`.
3. Errori bloccanti: exit code != 0.
4. Warning/fallback non bloccanti (es. `mode.txt` invalido) devono essere tracciabili.
5. Applica la mappa exit code standard definita nel master prompt (nessuna collisione).

## Output Richiesto per Ogni Step
1. File toccati
2. Cosa e stato implementato
3. Test eseguiti e risultato
4. Gap/bloccanti
5. Decisioni registrate in `docs/CSHARP_MIGRATION.md`

## Prompt 1/12 - Baseline e Scope Cross-Platform
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- fissare baseline tecnica e scope multi-OS senza ambiguita.

Task:
1. Verifica se esiste gia una solution C# target; se assente, crea baseline minima.
2. Mappa runtime PowerShell attuale (comandi, parser, classi chiave, template).
3. Definisci support matrix OS/arch del progetto C# (Linux: glibc only in V1).
4. Crea `docs/CSHARP_MIGRATION.md` con sezioni:
   - Baseline attuale
   - Scope V1 e non-scope
   - Risk register iniziale
   - Error catalog `KNOT-*` iniziale

Checkpoint fine step:
1. Review mirata su baseline doc.
2. Nessuna assunzione non documentata.

Done:
- Baseline e scope cross-platform documentati.

## Prompt 2/12 - Fondazioni Core e Platform Abstraction
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- introdurre astrazioni OS-aware pulite e testabili.

Task:
1. Definisci modelli core (`Installation`, `RemoteVersion`, ecc.).
2. Definisci enum (`HostOs`, `OperatingMode`, `ProxyType`, `ShellType`).
3. Definisci interfacce: `IPlatformService`, `IPathService`, `IProcessRunner`, ecc.
4. Definisci configurazione centralizzata con `KNOT_HOME` override + default OS-specific.
5. Definisci modello/contratto comune per errori applicativi codificati (`KNOT-*`).
6. Definisci mapping centralizzato `KNOT-* -> exit code`.

Checkpoint fine step:
1. Review mirata su boundary Core/Infrastructure.
2. `dotnet build` pulito.

Done:
- Contratti compilano e sono OS-aware.

## Prompt 3/12 - Path, Stato e Filesystem Services
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- rendere robusta la persistenza stato su tutti gli OS.

Task:
1. Implementa path strategy (`KNOT_HOME`, default Windows/Linux/macOS).
2. Implementa lettura/scrittura BOM-safe di `settings.txt` e `mode.txt`.
3. Implementa lock file cross-platform per operazioni mutanti.
4. Implementa utility filesystem (copy, delete robusta, temp dir, chmod).
5. Implementa preflight path/permessi con errori codificati.

Checkpoint fine step:
1. Review mirata su encoding/permessi/path.
2. Test unit path e stato verdi.

Done:
- Stato e filesystem affidabili multi-OS.

## Prompt 4/12 - Remote API, Artifact Resolution, Download e Security
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- risolvere correttamente artifact Node per OS/arch e verificarli.

Task:
1. `RemoteApiClient`: parsing `index.json`, filtri lts/all/limit.
2. `ArtifactResolver`: candidate list OS/arch con fallback sicuri.
3. `DownloadService`: retry esponenziale, progress, cleanup.
4. `SecurityService`: SHASUMS256 parsing + compare SHA256 bloccante.
5. `CacheService`: list/clear/clean con semantica esplicita.
6. Implementa detection Linux musl vs glibc e blocco con errore `KNOT-LNX-001`.

Checkpoint fine step:
1. Review mirata networking/security/cache.
2. Review ampia (step 4) su coerenza con model e CLI needs.

Done:
- Artifact multi-OS scaricabili e verificati.

## Prompt 5/12 - Installazione e Discovery Multi-OS
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- installare Node e scoprire installazioni attive su ogni OS.

Task:
1. Implementa `PrepareInstallation` con validazione alias.
2. Implementa estrazione zip/tar e copy finale installazione.
3. Implementa discovery versioni installate:
   - Windows: `node.exe --version`
   - Linux/macOS: `bin/node --version`
4. Implementa `GetCurrent/SetCurrent` robusti.

Checkpoint fine step:
1. Review mirata su detection e version probing.
2. Test integration install/discovery.

Done:
- Discovery e installazione parity su tutti gli OS target.

## Prompt 6/12 - Proxy Engine Cross-Platform (Template-Driven)
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- generazione proxy coerente su Windows/Linux/macOS.

Task:
1. Mantieni template Windows esistenti (`*.cmd`, shim C#).
2. Introduci template Unix (`generic-proxy.sh.template`, `package-manager.sh.template`).
3. Implementa `sync` normale vs `sync --force`.
4. Implementa mode naming `isolated/override` con semantica identica a Windows anche su Linux/macOS.
5. Implementa chmod +x per proxy Unix.
6. Standardizza errori proxy/sync (`KNOT-PROXY-001`, `KNOT-SYNC-001`).

Checkpoint fine step:
1. Review mirata su template rendering, encoding, permessi eseguibili.
2. Test su naming e mode.

Done:
- Proxy funzionanti su tutti gli OS target.

## Prompt 7/12 - Installation Manager, Version Manager, Policy Remove
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- chiudere le regole di gestione ciclo vita installazioni.

Task:
1. Implementa `use`, `rename`, `remove` con controlli robusti.
2. Definisci policy esplicita remove installazione attiva.
3. Aggiorna `mode.txt` e trigger `sync --force` quando necessario.

Checkpoint fine step:
1. Review mirata su invarianti stato.
2. Aggiorna decision log in docs.

Done:
- Manager completi e policy documentata.

## Prompt 8/12 - CLI Core Commands
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- rendere usabili i comandi principali.

Task:
1. Handler CLI per `list`, `list-remote`, `install`, `use`, `sync`.
2. Validazioni parser mutualmente esclusive.
3. Messaggi italiani consistenti + hint operativi.
4. Exit code chiari.
5. Formato errori CLI conforme a catalogo `KNOT-*`.

Checkpoint fine step:
1. Review mirata su UX CLI.
2. Review ampia (step 8) su coerenza end-to-end CLI/Core/Infra.

Done:
- Comandi core operativi multi-OS.

## Prompt 9/12 - CLI Estesa: run, cache, remove, rename, mode
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- completare superficie CLI parity v1.

Task:
1. Implementa `run` con resolution order OS-aware e env restore sicuro.
2. Implementa `cache --list|--clear|--clean`.
3. Completa `remove`, `rename`, `--isolated`, `--override`, `version`, `help`.
4. Copri casi errore codificati per command-not-found/config/state.

Checkpoint fine step:
1. Review mirata su run + cache + mode switch.
2. Test exit code propagation.

Done:
- CLI parity v1 completa.

## Prompt 10/12 - Lifecycle Scripts Multi-OS
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- install/update/uninstall affidabili su ogni OS.

Task:
1. Windows: `install.ps1`, `update.ps1`, `uninstall.ps1` idempotenti.
2. Linux/macOS: `install.sh`, `update.sh`, `uninstall.sh` idempotenti.
3. PATH update user-scope con marker blocks sicuri (bash/zsh ufficiali V1).
4. Uninstall con scelta preservazione dati (`versions/cache`).
5. Messaggi errore script allineati al catalogo `KNOT-*` quando applicabile.

Checkpoint fine step:
1. Review mirata su safety e rollback.
2. Smoke test script per OS.

Done:
- Lifecycle script funzionanti e sicuri.

## Prompt 11/12 - Test Matrix e CI Cross-Platform
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- validare stabilita con pipeline automatica multi-OS.

Task:
1. Completa unit + integration test suite.
2. Configura CI matrix: windows/ubuntu/macos.
3. Aggiungi smoke CLI in pipeline.
4. Introduci test anti-regressione su path/encoding/proxy.
5. Aggiungi test dedicati sui codici errore principali e su detection glibc/musl.
6. Aggiungi test dedicati di assert su mappa `KNOT-* -> exit code`.

Checkpoint fine step:
1. Review ampia (step 11) su copertura e aree scoperte.
2. Riduci flaky tests.

Done:
- CI matrix verde su OS target.

## Prompt 12/12 - Hardening Finale e Chiusura
Riferimento obbligatorio: leggi prima `PROMPT_KNOTVM_MIGRATION.md`; in caso di conflitto prevale `PROMPT_KNOTVM_MIGRATION.md`.

Obiettivo:
- chiudere migrazione con documentazione e rischio controllato.

Task:
1. Esegui:
   - `dotnet build`
   - `dotnet test`
   - smoke CLI su OS corrente
2. Aggiorna `docs/CSHARP_MIGRATION.md` con:
   - mapping definitivo PS -> C#
   - decision log completo
   - gap v2
   - checklist DoD
3. Esegui review globale codebase e solo refactor safe coperti da test.
4. Verifica finale: nessun errore utente senza codice `KNOT-*` nei flow principali.

Checkpoint fine step:
1. Nessuna regressione parity.
2. Nessun comportamento OS-specific non documentato.

Done:
- Migrazione v1 chiusa con evidenze tecniche complete.
