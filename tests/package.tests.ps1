param([Parameter(Mandatory=$true)][string]$Archive)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('agent-notify-package-test-' + [guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $Archive -DestinationPath $root
    $bundles = @(Get-ChildItem -LiteralPath $root -Directory)
    if ($bundles.Count -ne 1) { throw 'Expected one package directory' }
    $bundle = $bundles[0].FullName
    $files = @(Get-ChildItem -LiteralPath $bundle -File -Recurse)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($bundle.Length + 1)
        if ($relative -match '(^|[\\/])(local|project_handoff|\.git|src|tests)([\\/]|$)|(^|[\\/])(session|settings|state|inbox[^\\/]*)\.json$') { throw 'Unexpected private/developer package entry' }
        if ($file.Extension -eq '.md') {
            $content = Get-Content -LiteralPath $file.FullName -Raw
            foreach ($match in [regex]::Matches($content, '\[[^\]\n]+\]\(([^)\n]+)\)')) {
                $link = ($match.Groups[1].Value.Trim('<','>') -split '#')[0]
                if (!$link -or $link -match '^[a-z][a-z0-9+.-]*:') { continue }
                $target = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $link))
                if (!$target.StartsWith($bundle + '\', [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $target)) { throw ('Broken package link: ' + $relative + ' -> ' + $link) }
            }
        }
    }
    if (@($files | Where-Object { $_.Extension -eq '.exe' }).Count -ne 1) { throw 'Unexpected executable payload count' }
    $destination = Join-Path $root 'installed'
    & (Join-Path $bundle 'install.ps1') -Destination $destination -SkipRegistration | Out-Null
    if (!(Test-Path -LiteralPath (Join-Path $destination 'skills\agent-notify\SKILL.md'))) { throw 'Installed user guide skill link is missing' }
    & (Join-Path $bundle 'scripts\install-codex-skill.ps1') -SkillRoot (Join-Path $root 'skills') | Out-Null
    & (Join-Path $destination 'uninstall.ps1') -Destination $destination -SkipRegistration | Out-Null
    if (Test-Path -LiteralPath $destination) { throw 'Fresh package uninstall left a managed empty directory' }
    Write-Output 'PASS: extracted ZIP file boundaries, documentation links, portable app/skill installation and removal.'
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^agent-notify-package-test-[0-9a-f]{32}$') { throw 'Unsafe cleanup target' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
