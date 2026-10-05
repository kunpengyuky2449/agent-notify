$ErrorActionPreference = 'Stop'
$modulePath = Join-Path $PSScriptRoot '..\scripts\Pushover.psm1'
if (-not (Test-Path -LiteralPath $modulePath)) { $modulePath = Join-Path $PSScriptRoot 'Pushover.psm1' }
$module = Import-Module $modulePath -Force -PassThru -DisableNameChecking
$testHome = Join-Path ([IO.Path]::GetTempPath()) ('pushover-test-' + [Guid]::NewGuid().ToString('N'))
try {
    & $module {
        param($testHome)
        $script:testHome = $testHome
        $script:calls = 0
        $script:sleeps = @()
        $script:apiMode = 'success'
        $script:dummyKey = 'a' * 30
        function script:Get-NotificationHome { $script:testHome }
        function script:Read-Host { param($Prompt, [switch]$AsSecureString) ConvertTo-SecureString $script:dummyKey -AsPlainText -Force }
        function script:Start-Sleep { param($Seconds) $script:sleeps += $Seconds }
        function script:Invoke-RestMethod {
            param($Method, $Uri, $Body, $TimeoutSec, $ErrorAction)
            $script:calls++
            if ($Uri -notlike 'https://api.pushover.net/1/*' -or $Method -ne 'Post') { throw 'Bad API route' }
            if ($script:apiMode -eq 'timeout') { throw [TimeoutException]::new('sensitive raw error ' + $script:dummyKey) }
            if ($script:apiMode -in @('400', '500')) {
                $exception = [Exception]::new('sensitive raw error ' + $script:dummyKey)
                $exception | Add-Member -NotePropertyName Response -NotePropertyValue ([pscustomobject]@{ StatusCode = [int]$script:apiMode })
                throw $exception
            }
            [pscustomobject]@{ status = 1; request = 'mock-request' }
        }
        function Assert($Condition, $Label) { if (-not $Condition) { throw "Failed: $Label" } }
        function AssertThrows([scriptblock]$Action, [string]$Expected) {
            $caught = $false
            try { & $Action | Out-Null } catch {
                $caught = $true
                Assert ($_.Exception.Message -like "*$Expected*") "error: $Expected"
                Assert (-not $_.Exception.Message.Contains($script:dummyKey)) 'secret redaction'
            }
            Assert $caught 'expected failure'
        }
        Assert ((Get-NotificationStatus).outcome -eq 'not_configured') 'unconfigured status'
        $null = Write-NotificationConfig
        $acl = Get-Acl -LiteralPath $testHome
        $allowed = @($acl.Access | Where-Object { $_.AccessControlType -eq 'Allow' })
        Assert ($acl.AreAccessRulesProtected -and $allowed.Count -eq 2) 'private directory ACL'
        $ownerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        foreach ($rule in $allowed) {
            $ruleSid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
            Assert ($ruleSid -in @($ownerSid, 'S-1-5-18')) 'only configuring user and SYSTEM have access'
        }
        $configText = Get-Content -LiteralPath (Join-Path $testHome 'settings.json') -Raw
        Assert (-not $configText.Contains($script:dummyKey)) 'DPAPI ciphertext only'
        Assert ((Get-NotificationStatus).outcome -eq 'configured') 'DPAPI current-user round trip'
        AssertThrows { Write-NotificationConfig } 'already exists'
        Assert ((Test-NotificationAccount).outcome -eq 'account_valid') 'account validation'
        $payload = New-NotificationPayload -Project '项目' -Task '试听' -Event complete -Message '音频已保存。'
        Assert ($payload.priority -eq 0 -and $payload.message -eq '音频已保存。') 'Unicode and quiet-hour-respecting priority'
        AssertThrows { New-NotificationPayload -Project p -Task t -Event complete -Message ('x' * 1025) } 'exceeds'
        AssertThrows { New-NotificationPayload -Project p -Task t -Event complete -Message m -Url 'https://user:secret@example.org/' } 'URL must'
        $result = Send-ProjectNotification -Project p -Task t -Event complete -Message 'safe summary' -EventId run1
        Assert ($result.outcome -eq 'queued') 'queue success'
        $calls = $script:calls
        $result = Send-ProjectNotification -Project p -Task t -Event complete -Message 'changed summary' -EventId run1
        Assert ($result.reason -eq 'duplicate_or_uncertain_event' -and $script:calls -eq $calls) 'stable-event deduplication'
        $script:apiMode = 'timeout'
        AssertThrows { Send-ProjectNotification -Project p -Task t -Event failed -Message 'safe summary' -EventId run2 } 'uncertain'
        $calls = $script:calls
        $result = Send-ProjectNotification -Project p -Task t -Event failed -Message 'safe summary' -EventId run2
        Assert ($result.outcome -eq 'suppressed' -and $script:calls -eq $calls) 'timeout not blindly retried'
        $script:apiMode = '400'
        $calls = $script:calls
        AssertThrows { Invoke-PushoverRequest -Endpoint messages -Body @{} } 'HTTP 400'
        Assert ($script:calls -eq $calls + 1) '4xx not retried'
        $script:apiMode = '500'
        $calls = $script:calls
        AssertThrows { Invoke-PushoverRequest -Endpoint messages -Body @{} } 'uncertain'
        Assert ($script:calls -eq $calls + 3 -and $script:sleeps.Count -eq 2 -and @($script:sleeps | Where-Object { $_ -lt 5 }).Count -eq 0) 'bounded 5xx retry with 5-second minimum'
        $script:apiMode = 'success'
        foreach ($id in 3..10) { $null = Send-ProjectNotification -Project p -Task t -Event complete -Message 'safe summary' -EventId "run$id" }
        $calls = $script:calls
        $result = Send-ProjectNotification -Project p -Task t -Event complete -Message 'safe summary' -EventId run11
        Assert ($result.reason -eq 'hourly_limit' -and $script:calls -eq $calls) 'hourly rate cap'
        $ledger = Get-Content -LiteralPath (Join-Path $testHome 'state.json') -Raw
        Assert (-not $ledger.Contains('safe summary') -and -not $ledger.Contains($script:dummyKey)) 'content-free ledger'
        'PASS: offline account/configuration, encryption, Unicode, limits, errors, deduplication and retry scenarios. No real HTTP requests.'
    } $testHome
} finally {
    Remove-Module $module -ErrorAction SilentlyContinue
    $resolvedTarget = [IO.Path]::GetFullPath($testHome)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedTarget.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedTarget -Leaf) -notmatch '^pushover-test-[a-f0-9]{32}$') {
        throw 'Refusing cleanup outside the verified temporary test directory.'
    }
    if (Test-Path -LiteralPath $resolvedTarget) { Remove-Item -LiteralPath $resolvedTarget -Recurse -Force }
}
