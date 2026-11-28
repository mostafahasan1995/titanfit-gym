<#
    Install smoke test for the TitanFit setups on a clean Windows runner (pwsh, elevated). From the repo root:

      ./installer/tests/install-smoke.ps1 -InstallersDir artifacts/installers -LogDir artifacts/install-logs

    1. Server setup, silent: both services run, the database listens on localhost:5433 only, /health is ok,
       server.json is readable only by SYSTEM and Administrators, firewall rule, first admin, login, pg_dump,
       "Back up now" through the API (file in the protected backups folder) and restore-backup.ps1; the installed
       Admin app's --self-test (SkiaSharp, HarfBuzz, the dashboard chart).
    2. Server setup again (upgrade): config, data and login are kept.
    3. Station setup, silent: a bad /SERVERURL is refused; a good one writes station.json; apps and shortcuts; the
       Visual C++ runtime step in its log (ran with exit code 0 / 1638 / 3010, or skipped because the installed
       runtime is at least the bundled one) and the runtime registered; the installed Reception (OpenCV, ZXing,
       QRCoder, sounds) and Cafeteria (receipt drawing, XPS print document) apps' --self-test.
    4. Uninstall both: services, firewall rule and program files are gone; %ProgramData%\TitanFit is kept.
    5. Server setup once more: the kept database is used again (same admin login works).
    Never prints a password.
#>
param(
    [Parameter(Mandatory = $true)][string]$InstallersDir,
    [Parameter(Mandatory = $true)][string]$LogDir
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$PSNativeCommandUseErrorActionPreference = $false

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogDir = (Resolve-Path $LogDir).Path
$serverSetup = (Get-ChildItem $InstallersDir -Filter 'TitanFit-Server-Setup-*.exe' | Select-Object -First 1).FullName
$stationSetup = (Get-ChildItem $InstallersDir -Filter 'TitanFit-Station-Setup-*.exe' | Select-Object -First 1).FullName
if (-not $serverSetup -or -not $stationSetup) { throw "Setups not found in $InstallersDir" }

$api = 'http://localhost:5080'
$appDir = Join-Path $env:ProgramFiles 'TitanFit'
$dataRoot = Join-Path $env:ProgramData 'TitanFit'
$serverJson = Join-Path $dataRoot 'server.json'
$stationJson = Join-Path $dataRoot 'station.json'
$startMenu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\TitanFit'
$desktop = Join-Path $env:PUBLIC 'Desktop'
$pgBin = Join-Path $appDir 'pgsql\bin'
$adminUser = 'smoke.admin'
$adminPassword = 'Smoke-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$adminName = [regex]::Unescape('مدير النادي Smoke')   # Arabic + Latin
$serverAppId = '{F9725C28-3C15-4C8C-99BA-B5D5F7DE8F2C}_is1'
$stationAppId = '{5891D355-E549-4044-8A8D-82BD75ADCEEA}_is1'
$script:checks = 0

function Write-Step([string]$Text) { Write-Host "`n=== $Text" }

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAILED: $Message" }
    $script:checks++
    Write-Host "  ok  $Message"
}

function Invoke-Setup([string]$Exe, [string]$LogName, [string[]]$Arguments = @()) {
    $log = Join-Path $LogDir $LogName
    $all = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"") + $Arguments
    Write-Host "  > $(Split-Path -Leaf $Exe) $($all -join ' ')"
    $started = Get-Date
    $process = Start-Process -FilePath $Exe -ArgumentList $all -Wait -PassThru
    Write-Host ("    exit code {0} after {1:N0} s" -f $process.ExitCode, ((Get-Date) - $started).TotalSeconds)
    return $process.ExitCode
}

function Invoke-Uninstall([string]$AppId, [string]$LogName) {
    $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
    $uninstaller = (Get-ItemProperty $key).UninstallString.Trim('"')
    $code = Invoke-Setup $uninstaller $LogName
    $deadline = (Get-Date).AddMinutes(3)
    while ((Test-Path $key) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }   # the uninstaller runs a temp copy
    Assert-True ($code -eq 0) "uninstaller exit code 0 ($LogName)"
    Assert-True (-not (Test-Path $key)) "uninstall entry removed ($AppId)"
}

