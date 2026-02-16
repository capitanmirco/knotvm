# KnotVM

Gestore versioni Node.js cross-platform in C#/.NET 8, evoluzione di `node-local`.

## Cosa Offre

- Installazione e gestione di piu versioni Node.js con alias.
- Switch rapido della versione attiva (`knot use <alias>`).
- Esecuzione isolata di comandi (`knot run "<cmd>" --with-version <alias>`).
- Cache artifact (`cache/`) per evitare download ripetuti.
- Sync proxy (`knot sync`) con naming isolato `nlocal-*`.

## Installazione

### Windows (PowerShell)

```powershell
# Installazione da release
.\install.ps1

# Build da sorgenti locali
.\install.ps1 -Dev

# Reinstallazione forzata
.\install.ps1 -Force
```

`install.ps1` gestisce automaticamente la migrazione legacy da `node-local` (solo Windows):

- rileva una precedente installazione;
- riusa `versions/` e `cache/` (incluso `%APPDATA%\node-local\cache`);
- preserva alias/versione attiva;
- rimuove artefatti e PATH legacy non necessari.

### Linux/macOS (Bash)

```bash
# Installazione da release
./install.sh

# Build da sorgenti locali
./install.sh --dev

# Reinstallazione forzata
./install.sh --force
```

Su Linux/macOS non viene eseguita migrazione legacy da `node-local` (tool storico Windows-only): l'installazione procede normalmente.

Dopo l'installazione, riavvia il terminale e verifica:

```bash
knot --help
knot version
```

## Aggiornamento E Disinstallazione

### Aggiornamento

```powershell
# Windows
.\update.ps1
.\update.ps1 -Force

# Linux/macOS
./update.sh
./update.sh --force
```

### Disinstallazione

```powershell
# Windows
.\uninstall.ps1
.\uninstall.ps1 -KeepData
.\uninstall.ps1 -Yes

# Linux/macOS
./uninstall.sh
./uninstall.sh --keep-data
./uninstall.sh --yes
```

## Comandi CLI

- `knot list [--path|-p]`
- `knot list-remote [--lts] [--all] [--limit <n>]`
- `knot install <version> [--alias <name>] [--latest] [--latest-lts]`
- `knot use <installation>`
- `knot remove <installation> [--force]`
- `knot rename --from <old> --to <new>`
- `knot run "<command>" --with-version <installation>`
- `knot sync [--force]`
- `knot cache --list | --clear | --clean`
- `knot version`

## Percorsi Dati

`KNOT_HOME` puo essere impostata per personalizzare il path base.

Se non impostata:

- Windows: `%APPDATA%\node-local`
- Linux: `$HOME/.local/share/node-local`
- macOS: `$HOME/Library/Application Support/node-local`

Struttura principale:

- `bin/` binari e proxy
- `versions/` installazioni Node.js
- `cache/` artifact scaricati
- `templates/` template proxy
- `locks/` lock files
- `settings.txt` alias attivo

## Build E Test

```bash
# Build soluzione
dotnet build --nologo

# Test
dotnet test --no-build --nologo -v minimal

# Esecuzione locale CLI
dotnet run --project src/KnotVM.CLI -- --help
```

Stato validato corrente: `98/98` test passati.

## Architettura

```text
KnotVM/
├── src/
│   ├── KnotVM.CLI/            # Entry point e comandi
│   ├── KnotVM.Core/           # Modelli dominio, interfacce, errori
│   └── KnotVM.Infrastructure/ # Implementazioni filesystem/network/process
├── templates/                 # Template proxy multi-OS
└── tests/                     # Test unit/integration (xUnit + Moq)
```

Stack principale:

- .NET 8
- System.CommandLine
- Spectre.Console
- Microsoft.Extensions.DependencyInjection

## Support Matrix

OS:

- Windows 10/11 (x64, arm64)
- Linux glibc (x64, arm64)
- macOS (x64 Intel, arm64 Apple Silicon)
- Linux musl/Alpine: non supportato in V1

Shell:

- PowerShell (Windows)
- CMD (Windows)
- Bash (Linux/macOS)
- Zsh (Linux/macOS)

## Documentazione

- Migrazione e decisioni architetturali: [`docs/CSHARP_MIGRATION.md`](docs/CSHARP_MIGRATION.md)
- Analisi ottimizzazioni: [`docs/CODE_OPTIMIZATION_ANALYSIS.md`](docs/CODE_OPTIMIZATION_ANALYSIS.md)
- Prompt/step di migrazione: [`docs/docs-migration/`](docs/docs-migration/)

## License

MIT - vedi [`LICENSE`](LICENSE).
