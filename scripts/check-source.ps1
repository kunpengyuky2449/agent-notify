$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$files = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](\.git|build|dist|local)[\\/]' })
foreach ($file in $files) {
    if ($file.Extension -in @('.ps1','.psm1')) {
        $tokens = $null; $errors = $null
        $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw ('PowerShell syntax errors in ' + $file.Name + ': ' + $errors[0].Message) }
    }
    if ($file.Extension -eq '.md') {
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($match in [regex]::Matches($text, '\[[^\]]*\]\(([^\s)]+)\)')) {
            $link = $match.Groups[1].Value.Split('#')[0]
            if (!$link -or $link -match '^[a-z]+:' -or $link.StartsWith('#')) { continue }
            if (!(Test-Path -LiteralPath (Join-Path $file.DirectoryName $link))) { throw ('Broken local link in ' + $file.Name + ': ' + $link) }
        }
    }
}
Write-Output 'PASS: PowerShell syntax and local Markdown links.'
