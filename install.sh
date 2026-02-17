#!/usr/bin/env bash
#
# KnotVM Installer per Linux/macOS
#
# Installa KnotVM CLI in modo idempotente:
# - Crea struttura directory KNOT_HOME
# - Scarica/compila binario CLI
# - Aggiorna PATH con marker blocks in shell RC files
# - Verifica installazione
#
# Usage:
#   ./install.sh
#   ./install.sh --dev
#   ./install.sh --force
#

set -euo pipefail

# ============================================================================
# CONFIGURAZIONE
# ============================================================================

GITHUB_REPO="m-lelli/knotvm"
CLI_NAME="knot"
VERSION="latest"
VERSION_CHECK_URL="https://api.github.com/repos/$GITHUB_REPO/releases/latest"

# ============================================================================
# VARIABILI GLOBALI
# ============================================================================

FORCE_INSTALL=0
DEV_MODE=0

# ============================================================================
# FUNZIONI HELPER
# ============================================================================

log_info() {
    echo -e "\033[0;36m[INFO]\033[0m $1"
}

log_success() {
    echo -e "\033[0;32m[OK]\033[0m $1"
}

log_warn() {
    echo -e "\033[0;33m[WARN]\033[0m $1"
}

log_error() {
    echo -e "\033[0;31m[ERROR]\033[0m $1"
}

exit_with_error() {
    local code="$1"
    local message="$2"
    local hint="${3:-}"
    local exit_code="${4:-1}"
    
    log_error "$code: $message"
    if [ -n "$hint" ]; then
        log_warn "Hint: $hint"
    fi
    exit "$exit_code"
}

get_knot_home() {
    if [ -n "${KNOT_HOME:-}" ]; then
        echo "$KNOT_HOME"
        return
    fi
    
    local os_type
    os_type=$(uname -s)
    
    case "$os_type" in
        Linux)
            echo "${HOME}/.local/share/node-local"
            ;;
        Darwin)
            echo "${HOME}/Library/Application Support/node-local"
            ;;
        *)
            # Fallback generico
            echo "${HOME}/.local/share/node-local"
            ;;
    esac
}

get_os_type() {
    uname -s | tr '[:upper:]' '[:lower:]'
}

get_arch() {
    local arch
    arch=$(uname -m)
    
    case "$arch" in
        x86_64)
            echo "x64"
            ;;
        aarch64|arm64)
            echo "arm64"
            ;;
        *)
            echo "$arch"
            ;;
    esac
}

check_libc() {
    # Verifica glibc vs musl (solo Linux)
    if [ "$(get_os_type)" != "linux" ]; then
        return 0
    fi
    
    # Test ldd per rilevare musl
    if ldd --version 2>&1 | grep -qi musl; then
        exit_with_error \
            "KNOT-OS-001" \
            "Linux musl/Alpine non supportato in V1 (richiesta glibc)" \
            "Usa una distribuzione basata su glibc (Ubuntu, Debian, Fedora, ecc.)" \
            10
    fi
    
    log_success "✓ glibc rilevata"
}

# ============================================================================
# PREFLIGHT CHECKS
# ============================================================================

preflight_checks() {
    log_info "Esecuzione preflight checks..."
    
    # 1. Verifica OS
    local os_type
    os_type=$(get_os_type)
    
    case "$os_type" in
        linux|darwin)
            # OK
            ;;
        *)
            exit_with_error \
                "KNOT-OS-001" \
                "Sistema operativo non supportato: $os_type" \
                "Sistemi supportati: Linux, macOS. Usa install.ps1 per Windows" \
                10
            ;;
    esac
    
    # 2. Verifica architettura
    local arch
    arch=$(get_arch)
    
    case "$arch" in
        x64|arm64)
            # OK
            ;;
        *)
            exit_with_error \
                "KNOT-OS-002" \
                "Architettura non supportata: $arch" \
                "Architetture supportate: x64, arm64" \
                11
            ;;
    esac
    
    log_success "✓ OS: $os_type, Arch: $arch"
    
    # 3. Verifica libc (solo Linux)
    if [ "$os_type" = "linux" ]; then
        check_libc
    fi
}

# ============================================================================
# INIZIALIZZAZIONE KNOT_HOME
# ============================================================================

