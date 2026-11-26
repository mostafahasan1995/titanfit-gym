<#
    Builds TitanFit-Server-Setup-<version>.exe and TitanFit-Station-Setup-<version>.exe.
    Runs in CI on windows-latest (pwsh), from the repository root:

      ./installer/build-installers.ps1 -ExesDir dist -OutDir artifacts/installers [-Tag v0.2.0]

    - Version: <Version> in Directory.Build.props. On a tag the tag must be v<Version> (or v<Version>-suffix).
    - PostgreSQL 17 (EDB Windows x64 binaries zip) and Inno Setup 6 are pinned in installer/downloads.json and
      checked by SHA-256. Downloads are kept in -DownloadDir (cached by the workflow).
    - The PostgreSQL payload keeps bin, lib and share (no pgAdmin, StackBuilder, docs or headers) and gets the
      Visual C++ runtime DLLs from the runner's Visual Studio redist folder (app-local), so no VC++ install is needed.
    - The wizard images and the icon are rendered from src/Gym.UI (logo + font) by branding/make-branding.ps1.
#>
param(
    [Parameter(Mandatory = $true)][string]$ExesDir,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Tag = '',
    [string]$DownloadDir = 'artifacts/downloads',
    [string]$WorkDir = 'artifacts/installer-build'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$PSNativeCommandUseErrorActionPreference = $false

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo
foreach ($dir in $DownloadDir, $WorkDir, $OutDir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$DownloadDir = (Resolve-Path $DownloadDir).Path
$WorkDir = (Resolve-Path $WorkDir).Path
$OutDir = (Resolve-Path $OutDir).Path
$ExesDir = (Resolve-Path $ExesDir).Path
$pins = Get-Content -Raw (Join-Path $PSScriptRoot 'downloads.json') | ConvertFrom-Json

function Write-Step([string]$Text) { Write-Host "`n=== $Text" }

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path -Leaf $FilePath) failed with exit code $LASTEXITCODE" }
}

function Get-VerifiedDownload($Pin) {
    $path = Join-Path $DownloadDir $Pin.file
    if (Test-Path $path) {
        if ((Get-FileHash $path -Algorithm SHA256).Hash -eq $Pin.sha256) {
            Write-Host "From cache: $($Pin.file) (SHA-256 OK)"
            return $path
        }
        Write-Host "Cached $($Pin.file) has a wrong hash: downloading again."
        Remove-Item $path -Force
    }
    Write-Host "Downloading $($Pin.url)"
    Invoke-WebRequest -Uri $Pin.url -OutFile $path -MaximumRetryCount 3 -RetryIntervalSec 5
    $hash = (Get-FileHash $path -Algorithm SHA256).Hash
    if ($hash -ne $Pin.sha256) {
        Remove-Item $path -Force
        throw "SHA-256 mismatch for $($Pin.file): got $hash, expected $($Pin.sha256)"
    }
    Write-Host ("Downloaded {0} ({1:N1} MB, SHA-256 OK)" -f $Pin.file, ((Get-Item $path).Length / 1MB))
    return $path
}

# --- Version ---------------------------------------------------------------------------------------------------
Write-Step 'Version'
[xml]$props = Get-Content -Raw (Join-Path $repo 'Directory.Build.props')
$version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Directory.Build.props: <Version> must be x.y.z, found '$version'." }
$setupVersion = $version
if ($Tag) {
    $tagVersion = $Tag -replace '^v', ''
    if ($tagVersion -ne $version -and -not $tagVersion.StartsWith("$version-")) {
        throw "Tag $Tag does not match <Version>$version</Version> in Directory.Build.props. Set the version there, commit, then tag v<version>."
    }
    $setupVersion = $tagVersion
}
$fileVersion = "$version.0"
Write-Host "Setup version $setupVersion (file version $fileVersion)"
if ($env:GITHUB_OUTPUT) { "version=$setupVersion" | Add-Content $env:GITHUB_OUTPUT }

