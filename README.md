# KnotVM

Gestore versioni Node.js cross-platform in C#/.NET 8, evoluzione di `node-local`.

## Cosa Offre

- Installazione e gestione di piu versioni Node.js con alias.
- Switch rapido della versione attiva (`knot use <alias>`).
- Esecuzione isolata di comandi (`knot run "<cmd>" --with-version <alias>`).
- Cache artifact (`cache/`) per evitare download ripetuti.
- Sync proxy (`knot sync`) con naming isolato `nlocal-*`.

## Installazione

> **NOTA IMPORTANTE**: Al momento KnotVM non ha release pubbliche su GitHub. È necessario clonare il repository e compilare da sorgenti locali usando il flag `-Dev/--dev`.

### Prerequisiti

- **.NET 8.0 SDK**: Scarica da [https://dot.net](https://dot.net)
- **Git**: Per clonare il repository

### Windows (PowerShell)

```powershell
# Clona il repository
git clone https://github.com/m-lelli/knotvm.git
cd knotvm

# OBBLIGATORIO: Build da sorgenti locali (nessuna release disponibile)
.\install.ps1 -Dev

# Reinstallazione forzata
.\install.ps1 -Dev -Force
```

**Cosa fa il flag `-Dev`?**
- Compila `KnotVM.CLI` da sorgenti locali usando `dotnet publish`
- Produce un binario self-contained per la tua architettura (x64/arm64)
- Copia `knot.exe` in `KNOT_HOME\bin`

`install.ps1` gestisce automaticamente la migrazione legacy da `node-local` (solo Windows):

- rileva una precedente installazione;
- riusa `versions/` e `cache/` (incluso `%APPDATA%\node-local\cache`);
- preserva alias/versione attiva;
- rimuove artefatti e PATH legacy non necessari.

### Linux/macOS (Bash)

```bash
# Clona il repository
git clone https://github.com/m-lelli/knotvm.git
cd knotvm

# OBBLIGATORIO: Build da sorgenti locali (nessuna release disponibile)
./install.sh --dev

# Reinstallazione forzata
./install.sh --dev --force
```

**Cosa fa il flag `--dev`?**
- Compila `KnotVM.CLI` da sorgenti locali usando `dotnet publish`
- Produce un binario self-contained per la tua architettura (x64/arm64) e OS (Linux/macOS)
- Copia `knot` in `KNOT_HOME/bin`

Su Linux/macOS non viene eseguita migrazione legacy da `node-local` (tool storico Windows-only): l'installazione procede normalmente.

> **Nota**: In futuro saranno disponibili release pre-compilate scaricabili automaticamente senza `-Dev/--dev`. Gli script sono già predisposti per questo scenario.

Dopo l'installazione, riavvia il terminale e verifica:

```bash
knot --help
knot version
```

## Aggiornamento E Disinstallazione

### Aggiornamento

> **NOTA**: Al momento, senza release pubbliche, l'aggiornamento richiede di aggiornare il repository e ricompilare:

```powershell
# Windows
cd knotvm
git pull origin develop  # o il branch che stai usando
.\install.ps1 -Dev -Force

# Linux/macOS
cd knotvm
git pull origin develop  # o il branch che stai usando
./install.sh --dev --force
```

Gli script `update.ps1` e `update.sh` sono progettati per scaricare automaticamente nuove release da GitHub, ma saranno funzionali solo quando verranno pubblicate release ufficiali.

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
- `knot install <version> [--alias <name>] [--latest] [--latest-lts] [--from-file]`
- `knot use <installation> [--auto]`
- `knot auto-detect [--directory <path>]` — rileva versione da `.nvmrc`, `.node-version` o `package.json`
- `knot remove <installation> [--force]`
- `knot rename --from <old> --to <new>`
- `knot run "<command>" --with-version <installation>`
- `knot sync [--force]`
- `knot cache --list | --clear | --clean`
- `knot version`
- `knot completion <shell>` — genera script tab-completion (`bash`, `zsh`, `powershell`, `fish`)

## Risolutore Intelligente di Versioni

`knot install` e `knot use` accettano notazioni abbreviate senza richiedere versioni semver complete:

```bash
knot install 20          # risolve a 20.x.x più recente
knot install lts         # ultima LTS corrente
knot install hydrogen    # 18.x.x (codename LTS)
knot install iron        # 20.x.x (codename LTS)
knot install jod         # 22.x.x (codename LTS)
knot install latest      # ultima versione stabile
knot install 20.11.0     # versione esatta (passthrough)
```

## Auto-Rilevamento Versione Progetto

```bash
# Usa la versione indicata in .nvmrc / .node-version / package.json
knot use --auto

# Installa la versione indicata nel file di configurazione
knot install --from-file

# Rileva esplicitamente e attiva (con supporto directory custom)
knot auto-detect
knot auto-detect --directory /path/to/project
```

Ordine di precedenza file: `.nvmrc` > `.node-version` > `package.json` (`engines.node`).

## Shell Completion

KnotVM supporta tab-completion per Bash, Zsh, PowerShell e Fish.

### PowerShell (Windows / Linux / macOS)

```powershell
# Applica al profilo corrente (permanente, richiede riavvio del terminale)
knot completion powershell >> $PROFILE

# Crea il profilo se non esiste, poi applica
New-Item -ItemType File -Path $PROFILE -Force | Out-Null
knot completion powershell >> $PROFILE

# Solo per la sessione corrente
knot completion powershell | Invoke-Expression
```

### Bash (Linux / macOS)

```bash
# Applica solo per l'utente corrente (permanente)
mkdir -p ~/.bash_completion.d
knot completion bash > ~/.bash_completion.d/knot
echo 'source ~/.bash_completion.d/knot' >> ~/.bashrc

# Per tutte le sessioni del sistema (richiede sudo)
knot completion bash | sudo tee /etc/bash_completion.d/knot

# Solo per la sessione corrente
source <(knot completion bash)
```

### Zsh (Linux / macOS)

```bash
# Applica per l'utente corrente (permanente)
mkdir -p ~/.zsh/completions
knot completion zsh > ~/.zsh/completions/_knot

# Aggiungere a ~/.zshrc (se non già presente):
echo 'fpath=(~/.zsh/completions $fpath)\nautoload -Uz compinit && compinit' >> ~/.zshrc

# Solo per la sessione corrente
source <(knot completion zsh)
```

### Fish (Linux / macOS)

```bash
# Applica per l'utente corrente (permanente)
mkdir -p ~/.config/fish/completions
knot completion fish > ~/.config/fish/completions/knot.fish
# Fish ricarica automaticamente le completion alla prossima sessione
```

Dopo aver applicato il completion, riavviare il terminale (o ricaricare il profilo) e provare:

```bash
knot <TAB>           # mostra tutti i comandi
knot use <TAB>       # mostra gli alias installati
knot install <TAB>   # mostra le versioni suggerite
```

---

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

Stato validato corrente: `339/340` test passati (1 regressione pre-esistente in analisi).

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

- PowerShell (Windows / Linux / macOS)
- CMD (Windows)
- Bash (Linux/macOS)
- Zsh (Linux/macOS)
- Fish (Linux/macOS) — completions supportate (proxy first-class rinviato a V2)

## Documentazione

- Migrazione e decisioni architetturali: [`docs/CSHARP_MIGRATION.md`](docs/CSHARP_MIGRATION.md)
- Analisi ottimizzazioni: [`docs/CODE_OPTIMIZATION_ANALYSIS.md`](docs/CODE_OPTIMIZATION_ANALYSIS.md)
- Prompt/step di migrazione: [`docs/docs-migration/`](docs/docs-migration/)

## License

MIT - vedi [`LICENSE`](LICENSE).
