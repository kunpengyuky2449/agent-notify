param([Parameter(Mandatory=$true)][string]$SkillRoot)
$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'skills\agent-notify'
if (!(Test-Path -LiteralPath (Join-Path $source 'SKILL.md'))) { throw 'Skill payload absent; use the complete source or release package.' }
$SkillRoot = [IO.Path]::GetFullPath($SkillRoot).TrimEnd('\')
if ($SkillRoot -match '[\\/](\.system|plugins[\\/]cache)([\\/]|$)') { throw 'Choose a user-authored skill root, not a provider directory.' }
$ancestor = $SkillRoot
while ($ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked target preserved; choose a reviewed ordinary user skill root.' }
    $ancestor = Split-Path -Parent $ancestor
}
$destination = Join-Path $SkillRoot 'agent-notify'
$files = @(Get-ChildItem -LiteralPath $source -File -Recurse)
if (Test-Path -LiteralPath $destination) {
    if ((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked skill preserved.' }
    $existing = @(Get-ChildItem -LiteralPath $destination -Force -Recurse)
    if (@($existing | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Linked skill content preserved.' }
    if (@($existing | Where-Object { !$_.PSIsContainer }).Count -ne $files.Count) { throw 'Existing skill differs; review/reconcile it before replacing.' }
    foreach ($file in $files) {
        $target = Join-Path $destination $file.FullName.Substring($source.Length + 1)
        if (!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw 'Existing skill differs; preserved for review.' }
    }
    Write-Output ('Unchanged skill: ' + $destination)
    return
}
$null = New-Item -ItemType Directory -Path $SkillRoot -Force
Copy-Item -LiteralPath $source -Destination $destination -Recurse
foreach ($file in $files) {
    $target = Join-Path $destination $file.FullName.Substring($source.Length + 1)
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw 'Skill copy verification failed; inspect the target before retry.' }
}
Write-Output ('Installed skill: ' + $destination)
Write-Output 'Check discovery in the next Codex turn/session; use $agent-notify. Avoid another active copy in repository/plugin roots.'