initialize_knot_home() {
    local knot_home="$1"
    
    log_info "Inizializzazione KNOT_HOME: $knot_home"
    
    local directories=(
        "$knot_home"
        "$knot_home/bin"
        "$knot_home/versions"
        "$knot_home/cache"
        "$knot_home/templates"
        "$knot_home/locks"
    )
    
    for dir in "${directories[@]}"; do
        if [ ! -d "$dir" ]; then
            if ! mkdir -p "$dir" 2>/dev/null; then
                exit_with_error \
                    "KNOT-PATH-001" \
                    "Impossibile creare directory: $dir" \
                    "Verifica permessi scrittura o imposta KNOT_HOME in una posizione accessibile" \
                    20
            fi
            log_info "✓ Creata directory: $dir"
        fi
    done
    
    log_success "✓ Struttura KNOT_HOME verificata"
}

# ============================================================================
# INSTALLAZIONE BINARIO
# ============================================================================

get_release_url() {
    local os_type arch
    os_type=$(get_os_type)
    arch=$(get_arch)
    
    local os_suffix
    case "$os_type" in
        linux)
            os_suffix="linux"
            ;;
        darwin)
            os_suffix="osx"
            ;;
    esac
    
    echo "https://github.com/$GITHUB_REPO/releases/latest/download/knot-$os_suffix-$arch"
}

get_current_version() {
    local knot_binary="$1"
    
    if [ ! -f "$knot_binary" ]; then
        echo "unknown"
        return
    fi
    
    local output
    if output=$("$knot_binary" version 2>&1 | head -n1); then
        # Estrai versione da output tipo "KnotVM versione 1.0.0"
        if [[ "$output" =~ ([0-9]+\.[0-9]+\.[0-9]+) ]]; then
            echo "${BASH_REMATCH[1]}"
        else
            echo "unknown"
        fi
    else
        echo "unknown"
    fi
}

get_latest_version() {
    local version=""
    
    if command -v curl &>/dev/null; then
        version=$(curl -fsSL "$VERSION_CHECK_URL" 2>/dev/null | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"\([^"]*\)"/\1/' | sed 's/^v//')
    elif command -v wget &>/dev/null; then
        version=$(wget -qO- "$VERSION_CHECK_URL" 2>/dev/null | grep -o '"tag_name": *"[^"]*"' | sed 's/"tag_name": *"\([^"]*\)"/\1/' | sed 's/^v//')
    fi
    
    if [ -z "$version" ]; then
        echo ""
    else
        echo "$version"
    fi
}

install_cli_binary() {
    local bin_path="$1"
    local target_binary="$bin_path/$CLI_NAME"
    
    # Verifica se già installato
    if [ -f "$target_binary" ] && [ $FORCE_INSTALL -eq 0 ]; then
        log_info "$CLI_NAME già presente in $bin_path"
        
        # Test esecuzione e versione
        local current_version
        current_version=$(get_current_version "$target_binary")
        
        if [ "$current_version" != "unknown" ]; then
            log_success "✓ Versione installata: $current_version"
            
            # Verifica se c'è una nuova versione disponibile (solo se non in dev mode)
            if [ $DEV_MODE -eq 0 ]; then
                log_info "Verifica aggiornamenti disponibili..."
                local latest_version
                latest_version=$(get_latest_version)
                
                if [ -n "$latest_version" ] && [ "$latest_version" != "$current_version" ]; then
                    log_warn "Nuova versione disponibile: $latest_version"
                    echo ""
                    
                    read -rp "Vuoi aggiornare a questa versione? (s/N): " response
                    if [[ "$response" =~ ^[sS]$ ]]; then
                        update_existing_installation "$target_binary" "$current_version" "$latest_version"
                        return $?
                    else
                        log_info "Aggiornamento saltato. Installazione corrente mantenuta."
                        log_info "Hint: Usa 'knot --version' per verificare la versione corrente"
                        log_info "Hint: Esegui './update.sh' in qualsiasi momento per aggiornare"
                        return 0
                    fi
                elif [ -n "$latest_version" ] && [ "$latest_version" = "$current_version" ]; then
                    log_success "✓ Già all'ultima versione disponibile"
                    log_info "Hint: Usa --force per reinstallare"
                    return 0
                else
                    log_success "✓ Installazione esistente funzionante"
                    log_info "Hint: Usa --force per reinstallare"
                    return 0
                fi
            else
                log_success "✓ Installazione esistente funzionante (modalità dev)"
                log_info "Hint: Usa --force per reinstallare"
                return 0
            fi
        else
            log_warn "Binario esistente non funzionante, procedo con reinstallazione..."
        fi
    fi
    
    if [ $DEV_MODE -eq 1 ]; then
        log_info "Modalità sviluppo: compilazione da sorgenti..."
        install_from_source "$target_binary"
    else
        log_info "Download release da GitHub..."
        install_from_release "$target_binary"
    fi
}

