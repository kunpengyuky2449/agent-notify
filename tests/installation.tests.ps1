$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('agent-notify-install-test-' + [guid]::NewGuid().ToString('N'))
$destination = Join-Path $testRoot 'app'
function Assert($Condition, $Label) { if (!$Condition) { throw ('Failed: ' + $Label) } }
function AssertThrows([scriptblock]$Action, [string]$Message) {
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $_.Exception.Message -like ('*' + $Message + '*') }
    Assert $caught ('expected error: ' + $Message)
}
try {
    $null = New-Item -ItemType Directory -Path $destination -Force
    $note = Join-Path $destination 'user-note.txt'; [IO.File]::WriteAllText($note, 'dummy personal note')
    & (Join-Path $repoRoot 'install.ps1') -Destination $destination -SkipRegistration | Out-Null
    $exe = Join-Path $destination 'AgentNotify.exe'
    $hash = (Get-FileHash -LiteralPath $exe).Hash
    Assert ($hash -eq (Get-FileHash -LiteralPath (Join-Path $repoRoot 'build\AgentNotify.exe')).Hash) 'fresh installed binary integrity'
    $readme = Join-Path $destination 'README.md'
    $originalReadme = [IO.File]::ReadAllText($readme)
    [IO.File]::WriteAllText($readme, 'dummy user modification')
    AssertThrows { & (Join-Path $repoRoot 'install.ps1') -Destination $destination -SkipRegistration } 'support file preserved'
    AssertThrows { & (Join-Path $repoRoot 'uninstall.ps1') -Destination $destination -SkipRegistration } 'Modified installed file preserved'
    Assert ([IO.File]::ReadAllText($readme) -eq 'dummy user modification') 'modified support data untouched'
    [IO.File]::WriteAllText($readme, $originalReadme, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($exe, 'dummy prior executable revision')
    $oldHash = (Get-FileHash -LiteralPath $exe).Hash
    AssertThrows { & (Join-Path $repoRoot 'install.ps1') -Destination $destination -SkipRegistration -ExpectedInstalledSha256 ('a' * 64) } 'Existing version differs'
    Assert ((Get-FileHash -LiteralPath $exe).Hash -eq $oldHash) 'unexpected executable preserved'
    & (Join-Path $repoRoot 'install.ps1') -Destination $destination -SkipRegistration -ExpectedInstalledSha256 $oldHash | Out-Null
    Assert ((Get-FileHash -LiteralPath $exe).Hash -eq $hash) 'reviewed upgrade installed correct binary'
    Assert (@(Get-ChildItem -LiteralPath $destination -Filter 'AgentNotify.previous-*.exe').Count -eq 1) 'reviewed prior executable backed up'
    $manifestPath = Join-Path $destination 'install.json'
    $originalManifest = [IO.File]::ReadAllText($manifestPath)
    $victim = Join-Path $testRoot 'outside.txt'; [IO.File]::WriteAllText($victim, 'dummy outside file')
    $manifest = $originalManifest | ConvertFrom-Json
    $manifest.files = @($manifest.files) + @([pscustomobject]@{ path = '..\outside.txt'; sha256 = (Get-FileHash -LiteralPath $victim).Hash })
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    AssertThrows { & (Join-Path $repoRoot 'uninstall.ps1') -Destination $destination -SkipRegistration } 'escapes this installation'
    Assert ((Test-Path -LiteralPath $exe) -and [IO.File]::ReadAllText($victim) -eq 'dummy outside file') 'uninstall rejects traversal before deletion'
    [IO.File]::WriteAllText($manifestPath, $originalManifest, [Text.UTF8Encoding]::new($false))
    & (Join-Path $repoRoot 'uninstall.ps1') -Destination $destination -SkipRegistration | Out-Null
    Assert (!(Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath $note)) 'uninstall removes managed executable and preserves unknown files'
    Assert (@(Get-ChildItem -LiteralPath $destination -Filter 'AgentNotify.previous-*.exe').Count -eq 1) 'uninstall retains reviewed backup'
    Write-Output 'PASS: isolated fresh install, modified-file preservation, guarded upgrade, backup, traversal rejection and owned-file removal. No account, shortcut, startup or notification registration.'
} finally {
    $full = [IO.Path]::GetFullPath($testRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$full.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::GetFileName($full).StartsWith('agent-notify-install-test-')) { throw 'Unsafe test cleanup target.' }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
}
