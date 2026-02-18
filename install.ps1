#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Script di installazione KnotVM per Windows.
.DESCRIPTION
    Installa KnotVM CLI in modo idempotente:
    - Crea struttura directory KNOT_HOME
    - Scarica/compila binario CLI
    - Aggiorna PATH user-scope
    - Verifica installazione
.PARAMETER Dev
    Modalità sviluppo: compila da sorgenti locali invece di scaricare release.
.PARAMETER Force
    Forza reinstallazione anche se già presente.
.EXAMPLE
    .\install.ps1
    .\install.ps1 -Dev
    .\install.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch]$Dev,
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

function Get-DefaultLegacyHome {
    $appData = [Environment]::GetFolderPath("ApplicationData")
    return Join-Path $appData "node-local"
}

function Test-LegacyNodeLocalInstallation {
    param([string]$BasePath)

    if (-not (Test-Path $BasePath)) {
        return $false
    }

    $legacyMarkers = @(
        (Join-Path $BasePath "node-local.ps1"),
        (Join-Path $BasePath "node-local.cmd"),
        (Join-Path $BasePath "node-local.exe"),
        (Join-Path $BasePath "mode.txt"),
        (Join-Path $BasePath "lib"),
        (Join-Path (Join-Path $BasePath "bin") "node-local.exe"),
        (Join-Path (Join-Path $BasePath "bin") "node-local.cmd"),
        (Join-Path (Join-Path $BasePath "bin") "node-local.ps1"),
        (Join-Path (Join-Path $BasePath "bin") "node-local")
    )

    foreach ($marker in $legacyMarkers) {
        if (Test-Path $marker) {
            return $true
        }
    }

    $hasVersions = Test-Path (Join-Path $BasePath "versions")
    $hasSettings = Test-Path (Join-Path $BasePath "settings.txt")
    $hasKnotBinary = Test-Path (Join-Path (Join-Path $BasePath "bin") "$CLI_NAME.exe")

    return ($hasVersions -and $hasSettings -and -not $hasKnotBinary)
}

function Get-LegacyInstallPath {
    param([string]$KnotHome)

    if (Test-LegacyNodeLocalInstallation -BasePath $KnotHome) {
        return $KnotHome
    }

    $defaultLegacyHome = Get-DefaultLegacyHome
    if (($defaultLegacyHome -ne $KnotHome) -and (Test-LegacyNodeLocalInstallation -BasePath $defaultLegacyHome)) {
        return $defaultLegacyHome
    }

    return $null
}

function Get-ActiveAliasFromSettings {
    param([string]$BasePath)

    $settingsFile = Join-Path $BasePath "settings.txt"
    if (-not (Test-Path $settingsFile)) {
        return $null
    }

    try {
        $content = [System.IO.File]::ReadAllText($settingsFile).Trim()
        if ([string]::IsNullOrWhiteSpace($content)) {
            return $null
        }

        return $content
    }
    catch {
        return $null
    }
}

function Set-ActiveAliasInKnotHome {
    param(
        [string]$KnotHome,
        [string]$Alias
    )

    if ([string]::IsNullOrWhiteSpace($Alias)) {
        return $false
    }

    $settingsFile = Join-Path $KnotHome "settings.txt"
    try {
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($settingsFile, $Alias, $utf8NoBom)
        Write-ColorOutput "✓ Alias attivo legacy preservato: $Alias" -Level Success
        return $true
    }
    catch {
        Write-ColorOutput "WARN: Impossibile preservare alias legacy '$Alias': $($_.Exception.Message)" -Level Warning
        return $false
    }
}