install_from_source() {
    local target_path="$1"
    
    # Verifica dotnet CLI
    if ! command -v dotnet &>/dev/null; then
        exit_with_error \
            "KNOT-GEN-001" \
            "dotnet CLI non trovato nel PATH" \
            "Installa .NET 8.0 SDK da https://dot.net" \
            99
    fi
    
    # Determina directory script
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    # Verifica KnotVM.sln
    local solution_file="$script_dir/KnotVM.sln"
    if [ ! -f "$solution_file" ]; then
        exit_with_error \
            "KNOT-GEN-001" \
            "KnotVM.sln non trovato. Esegui install.sh dalla root del repository o usa modalità release" \
            "cd nella directory root del progetto oppure ometti --dev per scaricare release" \
            99
    fi
    
    # Determina RID per publish
    local os_type arch rid
    os_type=$(get_os_type)
    arch=$(get_arch)
    
    case "$os_type" in
        linux)
            rid="linux-$arch"
            ;;
        darwin)
            rid="osx-$arch"
            ;;
    esac
    
    log_info "Publish progetto KnotVM.CLI (RID: $rid)..."
    
    local project_path="$script_dir/src/KnotVM.CLI/KnotVM.CLI.csproj"
    
    # Publish self-contained per ottenere un eseguibile standalone
    if ! dotnet publish "$project_path" -c Release -r "$rid" --self-contained -p:PublishSingleFile=true --nologo -v quiet; then
        exit_with_error \
            "KNOT-GEN-001" \
            "Publish fallita" \
            "Verifica errori publish con: dotnet publish $project_path -c Release -r $rid --self-contained -p:PublishSingleFile=true" \
            99
    fi
    
    # Trova output publish
    local output_path="$script_dir/src/KnotVM.CLI/bin/Release/net8.0/$rid/publish/knot"
    if [ ! -f "$output_path" ]; then
        exit_with_error \
            "KNOT-GEN-001" \
            "Output binario non trovato: $output_path" \
            "Verifica processo build" \
            99
    fi
    
    # Copia
    cp "$output_path" "$target_path"
    chmod +x "$target_path"
    
    log_success "✓ Binario compilato e copiato in $target_path"
}

install_from_release() {
    local target_path="$1"
    local release_url
    release_url=$(get_release_url)
    
    local temp_file
    temp_file=$(mktemp)
    
    log_info "Download da $release_url..."
    
    # Download con curl o wget
    if command -v curl &>/dev/null; then
        if ! curl -fsSL "$release_url" -o "$temp_file"; then
            rm -f "$temp_file"
            exit_with_error \
                "KNOT-DL-001" \
                "Download release fallito" \
                "Verifica connessione internet o usa modalità --dev per compilare da sorgenti" \
                32
        fi
    elif command -v wget &>/dev/null; then
        if ! wget -q "$release_url" -O "$temp_file"; then
            rm -f "$temp_file"
            exit_with_error \
                "KNOT-DL-001" \
                "Download release fallito" \
                "Verifica connessione internet o usa modalità --dev per compilare da sorgenti" \
                32
        fi
    else
        exit_with_error \
            "KNOT-GEN-001" \
            "curl o wget non trovati nel PATH" \
            "Installa curl o wget per scaricare release" \
            99
    fi
    
    # Verifica dimensione minima
    local file_size
    file_size=$(stat -f%z "$temp_file" 2>/dev/null || stat -c%s "$temp_file" 2>/dev/null || echo 0)
    
    if [ "$file_size" -lt 102400 ]; then
        rm -f "$temp_file"
        exit_with_error \
            "KNOT-DL-001" \
            "Download incompleto: dimensione file sospetta ($file_size bytes)" \
            "Riprova o usa --dev" \
            32
    fi
    
    # Copia e rendi eseguibile
    mv "$temp_file" "$target_path"
    chmod +x "$target_path"
    
    log_success "✓ Download completato e binario installato"
}

