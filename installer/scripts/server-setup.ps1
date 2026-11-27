<#
    TitanFit server setup tasks. Run by TitanFit-Server-Setup.exe (Windows PowerShell 5.1, elevated).

      -Action stop       Stops the TitanFit services before the files are replaced (upgrade).
      -Action install    First install: creates the PostgreSQL 17 cluster (port 5433, localhost only), the
                         "titanfit" role and database with random passwords, and server.json.
                         Every run: registers both services, the firewall rule, starts them and waits for /health.
                         An existing database and server.json are kept as they are.
      -Action uninstall  Removes the services and the firewall rule. KEEPS %ProgramData%\TitanFit (database, photos,
                         settings): gym data is never deleted by an uninstall.

    Exit code 0 = done. The log is %ProgramData%\TitanFit\logs\server-setup.log. No password is ever written to the
    log or passed on a command line.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('stop', 'install', 'uninstall')]
    [string]$Action,

    # Install folder (Program Files\TitanFit). Default: the parent of this script's folder ({app}\Setup).
    [string]$AppDir = '',

    # Also write station.json (http://localhost:5080) for the Admin app on this PC, if there is none yet.
    [switch]$AdminApp
)

$ErrorActionPreference = 'Stop'
if (-not $AppDir) { $AppDir = Split-Path -Parent $PSScriptRoot }

# Setup may start the 32-bit PowerShell; services, Program Files and the firewall need the 64-bit one.
if (-not [Environment]::Is64BitProcess -and [Environment]::Is64BitOperatingSystem) {
    $native = Join-Path $env:windir 'sysnative\WindowsPowerShell\v1.0\powershell.exe'
    $forward = @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-Action', $Action, '-AppDir', $AppDir)
    if ($AdminApp) { $forward += '-AdminApp' }
    & $native @forward
    exit $LASTEXITCODE
}

$ApiService = 'TitanFitServer'
$ApiDisplayName = 'TitanFit Server'
$DbService = 'TitanFitDatabase'
$DbDisplayName = 'TitanFit Database (PostgreSQL 17)'
$ApiPort = 5080
$DbPort = 5433
$PgMajor = '17'
$FirewallRule = 'TitanFit-Server-TCP-5080'

$DataRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'TitanFit'
$PgData = Join-Path $DataRoot 'pgdata'
$ServerConfig = Join-Path $DataRoot 'server.json'
$DbAdminConfig = Join-Path $DataRoot 'database-admin.json'
$KeysDir = Join-Path $DataRoot 'keys'
$BackupDir = Join-Path $DataRoot 'backups'
$LogDir = Join-Path $DataRoot 'logs'
$LogFile = Join-Path $LogDir 'server-setup.log'
$PgBin = Join-Path $AppDir 'pgsql\bin'
$ApiExe = Join-Path $AppDir 'Server\Gym.Api.exe'

$SidSystem = 'S-1-5-18'
$SidAdmins = 'S-1-5-32-544'
$SidNetworkService = 'S-1-5-20'
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    Write-Host $line
}

# Runs a program, logs its output (unless -Quiet) and throws on a non-zero exit code (unless -AllowFailure).
# The exit code is left in $script:LastExit.
function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$InputText,
        [switch]$AllowFailure,
        [switch]$Quiet
    )
    Write-Log ('> {0} {1}' -f (Split-Path -Leaf $FilePath), ($Arguments -join ' '))
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # stderr lines must not stop the script; the exit code decides
    try {
        if ($PSBoundParameters.ContainsKey('InputText')) {
            $output = $InputText | & $FilePath @Arguments 2>&1
        } else {
            $output = & $FilePath @Arguments 2>&1
        }
        $script:LastExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    if ($Quiet) {
        if ($script:LastExit -ne 0) { Write-Log '  (output hidden)' }
    } else {
        foreach ($line in @($output)) {
            $text = "$line".TrimEnd()
            if ($text) { Write-Log "  $text" }
        }
    }
    if ($script:LastExit -ne 0 -and -not $AllowFailure) {
        throw ('{0} failed with exit code {1}' -f (Split-Path -Leaf $FilePath), $script:LastExit)
    }
}

