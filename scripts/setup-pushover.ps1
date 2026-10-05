param([string]$EventId = ('setup-' + [Guid]::NewGuid().ToString('N')))
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Pushover.psm1') -Force -DisableNameChecking
try {
    Write-Host 'Personal Pushover setup: Android and Windows'
    Write-Host 'Register your phone in the official Pushover app and sign into Agent Notify using the same account.'
    Write-Host 'Create an application named Personal Agent Notifications at https://pushover.net/apps/build'
    Write-Host 'Use the User Key from your account dashboard and the API Token from that application.'
    Write-Host ''
    $status = Get-NotificationStatus
    if ($status.outcome -eq 'not_configured') { $null = Write-NotificationConfig }
    else { Write-Host 'Using existing encrypted configuration for this Windows user.' }
    $validation = Test-NotificationAccount
    if ($validation.outcome -ne 'account_valid') { throw 'Account validation did not succeed.' }
    Write-Host 'Account validated. Sending one explicit device test to all registered devices.'
    $result = Send-ProjectNotification -Project personal -Task 'Android and Windows setup' -Event test -EventId $EventId -Message 'Pushover device test: check Android with the phone locked, and the Windows popup and notification center while another app is focused. Then report which devices received this message in the project chat.'
    $result | ConvertTo-Json -Compress
    Write-Host 'API acceptance is not device verification. Please check both receiving devices.'
    Write-Host 'Keep Agent Notify running in the tray; the native receiver does not need a browser.'
    Write-Host 'Check Windows banner and Do Not Disturb priority settings before relying on alerts while gaming.'
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    Write-Host 'Setup did not finish. If delivery is uncertain, check devices before another test.'
    exit 1
}
