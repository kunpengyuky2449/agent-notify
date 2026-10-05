param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'build' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path -Parent $compiler
$metadata = Join-Path $env:WINDIR 'System32\WinMetadata'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.8 on Windows x64 before building.' }
$references = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Runtime.dll','System.Runtime.WindowsRuntime.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
foreach ($name in @('Windows.UI.winmd','Windows.Data.winmd','Windows.Foundation.winmd')) {
    $file = Join-Path $metadata $name
    if (!(Test-Path -LiteralPath $file)) { throw ('Required Windows metadata is absent: ' + $name) }
    $references += '/r:' + $file
}
$sources = @('AssemblyInfo.cs','Engine.cs','WindowsIntegration.cs','Program.cs','SelfTests.cs') | ForEach-Object { Join-Path $repoRoot ('src\AgentNotify\' + $_) }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/out:' + (Join-Path $OutputDirectory 'AgentNotify.exe')) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'AgentNotify.exe') -Algorithm SHA256 | Select-Object Hash
