#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Script di disinstallazione KnotVM per Windows.
.DESCRIPTION
    Disinstalla KnotVM CLI in modo sicuro:
    - Rimuove binario CLI e configurazione
    - Opzionalmente preserva installazioni Node.js e cache
    - Rimuove PATH entry user-scope
    - Conferma operazione destructive
.PARAMETER KeepData
    Preserva installazioni Node.js (versions/) e cache download.
.PARAMETER Yes
    Conferma automatica senza prompt interattivo.
.EXAMPLE
    .\uninstall.ps1
    .\uninstall.ps1 -KeepData
    .\uninstall.ps1 -Yes
#>

[CmdletBinding()]
param(
    [switch]$KeepData,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ============================================================================
# CONFIGURAZIONE
# ============================================================================

$CLI_NAME = "knot"

# ============================================================================
# FUNZIONI HELPER
# ============================================================================

function Write-ColorOutput {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )
    
    $color = switch ($Level) {
        'Info' { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
    }
    
    $prefix = switch ($Level) {
        'Info' { '[INFO]' }
        'Success' { '[OK]' }
        'Warning' { '[WARN]' }
        'Error' { '[ERROR]' }
    }
    
    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Exit-WithError {
    param(
        [string]$Code,
        [string]$Message,
        [string]$Hint,
        [int]$ExitCode = 1
    )
    
    Write-ColorOutput "$Code`: $Message" -Level Error
    if ($Hint) {
        Write-ColorOutput "Hint: $Hint" -Level Warning
    }
    exit $ExitCode
}

function Get-KnotHome {
    $envKnotHome = [Environment]::GetEnvironmentVariable("KNOT_HOME", "User")
    if ($envKnotHome) {
        return $envKnotHome
    }
    
    $appData = [Environment]::GetFolderPath("ApplicationData")
    return Join-Path $appData "node-local"
}

function Get-DirectorySize {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        return 0
    }
    
    try {
        $size = (Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue | 
                 Measure-Object -Property Length -Sum).Sum
        return [Math]::Max(0, $size)
    }
    catch {
        return 0
    }
}

function Format-FileSize {
    param([long]$Bytes)
    
    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    }
    elseif ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    }
    elseif ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    }
    else {
        return "$Bytes bytes"
    }
}