# Random password from A-Z a-z 0-9 (no quoting problems in SQL or connection strings), without modulo bias.
function New-Secret([int]$Length = 32) {
    $chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'.ToCharArray()
    $limit = 256 - (256 % $chars.Length)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buffer = New-Object byte[] 64
    $result = New-Object System.Text.StringBuilder
    try {
        while ($result.Length -lt $Length) {
            $rng.GetBytes($buffer)
            foreach ($b in $buffer) {
                if ($b -lt $limit -and $result.Length -lt $Length) { [void]$result.Append($chars[$b % $chars.Length]) }
            }
        }
    } finally {
        $rng.Dispose()
    }
    $result.ToString()
}

# Only SYSTEM and Administrators: no inherited entries, owner = Administrators.
function Protect-Path([string]$Path, [switch]$Folder) {
    $inherit = ''
    if ($Folder) { $inherit = '(OI)(CI)' }
    Invoke-Native icacls.exe @($Path, '/inheritance:r', '/grant:r', "*${SidSystem}:${inherit}F", "*${SidAdmins}:${inherit}F") -Quiet
    Invoke-Native icacls.exe @($Path, '/setowner', "*$SidAdmins") -Quiet
}

# Creates (or replaces) a file that only SYSTEM and Administrators can read. The ACL is set before the content.
function Write-ProtectedFile([string]$Path, [string]$Content) {
    if (-not (Test-Path -LiteralPath $Path)) { [System.IO.File]::WriteAllText($Path, '') }
    Protect-Path $Path
    [System.IO.File]::WriteAllText($Path, $Content, $Utf8NoBom)
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        return [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
    } catch {
        Write-Log "WARNING: $Path is not valid JSON: $($_.Exception.Message)"
        return $null
    }
}

function Get-ServiceOrNull([string]$Name) {
    Get-Service -Name $Name -ErrorAction SilentlyContinue
}

function Stop-ServiceIfRunning([string]$Name) {
    $service = Get-ServiceOrNull $Name
    if ($null -eq $service) { return }
    if ($service.Status -ne 'Stopped') {
        Write-Log "Stopping service $Name"
        Stop-Service -Name $Name -Force   # -Force also stops the services that depend on it
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(120))
    }
    Write-Log "Service $Name is stopped."
}

function Write-RecentServiceEvents {
    try {
        $since = (Get-Date).AddMinutes(-10)
        $events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $since } -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -match 'PostgreSQL|pg_ctl|TitanFit|Gym\.Api|\.NET Runtime|Application Error' } |
            Select-Object -First 15
        foreach ($e in @($events)) { Write-Log ("  event {0} {1}: {2}" -f $e.TimeCreated, $e.ProviderName, ($e.Message -replace '\s+', ' ')) }
        $events = Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'Service Control Manager'; StartTime = $since } -ErrorAction SilentlyContinue |
            Select-Object -First 10
        foreach ($e in @($events)) { Write-Log ("  event {0} SCM: {1}" -f $e.TimeCreated, ($e.Message -replace '\s+', ' ')) }
    } catch {
        Write-Log "  (could not read the event log: $($_.Exception.Message))"
    }
}

function Write-DatabaseLogTail {
    $logFolder = Join-Path $PgData 'log'
    $latest = Get-ChildItem -Path $logFolder -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Log "  last lines of $($latest.FullName):"
        foreach ($line in (Get-Content -LiteralPath $latest.FullName -Tail 30)) { Write-Log "    $line" }
    }
}

function Start-ServiceAndWait([string]$Name) {
    Write-Log "Starting service $Name"
    try {
        $service = Get-Service -Name $Name
        if ($service.Status -eq 'Stopped') { $service.Start() }
        $deadline = (Get-Date).AddMinutes(2)
        do {
            Start-Sleep -Milliseconds 500
            $service.Refresh()
        } while ($service.Status -eq 'StartPending' -and (Get-Date) -lt $deadline)
        if ($service.Status -ne 'Running') { throw "status: $($service.Status)" }
    } catch {
        Write-Log "ERROR: service $Name did not start: $($_.Exception.Message)"
        Write-RecentServiceEvents
        Write-DatabaseLogTail
        throw
    }
    Write-Log "Service $Name is running."
}

# ---------------------------------------------------------------------------------------------------------------
# Database
# ---------------------------------------------------------------------------------------------------------------

