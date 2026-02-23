#!/usr/bin/env bash
#
# KnotVM Uninstaller per Linux/macOS
#
# Disinstalla KnotVM CLI in modo sicuro:
# - Rimuove binario CLI e configurazione
# - Opzionalmente preserva installazioni Node.js e cache
# - Rimuove PATH entry da shell RC files (marker blocks)
# - Conferma operazioni destructive
#
# Usage:
#   ./uninstall.sh
#   ./uninstall.sh --keep-data
#   ./uninstall.sh --yes
#

set -euo pipefail

# ============================================================================
# CONFIGURAZIONE
# ============================================================================

CLI_NAME="knot"

# ============================================================================
# VARIABILI GLOBALI
# ============================================================================

KEEP_DATA=0
AUTO_YES=0

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

get_directory_size() {
    local path="$1"
    
    if [ ! -d "$path" ]; then
        echo 0
        return
    fi
    
    du -sk "$path" 2>/dev/null | cut -f1 || echo 0
}

format_file_size() {
    local kb=$1
    
    if [ "$kb" -ge 1048576 ]; then
        echo "$(echo "scale=2; $kb / 1048576" | bc) GB"
    elif [ "$kb" -ge 1024 ]; then
        echo "$(echo "scale=2; $kb / 1024" | bc) MB"
    else
        echo "$kb KB"
    fi
}

# ============================================================================
# CONFERMA
# ============================================================================

confirm_uninstall() {
    local knot_home="$1"
    local will_keep_data="$2"
    
    echo ""
    log_warn "========================================"
    log_warn "ATTENZIONE: Operazione Destructive"
    log_warn "========================================"
    echo ""
    
    log_info "Verrà disinstallato KnotVM CLI da:"
    log_info "  $knot_home"
    echo ""
    
    if [ "$will_keep_data" -eq 1 ]; then
        log_success "Verranno PRESERVATI:"
        log_success "  • Installazioni Node.js (versions/)"
        log_success "  • Cache download (cache/)"
        echo ""
        log_warn "Verranno RIMOSSI:"
        log_warn "  • Binario CLI (bin/knot)"
        log_warn "  • Configurazione (settings.txt, templates/, locks/)"
    else
        log_warn "Verranno RIMOSSI COMPLETAMENTE:"
        log_warn "  • Binario CLI (bin/)"
        log_warn "  • Installazioni Node.js (versions/)"
        log_warn "  • Cache download (cache/)"
        log_warn "  • Configurazione (settings.txt, templates/, locks/)"
        
        # Calcola dimensione totale
        local versions_size cache_size total_size
        versions_size=$(get_directory_size "$knot_home/versions")
        cache_size=$(get_directory_size "$knot_home/cache")
        total_size=$((versions_size + cache_size))
        
        if [ "$total_size" -gt 0 ]; then
            echo ""
            log_info "Spazio disco che verrà liberato: $(format_file_size $total_size)"
        fi
    fi
    
    echo ""
    read -rp "Confermi disinstallazione? (s/N): " response
    
    [[ "$response" =~ ^[sS]$ ]]
}

# ============================================================================
# VERIFICA INSTALLAZIONE
# ============================================================================

test_existing_installation() {
    local knot_home="$1"
    
    if [ ! -d "$knot_home" ]; then
        log_warn "KnotVM non installato (directory non trovata: $knot_home)"
        log_info "Nulla da disinstallare."
        exit 0
    fi
    
    local knot_binary="$knot_home/bin/$CLI_NAME"
    
    if [ ! -f "$knot_binary" ]; then
        log_warn "Binario knot non trovato, ma directory KNOT_HOME esiste"
        log_info "Procedo con pulizia directory..."
    fi
}

# ============================================================================
# RIMOZIONE PATH DA SHELL RC
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
            # Tenta bashrc come fallback
            [ -f "$HOME/.bashrc" ] && rc_files+=("$HOME/.bashrc")
            ;;
    esac
    
    printf '%s\n' "${rc_files[@]}"
}

remove_from_shell_path() {
    local marker_start="# >>> KnotVM >>>"
    local marker_end="# <<< KnotVM <<<"
    
    local rc_files
    rc_files=$(get_shell_rc_files)
    
    if [ -z "$rc_files" ]; then
        log_info "Nessun file RC shell trovato (PATH già pulito)"
        return
    fi
    
    local removed=0
    
    while IFS= read -r rc_file; do
        [ -z "$rc_file" ] && continue
        
        if ! grep -Fq "$marker_start" "$rc_file" 2>/dev/null; then
            continue
        fi
        
        # Rimuovi marker block usando sed
        local temp_file
        temp_file=$(mktemp)
        
        # Rimuovi righe tra marker (inclusi i marker)
        sed "/$marker_start/,/$marker_end/d" "$rc_file" > "$temp_file"
        
        # Sostituisci file originale
        if mv "$temp_file" "$rc_file" 2>/dev/null; then
            log_success "✓ PATH pulito da $rc_file"
            removed=1
        else
            log_warn "WARN: Impossibile modificare $rc_file"
            rm -f "$temp_file"
        fi
    done <<< "$rc_files"
    
    if [ $removed -eq 0 ]; then
        log_info "PATH non contiene marker KnotVM (già pulito)"
    fi
}

# ============================================================================
# DISINSTALLAZIONE
# ============================================================================