function Show-LegacyMigrationGuide {
    param(
        [string]$LegacyPath,
        [string]$TargetPath,
        [string]$ActiveAlias
    )

    Write-Host ""
    Write-ColorOutput "Rilevata installazione precedente di node-local." -Level Warning
    Write-ColorOutput "Percorso legacy: $LegacyPath" -Level Warning
    Write-ColorOutput "Migrazione automatica verso KnotVM in corso..." -Level Info
    Write-ColorOutput "  • Le versioni in versions/ verranno riutilizzate con gli stessi alias" -Level Info
    Write-ColorOutput "  • La cache in cache/ verrà riutilizzata (niente download duplicati)" -Level Info
    Write-ColorOutput "  • Verranno rimossi artefatti legacy non più necessari" -Level Info

    if ($LegacyPath -ne $TargetPath) {
        Write-ColorOutput "  • I dati legacy verranno copiati in: $TargetPath" -Level Info
    }

    if (-not [string]::IsNullOrWhiteSpace($ActiveAlias)) {
        Write-ColorOutput "  • Alias attivo rilevato: $ActiveAlias" -Level Info
    }
}

function Merge-LegacyDataIntoKnotHome {
    param(
        [string]$LegacyPath,
        [string]$KnotHome
    )

    if ($LegacyPath -eq $KnotHome) {
        return
    }

    Write-ColorOutput "Migrazione dati legacy da '$LegacyPath' a '$KnotHome'..." -Level Info

    $migrations = @(
        @{ Name = "versions"; Source = (Join-Path $LegacyPath "versions"); Target = (Join-Path $KnotHome "versions") },
        @{ Name = "cache"; Source = (Join-Path $LegacyPath "cache"); Target = (Join-Path $KnotHome "cache") }
    )

    foreach ($migration in $migrations) {
        if (-not (Test-Path $migration.Source)) {
            continue
        }

        if (-not (Test-Path $migration.Target)) {
            New-Item -ItemType Directory -Path $migration.Target -Force | Out-Null
        }

        Get-ChildItem -Path $migration.Source -Force | ForEach-Object {
            $destinationItem = Join-Path $migration.Target $_.Name
            if (-not (Test-Path $destinationItem)) {
                Copy-Item -Path $_.FullName -Destination $destinationItem -Recurse -Force
            }
        }

        Write-ColorOutput "✓ Dati migrati: $($migration.Name)/" -Level Success
    }

    $sourceSettings = Join-Path $LegacyPath "settings.txt"
    $targetSettings = Join-Path $KnotHome "settings.txt"
    if (Test-Path $sourceSettings) {
        Copy-Item -Path $sourceSettings -Destination $targetSettings -Force
        Write-ColorOutput "✓ settings.txt legacy migrato" -Level Success
    }
}

function Remove-LegacyNodeLocalArtifacts {
    param([string]$BasePath)

    $legacyItems = @(
        (Join-Path $BasePath "node-local.ps1"),
        (Join-Path $BasePath "node-local.cmd"),
        (Join-Path $BasePath "node-local.exe"),
        (Join-Path $BasePath "mode.txt"),
        (Join-Path $BasePath "lib")
    )

    $legacyBinPath = Join-Path $BasePath "bin"
    if (Test-Path $legacyBinPath) {
        $legacyItems += @(
            (Join-Path $legacyBinPath "node-local.exe"),
            (Join-Path $legacyBinPath "node-local.cmd"),
            (Join-Path $legacyBinPath "node-local.ps1"),
            (Join-Path $legacyBinPath "node-local")
        )
    }

    $removed = 0
    foreach ($item in $legacyItems) {
        if (-not (Test-Path $item)) {
            continue
        }

        try {
            Remove-Item -Path $item -Recurse -Force -ErrorAction Stop
            $removed++
        }
        catch {
            Write-ColorOutput "WARN: Impossibile rimuovere artefatto legacy '$item': $($_.Exception.Message)" -Level Warning
        }
    }

    if ($removed -gt 0) {
        Write-ColorOutput "✓ Rimossi $removed artefatti legacy node-local da $BasePath" -Level Success
    }
}