# Creates the cluster on the first install. Returns the superuser password for a new cluster, $null otherwise.
function Initialize-Cluster {
    $versionFile = Join-Path $PgData 'PG_VERSION'
    if (Test-Path -LiteralPath $versionFile) {
        $version = (Get-Content -LiteralPath $versionFile -TotalCount 1).Trim()
        Write-Log "Existing database found in $PgData (PostgreSQL $version): kept as it is."
        if ($version -ne $PgMajor) {
            throw "The database in $PgData is PostgreSQL $version, but this setup has PostgreSQL $PgMajor. It needs a manual upgrade (pg_upgrade or a backup and restore)."
        }
        return $null
    }
    if ((Test-Path -LiteralPath $PgData) -and (Get-ChildItem -LiteralPath $PgData -Force | Select-Object -First 1)) {
        throw "$PgData is not empty but holds no database. Move it away and run the setup again."
    }

    Write-Log "Creating a new PostgreSQL $PgMajor database cluster in $PgData"
    New-Item -ItemType Directory -Force -Path $PgData | Out-Null
    # The service runs as NetworkService. initdb drops the Administrators group from its token, so it needs its
    # own entry for the installing account. Nobody else (no ordinary user) can read the database files.
    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    Invoke-Native icacls.exe @($PgData, '/inheritance:r', '/grant:r',
        "*${SidSystem}:(OI)(CI)F", "*${SidAdmins}:(OI)(CI)F", "*${SidNetworkService}:(OI)(CI)F", "*${me}:(OI)(CI)F")

    $superPassword = New-Secret 32
    $passwordFile = Join-Path ([System.IO.Path]::GetTempPath()) ('titanfit-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllText($passwordFile, $superPassword, $Utf8NoBom)
        # UTF-8 with the built-in C.UTF-8 locale: stable sorting that does not depend on the Windows version.
        Invoke-Native (Join-Path $PgBin 'initdb.exe') @('-D', $PgData, '-U', 'postgres', "--pwfile=$passwordFile",
            '-E', 'UTF8', '--locale=C', '--locale-provider=builtin', '--builtin-locale=C.UTF-8',
            '--auth=scram-sha-256', '--data-checksums')
    } finally {
        Remove-Item -LiteralPath $passwordFile -Force -ErrorAction SilentlyContinue
    }

    $settings = @(
        '',
        '# --- TitanFit (written by the server setup; later lines win) ---',
        "listen_addresses = 'localhost'",
        "port = $DbPort",
        'logging_collector = on',
        "log_directory = 'log'",
        "log_filename = 'postgresql-%a.log'",
        'log_truncate_on_rotation = on',
        'log_rotation_age = 1d',
        'log_rotation_size = 0'
    )
    Add-Content -LiteralPath (Join-Path $PgData 'postgresql.conf') -Value $settings -Encoding ASCII

    $admin = [ordered]@{
        note     = 'PostgreSQL superuser of the TitanFit database server. Keep this file; it is needed for repairs and upgrades.'
        host     = 'localhost'
        port     = $DbPort
        username = 'postgres'
        password = $superPassword
    }
    Write-ProtectedFile $DbAdminConfig ($admin | ConvertTo-Json)
    Write-Log "Superuser credentials saved to $DbAdminConfig (SYSTEM and Administrators only)."
    return $superPassword
}

