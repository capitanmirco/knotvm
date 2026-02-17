#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Script di aggiornamento KnotVM per Windows.
.DESCRIPTION
    Aggiorna KnotVM CLI all'ultima versione disponibile:
    - Verifica installazione esistente
    - Backup versione corrente
    - Download nuova versione
    - Rollback automatico su errore
.PARAMETER Force
    Forza aggiornamento anche se già all'ultima versione.
.EXAMPLE
    .\update.ps1
    .\update.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ============================================================================
# CONFIGURAZIONE
# ============================================================================

$GITHUB_REPO = "m-lelli/knotvm"
$CLI_NAME = "knot"
$VERSION_CHECK_URL = "https://api.github.com/repos/$GITHUB_REPO/releases/latest"

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

function Get-CurrentVersion {
    param([string]$KnotExe)
    
    try {
        $output = & $KnotExe version 2>&1 | Select-Object -First 1
        if ($LASTEXITCODE -eq 0 -and $output) {
            # Estrai versione da output tipo "KnotVM versione 1.0.0"
            if ($output -match '\d+\.\d+\.\d+') {
                return $matches[0]
            }
        }
        return "unknown"
    }
    catch {
        return "unknown"
    }
}

function Get-LatestVersion {
    try {
        $ProgressPreference = 'SilentlyContinue'
        $response = Invoke-RestMethod -Uri $VERSION_CHECK_URL -UseBasicParsing -ErrorAction Stop
        $ProgressPreference = 'Continue'
        
        if ($response.tag_name) {
            # Rimuovi 'v' prefix se presente
            return $response.tag_name -replace '^v', ''
        }
        
        return $null
    }
    catch {
        Write-ColorOutput "Impossibile verificare ultima versione: $($_.Exception.Message)" -Level Warning
        return $null
    }
}

function Get-SystemArchitecture {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    return $arch.ToString().ToLower()
}

function Get-ReleaseRuntimeIdentifier {
    $arch = Get-SystemArchitecture
    switch ($arch) {
        "x64" { return "win-x64" }
        "arm64" { return "win-arm64" }
        default {
            Exit-WithError `
                -Code "KNOT-OS-002" `
                -Message "Architettura non supportata: $arch" `
                -Hint "Architetture supportate: x64, arm64" `
                -ExitCode 11
        }
    }
}

function Get-ReleaseUrl {
    $rid = Get-ReleaseRuntimeIdentifier
    $archSuffix = $rid -replace "^win-", ""
    return "https://github.com/$GITHUB_REPO/releases/latest/download/knot-win-$archSuffix.exe"
}

# ============================================================================
# VERIFICA INSTALLAZIONE
# ============================================================================

function Test-ExistingInstallation {
    param([string]$BinPath)
    
    $knotExe = Join-Path $BinPath "$CLI_NAME.exe"
    
    if (-not (Test-Path $knotExe)) {
        Exit-WithError `
            -Code "KNOT-INS-001" `
            -Message "KnotVM non installato. Esegui install.ps1 prima di aggiornare." `
            -Hint ".\install.ps1" `
            -ExitCode 40
    }
    
    Write-ColorOutput "✓ Installazione esistente trovata: $knotExe" -Level Success
    return $knotExe
}

# ============================================================================
# AGGIORNAMENTO
# ============================================================================

