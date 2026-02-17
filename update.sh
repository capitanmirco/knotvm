#!/usr/bin/env bash
#
# KnotVM Updater per Linux/macOS
#
# Aggiorna KnotVM CLI all'ultima versione disponibile:
# - Verifica installazione esistente
# - Backup versione corrente
# - Download nuova versione
# - Rollback automatico su errore
#
# Usage:
#   ./update.sh
#   ./update.sh --force
#

set -euo pipefail

# ============================================================================
# CONFIGURAZIONE
# ============================================================================

GITHUB_REPO="m-lelli/knotvm"
CLI_NAME="knot"
VERSION_CHECK_URL="https://api.github.com/repos/$GITHUB_REPO/releases/latest"

# ============================================================================
# VARIABILI GLOBALI
# ============================================================================

FORCE_UPDATE=0

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
        log_warn "Impossibile verificare ultima versione"
        echo ""
    else
        echo "$version"
    fi
}

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

# ============================================================================
# VERIFICA INSTALLAZIONE
# ============================================================================

test_existing_installation() {
    local bin_path="$1"
    local knot_binary="$bin_path/$CLI_NAME"
    
    if [ ! -f "$knot_binary" ]; then
        exit_with_error \
            "KNOT-INS-001" \
            "KnotVM non installato. Esegui install.sh prima di aggiornare" \
            "./install.sh" \
            40
    fi
    
    log_success "✓ Installazione esistente trovata: $knot_binary"
    echo "$knot_binary"
}

# ============================================================================
# AGGIORNAMENTO
# ============================================================================

update_cli_binary() {
    local knot_binary="$1"
    local current_version="$2"
    local latest_version="$3"
    
    # Verifica se aggiornamento necessario
    if [ $FORCE_UPDATE -eq 0 ] && [ "$current_version" != "unknown" ] && [ -n "$latest_version" ]; then
        if [ "$current_version" = "$latest_version" ]; then
            log_success "Già all'ultima versione: $current_version"
            log_info "Usa --force per forzare reinstallazione"
            return 1
        fi
    fi
    
    log_info "Aggiornamento: $current_version -> $latest_version"
    
    # Backup versione corrente
    local backup_path="${knot_binary}.backup"
    
    if ! cp "$knot_binary" "$backup_path" 2>/dev/null; then
        exit_with_error \
            "KNOT-PERM-001" \
            "Impossibile creare backup" \
            "Verifica permessi o chiudi processi che usano knot" \
            21
    fi
    
    log_info "✓ Backup creato: $backup_path"
    
    # Download nuova versione
    local release_url
    release_url=$(get_release_url)
    
    local temp_file
    temp_file=$(mktemp)
    
    log_info "Download nuova versione da $release_url..."
    
    local download_success=0
    
    if command -v curl &>/dev/null; then
        if curl -fsSL "$release_url" -o "$temp_file"; then
            download_success=1
        fi
    elif command -v wget &>/dev/null; then
        if wget -q "$release_url" -O "$temp_file"; then
            download_success=1
        fi
    else
        exit_with_error \
            "KNOT-GEN-001" \
            "curl o wget non trovati nel PATH" \
            "Installa curl o wget" \
            99
    fi
    
    if [ $download_success -eq 0 ]; then
        rm -f "$temp_file"
        # Ripristina backup
        cp "$backup_path" "$knot_binary"
        rm -f "$backup_path"
        
        exit_with_error \
            "KNOT-DL-001" \
            "Download fallito" \
            "Verifica connessione internet e riprova" \
            32
    fi
    
    # Verifica dimensione minima
    local file_size
    file_size=$(stat -f%z "$temp_file" 2>/dev/null || stat -c%s "$temp_file" 2>/dev/null || echo 0)
    
    if [ "$file_size" -lt 102400 ]; then
        rm -f "$temp_file"
        cp "$backup_path" "$knot_binary"
        rm -f "$backup_path"
        
        exit_with_error \
            "KNOT-DL-001" \
            "Download incompleto: dimensione sospetta ($file_size bytes)" \
            "Riprova" \
            32
    fi
    
    log_success "✓ Download completato ($(echo "scale=2; $file_size / 1048576" | bc 2>/dev/null || echo "?") MB)"
    
    # Sostituisci binario
    log_info "Installazione nuova versione..."
    
    if ! mv "$temp_file" "$knot_binary" 2>/dev/null; then
        rm -f "$temp_file"
        cp "$backup_path" "$knot_binary"
        rm -f "$backup_path"
        
        exit_with_error \
            "KNOT-PERM-001" \
            "Impossibile sostituire binario" \
            "Chiudi tutti i processi knot e riprova" \
            21
    fi
    
    chmod +x "$knot_binary"
    log_success "✓ Binario aggiornato"
    
    # Test nuova versione
    local new_version
    new_version=$(get_current_version "$knot_binary")
    
    if [ "$new_version" = "unknown" ]; then
        log_error "Impossibile verificare versione dopo aggiornamento"
        rollback_update "$knot_binary" "$backup_path"
        
        exit_with_error \
            "KNOT-GEN-001" \
            "Nuova versione non verificabile, rollback eseguito" \
            "Segnala il problema su GitHub: https://github.com/$GITHUB_REPO/issues" \
            99
    fi
    
    # Test comando base
    if ! "$knot_binary" --help &>/dev/null; then
        log_error "Nuova versione non funzionante"
        rollback_update "$knot_binary" "$backup_path"
        
        exit_with_error \
            "KNOT-GEN-001" \
            "Nuova versione difettosa, rollback eseguito" \
            "Segnala il problema su GitHub: https://github.com/$GITHUB_REPO/issues" \
            99
    fi
    
    log_success "✓ Nuova versione verificata: $new_version"
    
    # Rimuovi backup
    rm -f "$backup_path"
    
    return 0
}