function Register-DatabaseService {
    $service = Get-ServiceOrNull $DbService
    if ($null -eq $service) {
        Write-Log "Registering service $DbService"
        Invoke-Native (Join-Path $PgBin 'pg_ctl.exe') @('register', '-N', $DbService, '-U', 'NT AUTHORITY\NetworkService',
            '-D', $PgData, '-S', 'auto', '-w', '-t', '120')
    } else {
        Write-Log "Service $DbService already exists."
    }
    Set-Service -Name $DbService -DisplayName $DbDisplayName -StartupType Automatic `
        -Description "PostgreSQL $PgMajor for TitanFit (localhost:$DbPort only). Data: $PgData"
    Invoke-Native sc.exe @('failure', $DbService, 'reset=', '86400', 'actions=', 'restart/5000/restart/15000/restart/60000') -Quiet
}

function Wait-ForDatabase {
    $isReady = Join-Path $PgBin 'pg_isready.exe'
    for ($i = 0; $i -lt 60; $i++) {
        Invoke-Native $isReady @('-h', 'localhost', '-p', "$DbPort", '-t', '2') -AllowFailure -Quiet
        if ($script:LastExit -eq 0) { Write-Log "The database accepts connections on localhost:$DbPort."; return }
        Start-Sleep -Seconds 1
    }
    Write-DatabaseLogTail
    throw "The database does not accept connections on localhost:$DbPort."
}

function Invoke-Psql([string]$Password, [string]$Sql, [switch]$Quiet) {
    $env:PGPASSWORD = $Password
    try {
        Invoke-Native (Join-Path $PgBin 'psql.exe') @('-X', '-q', '-v', 'ON_ERROR_STOP=1', '-h', 'localhost', '-p', "$DbPort",
            '-U', 'postgres', '-d', 'postgres') -InputText $Sql -Quiet:$Quiet
    } finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

# Makes sure the "titanfit" role and database exist and server.json holds a working connection string.
function Initialize-AppDatabase([string]$NewSuperPassword) {
    $existing = Read-JsonFile $ServerConfig
    $hasConnection = $false
    if ($null -ne $existing -and $null -ne $existing.ConnectionStrings -and $existing.ConnectionStrings.Gym) { $hasConnection = $true }
    if (-not $NewSuperPassword -and $hasConnection) {
        Write-Log "Keeping the existing server settings ($ServerConfig)."
        return
    }

    $superPassword = $NewSuperPassword
    if (-not $superPassword) {
        $admin = Read-JsonFile $DbAdminConfig
        if ($null -ne $admin) { $superPassword = $admin.password }
    }
    if (-not $superPassword) {
        throw "The database exists but $ServerConfig and $DbAdminConfig are missing, so the setup cannot log in to it. Restore one of them from a backup."
    }

    Write-Log 'Creating the titanfit role and database (if missing).'
    Invoke-Psql $superPassword @'
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'titanfit') THEN CREATE ROLE titanfit LOGIN; END IF;
END $$;
SELECT 'CREATE DATABASE titanfit OWNER titanfit' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'titanfit')\gexec
'@
    $appPassword = New-Secret 32
    Write-Log 'Setting a new random password for the titanfit role.'
    Invoke-Psql $superPassword "ALTER ROLE titanfit WITH LOGIN PASSWORD '$appPassword';" -Quiet

    $config = [ordered]@{
        ConnectionStrings = [ordered]@{ Gym = "Host=localhost;Port=$DbPort;Database=titanfit;Username=titanfit;Password=$appPassword" }
        Database          = [ordered]@{ MigrateOnStartup = $true }
        Storage           = [ordered]@{ DataPath = $DataRoot }
    }
    Write-ProtectedFile $ServerConfig ($config | ConvertTo-Json -Depth 5)
    Write-Log "Server settings saved to $ServerConfig (SYSTEM and Administrators only)."
}

# ---------------------------------------------------------------------------------------------------------------
# API service, firewall, health
# ---------------------------------------------------------------------------------------------------------------

function Register-ApiService {
    $binPath = '"' + $ApiExe + '"'
    $description = "TITANFIT gym server: API and live updates on port $ApiPort. Settings: $ServerConfig"
    $service = Get-ServiceOrNull $ApiService
    if ($null -eq $service) {
        Write-Log "Registering service $ApiService"
        New-Service -Name $ApiService -BinaryPathName $binPath -DisplayName $ApiDisplayName -Description $description `
            -StartupType Automatic -DependsOn $DbService | Out-Null
    } else {
        Write-Log "Service $ApiService already exists: updating it."
        Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ApiService" -Name ImagePath -Value $binPath
        Set-Service -Name $ApiService -DisplayName $ApiDisplayName -Description $description -StartupType Automatic
        Invoke-Native sc.exe @('config', $ApiService, 'depend=', $DbService) -Quiet
    }
    Invoke-Native sc.exe @('failure', $ApiService, 'reset=', '86400', 'actions=', 'restart/5000/restart/15000/restart/60000') -Quiet
    Invoke-Native sc.exe @('failureflag', $ApiService, '1') -Quiet
}

function Set-FirewallRule {
    Remove-NetFirewallRule -Name $FirewallRule -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name $FirewallRule -DisplayName "TitanFit Server (TCP $ApiPort, local network)" -Group 'TitanFit' `
        -Description 'Lets the TitanFit stations on the same local network reach the server.' `
        -Direction Inbound -Action Allow -Protocol TCP -LocalPort $ApiPort -RemoteAddress LocalSubnet -Profile Domain, Private | Out-Null
    Write-Log "Firewall rule ${FirewallRule}: inbound TCP $ApiPort, local subnet only, Private and Domain networks."
}