# --- Setup scripts: they run on Windows PowerShell 5.1 on the gym PCs, so parse them with it ---------------------
Write-Step 'Check the setup scripts (Windows PowerShell 5.1 parser)'
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$parseCheck = Join-Path $WorkDir 'parse-check.ps1'
Set-Content -Path $parseCheck -Encoding ascii -Value @'
$failed = $false
foreach ($file in $args) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
    foreach ($e in $errors) { Write-Host ('{0}:{1}: {2}' -f $file, $e.Extent.StartLineNumber, $e.Message); $failed = $true }
    if (-not $errors) { Write-Host "OK $file" }
}
if ($failed) { exit 1 }
'@
Invoke-Checked $powershell (@('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $parseCheck) +
    @(Get-ChildItem (Join-Path $PSScriptRoot 'scripts\*.ps1'), (Join-Path $PSScriptRoot 'branding\*.ps1') | ForEach-Object FullName))

# --- Payload: exes + trimmed PostgreSQL + VC++ runtime ---------------------------------------------------------
Write-Step 'Payload'
$payload = Join-Path $WorkDir 'payload'
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Path $payload | Out-Null
foreach ($app in 'Gym.Api', 'Gym.Admin', 'Gym.Reception', 'Gym.Cafeteria') {
    $exe = Join-Path $ExesDir "$app.exe"
    if (-not (Test-Path $exe)) { throw "Missing $exe (from the titanfit-exes artifact)" }
    Copy-Item $exe $payload
}

$zip = Get-VerifiedDownload $pins.postgresql
$tar = Join-Path $env:SystemRoot 'System32\tar.exe'   # bsdtar reads zip files (Git's GNU tar does not)
Invoke-Checked $tar @('-xf', $zip, '-C', $payload, 'pgsql/bin', 'pgsql/lib', 'pgsql/share',
    'pgsql/server_license.txt', 'pgsql/commandlinetools_3rd_party_licenses.txt')
$pg = Join-Path $payload 'pgsql'
# Not needed on the gym PC: StackBuilder (and its wx DLLs), import libraries, the build kit, docs.
Remove-Item -Force -ErrorAction SilentlyContinue -Path "$pg\bin\stackbuilder.exe", "$pg\bin\wx*.dll", "$pg\bin\*.lib", "$pg\lib\*.lib"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue -Path "$pg\lib\pgxs", "$pg\share\doc"
foreach ($tool in 'postgres', 'initdb', 'pg_ctl', 'psql', 'pg_isready', 'pg_dump', 'pg_restore') {
    if (-not (Test-Path "$pg\bin\$tool.exe")) { throw "PostgreSQL payload: $tool.exe is missing" }
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsPath = & $vswhere -latest -products * -property installationPath
$crt = Get-ChildItem (Join-Path $vsPath 'VC\Redist\MSVC') -Directory |
    Where-Object { $_.Name -match '^\d+(\.\d+)+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Get-ChildItem (Join-Path $_.FullName 'x64') -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue } |
    Select-Object -First 1
if (-not $crt) { throw "No Visual C++ redist (x64 Microsoft.VC*.CRT) found under $vsPath" }
Write-Host "Visual C++ runtime (app-local for PostgreSQL): $($crt.FullName)"
Copy-Item (Join-Path $crt.FullName '*.dll') (Join-Path $pg 'bin')
Get-ChildItem (Join-Path $crt.FullName '*.dll') | ForEach-Object { Write-Host "  $($_.Name) $($_.VersionInfo.FileVersion)" }
& "$pg\bin\postgres.exe" --version
if ($LASTEXITCODE -ne 0) { throw 'postgres.exe --version failed' }

$pgFiles = Get-ChildItem $pg -Recurse -File
Write-Host ("PostgreSQL payload: {0:N0} files, {1:N1} MB" -f $pgFiles.Count, (($pgFiles | Measure-Object Length -Sum).Sum / 1MB))

# --- Branding --------------------------------------------------------------------------------------------------
Write-Step 'Branding (wizard images and icon from src/Gym.UI)'
$brand = Join-Path $WorkDir 'branding'
Invoke-Checked $powershell @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
    (Join-Path $PSScriptRoot 'branding\make-branding.ps1'), '-RepoDir', $repo, '-OutDir', $brand)

# --- Inno Setup ------------------------------------------------------------------------------------------------
Write-Step "Inno Setup $($pins.innosetup.version)"
$iscc = $null
foreach ($dir in "${env:ProgramFiles(x86)}\Inno Setup 6", "$env:ProgramFiles\Inno Setup 6") {
    $candidate = Join-Path $dir 'ISCC.exe'
    if (Test-Path $candidate) {
        $found = (Get-Item $candidate).VersionInfo.ProductVersion
        Write-Host "Preinstalled on the runner: $candidate ($found)"
        if ($found -like "$($pins.innosetup.version)*") { $iscc = $candidate }
    }
}
if (-not $iscc) {
    $innoSetup = Get-VerifiedDownload $pins.innosetup
    $innoDir = Join-Path $WorkDir 'innosetup'
    Write-Host "Installing the pinned Inno Setup to $innoDir"
    $process = Start-Process -FilePath $innoSetup -Wait -PassThru -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CURRENTUSER', '/NOICONS', "/DIR=`"$innoDir`"")
    if ($process.ExitCode -ne 0) { throw "The Inno Setup installer failed with exit code $($process.ExitCode)" }
    $iscc = Join-Path $innoDir 'ISCC.exe'
}
if (-not (Test-Path (Join-Path (Split-Path $iscc) 'Languages\Arabic.isl'))) { throw 'Inno Setup has no Languages\Arabic.isl' }
Write-Host "Using $iscc ($((Get-Item $iscc).VersionInfo.ProductVersion))"

# --- Compile ---------------------------------------------------------------------------------------------------
Get-ChildItem $OutDir -File | Remove-Item -Force
$defines = @("/DAppVersion=$setupVersion", "/DFileVersion=$fileVersion", "/DPayloadDir=$payload", "/DBrandDir=$brand",
    "/DRepoDir=$repo", "/O$OutDir")
foreach ($script in 'server', 'station') {
    Write-Step "Compile $script.iss"
    Invoke-Checked $iscc (@('/Qp') + $defines + @((Join-Path $PSScriptRoot "$script.iss")))
}

Write-Step 'Output'
$setups = Get-ChildItem $OutDir -Filter 'TitanFit-*-Setup-*.exe'
if ($setups.Count -ne 2) { throw "Expected 2 setups in $OutDir, found $($setups.Count)" }
$setups | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content (Join-Path $OutDir 'SHA256SUMS.txt') -Encoding ascii
$setups | Format-Table Name, @{ Name = 'Size (MB)'; Expression = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize | Out-String | Write-Host
Get-Content (Join-Path $OutDir 'SHA256SUMS.txt') | Write-Host
