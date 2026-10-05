param([string]$ExpectedInstalledSha256, [switch]$Launch, [string]$Destination, [switch]$SkipRegistration)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT' -or ![Environment]::Is64BitOperatingSystem) { throw 'Agent Notify requires Windows x64.' }
if ($ExpectedInstalledSha256 -and $ExpectedInstalledSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'ExpectedInstalledSha256 must be a SHA256 hash.' }
$payload = Join-Path $PSScriptRoot 'AgentNotify.exe'
if (!(Test-Path -LiteralPath $payload)) {
    $payload = Join-Path $PSScriptRoot 'build\AgentNotify.exe'
    if (!(Test-Path -LiteralPath $payload)) { & (Join-Path $PSScriptRoot 'scripts\build.ps1') }
}
if (!$Destination) { $Destination = Join-Path $env:LOCALAPPDATA 'Programs\AgentNotify' }
elseif (!$SkipRegistration) { throw 'A custom destination is only supported for isolated installation checks with -SkipRegistration.' }
$Destination = [IO.Path]::GetFullPath($Destination)
if ([IO.Path]::GetFullPath($payload) -eq (Join-Path $Destination 'AgentNotify.exe')) { throw 'Run the installer from the extracted release/source, not from the installed program directory.' }
if (Test-Path -LiteralPath $Destination) {
    if ((Get-Item -LiteralPath $Destination).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Preserving a linked installation directory.' }
}
$exe = Join-Path $Destination 'AgentNotify.exe'
$newHash = (Get-FileHash -LiteralPath $payload).Hash
if (Test-Path -LiteralPath $exe) {
    $installedHash = (Get-FileHash -LiteralPath $exe).Hash
    if ($installedHash -ne $newHash -and (!$ExpectedInstalledSha256 -or $installedHash -ne $ExpectedInstalledSha256)) { throw 'Existing version differs. Exit Agent Notify, review its SHA256, then upgrade with -ExpectedInstalledSha256. No installation file was changed.' }
}
if (@(Get-Process AgentNotify -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }).Count) { throw 'Exit Agent Notify using its tray menu before installing or upgrading.' }
$relativeFiles = @('app.json','LICENSE','README.md','README.zh-CN.md','CONTRIBUTING.md','SECURITY.md','CHANGELOG.md','install.ps1','uninstall.ps1','Install.cmd','Uninstall.cmd')
$relativeFiles += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'docs') -File -Recurse | ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1) })
$relativeFiles += @('scripts\Pushover.psm1','scripts\notify.ps1','scripts\setup-pushover.ps1','scripts\setup-pushover.cmd','scripts\install-codex-skill.ps1')
$relativeFiles += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'skills') -File -Recurse | ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1) })
$existingManifestPath = Join-Path $Destination 'install.json'
$managed = @{}
if (Test-Path -LiteralPath $existingManifestPath) {
    $oldManifest = Get-Content -LiteralPath $existingManifestPath -Raw | ConvertFrom-Json
    if ($oldManifest.app -ne 'Agent Notify' -or !$oldManifest.files) { throw 'Preserving an unrecognized installation manifest.' }
    foreach ($entry in $oldManifest.files) { $managed[$entry.path] = $entry.sha256 }
}
foreach ($relative in $relativeFiles) {
    $target = Join-Path $Destination $relative
    $parent = Split-Path -Parent $target
    while ($parent.Length -gt $Destination.Length) {
        if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Preserving a linked installation subdirectory.' }
        $parent = Split-Path -Parent $parent
    }
    if (Test-Path -LiteralPath $target) {
        if (!$managed.ContainsKey($relative) -or (Get-FileHash -LiteralPath $target).Hash -ne $managed[$relative]) { throw ('Modified or unknown installed support file preserved: ' + $relative) }
    }
}
$null = New-Item -ItemType Directory -Path $Destination -Force
if ((Test-Path -LiteralPath $exe) -and (Get-FileHash -LiteralPath $exe).Hash -ne $newHash) {
    Copy-Item -LiteralPath $exe -Destination (Join-Path $Destination ('AgentNotify.previous-' + [guid]::NewGuid().ToString('N') + '.exe'))
}
$manifestFiles = @()
Copy-Item -LiteralPath $payload -Destination $exe -Force
foreach ($relative in $relativeFiles) {
    $target = Join-Path $Destination $relative
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relative) -Destination $target -Force
}
foreach ($relative in @('AgentNotify.exe') + $relativeFiles) { $manifestFiles += @{ path = $relative; sha256 = (Get-FileHash -LiteralPath (Join-Path $Destination $relative)).Hash } }
if ((Get-FileHash -LiteralPath $exe).Hash -ne $newHash) { throw 'Installed binary hash mismatch.' }
@{ app = 'Agent Notify'; version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.json') -Raw | ConvertFrom-Json).version; binary_sha256 = $newHash; files = $manifestFiles } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Destination 'install.json') -Encoding UTF8
if (!$SkipRegistration) {
    $registration = Start-Process -FilePath $exe -ArgumentList '--register-only' -WindowStyle Hidden -Wait -PassThru
    if ($registration.ExitCode -ne 0) { throw 'Installed files are preserved, but shortcut registration failed. Inspect the local installation before retrying.' }
}
Write-Output ('Installed: ' + $exe)
Write-Output 'Open Agent Notify from Start to sign in. Passwords/keys are entered locally; startup is chosen in the app.'
if ($Launch) { Start-Process -FilePath $exe }