function Remove-FromUserPath {
    param([string]$PathToRemove)
    
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
    
    # Normalizza per confronto case-insensitive
    $pathEntries = $currentPath -split ';' | Where-Object { $_ }
    $normalizedRemove = $PathToRemove.TrimEnd('\').ToLowerInvariant()
    
    $newEntries = $pathEntries | Where-Object {
        $_.TrimEnd('\').ToLowerInvariant() -ne $normalizedRemove
    }
    
    if ($newEntries.Count -eq $pathEntries.Count) {
        Write-ColorOutput "PATH non contiene $PathToRemove (già pulito)" -Level Info
        return $false
    }
    
    try {
        $newPath = $newEntries -join ';'
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        Write-ColorOutput "✓ PATH aggiornato: $PathToRemove rimosso" -Level Success
        return $true
    }
    catch {
        Write-ColorOutput "WARN: Impossibile rimuovere da PATH: $($_.Exception.Message)" -Level Warning
        Write-ColorOutput "Hint: Rimuovi manualmente da Variabili Ambiente Utente" -Level Warning
        return $false
    }
}

function Confirm-Uninstall {
    param(
        [string]$KnotHome,
        [bool]$WillKeepData
    )
    
    Write-Host ""
    Write-ColorOutput "========================================" -Level Warning
    Write-ColorOutput "ATTENZIONE: Operazione Destructive" -Level Warning
    Write-ColorOutput "========================================" -Level Warning
    Write-Host ""
    
    Write-ColorOutput "Verrà disinstallato KnotVM CLI da:" -Level Info
    Write-ColorOutput "  $KnotHome" -Level Info
    Write-Host ""
    
    if ($WillKeepData) {
        Write-ColorOutput "Verranno PRESERVATI:" -Level Success
        Write-ColorOutput "  • Installazioni Node.js (versions/)" -Level Success
        Write-ColorOutput "  • Cache download (cache/)" -Level Success
        Write-Host ""
        Write-ColorOutput "Verranno RIMOSSI:" -Level Warning
        Write-ColorOutput "  • Binario CLI (bin/knot.exe)" -Level Warning
        Write-ColorOutput "  • Configurazione (settings.txt, templates/, locks/)" -Level Warning
    }
    else {
        Write-ColorOutput "Verranno RIMOSSI COMPLETAMENTE:" -Level Warning
        Write-ColorOutput "  • Binario CLI (bin/)" -Level Warning
        Write-ColorOutput "  • Installazioni Node.js (versions/)" -Level Warning
        Write-ColorOutput "  • Cache download (cache/)" -Level Warning
        Write-ColorOutput "  • Configurazione (settings.txt, templates/, locks/)" -Level Warning
        
        # Calcola dimensione totale
        $versionsSize = Get-DirectorySize -Path (Join-Path $KnotHome "versions")
        $cacheSize = Get-DirectorySize -Path (Join-Path $KnotHome "cache")
        $totalSize = $versionsSize + $cacheSize
        
        if ($totalSize -gt 0) {
            Write-Host ""
            Write-ColorOutput "Spazio disco che verrà liberato: $(Format-FileSize $totalSize)" -Level Info
        }
    }
    
    Write-Host ""
    $response = Read-Host "Confermi disinstallazione? (s/N)"
    
    return ($response -match '^[sS]$')
}

# ============================================================================
# VERIFICA INSTALLAZIONE
# ============================================================================

function Test-ExistingInstallation {
    param([string]$KnotHome)
    
    if (-not (Test-Path $KnotHome)) {
        Write-ColorOutput "KnotVM non installato (directory non trovata: $KnotHome)" -Level Warning
        Write-ColorOutput "Nulla da disinstallare." -Level Info
        exit 0
    }
    
    $binPath = Join-Path $KnotHome "bin"
    $knotExe = Join-Path $binPath "$CLI_NAME.exe"
    
    if (-not (Test-Path $knotExe)) {
        Write-ColorOutput "Binario knot.exe non trovato, ma directory KNOT_HOME esiste" -Level Warning
        Write-ColorOutput "Procedo con pulizia directory..." -Level Info
    }
}

# ============================================================================
# DISINSTALLAZIONE
# ============================================================================

function Remove-KnotInstallation {
    param(
        [string]$KnotHome,
        [bool]$KeepVersionsAndCache
    )
    
    Write-ColorOutput "Rimozione KnotVM..." -Level Info
    Write-Host ""
    
    $itemsToRemove = @()
    $itemsToKeep = @()
    
    # Binario CLI
    $binPath = Join-Path $KnotHome "bin"
    if (Test-Path $binPath) {
        $itemsToRemove += @{ Name = "Binario CLI"; Path = $binPath }
    }
    
    # Configurazione
    $settingsFile = Join-Path $KnotHome "settings.txt"
    if (Test-Path $settingsFile) {
        $itemsToRemove += @{ Name = "settings.txt"; Path = $settingsFile }
    }
    
    # Templates
    $templatesPath = Join-Path $KnotHome "templates"
    if (Test-Path $templatesPath) {
        $itemsToRemove += @{ Name = "Templates"; Path = $templatesPath }
    }
    
    # Locks
    $locksPath = Join-Path $KnotHome "locks"
    if (Test-Path $locksPath) {
        $itemsToRemove += @{ Name = "Locks"; Path = $locksPath }
    }
    
    # Versions e Cache (condizionali)
    $versionsPath = Join-Path $KnotHome "versions"
    $cachePath = Join-Path $KnotHome "cache"
    
    if ($KeepVersionsAndCache) {
        if (Test-Path $versionsPath) {
            $itemsToKeep += @{ Name = "Installazioni Node.js"; Path = $versionsPath }
        }
        if (Test-Path $cachePath) {
            $itemsToKeep += @{ Name = "Cache download"; Path = $cachePath }
        }
    }
    else {
        if (Test-Path $versionsPath) {
            $itemsToRemove += @{ Name = "Installazioni Node.js"; Path = $versionsPath }
        }
        if (Test-Path $cachePath) {
            $itemsToRemove += @{ Name = "Cache download"; Path = $cachePath }
        }
    }
    
    # Rimuovi items
    $removedCount = 0
    foreach ($item in $itemsToRemove) {
        try {
            if (Test-Path $item.Path) {
                Remove-Item -Path $item.Path -Recurse -Force -ErrorAction Stop
                Write-ColorOutput "✓ Rimosso: $($item.Name)" -Level Success
                $removedCount++
            }
        }
        catch {
            Write-ColorOutput "WARN: Impossibile rimuovere $($item.Name): $($_.Exception.Message)" -Level Warning
        }
    }
    
    # Report items preservati
    if ($itemsToKeep.Count -gt 0) {
        Write-Host ""
        Write-ColorOutput "Preservati (come richiesto):" -Level Info
        foreach ($item in $itemsToKeep) {
            if (Test-Path $item.Path) {
                Write-ColorOutput "  ✓ $($item.Name)" -Level Success
            }
        }
    }
    
    # Rimuovi directory base se vuota o solo con items preservati
    try {
        $remainingItems = Get-ChildItem -Path $KnotHome -ErrorAction SilentlyContinue
        if ($remainingItems.Count -eq 0) {
            Remove-Item -Path $KnotHome -Force -ErrorAction Stop
            Write-ColorOutput "✓ Directory KNOT_HOME rimossa (vuota)" -Level Success
        }
        elseif ($remainingItems.Count -eq $itemsToKeep.Count) {
            Write-ColorOutput "Directory KNOT_HOME preservata (contiene dati utente)" -Level Info
        }
    }
    catch {
        Write-ColorOutput "INFO: Directory KNOT_HOME non rimossa (contiene ancora file)" -Level Info
    }
    
    Write-Host ""
    Write-ColorOutput "✓ Rimozione file completata ($removedCount elementi)" -Level Success
}

# ============================================================================
# MAIN
# ============================================================================

function Main {
    Write-Host ""
    Write-ColorOutput "=== KnotVM Uninstaller per Windows ===" -Level Info
    Write-Host ""
    
    # Determina KNOT_HOME
    $knotHome = Get-KnotHome
    Write-ColorOutput "KNOT_HOME: $knotHome" -Level Info
    
    # Verifica installazione
    Test-ExistingInstallation -KnotHome $knotHome
    
    # Conferma operazione
    if (-not $Yes) {
        $confirmed = Confirm-Uninstall -KnotHome $knotHome -WillKeepData $KeepData
        if (-not $confirmed) {
            Write-ColorOutput "Disinstallazione annullata dall'utente" -Level Info
            exit 0
        }
    }
    
    Write-Host ""
    Write-ColorOutput "Avvio disinstallazione..." -Level Info
    Write-Host ""
    
    # Rimuovi da PATH
    $binPath = Join-Path $knotHome "bin"
    Remove-FromUserPath -PathToRemove $binPath
    
    Write-Host ""
    
    # Rimuovi installazione
    Remove-KnotInstallation -KnotHome $knotHome -KeepVersionsAndCache $KeepData
    
    # Messaggio finale
    Write-Host ""
    Write-ColorOutput "========================================" -Level Success
    Write-ColorOutput "Disinstallazione completata!" -Level Success
    Write-ColorOutput "========================================" -Level Success
    Write-Host ""
    
    if ($KeepData) {
        Write-ColorOutput "Le tue installazioni Node.js sono state preservate in:" -Level Info
        Write-ColorOutput "  $(Join-Path $knotHome 'versions')" -Level Info
        Write-Host ""
        Write-ColorOutput "Per rimuoverle manualmente:" -Level Info
        Write-ColorOutput "  Remove-Item -Recurse -Force '$knotHome'" -Level Info
    }
    else {
        Write-ColorOutput "KnotVM è stato completamente rimosso dal sistema" -Level Success
    }
    
    Write-Host ""
    Write-ColorOutput "IMPORTANTE: Riavvia il terminale per applicare modifiche al PATH" -Level Warning
    Write-Host ""
}

# Esegui main
try {
    Main
}
catch {
    Exit-WithError `
        -Code "KNOT-GEN-001" `
        -Message "Errore inatteso: $($_.Exception.Message)" `
        -Hint "Esegui con -Verbose per maggiori dettagli" `
        -ExitCode 99
}
