param([string]$Destination, [switch]$SkipRegistration)
$ErrorActionPreference = 'Stop'
if (!$Destination) { $Destination = Join-Path $env:LOCALAPPDATA 'Programs\AgentNotify' }
elseif (!$SkipRegistration) { throw 'A custom destination is only supported for isolated checks with -SkipRegistration.' }
$Destination = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
$manifestPath = Join-Path $Destination 'install.json'
if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Installation manifest is absent; preserve this directory and remove manually after review.' }
if ((Get-Item -LiteralPath $Destination).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Preserving linked installation directory.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.app -ne 'Agent Notify' -or !$manifest.files) { throw 'Invalid installation manifest.' }
$exe = Join-Path $Destination 'AgentNotify.exe'
if (@(Get-Process AgentNotify -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }).Count) { throw 'Exit Agent Notify from its tray menu before removal.' }
foreach ($entry in $manifest.files) {
    $file = [IO.Path]::GetFullPath((Join-Path $Destination $entry.path))
    if (!$file.StartsWith($Destination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes this installation.' }
    $parent = Split-Path -Parent $file
    while ($parent.Length -gt $Destination.Length) {
        if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Preserving linked installation content.' }
        $parent = Split-Path -Parent $parent
    }
    if ((Test-Path -LiteralPath $file) -and (Get-FileHash -LiteralPath $file).Hash -ne $entry.sha256) { throw ('Modified installed file preserved: ' + $entry.path) }
}
if (!$SkipRegistration) {
    $run = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true)
    if ($run) { try { if ($run.GetValue('PersonalAgentNotify') -eq ('"' + $exe + '" --background')) { $run.DeleteValue('PersonalAgentNotify', $false) } } finally { $run.Dispose() } }
    $protocolPath = 'Software\Classes\personalagentnotify'
    $command = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($protocolPath + '\shell\open\command')
    $ownsProtocol = $false
    if ($command) { try { $ownsProtocol = $command.GetValue('') -eq ('"' + $exe + '" --open-inbox') } finally { $command.Dispose() } }
    if ($ownsProtocol) {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($protocolPath, $false)
        $identityPath = 'Software\Classes\AppUserModelId\Personal.AgentNotify'
        $identity = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($identityPath)
        $ownsIdentity = $false
        if ($identity) { try { $ownsIdentity = $identity.GetValue('CustomActivator') -eq '{4d36a2d9-b740-46cd-b175-4eb8fa534d11}' } finally { $identity.Dispose() } }
        if ($ownsIdentity) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($identityPath, $false) }
    }
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Agent Notify.lnk'
    if (Test-Path -LiteralPath $shortcutPath) {
        $shell = New-Object -ComObject WScript.Shell
        if ($shell.CreateShortcut($shortcutPath).TargetPath -eq $exe) { Remove-Item -LiteralPath $shortcutPath }
    }
}
foreach ($entry in $manifest.files) { $file = Join-Path $Destination $entry.path; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
Remove-Item -LiteralPath $manifestPath
$managedParents = @($manifest.files | ForEach-Object {
    $parent = Split-Path -Parent (Join-Path $Destination $_.path)
    while ($parent.Length -gt $Destination.Length) { $parent; $parent = Split-Path -Parent $parent }
} | Sort-Object -Unique | Sort-Object Length -Descending)
foreach ($path in $managedParents) { if ($path -ne $Destination -and (Test-Path -LiteralPath $path) -and @(Get-ChildItem -LiteralPath $path -Force).Count -eq 0) { Remove-Item -LiteralPath $path } }
if (@(Get-ChildItem -LiteralPath $Destination -Force).Count -eq 0) { Remove-Item -LiteralPath $Destination }
Write-Output 'Program removed. Receiver inbox, encrypted sessions, sender settings and any reviewed backup executables were preserved. Remove the device separately from your Pushover account if desired.'