function Get-Json([string]$Path, [string]$Token) {
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    Invoke-RestMethod -Uri "$api$Path" -Headers $headers -TimeoutSec 20
}

function Send-Json([string]$Path, $Body, [string]$Token, [string]$Method = 'Post') {
    $status = 0
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $json = $Body | ConvertTo-Json -Compress
    $response = Invoke-RestMethod -Method $Method -Uri "$api$Path" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $json `
        -SkipHttpErrorCheck -StatusCodeVariable status -TimeoutSec 120
    [pscustomobject]@{ Status = [int]$status; Body = $response }
}

function Get-Sids([string]$Path) {
    @((Get-Acl $Path).Access | ForEach-Object { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } | Sort-Object -Unique)
}

function Get-AppConnection {
    $settings = Get-Content -Raw $serverJson | ConvertFrom-Json
    $parts = @{}
    foreach ($pair in $settings.ConnectionStrings.Gym.Split(';')) {
        $kv = $pair.Split('=', 2)
        if ($kv.Count -eq 2) { $parts[$kv[0].Trim()] = $kv[1] }
    }
    $parts
}

function Invoke-Pg([string]$Tool, [string[]]$Arguments) {
    $connection = Get-AppConnection
    $env:PGPASSWORD = $connection.Password
    try {
        $output = & (Join-Path $pgBin "$Tool.exe") -h localhost -p $connection.Port -U $connection.Username @Arguments 2>&1
        $code = $LASTEXITCODE
    } finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
    if ($code -ne 0) { throw "$Tool failed ($code): $output" }
    $output
}

function Assert-ServerRunning([string]$When) {
    foreach ($name in 'TitanFitDatabase', 'TitanFitServer') {
        $service = Get-Service -Name $name -ErrorAction SilentlyContinue
        Assert-True ($null -ne $service -and $service.Status -eq 'Running') "service $name is running ($When)"
        Assert-True ($service.StartType -eq 'Automatic') "service $name starts automatically"
    }
    $health = Get-Json '/health'
    Assert-True ($health.status -eq 'ok') "GET /health status ok, version $($health.version) ($When)"
}

function Assert-Login([string]$When) {
    $login = Send-Json '/api/auth/login' @{ username = $adminUser; password = $adminPassword }
    Assert-True ($login.Status -eq 200 -and $login.Body.token) "admin login works ($When)"
    $me = Get-Json '/api/auth/me' $login.Body.token
    Assert-True ($me.fullName -eq $adminName -and $me.role -eq 'Admin') "GET /api/auth/me: Arabic full name and role Admin ($When)"
}

# Runs an installed app with --self-test (no window, camera or printer): exit code 0, every expected check passed.
function Assert-SelfTest([string]$Exe, [string[]]$Expected) {
    $name = [IO.Path]::GetFileNameWithoutExtension($Exe)
    $out = Join-Path $LogDir "$name-self-test.txt"
    $process = Start-Process -FilePath $Exe -ArgumentList '--self-test' -NoNewWindow -PassThru `
        -RedirectStandardOutput $out -RedirectStandardError "$out.err"
    $null = $process.Handle   # keeps the exit code readable after the process ends
    if (-not $process.WaitForExit(180000)) {
        $process.Kill()
        throw "FAILED: $name --self-test timed out"
    }
    $process.WaitForExit()
    $lines = @(Get-Content $out -ErrorAction SilentlyContinue) + @(Get-Content "$out.err" -ErrorAction SilentlyContinue)
    $lines | ForEach-Object { Write-Host "    | $_" }
    Assert-True ($process.ExitCode -eq 0) "$name --self-test exit code $($process.ExitCode) (installed exe)"
    $all = $Expected.Count + 4   # + embedded fonts, WPF rendering, per-monitor DPI awareness, Visual C++ runtime in use
    Assert-True (@($lines | Where-Object { $_ -match "self-test: all $all checks passed\.$" }).Count -eq 1) "$name self-test: all $all checks passed"
    $missing = @(@('Embedded fonts', 'WPF rendering', 'Per-monitor DPI awareness', 'Visual C++ runtime in use') + $Expected | Where-Object {
        $check = $_
        @($lines | Where-Object { $_.StartsWith("PASS  $check", [StringComparison]::Ordinal) }).Count -ne 1
    })
    Assert-True ($missing.Count -eq 0) "$name self-test PASS lines: $(($Expected) -join ', ') $(if ($missing) { "(missing: $($missing -join ', '))" })"
}

