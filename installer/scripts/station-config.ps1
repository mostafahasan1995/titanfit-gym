<#
    Writes %ProgramData%\TitanFit\station.json: { "serverUrl": "http://SERVER-PC:5080" }.
    Run by the TitanFit setups (Windows PowerShell 5.1, elevated). Other fields already in the file are kept.
    The file stays readable by every user of the PC (the apps read it); it holds no secret.

      -ServerUrl      http(s)://host[:port], no path
      -OnlyIfMissing  do nothing when station.json already exists (server setup, Admin app on the server PC)

    Exit code 0 = written (or kept), 2 = invalid URL.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ServerUrl,
    [string]$Path = (Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'TitanFit\station.json'),
    [switch]$OnlyIfMissing
)

$ErrorActionPreference = 'Stop'

$url = $ServerUrl.Trim().TrimEnd('/')
$uri = $null
$valid = [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$uri) -and
    ($uri.Scheme -eq 'http' -or $uri.Scheme -eq 'https') -and $uri.Host -and
    $uri.AbsolutePath -eq '/' -and -not $uri.Query -and -not $uri.Fragment -and -not $uri.UserInfo
if (-not $valid) {
    Write-Host "Invalid server address: '$ServerUrl'. Expected http://SERVER-PC:5080"
    exit 2
}

if ($OnlyIfMissing -and (Test-Path -LiteralPath $Path)) {
    Write-Host "Kept the existing $Path"
    exit 0
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null

$settings = [ordered]@{ serverUrl = $url }
if (Test-Path -LiteralPath $Path) {
    try {
        $existing = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
        foreach ($property in $existing.PSObject.Properties) {
            if ($property.Name -ne 'serverUrl') { $settings[$property.Name] = $property.Value }
        }
    } catch {
        Write-Host "The existing $Path is not valid JSON; it is replaced."
    }
}

$json = New-Object PSObject -Property $settings | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $Path (serverUrl = $url)"
exit 0