function Remove-LegacyRootIfMigrated {
    param(
        [string]$LegacyPath,
        [string]$KnotHome
    )

    if ($LegacyPath -eq $KnotHome) {
        return
    }

    if (-not (Test-Path $LegacyPath)) {
        return
    }

    $leafName = Split-Path -Leaf $LegacyPath
    if ($leafName -ne "node-local") {
        Write-ColorOutput "WARN: Skip rimozione root legacy non attesa: $LegacyPath" -Level Warning
        return
    }

    try {
        Remove-Item -Path $LegacyPath -Recurse -Force -ErrorAction Stop
        Write-ColorOutput "✓ Rimossa root legacy node-local: $LegacyPath" -Level Success
    }
    catch {
        Write-ColorOutput "WARN: Impossibile rimuovere root legacy '$LegacyPath': $($_.Exception.Message)" -Level Warning
    }
}

function Invoke-PostMigrationTasks {
    param(
        [string]$BinPath,
        [string]$ActiveAlias
    )

    $knotExe = Join-Path $BinPath "$CLI_NAME.exe"
    if (-not (Test-Path $knotExe)) {
        return
    }

    $aliasRestored = $false
    if (-not [string]::IsNullOrWhiteSpace($ActiveAlias)) {
        try {
            Write-ColorOutput "Ripristino alias attivo legacy '$ActiveAlias'..." -Level Info
            & $knotExe use $ActiveAlias | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-ColorOutput "✓ Alias attivo preservato: $ActiveAlias" -Level Success
                $aliasRestored = $true
            }
            else {
                Write-ColorOutput "WARN: Impossibile attivare alias legacy '$ActiveAlias' (exit code $LASTEXITCODE)" -Level Warning
            }
        }
        catch {
            Write-ColorOutput "WARN: Errore durante ripristino alias legacy '$ActiveAlias': $($_.Exception.Message)" -Level Warning
        }
    }

    if ($aliasRestored) {
        return
    }

    try {
        Write-ColorOutput "Rigenerazione proxy KnotVM..." -Level Info
        & $knotExe sync --force | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ Proxy rigenerati con successo" -Level Success
        }
        else {
            Write-ColorOutput "WARN: Sync proxy completata con exit code $LASTEXITCODE" -Level Warning
        }
    }
    catch {
        Write-ColorOutput "WARN: Impossibile completare sync post-migrazione: $($_.Exception.Message)" -Level Warning
    }
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Add-ToUserPath {
    param([string]$PathToAdd)
    
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
    
    # Normalizza path per confronto case-insensitive
    $pathEntries = $currentPath -split ';' | Where-Object { $_ }
    $normalizedEntries = $pathEntries | ForEach-Object { $_.TrimEnd('\').ToLowerInvariant() }
    $normalizedNew = $PathToAdd.TrimEnd('\').ToLowerInvariant()
    
    if ($normalizedEntries -contains $normalizedNew) {
        Write-ColorOutput "PATH già contiene $PathToAdd (idempotente)" -Level Info
        return $false
    }
    
    try {
        $newPath = "$PathToAdd;$currentPath"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        Write-ColorOutput "PATH aggiornato (user-scope): $PathToAdd aggiunto" -Level Success
        return $true
    }
    catch {
        Exit-WithError `
            -Code "KNOT-PATH-001" `
            -Message "Impossibile aggiornare PATH user-scope" `
            -Hint "Verifica permessi utente o esegui manualmente: `$env:Path += ';$PathToAdd'" `
            -ExitCode 20
    }
}

function Remove-FromUserPathIfPresent {
    param([string]$PathToRemove)

    $currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ([string]::IsNullOrWhiteSpace($currentPath)) {
        return $false
    }

    $pathEntries = $currentPath -split ';' | Where-Object { $_ }
    $normalizedRemove = $PathToRemove.TrimEnd('\').ToLowerInvariant()

    $newEntries = $pathEntries | Where-Object {
        $_.TrimEnd('\').ToLowerInvariant() -ne $normalizedRemove
    }

    if ($newEntries.Count -eq $pathEntries.Count) {
        return $false
    }

    try {
        $newPath = $newEntries -join ';'
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        Write-ColorOutput "✓ PATH legacy rimosso: $PathToRemove" -Level Success
        return $true
    }
    catch {
        Write-ColorOutput "WARN: Impossibile rimuovere PATH legacy '$PathToRemove': $($_.Exception.Message)" -Level Warning
        return $false
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
                -Message "Architettura non supportata per publish: $arch" `
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

# ============================================================================
# PREFLIGHT CHECKS
# ============================================================================

function Invoke-PreflightChecks {
    Write-ColorOutput "Esecuzione preflight checks..." -Level Info
    
    # 1. Verifica OS
    if (-not $IsWindows -and -not ($PSVersionTable.PSVersion.Major -ge 6 -eq $false)) {
        # PowerShell 5.x è solo Windows, PowerShell 6+ può essere cross-platform
        if ($PSVersionTable.PSVersion.Major -ge 6 -and -not $IsWindows) {
            Exit-WithError `
                -Code "KNOT-OS-001" `
                -Message "Sistema operativo non supportato. Richiesto: Windows" `
                -Hint "Usa install.sh per Linux/macOS" `
                -ExitCode 10
        }
    }
    
    # 2. Verifica architettura
    $arch = Get-SystemArchitecture
    $supportedArchs = @('x64', 'arm64')
    if ($arch -notin $supportedArchs) {
        Exit-WithError `
            -Code "KNOT-OS-002" `
            -Message "Architettura non supportata: $arch" `
            -Hint "Architetture supportate: x64, arm64" `
            -ExitCode 11
    }
    
    Write-ColorOutput "✓ OS: Windows, Arch: $arch" -Level Success
    
    # 3. Avviso se Administrator (non necessario)
    if (Test-Administrator) {
        Write-ColorOutput "Esecuzione come Administrator rilevata. Non necessario per installazione user-scope." -Level Warning
    }
}

# ============================================================================
# INSTALLAZIONE
# ============================================================================

function Initialize-KnotHome {
    param([string]$KnotHome)
    
    Write-ColorOutput "Inizializzazione KNOT_HOME: $KnotHome" -Level Info
    
    $directories = @(
        $KnotHome,
        (Join-Path $KnotHome "bin"),
        (Join-Path $KnotHome "versions"),
        (Join-Path $KnotHome "cache"),
        (Join-Path $KnotHome "templates"),
        (Join-Path $KnotHome "locks")
    )
    
    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            try {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Write-ColorOutput "✓ Creata directory: $dir" -Level Info
            }
            catch {
                Exit-WithError `
                    -Code "KNOT-PATH-001" `
                    -Message "Impossibile creare directory: $dir" `
                    -Hint "Verifica permessi scrittura o imposta KNOT_HOME in una posizione accessibile" `
                    -ExitCode 20
            }
        }
    }
    
    # Copia template se disponibili
    $repoTemplatesDir = Join-Path $PSScriptRoot "templates"
    if (Test-Path $repoTemplatesDir) {
        $targetTemplatesDir = Join-Path $KnotHome "templates"
        try {
            Get-ChildItem -Path $repoTemplatesDir -Filter "*.template" | ForEach-Object {
                Copy-Item -Path $_.FullName -Destination $targetTemplatesDir -Force
            }
            Write-ColorOutput "✓ Template copiati" -Level Info
        }
        catch {
            Write-ColorOutput "⚠ Impossibile copiare template: $_" -Level Warning
        }
    }
    
    Write-ColorOutput "✓ Struttura KNOT_HOME verificata" -Level Success
}

function Install-CliBinary {
    param(
        [string]$BinPath,
        [bool]$DevMode
    )
    
    $targetExe = Join-Path $BinPath "$CLI_NAME.exe"
    
    # Verifica se già installato
    if ((Test-Path $targetExe) -and -not $Force) {
        Write-ColorOutput "$CLI_NAME.exe già presente in $BinPath" -Level Info
        
        # Test esecuzione
        try {
            $currentVersion = Get-CurrentVersion -KnotExe $targetExe
            if ($currentVersion -ne "unknown") {
                Write-ColorOutput "✓ Versione installata: $currentVersion" -Level Success
                
                # Verifica se c'è una nuova versione disponibile (solo se non in dev mode)
                if (-not $DevMode) {
                    Write-ColorOutput "Verifica aggiornamenti disponibili..." -Level Info
                    $latestVersion = Get-LatestVersion
                    
                    if ($latestVersion -and $latestVersion -ne $currentVersion) {
                        Write-ColorOutput "Nuova versione disponibile: $latestVersion" -Level Warning
                        Write-Host ""
                        
                        if ((Read-Host "Vuoi aggiornare a questa versione? (s/N)") -match '^[sS]$') {
                            return Update-ExistingInstallation -KnotExe $targetExe -CurrentVersion $currentVersion -LatestVersion $latestVersion
                        }
                        else {
                            Write-ColorOutput "Aggiornamento saltato. Installazione corrente mantenuta." -Level Info
                            Write-ColorOutput "Hint: Usa 'knot --version' per verificare la versione corrente" -Level Info
                            Write-ColorOutput "Hint: Esegui '.\update.ps1' in qualsiasi momento per aggiornare" -Level Info
                            return $true
                        }
                    }
                    elseif ($latestVersion -and $latestVersion -eq $currentVersion) {
                        Write-ColorOutput "✓ Già all'ultima versione disponibile" -Level Success
                        Write-ColorOutput "Hint: Usa -Force per reinstallare" -Level Info
                        return $true
                    }
                    else {
                        Write-ColorOutput "✓ Installazione esistente funzionante" -Level Success
                        Write-ColorOutput "Hint: Usa -Force per reinstallare" -Level Info
                        return $true
                    }
                }
                else {
                    Write-ColorOutput "✓ Installazione esistente funzionante (modalità dev)" -Level Success
                    Write-ColorOutput "Hint: Usa -Force per reinstallare" -Level Info
                    return $true
                }
            }
        }
        catch {
            Write-ColorOutput "Binario esistente non funzionante, procedo con reinstallazione..." -Level Warning
        }
    }
    
    if ($DevMode) {
        Write-ColorOutput "Modalità sviluppo: compilazione da sorgenti..." -Level Info
        return Install-FromSource -TargetPath $targetExe
    }
    else {
        Write-ColorOutput "Download release da GitHub..." -Level Info
        return Install-FromRelease -TargetPath $targetExe
    }
}

function Install-FromSource {
    param([string]$TargetPath)
    
    # Verifica che siamo nella directory corretta
    $solutionFile = Join-Path $PSScriptRoot "KnotVM.sln"
    if (-not (Test-Path $solutionFile)) {
        Write-ColorOutput "KnotVM.sln non trovato. Esegui install.ps1 dalla root del repository o usa modalità release." -Level Error
        Write-ColorOutput "Hint: cd nella directory root del progetto oppure ometti -Dev per scaricare release" -Level Warning
        return $false
    }
    
    # Verifica dotnet CLI
    $dotnetPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetPath) {
        Write-ColorOutput "dotnet CLI non trovato nel PATH" -Level Error
        Write-ColorOutput "Hint: Installa .NET 8.0 SDK da https://dot.net" -Level Warning
        return $false
    }
    
    $runtimeIdentifier = Get-ReleaseRuntimeIdentifier
    Write-ColorOutput "Publish progetto KnotVM.CLI (RID: $runtimeIdentifier)..." -Level Info
    
    try {
        $projectPath = Join-Path $PSScriptRoot "src\KnotVM.CLI\KnotVM.CLI.csproj"
        
        # Publish self-contained per ottenere un eseguibile standalone.
        & dotnet publish $projectPath -c Release -r $runtimeIdentifier --self-contained -p:PublishSingleFile=true --nologo -v quiet
        if ($LASTEXITCODE -ne 0) {
            throw "Publish fallita con exit code $LASTEXITCODE"
        }
        
        # Trova output publish
        $outputPath = Join-Path $PSScriptRoot "src\KnotVM.CLI\bin\Release\net8.0\$runtimeIdentifier\publish\knot.exe"
        if (-not (Test-Path $outputPath)) {
            throw "Output binario non trovato: $outputPath"
        }
        
        # Copia
        Copy-Item -Path $outputPath -Destination $TargetPath -Force
        Write-ColorOutput "✓ Binario pubblicato e copiato in $TargetPath" -Level Success
        
        return $true
    }
    catch {
        Write-ColorOutput "Publish fallita: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Verifica errori publish con: dotnet publish src\KnotVM.CLI\KnotVM.CLI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true" -Level Warning
        return $false
    }
}

function Install-FromRelease {
    param([string]$TargetPath)
    
    $tempFile = Join-Path $env:TEMP "$CLI_NAME-latest.exe"
    $releaseUrl = Get-ReleaseUrl
    
    try {
        Write-ColorOutput "Download da $releaseUrl..." -Level Info
        
        # Download con progress
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $releaseUrl -OutFile $tempFile -UseBasicParsing
        $ProgressPreference = 'Continue'
        
        if (-not (Test-Path $tempFile)) {
            throw "Download fallito: file non trovato"
        }
        
        # Verifica dimensione minima (>100KB)
        $fileInfo = Get-Item $tempFile
        if ($fileInfo.Length -lt 102400) {
            throw "Download incompleto: dimensione file sospetta ($($fileInfo.Length) bytes)"
        }
        
        # Copia in posizione finale
        Copy-Item -Path $tempFile -Destination $TargetPath -Force
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        Write-ColorOutput "✓ Download completato e binario installato" -Level Success
        return $true
    }
    catch {
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        Write-ColorOutput "Download release fallito: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Verifica connessione internet o usa modalità -Dev per compilare da sorgenti" -Level Warning
        return $false
    }
}

function Update-ExistingInstallation {
    param(
        [string]$KnotExe,
        [string]$CurrentVersion,
        [string]$LatestVersion
    )
    
    Write-Host ""
    Write-ColorOutput "Aggiornamento: $CurrentVersion -> $LatestVersion" -Level Info
    Write-Host ""
    
    # Backup versione corrente
    $backupPath = "$KnotExe.backup"
    try {
        Copy-Item -Path $KnotExe -Destination $backupPath -Force
        Write-ColorOutput "✓ Backup creato" -Level Info
    }
    catch {
        Write-ColorOutput "Impossibile creare backup: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Chiudi processi che usano knot.exe e riprova" -Level Warning
        return $false
    }
    
    # Download nuova versione
    $tempFile = Join-Path $env:TEMP "$CLI_NAME-update.exe"
    $releaseUrl = Get-ReleaseUrl
    
    try {
        Write-ColorOutput "Download nuova versione..." -Level Info
        
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $releaseUrl -OutFile $tempFile -UseBasicParsing
        $ProgressPreference = 'Continue'
        
        if (-not (Test-Path $tempFile)) {
            throw "Download fallito: file non trovato"
        }
        
        # Verifica dimensione
        $fileInfo = Get-Item $tempFile
        if ($fileInfo.Length -lt 102400) {
            throw "Download incompleto: dimensione sospetta"
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
        
        Write-ColorOutput "Download fallito: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Verifica connessione internet e riprova" -Level Warning
        return $false
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
            Write-ColorOutput "✓ Rollback completato" -Level Success
        }
        
        Write-ColorOutput "Impossibile sostituire binario: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Chiudi tutti i processi knot.exe e riprova" -Level Warning
        return $false
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
            throw "Comando --help fallito"
        }
        
        Write-ColorOutput "✓ Nuova versione verificata: $newVersion" -Level Success
        
        # Rimuovi backup dopo successo
        Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
        
        Write-Host ""
        Write-ColorOutput "✓ Aggiornamento completato con successo!" -Level Success
        
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
        
        Write-ColorOutput "Aggiornamento fallito: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Versione precedente ripristinata" -Level Warning
        return $false
    }
}

function Test-Installation {
    param([string]$BinPath)
    
    Write-ColorOutput "Verifica installazione..." -Level Info
    
    $knotExe = Join-Path $BinPath "$CLI_NAME.exe"
    
    if (-not (Test-Path $knotExe)) {
        Write-ColorOutput "Binario knot.exe non trovato dopo installazione" -Level Error
        Write-ColorOutput "Hint: Ripeti installazione o contatta supporto" -Level Warning
        return $false
    }
    
    try {
        # Test esecuzione version
        $output = & $knotExe version 2>&1
        $exitCode = $LASTEXITCODE
        
        if ($exitCode -ne 0) {
            throw "Exit code non zero: $exitCode"
        }
        
        Write-ColorOutput "✓ $CLI_NAME.exe funzionante: $output" -Level Success
        return $true
    }
    catch {
        Write-ColorOutput "Binario installato ma non eseguibile: $($_.Exception.Message)" -Level Error
        Write-ColorOutput "Hint: Verifica antivirus o permessi esecuzione" -Level Warning
        return $false
    }
}

# ============================================================================
# MAIN
# ============================================================================

function Main {
    Write-Host ""
    Write-ColorOutput "=== KnotVM Installer per Windows ===" -Level Info
    Write-Host ""
    
    # Preflight
    Invoke-PreflightChecks
    
    # Determina KNOT_HOME
    $knotHome = Get-KnotHome
    $binPath = Join-Path $knotHome "bin"
    $legacyPath = Get-LegacyInstallPath -KnotHome $knotHome
    $legacyDetected = -not [string]::IsNullOrWhiteSpace($legacyPath)
    $legacyAlias = $null

    if ($legacyDetected) {
        $legacyAlias = Get-ActiveAliasFromSettings -BasePath $legacyPath
        Show-LegacyMigrationGuide -LegacyPath $legacyPath -TargetPath $knotHome -ActiveAlias $legacyAlias
    }
    
    Write-ColorOutput "KNOT_HOME: $knotHome" -Level Info
    Write-Host ""
    
    # Inizializza struttura
    Initialize-KnotHome -KnotHome $knotHome

    # Migra dati legacy se installazione rilevata in path diverso
    if ($legacyDetected) {
        Merge-LegacyDataIntoKnotHome -LegacyPath $legacyPath -KnotHome $knotHome
        if (-not [string]::IsNullOrWhiteSpace($legacyAlias)) {
            Set-ActiveAliasInKnotHome -KnotHome $knotHome -Alias $legacyAlias | Out-Null
        }
    }
    
    # ========================================================================
    # FASE CRITICA: Installazione binario
    # Se fallisce, terminare senza modificare node-local
    # ========================================================================
    
    Write-Host ""
    Write-ColorOutput "Installazione binario KnotVM..." -Level Info
    
    # Installa binario
    $installed = Install-CliBinary -BinPath $binPath -DevMode $Dev
    
    if (-not $installed) {
        Write-Host ""
        Write-ColorOutput "=== Installazione fallita ===" -Level Error
        Write-Host ""
        Write-ColorOutput "Il binario KnotVM non è stato installato correttamente." -Level Error
        
        if ($legacyDetected) {
            Write-ColorOutput "La tua installazione node-local è ancora intatta e funzionante." -Level Info
            Write-ColorOutput "Risolvi il problema sopra e rilancia install.ps1 per riprovare." -Level Info
        }
        
        Exit-WithError `
            -Code "KNOT-INSTALL-001" `
            -Message "Installazione binario fallita" `
            -Hint "Verifica connessione internet per download release o usa -Dev se hai i sorgenti" `
            -ExitCode 32
    }
    
    # Test installazione
    Write-Host ""
    $testPassed = Test-Installation -BinPath $binPath
    
    if (-not $testPassed) {
        Write-Host ""
        Write-ColorOutput "=== Installazione fallita ===" -Level Error
        Write-Host ""
        Write-ColorOutput "Il binario KnotVM è stato scaricato ma non funziona correttamente." -Level Error
        
        # Rollback: rimuovi binario non funzionante
        $knotExe = Join-Path $binPath "$CLI_NAME.exe"
        if (Test-Path $knotExe) {
            try {
                Remove-Item -Path $knotExe -Force -ErrorAction Stop
                Write-ColorOutput "Binario non funzionante rimosso." -Level Info
            }
            catch {
                Write-ColorOutput "WARN: Impossibile rimuovere binario non funzionante: $($_.Exception.Message)" -Level Warning
            }
        }
        
        if ($legacyDetected) {
            Write-ColorOutput "La tua installazione node-local è ancora intatta e funzionante." -Level Info
        }
        
        Exit-WithError `
            -Code "KNOT-INSTALL-002" `
            -Message "Test installazione fallito" `
            -Hint "Verifica antivirus o permessi, poi rilancia install.ps1" `
            -ExitCode 33
    }

    # ========================================================================
    # SUCCESSO: Ora è sicuro pulire node-local e aggiornare PATH
    # ========================================================================

    Write-Host ""
    Write-ColorOutput "✓ Installazione binario completata con successo!" -Level Success

    # Pulizia legacy + sync proxy post-migrazione
    if ($legacyDetected) {
        Write-Host ""
        Write-ColorOutput "Pulizia artefatti legacy node-local..." -Level Info
        
        Remove-LegacyNodeLocalArtifacts -BasePath $knotHome
        if ($legacyPath -ne $knotHome) {
            Remove-LegacyNodeLocalArtifacts -BasePath $legacyPath
            Remove-LegacyRootIfMigrated -LegacyPath $legacyPath -KnotHome $knotHome
        }
        Invoke-PostMigrationTasks -BinPath $binPath -ActiveAlias $legacyAlias
    }

    # Rimuovi eventuale PATH legacy quando migrazione da path diverso
    if ($legacyDetected -and ($legacyPath -ne $knotHome)) {
        $legacyBinPath = Join-Path $legacyPath "bin"
        Remove-FromUserPathIfPresent -PathToRemove $legacyBinPath | Out-Null
    }
    
    # Aggiorna PATH
    Write-Host ""
    $pathUpdated = Add-ToUserPath -PathToAdd $binPath
    
    # Messaggio finale
    Write-Host ""
    
    # Esegue knot version per mostrare il banner
    try {
        $knotExe = Join-Path $binPath "$CLI_NAME.exe"
        if (Test-Path $knotExe) {
            & $knotExe version
        }
    }
    catch {
        # Fallback se knot version fallisce
        Write-ColorOutput "=== Installazione completata con successo! ===" -Level Success
    }
    
    Write-Host ""
    Write-ColorOutput "Per iniziare:" -Level Info
    Write-ColorOutput "  1. Riavvia il terminale per caricare il nuovo PATH" -Level Info
    Write-ColorOutput "  2. Esegui: knot --help" -Level Info
    Write-ColorOutput "  3. Installa Node.js: knot install --latest-lts" -Level Info
    Write-Host ""
    
    if ($pathUpdated) {
        Write-ColorOutput "IMPORTANTE: Il PATH è stato aggiornato. Riavvia il terminale!" -Level Warning
    }

    if ($legacyDetected) {
        Write-ColorOutput "Migrazione node-local completata: versioni/cache/alias legacy riutilizzati." -Level Success
    }
}

# Esegui main
try {
    Main
}
catch {
    Exit-WithError `
        -Code "KNOT-GEN-001" `
        -Message "Errore inatteso: $($_.Exception.Message)" `
        -Hint "Esegui con -Verbose per maggiori dettagli o contatta supporto" `
        -ExitCode 99
}