remove_knot_installation() {
    local knot_home="$1"
    local keep_versions_and_cache="$2"
    
    log_info "Rimozione KnotVM..."
    echo ""
    
    local removed_count=0
    
    # Binario CLI
    if [ -d "$knot_home/bin" ]; then
        if rm -rf "$knot_home/bin" 2>/dev/null; then
            log_success "✓ Rimosso: Binario CLI"
            removed_count=$((removed_count + 1))
        else
            log_warn "WARN: Impossibile rimuovere bin/"
        fi
    fi
    
    # Configurazione
    if [ -f "$knot_home/settings.txt" ]; then
        if rm -f "$knot_home/settings.txt" 2>/dev/null; then
            log_success "✓ Rimosso: settings.txt"
            removed_count=$((removed_count + 1))
        fi
    fi
    
    # Templates
    if [ -d "$knot_home/templates" ]; then
        if rm -rf "$knot_home/templates" 2>/dev/null; then
            log_success "✓ Rimosso: Templates"
            removed_count=$((removed_count + 1))
        fi
    fi
    
    # Locks
    if [ -d "$knot_home/locks" ]; then
        if rm -rf "$knot_home/locks" 2>/dev/null; then
            log_success "✓ Rimosso: Locks"
            removed_count=$((removed_count + 1))
        fi
    fi
    
    # Versions e Cache (condizionali)
    if [ "$keep_versions_and_cache" -eq 0 ]; then
        if [ -d "$knot_home/versions" ]; then
            if rm -rf "$knot_home/versions" 2>/dev/null; then
                log_success "✓ Rimosso: Installazioni Node.js"
                removed_count=$((removed_count + 1))
            else
                log_warn "WARN: Impossibile rimuovere versions/"
            fi
        fi
        
        if [ -d "$knot_home/cache" ]; then
            if rm -rf "$knot_home/cache" 2>/dev/null; then
                log_success "✓ Rimosso: Cache download"
                removed_count=$((removed_count + 1))
            else
                log_warn "WARN: Impossibile rimuovere cache/"
            fi
        fi
    else
        # Report items preservati
        echo ""
        log_info "Preservati (come richiesto):"
        
        if [ -d "$knot_home/versions" ]; then
            log_success "  ✓ Installazioni Node.js"
        fi
        
        if [ -d "$knot_home/cache" ]; then
            log_success "  ✓ Cache download"
        fi
    fi
    
    # Rimuovi directory base se vuota
    local remaining_items
    remaining_items=$(find "$knot_home" -mindepth 1 2>/dev/null | wc -l)
    
    if [ "$remaining_items" -eq 0 ]; then
        if rmdir "$knot_home" 2>/dev/null; then
            log_success "✓ Directory KNOT_HOME rimossa (vuota)"
        fi
    else
        log_info "Directory KNOT_HOME preservata (contiene dati utente)"
    fi
    
    echo ""
    log_success "✓ Rimozione file completata ($removed_count elementi)"
}

# ============================================================================
# ARGUMENT PARSING
# ============================================================================

parse_arguments() {
    while [ $# -gt 0 ]; do
        case "$1" in
            --keep-data)
                KEEP_DATA=1
                shift
                ;;
            --yes|-y)
                AUTO_YES=1
                shift
                ;;
            --help|-h)
                cat <<EOF
KnotVM Uninstaller per Linux/macOS

Usage: $0 [OPTIONS]

Options:
  --keep-data    Preserva installazioni Node.js e cache
  --yes, -y      Conferma automatica senza prompt
  --help, -h     Mostra questo aiuto

Examples:
  $0
  $0 --keep-data
  $0 --yes

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
    log_info "=== KnotVM Uninstaller per Linux/macOS ==="
    echo ""
    
    # Determina KNOT_HOME
    local knot_home
    knot_home=$(get_knot_home)
    log_info "KNOT_HOME: $knot_home"
    
    # Verifica installazione
    test_existing_installation "$knot_home"
    
    # Conferma operazione
    if [ $AUTO_YES -eq 0 ]; then
        if ! confirm_uninstall "$knot_home" "$KEEP_DATA"; then
            log_info "Disinstallazione annullata dall'utente"
            exit 0
        fi
    fi
    
    echo ""
    log_info "Avvio disinstallazione..."
    echo ""
    
    # Rimuovi da PATH
    remove_from_shell_path
    
    echo ""
    
    # Rimuovi installazione
    remove_knot_installation "$knot_home" "$KEEP_DATA"
    
    # Messaggio finale
    echo ""
    log_success "========================================"
    log_success "Disinstallazione completata!"
    log_success "========================================"
    echo ""
    
    if [ $KEEP_DATA -eq 1 ]; then
        log_info "Le tue installazioni Node.js sono state preservate in:"
        log_info "  $knot_home/versions"
        echo ""
        log_info "Per rimuoverle manualmente:"
        log_info "  rm -rf '$knot_home'"
    else
        log_success "KnotVM è stato completamente rimosso dal sistema"
    fi
    
    echo ""
    log_warn "IMPORTANTE: Riavvia il terminale (o esegui source sul file RC) per applicare modifiche al PATH"
    echo ""
}

# Entry point
parse_arguments "$@"

trap 'log_error "Script interrotto"; exit 130' INT TERM

main
