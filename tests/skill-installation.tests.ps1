$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('agent-notify-skill-test-' + [guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'hidden-parent\skills'
try {
    $hidden = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    $hidden.Attributes = $hidden.Attributes -bor [IO.FileAttributes]::Hidden
    & (Join-Path $repo 'scripts\install-codex-skill.ps1') -SkillRoot $target | Out-Null
    & (Join-Path $repo 'scripts\install-codex-skill.ps1') -SkillRoot $target | Out-Null
    $file = Join-Path $target 'agent-notify\SKILL.md'
    Add-Content -LiteralPath $file -Value 'Local customization'
    $before = (Get-FileHash -LiteralPath $file).Hash
    $refused = $false
    try { & (Join-Path $repo 'scripts\install-codex-skill.ps1') -SkillRoot $target | Out-Null } catch { $refused = $true }
    if (!$refused -or (Get-FileHash -LiteralPath $file).Hash -ne $before) { throw 'Changed skill was not preserved' }
    Write-Output 'PASS: isolated skill installation through a hidden parent, identical reuse and modified-copy preservation.'
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^agent-notify-skill-test-[0-9a-f]{32}$') { throw 'Unsafe cleanup target' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
