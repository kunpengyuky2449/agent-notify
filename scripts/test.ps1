$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1')
$exe = Join-Path $repoRoot 'build\AgentNotify.exe'
$report = Join-Path $repoRoot 'build\self-test-result.txt'
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath $report
if ($process.ExitCode -ne 0) { throw 'Receiver offline checks failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot 'tests\pushover.tests.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Sender checks failed on Windows PowerShell 5.1.' }
if (Get-Command pwsh.exe -ErrorAction SilentlyContinue) {
    & pwsh.exe -NoProfile -File (Join-Path $repoRoot 'tests\pushover.tests.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Sender checks failed on PowerShell 7.' }
}
& (Join-Path $PSScriptRoot 'check-source.ps1')
