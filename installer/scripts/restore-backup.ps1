<#
    Restores a TitanFit database backup (a titanfit-*.dump file from the daily backup or "Back up now") on the
    server PC. Run it in PowerShell as administrator:

      & 'C:\Program Files\TitanFit\Setup\restore-backup.ps1' -BackupFile 'E:\TitanFit-Backups\titanfit-20261127-0200.dump'

    1. Checks that the file is a TitanFit backup (pg_restore --list).
    2. Asks for YES (skip with -Force).
    3. Backs up the current database first: %ProgramData%\TitanFit\backups\titanfit-before-restore-<time>.dump
       (skip with -SkipSafetyBackup when the current database is broken). The daily clean-up never deletes it.
    4. Stops the TitanFit Server service, replaces the titanfit database with the backup (as the PostgreSQL superuser
       from database-admin.json; the tables belong to the titanfit role again), starts the service (it applies newer
       migrations) and waits for http://localhost:5080/health.

    Everything in the database goes back to the time of the backup: members, payments, check-ins, staff and logins,
    products, stock and sales. Photos, keys and settings files are not in the database and stay as they are.
    Exit code 0 = restored, 1 = failed (the service is started again), 2 = cancelled. Log: %ProgramData%\TitanFit\logs\restore.log.
    Runs on Windows PowerShell 5.1. Never prints a password.
#>
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [switch]$Force,
    [switch]$SkipSafetyBackup
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$AppDir = Split-Path -Parent $PSScriptRoot
$PgBin = Join-Path $AppDir 'pgsql\bin'
$DataRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'TitanFit'
$DbAdminConfig = Join-Path $DataRoot 'database-admin.json'
$SafetyDir = Join-Path $DataRoot 'backups'
$LogFile = Join-Path $DataRoot 'logs\restore.log'
$ApiService = 'TitanFitServer'
$Database = 'titanfit'

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    Write-Host $line
}

# Runs a PostgreSQL tool with the password in the environment; logs its output unless -Quiet; throws on an error.
function Invoke-PgTool {
    param(
        [Parameter(Mandatory = $true)][string]$Tool,
        [string[]]$Arguments = @(),
        [string]$Password,
        [switch]$Quiet
    )
    Write-Log ('> {0} {1}' -f $Tool, ($Arguments -join ' '))
    $env:PGPASSWORD = $Password
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & (Join-Path $PgBin "$Tool.exe") @Arguments 2>&1
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
    if (-not $Quiet -or $code -ne 0) {
        foreach ($line in @($output)) {
            $text = "$line".TrimEnd()
            if ($text) { Write-Log "  $text" }
        }
    }
    if ($code -ne 0) { throw "$Tool failed with exit code $code" }
    return @($output | ForEach-Object { "$_" })
}

function Wait-ForHealth {
    $deadline = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri 'http://localhost:5080/health' -TimeoutSec 5
            if ($health.status -eq 'ok') { return $health.version }
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    throw 'The TitanFit Server did not answer on http://localhost:5080/health within 3 minutes.'
}

$stopped = $false
$safety = $null
$exitCode = 0
try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script in PowerShell as administrator.'
    }
    if (-not (Test-Path -LiteralPath $BackupFile -PathType Leaf)) { throw "Backup file not found: $BackupFile" }
    $BackupFile = (Resolve-Path -LiteralPath $BackupFile).Path
    if (-not (Test-Path -LiteralPath $DbAdminConfig)) { throw "$DbAdminConfig is missing: the setup writes it. Restore it from a copy first." }
    $admin = [System.IO.File]::ReadAllText($DbAdminConfig) | ConvertFrom-Json
    $connection = @('-h', "$($admin.host)", '-p', "$($admin.port)", '-U', "$($admin.username)", '--no-password')

    Write-Log "=== Restore from $BackupFile ($([math]::Round((Get-Item -LiteralPath $BackupFile).Length / 1MB, 1)) MB) ==="
    $list = Invoke-PgTool -Tool 'pg_restore' -Arguments @('--list', $BackupFile) -Quiet
    if (-not (($list -join "`n") -match 'TABLE public members')) { throw "$BackupFile is not a TitanFit database backup." }

    if (-not $Force) {
        Write-Host ''
        Write-Host 'This replaces ALL data in the TitanFit database with the backup above.' -ForegroundColor Yellow
        Write-Host 'Everything entered after the backup was made is lost (a copy of the current data is made first).' -ForegroundColor Yellow
        $answer = Read-Host 'Type YES to restore'
        if ($answer -cne 'YES') {
            Write-Log 'Cancelled: nothing was changed.'
            exit 2
        }
    }

    if (-not $SkipSafetyBackup) {
        New-Item -ItemType Directory -Force -Path $SafetyDir | Out-Null
        $safety = Join-Path $SafetyDir ('titanfit-before-restore-{0:yyyyMMdd-HHmmss}.dump' -f (Get-Date))
        [void](Invoke-PgTool -Tool 'pg_dump' -Arguments ($connection + @('--format=custom', "--dbname=$Database", "--file=$safety")) -Password $admin.password)
        Write-Log "Current data saved to $safety"
    }

    Write-Log "Stopping $ApiService"
    Stop-Service -Name $ApiService -Force
    $stopped = $true

    [void](Invoke-PgTool -Tool 'psql' -Arguments ($connection + @('-X', '-q', '-v', 'ON_ERROR_STOP=1', '-d', 'postgres', '-c', "DROP DATABASE IF EXISTS $Database WITH (FORCE)")) -Password $admin.password)
    [void](Invoke-PgTool -Tool 'psql' -Arguments ($connection + @('-X', '-q', '-v', 'ON_ERROR_STOP=1', '-d', 'postgres', '-c', "CREATE DATABASE $Database OWNER titanfit")) -Password $admin.password)
    [void](Invoke-PgTool -Tool 'pg_restore' -Arguments ($connection + @("--dbname=$Database", '--no-owner', '--role=titanfit', '--exit-on-error', $BackupFile)) -Password $admin.password)
    Write-Log 'Database restored.'

    Write-Log "Starting $ApiService"
    Start-Service -Name $ApiService
    $stopped = $false
    $version = Wait-ForHealth
    Write-Log "=== Done: the server runs again (version $version) with the data of the backup. ==="
} catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    if ($safety -and (Test-Path -LiteralPath $safety)) {
        Write-Log "The data from before the restore is in $safety (restore that file to go back)."
    }
    $exitCode = 1
    if ($stopped) {
        try {
            Start-Service -Name $ApiService
            Write-Log "$ApiService started again."
        } catch {
            Write-Log "Could not start ${ApiService}: $($_.Exception.Message)"
        }
    }
}
exit $exitCode