rollback_update() {
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

# ============================================================================
# ARGUMENT PARSING
# ============================================================================

parse_arguments() {
    while [ $# -gt 0 ]; do
        case "$1" in
            --force)
                FORCE_UPDATE=1
                shift
                ;;
            --help|-h)
                cat <<EOF
KnotVM Updater per Linux/macOS

Usage: $0 [OPTIONS]

Options:
  --force        Forza aggiornamento anche se già all'ultima versione
  --help, -h     Mostra questo aiuto

Examples:
  $0
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
    log_info "=== KnotVM Updater per Linux/macOS ==="
    echo ""
    
    # Determina paths
    local knot_home bin_path
    knot_home=$(get_knot_home)
    bin_path="$knot_home/bin"
    
    log_info "KNOT_HOME: $knot_home"
    echo ""
    
    # Verifica installazione esistente
    local knot_binary
    knot_binary=$(test_existing_installation "$bin_path")
    
    # Verifica versioni
    log_info "Verifica versioni..."
    
    local current_version latest_version
    current_version=$(get_current_version "$knot_binary")
    log_info "Versione corrente: $current_version"
    
    latest_version=$(get_latest_version)
    if [ -n "$latest_version" ]; then
        log_info "Ultima versione disponibile: $latest_version"
    else
        log_warn "Impossibile determinare ultima versione (procedo comunque)"
        latest_version="latest"
    fi
    
    echo ""
    
    # Esegui aggiornamento
    if update_cli_binary "$knot_binary" "$current_version" "$latest_version"; then
        # Messaggio finale
        echo ""
        log_success "=== Aggiornamento completato con successo! ==="
        echo ""
        log_info "Esegui 'knot version' per verificare la nuova versione"
        echo ""
    else
        # Nessun aggiornamento necessario
        echo ""
        log_success "=== Nessun aggiornamento necessario ==="
        echo ""
    fi
}

# Entry point
parse_arguments "$@"

trap 'log_error "Script interrotto"; exit 130' INT TERM

main