update_existing_installation() {
    local knot_binary="$1"
    local current_version="$2"
    local latest_version="$3"
    
    echo ""
    log_info "Aggiornamento: $current_version -> $latest_version"
    echo ""
    
    # Backup versione corrente
    local backup_path="${knot_binary}.backup"
    
    if ! cp "$knot_binary" "$backup_path" 2>/dev/null; then
        log_error "Impossibile creare backup"
        log_warn "Hint: Chiudi processi che usano knot e riprova"
        return 1
    fi
    
    log_info "✓ Backup creato"
    
    # Download nuova versione
    local release_url
    release_url=$(get_release_url)
    
    local temp_file
    temp_file=$(mktemp)
    
    log_info "Download nuova versione..."
    
    local download_success=0
    
    if command -v curl &>/dev/null; then
        if curl -fsSL "$release_url" -o "$temp_file"; then
            download_success=1
        fi
    elif command -v wget &>/dev/null; then
        if wget -q "$release_url" -O "$temp_file"; then
            download_success=1
        fi
    fi
    
    if [ $download_success -eq 0 ]; then
        rm -f "$temp_file"
        cp "$backup_path" "$knot_binary"
        rm -f "$backup_path"
        
        log_error "Download fallito"
        log_warn "Hint: Verifica connessione internet e riprova"
        return 1
    fi
    
    # Verifica dimensione minima
    local file_size
    file_size=$(stat -f%z "$temp_file" 2>/dev/null || stat -c%s "$temp_file" 2>/dev/null || echo 0)
    
    if [ "$file_size" -lt 102400 ]; then
        rm -f "$temp_file"
        cp "$backup_path" "$knot_binary"
        rm -f "$backup_path"
        
        log_error "Download incompleto: dimensione sospetta"
        return 1
    fi
    
    log_success "✓ Download completato ($(echo "scale=2; $file_size / 1048576" | bc 2>/dev/null || echo "?") MB)"
    
    # Sostituisci binario
    log_info "Installazione nuova versione..."
    
    if ! mv "$temp_file" "$knot_binary" 2>/dev/null; then
        rm -f "$temp_file"
        cp "$backup_path" "$knot_binary"
        rm -f "$backup_path"
        
        log_error "Impossibile sostituire binario"
        log_warn "Hint: Chiudi tutti i processi knot e riprova"
        return 1
    fi
    
    chmod +x "$knot_binary"
    log_success "✓ Binario aggiornato"
    
    # Test nuova versione
    local new_version
    new_version=$(get_current_version "$knot_binary")
    
    if [ "$new_version" = "unknown" ]; then
        log_error "Impossibile verificare versione dopo aggiornamento"
        rollback_installation "$knot_binary" "$backup_path"
        return 1
    fi
    
    # Test comando base
    if ! "$knot_binary" --help &>/dev/null; then
        log_error "Nuova versione non funzionante"
        rollback_installation "$knot_binary" "$backup_path"
        return 1
    fi
    
    log_success "✓ Nuova versione verificata: $new_version"
    
    # Rimuovi backup
    rm -f "$backup_path"
    
    echo ""
    log_success "✓ Aggiornamento completato con successo!"
    
    return 0
}

rollback_installation() {
    local knot_binary="$1"
    local backup_path="$2"
    
    log_warn "Rollback in corso..."
    
    if [ -f "$backup_path" ]; then
        cp "$backup_path" "$knot_binary"
        chmod +x "$knot_binary"
        rm -f "$backup_path"
        log_success "✓ Rollback completato, versione precedente ripristinata"
    else
        log_error "Backup non trovato, impossibile rollback"
    fi
}

test_installation() {
    local bin_path="$1"
    local knot_binary="$bin_path/$CLI_NAME"
    
    log_info "Verifica installazione..."
    
    if [ ! -f "$knot_binary" ]; then
        exit_with_error \
            "KNOT-GEN-001" \
            "Binario knot non trovato dopo installazione" \
            "Ripeti installazione o contatta supporto" \
            99
    fi
    
    if [ ! -x "$knot_binary" ]; then
        exit_with_error \
            "KNOT-PERM-001" \
            "Binario knot non eseguibile" \
            "Verifica permessi: chmod +x $knot_binary" \
            21
    fi
    
    # Test esecuzione
    local output
    if output=$("$knot_binary" version 2>&1); then
        log_success "✓ $CLI_NAME funzionante: $output"
    else
        exit_with_error \
            "KNOT-GEN-001" \
            "Binario installato ma non eseguibile" \
            "Verifica compatibilità sistema o ricompila con --dev" \
            99
    fi
}

# ============================================================================
# PATH UPDATE CON MARKER BLOCKS
# ============================================================================

