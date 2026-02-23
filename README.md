# KnotVM

Cross-platform Node.js version manager written in C#/.NET 8, evolved from `node-local`.

## Features

- Install and manage multiple Node.js versions with aliases.
- Fast active version switching (`knot use <alias>`).
- Isolated command execution (`knot run "<cmd>" --with-version <alias>`).
- Artifact cache (`cache/`) to avoid repeated downloads.
- Proxy sync (`knot sync`) with isolated naming `nlocal-*`.

## Installation

> **IMPORTANT NOTE**: KnotVM does not currently have public GitHub releases. You must clone the repository and build from local sources using the `-Dev/--dev` flag.

### Prerequisites

- **.NET 8.0 SDK**: Download from [https://dot.net](https://dot.net)
- **Git**: To clone the repository

### Windows (PowerShell)

```powershell
# Clone the repository
git clone https://github.com/m-lelli/knotvm.git
cd knotvm

# REQUIRED: Build from local sources (no release available)
.\install.ps1 -Dev

# Force reinstall
.\install.ps1 -Dev -Force
```

**What does the `-Dev` flag do?**
- Compiles `KnotVM.CLI` from local sources using `dotnet publish`
- Produces a self-contained binary for your architecture (x64/arm64)
- Copies `knot.exe` to `KNOT_HOME\bin`

`install.ps1` automatically handles legacy migration from `node-local` (Windows only):

- detects a previous installation;
- reuses `versions/` and `cache/` (including `%APPDATA%\node-local\cache`);
- preserves aliases and active version;
- removes unnecessary legacy artifacts and PATH entries.

### Linux/macOS (Bash)

```bash
# Clone the repository
git clone https://github.com/capitanmirco/knotvm.git
cd knotvm

# REQUIRED: Build from local sources (no release available)
./install.sh --dev

# Force reinstall
./install.sh --dev --force
```

**What does the `--dev` flag do?**
- Compiles `KnotVM.CLI` from local sources using `dotnet publish`
- Produces a self-contained binary for your architecture (x64/arm64) and OS (Linux/macOS)
- Copies `knot` to `KNOT_HOME/bin`

On Linux/macOS, no legacy migration from `node-local` is performed (a historical Windows-only tool): the installation proceeds normally.

> **Note**: Pre-built releases that can be downloaded automatically without `-Dev/--dev` will be available in the future. The scripts are already prepared for this scenario.

After installation, restart your terminal and verify:

```bash
knot --help
knot version
```

## Update and Uninstall

### Update

> **NOTE**: Currently, without public releases, updating requires pulling the repository and recompiling:

```powershell
# Windows
cd knotvm
git pull origin develop  # or the branch you are using
.\install.ps1 -Dev -Force

# Linux/macOS
cd knotvm
git pull origin develop  # or the branch you are using
./install.sh --dev --force
```

The `update.ps1` and `update.sh` scripts are designed to automatically download new releases from GitHub, but will only be functional once official releases are published.

### Uninstall

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

## CLI Commands

- `knot list [--path|-p]`
- `knot list-remote [--lts] [--all] [--limit <n>]`
- `knot install <version> [--alias <name>] [--latest] [--latest-lts] [--from-file]`
- `knot use <installation> [--auto]`
- `knot auto-detect [--directory <path>]` — detects version from `.nvmrc`, `.node-version` or `package.json`
- `knot remove <installation> [--force]`
- `knot rename --from <old> --to <new>`
- `knot run "<command>" [--with-version <installation>] [--auto]`
- `knot sync [--force]`
- `knot cache --list | --clear | --clean`
- `knot version`
- `knot completion <shell>` — generates tab-completion script (`bash`, `zsh`, `powershell`, `fish`)

## Smart Version Resolver

`knot install` and `knot use` accept shorthand notation without requiring full semver versions:

```bash
knot install 20          # resolves to the latest 20.x.x
knot install lts         # current latest LTS
knot install hydrogen    # 18.x.x (LTS codename)
knot install iron        # 20.x.x (LTS codename)
knot install jod         # 22.x.x (LTS codename)
knot install latest      # latest stable version
knot install 20.11.0     # exact version (passthrough)
```

## Project Version Auto-Detection

```bash
# Use the version specified in .nvmrc / .node-version / package.json
knot use --auto

# Install the version specified in the configuration file
knot install --from-file

# Explicitly detect and activate (with custom directory support)
knot auto-detect
knot auto-detect --directory /path/to/project
```

File precedence order: `.nvmrc` > `.node-version` > `package.json` (`engines.node`).

## Shell Completion

KnotVM supports tab-completion for Bash, Zsh, PowerShell and Fish.

### PowerShell (Windows / Linux / macOS)

```powershell
# Apply to current profile (permanent, requires terminal restart)
knot completion powershell >> $PROFILE

# Create profile if it doesn't exist, then apply
New-Item -ItemType File -Path $PROFILE -Force | Out-Null
knot completion powershell >> $PROFILE

# Current session only
knot completion powershell | Invoke-Expression
```

### Bash (Linux / macOS)

```bash
# Apply for current user only (permanent)
mkdir -p ~/.bash_completion.d
knot completion bash > ~/.bash_completion.d/knot
echo 'source ~/.bash_completion.d/knot' >> ~/.bashrc

# For all system sessions (requires sudo)
knot completion bash | sudo tee /etc/bash_completion.d/knot

# Current session only
source <(knot completion bash)
```

### Zsh (Linux / macOS)

```bash
# Apply for current user (permanent)
mkdir -p ~/.zsh/completions
knot completion zsh > ~/.zsh/completions/_knot

# Add to ~/.zshrc (if not already present):
echo 'fpath=(~/.zsh/completions $fpath)\nautoload -Uz compinit && compinit' >> ~/.zshrc

# Current session only
source <(knot completion zsh)
```

### Fish (Linux / macOS)

```bash
# Apply for current user (permanent)
mkdir -p ~/.config/fish/completions
knot completion fish > ~/.config/fish/completions/knot.fish
# Fish automatically reloads completions on the next session
```

After applying completion, restart the terminal (or reload the profile) and try:

```bash
knot <TAB>           # shows all commands
knot use <TAB>       # shows installed aliases
knot install <TAB>   # shows suggested versions
```

---

## Data Paths

`KNOT_HOME` can be set to customize the base path.

If not set:

- Windows: `%APPDATA%\node-local`
- Linux: `$HOME/.local/share/node-local`
- macOS: `$HOME/Library/Application Support/node-local`

Main structure:

- `bin/` binaries and proxies
- `versions/` Node.js installations
- `cache/` downloaded artifacts
- `templates/` proxy templates
- `locks/` lock files
- `settings.txt` active alias

## Build and Test

```bash
# Build solution
dotnet build --nologo

# Test
dotnet test --no-build --nologo -v minimal

# Run CLI locally
dotnet run --project src/KnotVM.CLI -- --help
```

Current validated state: `340/340` tests passing.

## Architecture

```text
KnotVM/
├── src/
│   ├── KnotVM.CLI/            # Entry point and commands
│   ├── KnotVM.Core/           # Domain models, interfaces, errors
│   └── KnotVM.Infrastructure/ # Filesystem/network/process implementations
├── templates/                 # Multi-OS proxy templates
└── tests/                     # Unit/integration tests (xUnit + Moq)
```

Main stack:

- .NET 8
- System.CommandLine
- Spectre.Console
- Microsoft.Extensions.DependencyInjection

## Support Matrix

OS:

- Windows 10/11 (x64, arm64)
- Linux glibc (x64, arm64)
- macOS (x64 Intel, arm64 Apple Silicon)
- Linux musl/Alpine: not supported in V1

Shell:

- PowerShell (Windows / Linux / macOS)
- CMD (Windows)
- Bash (Linux/macOS)
- Zsh (Linux/macOS)
- Fish (Linux/macOS) — completions supported (first-class proxy deferred to V2)

## Documentation

- Migration and architectural decisions: [`docs/CSHARP_MIGRATION.md`](docs/CSHARP_MIGRATION.md)
- Optimization analysis: [`docs/CODE_OPTIMIZATION_ANALYSIS.md`](docs/CODE_OPTIMIZATION_ANALYSIS.md)
- Migration prompts/steps: [`docs/docs-migration/`](docs/docs-migration/)

## License

MIT - see [`LICENSE`](LICENSE).
