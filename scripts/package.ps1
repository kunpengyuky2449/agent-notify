$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = (Get-Content -LiteralPath (Join-Path $repoRoot 'app.json') -Raw | ConvertFrom-Json).version
$exe = Join-Path $repoRoot 'build\AgentNotify.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build and test before packaging.' }
$dist = Join-Path $repoRoot 'dist'
$bundle = Join-Path $dist ('AgentNotify-' + $version + '-windows-x64')
if (Test-Path -LiteralPath $bundle) { throw 'Bundle directory already exists; preserve it and use a clean checkout/build for another package.' }
$null = New-Item -ItemType Directory -Path $bundle -Force
Copy-Item -LiteralPath $exe -Destination (Join-Path $bundle 'AgentNotify.exe')
foreach ($relative in @('app.json','LICENSE','README.md','README.zh-CN.md','CONTRIBUTING.md','SECURITY.md','CHANGELOG.md','install.ps1','uninstall.ps1','Install.cmd','Uninstall.cmd')) { Copy-Item -LiteralPath (Join-Path $repoRoot $relative) -Destination (Join-Path $bundle $relative) }
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination (Join-Path $bundle 'docs') -Recurse
$null = New-Item -ItemType Directory -Path (Join-Path $bundle 'scripts')
foreach ($name in @('Pushover.psm1','notify.ps1','setup-pushover.ps1','setup-pushover.cmd','install-codex-skill.ps1')) { Copy-Item -LiteralPath (Join-Path $repoRoot ('scripts\' + $name)) -Destination (Join-Path $bundle ('scripts\' + $name)) }
Copy-Item -LiteralPath (Join-Path $repoRoot 'skills') -Destination (Join-Path $bundle 'skills') -Recurse
$zip = $bundle + '.zip'
Compress-Archive -Path $bundle -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), $hash + '  ' + [IO.Path]::GetFileName($zip) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output ('Packaged: ' + $zip)