get_shell_rc_files() {
    local shell_name
    shell_name=$(basename "$SHELL")
    
    local rc_files=()
    
    case "$shell_name" in
        bash)
            [ -f "$HOME/.bashrc" ] && rc_files+=("$HOME/.bashrc")
            ;;
        zsh)
            [ -f "$HOME/.zshrc" ] && rc_files+=("$HOME/.zshrc")
            ;;
        *)
            log_warn "Shell $shell_name non ufficialmente supportata in V1"
            log_warn "Supportate: bash, zsh"
            # Tenta comunque bashrc come fallback
            [ -f "$HOME/.bashrc" ] && rc_files+=("$HOME/.bashrc")
            ;;
    esac
    
    printf '%s\n' "${rc_files[@]}"
}

add_to_shell_path() {
    local bin_path="$1"
    
    local rc_files
    rc_files=$(get_shell_rc_files)
    
    if [ -z "$rc_files" ]; then
        log_warn "Nessun file RC shell trovato per aggiornamento PATH automatico"
        log_warn "Aggiungi manualmente al tuo shell RC:"
        log_warn "  export PATH=\"$bin_path:\$PATH\""
        return 1
    fi
    
    local marker_start="# >>> KnotVM >>>"
    local marker_end="# <<< KnotVM <<<"
    local path_export="export PATH=\"$bin_path:\$PATH\""
    
    local updated=0
    
    while IFS= read -r rc_file; do
        [ -z "$rc_file" ] && continue
        
        # Verifica se marker già presente
        if grep -Fq "$marker_start" "$rc_file" 2>/dev/null; then
            log_info "PATH marker già presente in $rc_file (idempotente)"
            continue
        fi
        
        # Aggiungi marker block
        {
            echo ""
            echo "$marker_start"
            echo "# KnotVM PATH setup (managed by installer)"
            echo "$path_export"
            echo "$marker_end"
        } >> "$rc_file"
        
        log_success "✓ PATH aggiornato in $rc_file"
        updated=1
    done <<< "$rc_files"
    
    if [ $updated -eq 1 ]; then
        return 0
    fi

    return 1
}

# ============================================================================
# ARGUMENT PARSING
# ============================================================================

parse_arguments() {
    while [ $# -gt 0 ]; do
        case "$1" in
            --dev)
                DEV_MODE=1
                shift
                ;;
            --force)
                FORCE_INSTALL=1
                shift
                ;;
            --help|-h)
                cat <<EOF
KnotVM Installer per Linux/macOS

Usage: $0 [OPTIONS]

Options:
  --dev          Modalità sviluppo: compila da sorgenti locali
  --force        Forza reinstallazione anche se già presente
  --help, -h     Mostra questo aiuto

Examples:
  $0
  $0 --dev
  $0 --force

EOF
                exit 0
                ;;
            *)
                log_error "Opzione sconosciuta: $1"
                echo "Usa --help per aiuto"
                exit 1
                ;;
        esac
    done
}

# ============================================================================
# MAIN
# ============================================================================

main() {
    echo ""
    log_info "=== KnotVM Installer per Linux/macOS ==="
    echo ""
    
    # Preflight
    preflight_checks
    
    # Determina KNOT_HOME
    local knot_home bin_path
    knot_home=$(get_knot_home)
    bin_path="$knot_home/bin"
    
    log_info "KNOT_HOME: $knot_home"
    echo ""
    
    # Inizializza struttura
    initialize_knot_home "$knot_home"
    
    # Installa binario
    install_cli_binary "$bin_path"
    
    # Test installazione
    test_installation "$bin_path"
    
    # Aggiorna PATH
    echo ""
    local path_updated=0
    if add_to_shell_path "$bin_path"; then
        path_updated=1
    fi
    
    # Messaggio finale
    echo ""
    log_success "=== Installazione completata con successo! ==="
    echo ""
    log_info "Per iniziare:"
    log_info "  1. Riavvia il terminale (o esegui: source ~/.bashrc o source ~/.zshrc)"
    log_info "  2. Esegui: knot --help"
    log_info "  3. Installa Node.js: knot install --latest-lts"
    echo ""
    
    if [ $path_updated -eq 1 ]; then
        log_warn "IMPORTANTE: Riavvia il terminale o esegui source sul file RC!"
    fi
}

# Entry point
parse_arguments "$@"

trap 'log_error "Script interrotto"; exit 130' INT TERM

main