function Wait-ForHealth {
    $url = "http://localhost:$ApiPort/health"
    $deadline = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri $url -TimeoutSec 5 -UseBasicParsing
            if ($health.status -eq 'ok') {
                Write-Log "The server answers $url (version $($health.version))."
                return
            }
        } catch {
            # not up yet
        }
        if ((Get-Service -Name $ApiService).Status -eq 'Stopped') {
            Write-Log "Service $ApiService stopped while starting."
            Write-RecentServiceEvents
        }
        Start-Sleep -Seconds 2
    }
    Write-RecentServiceEvents
    throw "The server did not answer $url within 3 minutes."
}

function Write-AdminStationConfig {
    $script = Join-Path $PSScriptRoot 'station-config.ps1'
    & $script -ServerUrl "http://localhost:$ApiPort" -OnlyIfMissing
    if ($LASTEXITCODE -ne 0) { throw "station-config.ps1 failed with exit code $LASTEXITCODE" }
}

# ---------------------------------------------------------------------------------------------------------------

function Invoke-Install {
    Write-Log "=== Install / upgrade (app folder: $AppDir) ==="
    foreach ($file in @($ApiExe, (Join-Path $PgBin 'initdb.exe'), (Join-Path $PgBin 'pg_ctl.exe'), (Join-Path $PgBin 'psql.exe'))) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Missing file: $file" }
    }
    # Leftover PG* variables (PGPORT, PGDATA, PGPASSWORD...) must not change what the tools do.
    Get-ChildItem Env: | Where-Object { $_.Name -like 'PG*' } | ForEach-Object { Remove-Item -LiteralPath ('Env:' + $_.Name) }

    New-Item -ItemType Directory -Force -Path $DataRoot, $KeysDir, $BackupDir | Out-Null
    Protect-Path $KeysDir -Folder   # the token signing key
    Protect-Path $BackupDir -Folder   # the default folder of the daily database backups (member data)

    $newSuperPassword = Initialize-Cluster
    Register-DatabaseService
    Start-ServiceAndWait $DbService
    Wait-ForDatabase
    Initialize-AppDatabase $newSuperPassword

    Register-ApiService
    Set-FirewallRule
    if ($AdminApp) { Write-AdminStationConfig }
    Start-ServiceAndWait $ApiService
    Wait-ForHealth
    Write-Log '=== Done ==='
}

function Invoke-Uninstall {
    Write-Log '=== Uninstall: removing the services and the firewall rule; the data in this folder is kept ==='
    $failed = $false
    try {
        Stop-ServiceIfRunning $ApiService
        if (Get-ServiceOrNull $ApiService) { Invoke-Native sc.exe @('delete', $ApiService) }
    } catch { Write-Log "ERROR: $($_.Exception.Message)"; $failed = $true }
    try {
        Stop-ServiceIfRunning $DbService
        if (Get-ServiceOrNull $DbService) {
            $pgCtl = Join-Path $PgBin 'pg_ctl.exe'
            if (Test-Path -LiteralPath $pgCtl) {
                Invoke-Native $pgCtl @('unregister', '-N', $DbService) -AllowFailure
            }
            if (Get-ServiceOrNull $DbService) { Invoke-Native sc.exe @('delete', $DbService) -AllowFailure }
        }
    } catch { Write-Log "ERROR: $($_.Exception.Message)"; $failed = $true }
    try {
        Remove-NetFirewallRule -Name $FirewallRule -ErrorAction SilentlyContinue
    } catch { Write-Log "ERROR: $($_.Exception.Message)"; $failed = $true }
    Write-Log "Kept: $DataRoot (database, backups, photos, keys, settings). Delete it by hand only if the data is no longer needed."
    if ($failed) { exit 1 }
}

try {
    switch ($Action) {
        'stop' {
            Write-Log '=== Stopping the TitanFit services before the update ==='
            Stop-ServiceIfRunning $ApiService
            Stop-ServiceIfRunning $DbService
        }
        'install' { Invoke-Install }
        'uninstall' { Invoke-Uninstall }
    }
    exit 0
} catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    Write-Log "  at $($_.InvocationInfo.PositionMessage -replace '\s+', ' ')"
    exit 1
}