function Assert-ServicesGone {
    $deadline = (Get-Date).AddMinutes(1)
    while ((Get-Service TitanFitServer, TitanFitDatabase -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep 1 }
    Assert-True (-not (Get-Service TitanFitServer -ErrorAction SilentlyContinue)) 'service TitanFitServer removed'
    Assert-True (-not (Get-Service TitanFitDatabase -ErrorAction SilentlyContinue)) 'service TitanFitDatabase removed'
}

$exitCode = 0
try {
    Write-Step "Runner: $([Environment]::OSVersion.VersionString), PostgreSQL already on the runner: $((Get-Service postgresql* -ErrorAction SilentlyContinue | ForEach-Object { "$($_.Name) ($($_.Status))" }) -join ', ')"
    Get-ChildItem $InstallersDir -Filter *.exe | ForEach-Object { Write-Host ("  {0} {1:N1} MB" -f $_.Name, ($_.Length / 1MB)) }

    # 1. Fresh server install ------------------------------------------------------------------------------------
    Write-Step '1. Server setup (fresh install, silent)'
    $code = Invoke-Setup $serverSetup 'server-install.log'
    Assert-True ($code -eq 0) 'server setup exit code 0'
    Assert-ServerRunning 'fresh install'

    $db = Get-CimInstance Win32_Service -Filter "Name='TitanFitDatabase'"
    $server = Get-CimInstance Win32_Service -Filter "Name='TitanFitServer'"
    Assert-True ($db.StartName -eq 'NT AUTHORITY\NetworkService') "database service account: $($db.StartName)"
    Assert-True ($server.StartName -eq 'LocalSystem') "server service account: $($server.StartName)"
    Assert-True ($server.PathName -eq "`"$appDir\Server\Gym.Api.exe`"") "server service path: $($server.PathName)"
    Assert-True ([System.Diagnostics.EventLog]::SourceExists('Gym.Api')) 'event log source Gym.Api registered by the setup'
    $failure = (& sc.exe qfailure TitanFitServer) -join ' '
    Assert-True ($failure -match 'RESTART') 'server service restarts on failure'
    $depends = (Get-Service TitanFitServer).ServicesDependedOn.Name
    Assert-True ($depends -contains 'TitanFitDatabase') 'server service depends on the database service'

    $dbListen = @(Get-NetTCPConnection -State Listen -LocalPort 5433 -ErrorAction SilentlyContinue | ForEach-Object LocalAddress | Sort-Object -Unique)
    Assert-True ($dbListen.Count -gt 0 -and -not ($dbListen | Where-Object { $_ -notin '127.0.0.1', '::1' })) "database listens on localhost:5433 only ($($dbListen -join ', '))"
    $apiListen = @(Get-NetTCPConnection -State Listen -LocalPort 5080 -ErrorAction SilentlyContinue | ForEach-Object LocalAddress | Sort-Object -Unique)
    Assert-True ($apiListen -contains '0.0.0.0') "API listens on 0.0.0.0:5080 ($($apiListen -join ', '))"

    $acl = Get-Acl $serverJson
    $sids = @($acl.Access | ForEach-Object { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } | Sort-Object -Unique)
    Assert-True ($acl.AreAccessRulesProtected) 'server.json does not inherit permissions'
    Assert-True (($sids -join ',') -eq 'S-1-5-18,S-1-5-32-544') "server.json readable only by SYSTEM and Administrators ($($sids -join ','))"
    $connection = Get-AppConnection
    Assert-True ($connection.Port -eq '5433' -and $connection.Username -eq 'titanfit' -and $connection.Password.Length -ge 32) 'server.json: titanfit role on port 5433 with a generated password'
    $adminAcl = @((Get-Acl (Join-Path $dataRoot 'database-admin.json')).Access | ForEach-Object { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } | Sort-Object -Unique)
    Assert-True (($adminAcl -join ',') -eq 'S-1-5-18,S-1-5-32-544') 'database-admin.json readable only by SYSTEM and Administrators'
    $dataAcl = @((Get-Acl (Join-Path $dataRoot 'pgdata')).Access | ForEach-Object { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } | Sort-Object -Unique)
    Assert-True (-not ($dataAcl | Where-Object { $_ -in 'S-1-5-32-545', 'S-1-5-11', 'S-1-1-0' })) "pgdata not readable by Users / Everyone ($($dataAcl -join ','))"

    $rule = Get-NetFirewallRule -Name 'TitanFit-Server-TCP-5080'
    $address = $rule | Get-NetFirewallAddressFilter
    $port = $rule | Get-NetFirewallPortFilter
    Assert-True ($rule.Enabled -eq 'True' -and $rule.Direction -eq 'Inbound' -and $rule.Action -eq 'Allow') 'firewall rule: inbound allow'
    Assert-True ($port.Protocol -eq 'TCP' -and $port.LocalPort -eq '5080') 'firewall rule: TCP 5080'
    Assert-True ($address.RemoteAddress -eq 'LocalSubnet') "firewall rule: remote address $($address.RemoteAddress)"
    Assert-True ("$($rule.Profile)" -eq 'Domain, Private') "firewall rule: profiles $($rule.Profile)"

    foreach ($tool in 'pg_dump', 'pg_restore', 'psql') { Assert-True (Test-Path "$pgBin\$tool.exe") "$tool.exe installed" }
    Assert-True (Test-Path "$appDir\Admin\Gym.Admin.exe") 'Admin app installed (default component)'
    Assert-True ((Test-Path "$startMenu\TitanFit Admin.lnk") -and (Test-Path "$desktop\TitanFit Admin.lnk")) 'Admin app shortcuts (Start menu + desktop)'
    Assert-SelfTest "$appDir\Admin\Gym.Admin.exe" @('SkiaSharp native library', 'HarfBuzz native library', 'Dashboard chart')
    Assert-True ((Get-Content -Raw $stationJson | ConvertFrom-Json).serverUrl -eq 'http://localhost:5080') 'station.json for the Admin app on the server: http://localhost:5080'

    $locale = Invoke-Pg 'psql' @('-d', 'titanfit', '-At', '-c', "SELECT pg_encoding_to_char(encoding) || ' ' || datlocprovider::text || ' ' || coalesce(datlocale, '') FROM pg_database WHERE datname = 'titanfit'")
    Assert-True ("$locale".Trim() -eq 'UTF8 b C.UTF-8') "database titanfit: encoding and locale '$("$locale".Trim())'"

    $status = Get-Json '/api/setup/status'
    Assert-True ($status.needsSetup -eq $true) 'GET /api/setup/status: needs the first admin'
    $created = Send-Json '/api/setup/admin' @{ fullName = $adminName; username = $adminUser; password = $adminPassword }
    Assert-True ($created.Status -eq 200 -and $created.Body.token) "POST /api/setup/admin creates the first admin (HTTP $($created.Status))"
    $again = Send-Json '/api/setup/admin' @{ fullName = 'Second'; username = 'second.admin'; password = $adminPassword }
    Assert-True ($again.Status -eq 409) "POST /api/setup/admin refused once an admin exists (HTTP $($again.Status))"
    Assert-Login 'fresh install'
    $plans = Get-Json '/api/plans' $created.Body.token
    Assert-True (@($plans).Count -ge 4) "migrations and default data: $(@($plans).Count) plans"

    $dump = Join-Path $env:RUNNER_TEMP 'titanfit-smoke.dump'
    [void](Invoke-Pg 'pg_dump' @('-d', 'titanfit', '-Fc', '-f', $dump))
    $list = Invoke-Pg 'pg_restore' @('--list', $dump)
    Assert-True ((($list -join "`n") -match 'TABLE public members') -and (Get-Item $dump).Length -gt 1KB) "pg_dump backup + pg_restore --list ($((Get-Item $dump).Length) bytes)"
    Remove-Item $dump -Force

    # Daily backups: "Back up now" through the API writes a pg_dump file into the protected default folder, and the
    # restore helper brings the database back to it.
    $token = $created.Body.token
    $backupDir = Join-Path $dataRoot 'backups'
    Assert-True (((Get-Sids $backupDir) -join ',') -eq 'S-1-5-18,S-1-5-32-544') "backups folder readable only by SYSTEM and Administrators ($((Get-Sids $backupDir) -join ','))"
    $settings = Get-Json '/api/backups/settings' $token
    Assert-True ($settings.isDefaultFolder -and $settings.folder -eq $backupDir) "backup settings: default folder $($settings.folder), daily at $($settings.time), keep $($settings.keepLast)"
    Assert-True ($settings.resolvedPgDumpPath -eq "$pgBin\pg_dump.exe") "the server finds pg_dump: $($settings.resolvedPgDumpPath)"
    for ($try = 1; $try -le 5; $try++) {
        $backup = Send-Json '/api/backups' @{} $token
        if ($backup.Status -ne 409) { break }
        Start-Sleep -Seconds 5   # the daily job is running right now
    }
    Assert-True ($backup.Status -eq 200 -and $backup.Body.succeeded) "POST /api/backups (back up now): HTTP $($backup.Status), $($backup.Body.fileName) $($backup.Body.errorCode) $($backup.Body.error)"
    $backupFile = Join-Path $backupDir $backup.Body.fileName
    Assert-True ((Test-Path $backupFile) -and (Get-Item $backupFile).Length -gt 1KB -and $backup.Body.fileName -match '^titanfit-\d{8}-\d{4}(-\d+)?\.dump$') "backup file written: $backupFile ($((Get-Item $backupFile).Length) bytes)"
    Assert-True (((Get-Sids $backupFile) -join ',') -eq 'S-1-5-18,S-1-5-32-544') 'backup file readable only by SYSTEM and Administrators'
    $listed = Get-Json '/api/backups' $token   # Invoke-RestMethod returns a JSON array as one object: assign, then @()
    $listed = @($listed)
    Assert-True ($listed[0].name -eq $backup.Body.fileName) "GET /api/backups lists it first ($($listed.Count) backups)"
    $status = Get-Json '/api/backups/status' $token
    Assert-True ($status.warning -eq 'None' -and $status.lastSuccess.fileName -eq $backup.Body.fileName) "GET /api/backups/status: warning $($status.warning), next run $($status.nextRunAt)"
    $list = Invoke-Pg 'pg_restore' @('--list', $backupFile)
    Assert-True (($list -join "`n") -match 'TABLE public members') 'pg_restore --list reads the backup'

    $marker = Send-Json '/api/plans' @{ months = 7; price = 777000; isActive = $true } $token
    Assert-True ($marker.Status -eq 201) "a plan added after the backup (HTTP $($marker.Status))"
    $restoreScript = Join-Path $appDir 'Setup\restore-backup.ps1'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $restoreScript -BackupFile $backupFile -Force *>&1 | ForEach-Object { Write-Host "    $_" }
    Assert-True ($LASTEXITCODE -eq 0) "restore-backup.ps1 (Windows PowerShell 5.1) exit code $LASTEXITCODE"
    Assert-ServerRunning 'after the restore'
    $plansAfter = Get-Json '/api/plans?includeInactive=true' $token
    $plansAfter = @($plansAfter)
    Assert-True (-not ($plansAfter | Where-Object { $_.months -eq 7 })) "restored: the plan added after the backup is gone ($($plansAfter.Count) plans)"
    Assert-Login 'after the restore'
    Assert-True (@(Get-ChildItem $backupDir -Filter 'titanfit-before-restore-*.dump').Count -eq 1) 'the restore saved the data from before it'

    # 2. Upgrade (same setup again) -----------------------------------------------------------------------------
    Write-Step '2. Server setup again (upgrade over the running server)'
    $configHash = (Get-FileHash $serverJson).Hash
    $code = Invoke-Setup $serverSetup 'server-upgrade.log'
    Assert-True ($code -eq 0) 'upgrade exit code 0'
    Assert-ServerRunning 'after upgrade'
    Assert-True ((Get-FileHash $serverJson).Hash -eq $configHash) 'server.json unchanged by the upgrade'
    Assert-True ((Get-Json '/api/setup/status').needsSetup -eq $false) 'data kept: the admin still exists'
    Assert-Login 'after upgrade'
    Assert-True (@(Get-NetFirewallRule -Name 'TitanFit-Server-TCP-5080').Count -eq 1) 'still exactly one firewall rule'

    # 3. Station setup ------------------------------------------------------------------------------------------
    Write-Step '3. Station setup (silent)'
    $before = Get-Content -Raw $stationJson
    $code = Invoke-Setup $stationSetup 'station-bad-url.log' @('/COMPONENTS="reception,cafeteria"', '/SERVERURL=not-a-url')
    Assert-True ($code -ne 0) "a bad /SERVERURL is refused (exit code $code)"
    Assert-True ((Get-Content -Raw $stationJson) -eq $before) 'station.json untouched by the refused setup'
    $code = Invoke-Setup $stationSetup 'station-no-app.log' @('/COMPONENTS=""', '/SERVERURL=http://gym-server:5080')
    Assert-True ($code -ne 0) "no app chosen is refused (exit code $code)"

    $code = Invoke-Setup $stationSetup 'station-install.log' @('/COMPONENTS="reception,cafeteria"', '/SERVERURL=http://gym-server:5080/')
    Assert-True ($code -eq 0) 'station setup exit code 0'
    $station = Get-Content -Raw $stationJson | ConvertFrom-Json
    Assert-True ($station.serverUrl -eq 'http://gym-server:5080') "station.json serverUrl = $($station.serverUrl)"
    foreach ($app in 'Reception', 'Cafeteria') {
        Assert-True (Test-Path "$appDir\$app\Gym.$app.exe") "$app app installed"
        Assert-True ((Test-Path "$startMenu\TitanFit $app.lnk") -and (Test-Path "$desktop\TitanFit $app.lnk")) "$app shortcuts (Start menu + desktop)"
    }

    # The Visual C++ runtime for OpenCV (Reception camera): the setup ran vc_redist.x64.exe, or skipped it because the
    # installed runtime is at least the bundled one. Either way the runtime is registered afterwards.
    $vcLines = @(Get-Content (Join-Path $LogDir 'station-install.log') | Where-Object { $_ -match 'VC\+\+ runtime x64: ' } |
        ForEach-Object { ($_ -split 'VC\+\+ runtime x64: ', 2)[1] })
    $vcLines | ForEach-Object { Write-Host "    | $_" }
    $plan = @($vcLines | Where-Object { $_ -match '^installed \S+, bundled \d+(\.\d+){3}: (up to date|running)' })
    Assert-True ($plan.Count -eq 1) "station setup log: the VC++ runtime step ($($vcLines -join ' | '))"
    $null = $plan[0] -match '^installed (?<installed>\S+), bundled (?<bundled>[\d.]+): (?<action>up to date|running)'
    $vcStep = $Matches
    $bundled = [version]$vcStep.bundled
    if ($vcStep.action -eq 'running') {
        $result = @($vcLines | Where-Object { $_ -match '^vc_redist\.x64\.exe exit code (\d+)' })
        $vcCode = if ($result.Count -eq 1 -and $result[0] -match 'exit code (\d+)') { [int]$Matches[1] } else { -1 }
        Assert-True ($vcCode -in 0, 1638, 3010) "VC++ runtime step ran: installed $($vcStep.installed) was older than $bundled, vc_redist.x64.exe exit code $vcCode"
    } else {
        Assert-True ([version]$vcStep.installed -ge $bundled) "VC++ runtime step skipped correctly: installed $($vcStep.installed) >= bundled $bundled"
    }
    $registered = @()
    foreach ($key in 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64') {
        $value = Get-ItemProperty $key -ErrorAction SilentlyContinue
        if ($value -and $value.Installed -eq 1) {
            $rbld = if ($null -ne $value.Rbld) { $value.Rbld } else { 0 }
            $registered += [version]('{0}.{1}.{2}.{3}' -f $value.Major, $value.Minor, $value.Bld, $rbld)
        }
    }
    $registered = @($registered | Sort-Object -Descending)
    Assert-True ($registered.Count -gt 0 -and $registered[0] -ge $bundled) "VC++ runtime x64 registered: $($registered -join ', ') (bundled $bundled)"
    Assert-True (-not (Test-Path "$appDir\Reception\vc_redist.x64.exe") -and -not (Test-Path "$appDir\Setup\vc_redist.x64.exe")) 'vc_redist.x64.exe not left in the program folder'

    Assert-SelfTest "$appDir\Reception\Gym.Reception.exe" @('OpenCV native library', 'Camera frame to member code', 'Member QR image', 'Check-in sounds')
    Assert-SelfTest "$appDir\Cafeteria\Gym.Cafeteria.exe" @('Receipt drawing', 'Receipt print document')

    # 4. Uninstall ----------------------------------------------------------------------------------------------
    Write-Step '4. Uninstall station and server (silent)'
    Invoke-Uninstall $stationAppId 'station-uninstall.log'
    Assert-True (-not (Test-Path "$appDir\Reception\Gym.Reception.exe")) 'Reception app removed'
    Assert-True (Test-Path "$appDir\Admin\Gym.Admin.exe") 'Admin app of the server setup still there'
    Assert-True ((Get-Content -Raw $stationJson | ConvertFrom-Json).serverUrl -eq 'http://gym-server:5080') 'station.json kept'

    Invoke-Uninstall $serverAppId 'server-uninstall.log'
    Assert-ServicesGone
    Assert-True (-not (Get-NetFirewallRule -Name 'TitanFit-Server-TCP-5080' -ErrorAction SilentlyContinue)) 'firewall rule removed'
    Assert-True (-not (Test-Path "$appDir\Server\Gym.Api.exe") -and -not (Test-Path "$pgBin\postgres.exe")) 'server and PostgreSQL program files removed'
    foreach ($kept in 'pgdata\PG_VERSION', 'server.json', 'database-admin.json', 'keys\jwt-signing.key', 'station.json', "backups\$($backup.Body.fileName)") {
        Assert-True (Test-Path (Join-Path $dataRoot $kept)) "kept: %ProgramData%\TitanFit\$kept"
    }

    # 5. Reinstall on the kept data -----------------------------------------------------------------------------
    Write-Step '5. Server setup on the kept data (reinstall)'
    $code = Invoke-Setup $serverSetup 'server-reinstall.log' @('/COMPONENTS="server"')
    Assert-True ($code -eq 0) 'reinstall exit code 0'
    Assert-ServerRunning 'reinstall'
    Assert-True ((Get-Json '/api/setup/status').needsSetup -eq $false) 'the kept database is used again'
    Assert-Login 'reinstall'

    Write-Host "`nInstall smoke test passed: $script:checks checks."
} catch {
    Write-Host "::error::$($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
    $exitCode = 1
} finally {
    Write-Step 'Collecting logs'
    $collect = Join-Path $LogDir 'programdata'
    New-Item -ItemType Directory -Force -Path $collect | Out-Null
    Copy-Item (Join-Path $dataRoot 'logs\*') $collect -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path (Join-Path $collect 'pgdata-log') | Out-Null
    Copy-Item (Join-Path $dataRoot 'pgdata\log\*') (Join-Path $collect 'pgdata-log') -ErrorAction SilentlyContinue
    Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddHours(-1) } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -match 'PostgreSQL|pg_ctl|TitanFit|Gym\.Api|\.NET Runtime|Application Error' } |
        ForEach-Object { "{0:u} {1} {2}: {3}" -f $_.TimeCreated, $_.LevelDisplayName, $_.ProviderName, ($_.Message -replace '\s+', ' ') } |
        Set-Content (Join-Path $LogDir 'application-events.txt')
    Get-ChildItem $LogDir -Recurse -File | ForEach-Object { Write-Host ("  {0} ({1:N0} bytes)" -f $_.FullName.Substring($LogDir.Length + 1), $_.Length) }
    $setupLog = Join-Path $collect 'server-setup.log'
    if (Test-Path $setupLog) {
        Write-Step 'server-setup.log'
        Get-Content $setupLog | Write-Host
    }
}
exit $exitCode