function Update-CliBinary {
    param(
        [string]$KnotExe,
        [string]$CurrentVersion,
        [string]$LatestVersion
    )
    
    # Verifica se aggiornamento necessario
    if (-not $Force -and $CurrentVersion -ne "unknown" -and $LatestVersion) {
        if ($CurrentVersion -eq $LatestVersion) {
            Write-ColorOutput "Già all'ultima versione: $CurrentVersion" -Level Success
            Write-ColorOutput "Usa -Force per forzare reinstallazione" -Level Info
            return $false
        }
    }
    
    Write-ColorOutput "Aggiornamento: $CurrentVersion -> $LatestVersion" -Level Info
    
    # Backup versione corrente
    $backupPath = "$KnotExe.backup"
    try {
        Copy-Item -Path $KnotExe -Destination $backupPath -Force
        Write-ColorOutput "✓ Backup creato: $backupPath" -Level Info
    }
    catch {
        Exit-WithError `
            -Code "KNOT-PERM-001" `
            -Message "Impossibile creare backup: $($_.Exception.Message)" `
            -Hint "Verifica permessi o chiudi processi che usano knot.exe" `
            -ExitCode 21
    }
    
    # Download nuova versione
    $tempFile = Join-Path $env:TEMP "$CLI_NAME-update.exe"
    $releaseUrl = Get-ReleaseUrl
    
    try {
        Write-ColorOutput "Download nuova versione da $releaseUrl..." -Level Info
        
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $releaseUrl -OutFile $tempFile -UseBasicParsing
        $ProgressPreference = 'Continue'
        
        if (-not (Test-Path $tempFile)) {
            throw "Download fallito: file non trovato"
        }
        
        # Verifica dimensione
        $fileInfo = Get-Item $tempFile
        if ($fileInfo.Length -lt 102400) {
            throw "Download incompleto: dimensione sospetta ($($fileInfo.Length) bytes)"
        }
        
        Write-ColorOutput "✓ Download completato ($('{0:N2}' -f ($fileInfo.Length / 1MB)) MB)" -Level Success
    }
    catch {
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        # Ripristina backup
        if (Test-Path $backupPath) {
            Copy-Item -Path $backupPath -Destination $KnotExe -Force
            Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
        }
        
        Exit-WithError `
            -Code "KNOT-DL-001" `
            -Message "Download fallito: $($_.Exception.Message)" `
            -Hint "Verifica connessione internet e riprova" `
            -ExitCode 32
    }
    
    # Sostituisci binario
    try {
        Write-ColorOutput "Installazione nuova versione..." -Level Info
        Copy-Item -Path $tempFile -Destination $KnotExe -Force
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        Write-ColorOutput "✓ Binario aggiornato" -Level Success
    }
    catch {
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        # Rollback
        Write-ColorOutput "Errore durante sostituzione, rollback in corso..." -Level Warning
        if (Test-Path $backupPath) {
            Copy-Item -Path $backupPath -Destination $KnotExe -Force
            Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
            Write-ColorOutput "✓ Rollback completato, versione precedente ripristinata" -Level Success
        }
        
        Exit-WithError `
            -Code "KNOT-PERM-001" `
            -Message "Impossibile sostituire binario: $($_.Exception.Message)" `
            -Hint "Chiudi tutti i processi knot.exe e riprova" `
            -ExitCode 21
    }
    
    # Test nuova versione
    try {
        $newVersion = Get-CurrentVersion -KnotExe $KnotExe
        if ($newVersion -eq "unknown") {
            throw "Impossibile verificare versione dopo aggiornamento"
        }
        
        # Test comando base
        & $KnotExe --help | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Comando --help fallito con exit code $LASTEXITCODE"
        }
        
        Write-ColorOutput "✓ Nuova versione verificata: $newVersion" -Level Success
        
        # Rimuovi backup dopo successo
        Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
        
        return $true
    }
    catch {
        # Rollback critico
        Write-ColorOutput "Nuova versione non funzionante, rollback..." -Level Error
        if (Test-Path $backupPath) {
            Copy-Item -Path $backupPath -Destination $KnotExe -Force
            Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
            Write-ColorOutput "✓ Rollback completato" -Level Success
        }
        
        Exit-WithError `
            -Code "KNOT-GEN-001" `
            -Message "Nuova versione difettosa, rollback eseguito: $($_.Exception.Message)" `
            -Hint "Segnala il problema su GitHub: https://github.com/$GITHUB_REPO/issues" `
            -ExitCode 99
    }
}

# ============================================================================
# MAIN
# ============================================================================

function Main {
    Write-Host ""
    Write-ColorOutput "=== KnotVM Updater per Windows ===" -Level Info
    Write-Host ""
    
    # Determina paths
    $knotHome = Get-KnotHome
    $binPath = Join-Path $knotHome "bin"
    
    Write-ColorOutput "KNOT_HOME: $knotHome" -Level Info
    Write-Host ""
    
    # Verifica installazione esistente
    $knotExe = Test-ExistingInstallation -BinPath $binPath
    
    # Verifica versioni
    $currentVersion = Get-CurrentVersion -KnotExe $knotExe
    Write-ColorOutput "Versione corrente: $currentVersion" -Level Info
    
    $latestVersion = Get-LatestVersion
    if ($latestVersion) {
        Write-ColorOutput "Ultima versione disponibile: $latestVersion" -Level Info
    }
    else {
        Write-ColorOutput "Impossibile determinare ultima versione (procedo comunque)" -Level Warning
        $latestVersion = "latest"
    }
    
    Write-Host ""
    
    # Esegui aggiornamento
    $updated = Update-CliBinary -KnotExe $knotExe -CurrentVersion $currentVersion -LatestVersion $latestVersion
    
    # Messaggio finale
    Write-Host ""
    if ($updated) {
        Write-ColorOutput "=== Aggiornamento completato con successo! ===" -Level Success
        Write-Host ""
        Write-ColorOutput "Esegui 'knot version' per verificare la nuova versione" -Level Info
    }
    else {
        Write-ColorOutput "=== Nessun aggiornamento necessario ===" -Level Success
    }
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
