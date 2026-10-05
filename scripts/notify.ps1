param(
    [ValidateSet('Configure', 'Status', 'Validate', 'DryRun', 'Send')][string]$Mode = 'Status',
    [string]$Project,
    [string]$Task,
    [ValidateSet('complete', 'action-required', 'failed', 'test')][string]$Event = 'complete',
    [string]$Message,
    [string]$EventId,
    [string]$Url
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Pushover.psm1') -Force -DisableNameChecking
try {
    $result = switch ($Mode) {
        'Configure' { Write-NotificationConfig }
        'Status' { Get-NotificationStatus }
        'Validate' { Test-NotificationAccount }
        'DryRun' { New-NotificationPayload -Project $Project -Task $Task -Event $Event -Message $Message -Url $Url }
        'Send' { Send-ProjectNotification -Project $Project -Task $Task -Event $Event -Message $Message -EventId $EventId -Url $Url }
    }
    $result | ConvertTo-Json -Compress
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
