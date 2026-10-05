Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NotificationHome {
    if (-not $env:LOCALAPPDATA) { throw 'Windows LOCALAPPDATA is required.' }
    $current = Join-Path $env:LOCALAPPDATA 'AgentNotify\sender'
    $legacy = Join-Path $env:LOCALAPPDATA 'AgentToolkit\pushover'
    if (!(Test-Path -LiteralPath (Join-Path $current 'settings.json')) -and (Test-Path -LiteralPath (Join-Path $legacy 'settings.json'))) { return $legacy }
    $current
}

function Initialize-PrivateDirectory([string]$Directory) {
    if (-not (Test-Path -LiteralPath $Directory)) {
        $null = New-Item -ItemType Directory -Path $Directory -Force
    }
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $acl = [System.Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @($identity.User, [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    $directoryInfo = [IO.DirectoryInfo]::new($Directory)
    if ('System.IO.FileSystemAclExtensions' -as [type]) {
        [IO.FileSystemAclExtensions]::SetAccessControl($directoryInfo, $acl)
    } else {
        $directoryInfo.SetAccessControl($acl)
    }
}

function ConvertTo-PlainText([Security.SecureString]$Value) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function Write-NotificationConfig {
    $directory = Get-NotificationHome
    $configPath = Join-Path $directory 'settings.json'
    if (Test-Path -LiteralPath $configPath) { throw 'Configuration already exists. Preserve it; remove it manually only when intentionally replacing credentials.' }
    $userKey = Read-Host 'Pushover personal User Key (hidden)' -AsSecureString
    $appToken = Read-Host 'Pushover application API Token (hidden)' -AsSecureString
    try {
        foreach ($value in @($userKey, $appToken)) {
            if ((ConvertTo-PlainText $value) -cnotmatch '^[A-Za-z0-9]{30}$') {
                throw 'Keys must contain exactly 30 ASCII letters or digits.'
            }
        }
        Initialize-PrivateDirectory $directory
        $data = @{
            schema_version = 1
            user_key = ConvertFrom-SecureString $userKey
            app_token = ConvertFrom-SecureString $appToken
        }
        $data | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
        @{ outcome = 'configured'; online_validation = 'pending' }
    } finally { $userKey.Dispose(); $appToken.Dispose() }
}

function Read-NotificationConfig {
    $path = Join-Path (Get-NotificationHome) 'settings.json'
    if (-not (Test-Path -LiteralPath $path)) { throw 'Pushover is not configured. Run Configure as the Windows user who receives notifications.' }
    try {
        $data = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($data.schema_version -ne 1) { throw 'version' }
        $userKey = ConvertTo-SecureString $data.user_key
        $appToken = ConvertTo-SecureString $data.app_token
        try {
            $credentials = @{ user = ConvertTo-PlainText $userKey; token = ConvertTo-PlainText $appToken }
            if ($credentials.user -cnotmatch '^[A-Za-z0-9]{30}$' -or $credentials.token -cnotmatch '^[A-Za-z0-9]{30}$') { throw 'format' }
            $credentials
        } finally { $userKey.Dispose(); $appToken.Dispose() }
    } catch { throw 'Configuration cannot be decrypted by this Windows user on this machine. Configure separately for this identity; do not copy credentials between users or PCs.' }
}

function New-NotificationPayload {
    param([string]$Project, [string]$Task, [string]$Event, [string]$Message, [string]$Url)
    if ([string]::IsNullOrWhiteSpace($Project) -or [string]::IsNullOrWhiteSpace($Task) -or [string]::IsNullOrWhiteSpace($Message)) {
        throw 'Project, Task and Message are required.'
    }
    $labels = @{ complete = 'Complete'; 'action-required' = 'Action needed'; failed = 'Failed'; test = 'Test' }
    if (-not $labels.ContainsKey($Event)) { throw 'Event must be complete, action-required, failed or test.' }
    $title = "[$Project] $($labels[$Event]): $Task"
    if ($title.Length -gt 250 -or $Message.Length -gt 1024) { throw 'Title exceeds 250 characters or message exceeds 1024 characters. Send a concise summary.' }
    $body = @{ title = $title; message = $Message; priority = 0 }
    if ($Url) {
        $parsed = $null
        if ($Url.Length -gt 512 -or -not [Uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$parsed) -or $parsed.Scheme -notin @('http', 'https') -or $parsed.UserInfo) {
            throw 'URL must be an HTTP(S) link of at most 512 characters without embedded credentials.'
        }
        $body.url = $Url
    }
    $body
}

function Invoke-PushoverRequest {
    param([ValidateSet('users/validate', 'messages')][string]$Endpoint, [hashtable]$Body)
    # Never expose raw API response errors, request bodies, tokens or keys.
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-RestMethod -Method Post -Uri "https://api.pushover.net/1/$Endpoint.json" -Body $Body -TimeoutSec 20 -ErrorAction Stop
        } catch {
            $code = 0
            if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) {
                $code = [int]$_.Exception.Response.StatusCode
            }
            if ($code -ge 500 -and $code -le 599 -and $attempt -lt 3) { Start-Sleep -Seconds 5; continue }
            if ($code -ge 400 -and $code -le 499) { throw "Pushover rejected the request (HTTP $code). No retry; check configuration or quota." }
            if ($Endpoint -eq 'messages') { throw 'Delivery is uncertain. Do not blindly resend: check the receiving devices first.' }
            throw 'Pushover validation could not complete. Check connectivity and try later.'
        }
        if (-not $response -or -not $response.PSObject.Properties['status'] -or $response.status -ne 1) {
            throw 'Pushover did not confirm success. No automatic retry.'
        }
        return $response
    }
}

function Test-NotificationAccount {
    $credentials = Read-NotificationConfig
    try {
        $null = Invoke-PushoverRequest -Endpoint 'users/validate' -Body $credentials
        @{ outcome = 'account_valid'; device_delivery = 'not_tested' }
    } finally { $credentials.Clear() }
}

function Get-NotificationStatus {
    $path = Join-Path (Get-NotificationHome) 'settings.json'
    if (-not (Test-Path -LiteralPath $path)) { return @{ outcome = 'not_configured' } }
    $credentials = Read-NotificationConfig
    $credentials.Clear()
    @{ outcome = 'configured'; online_validation = 'not_checked'; device_delivery = 'not_verified' }
}

function Get-NotificationFingerprint([string]$Project, [string]$Task, [string]$Event, [string]$EventId) {
    $bytes = [Text.Encoding]::UTF8.GetBytes((@($Project, $Task, $Event, $EventId) | ConvertTo-Json -Compress))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Send-ProjectNotification {
    param([string]$Project, [string]$Task, [string]$Event, [string]$Message, [string]$EventId, [string]$Url)
    if ([string]::IsNullOrWhiteSpace($EventId)) { throw 'EventId is required. Use a stable task-run/event identifier for deduplication.' }
    $body = New-NotificationPayload -Project $Project -Task $Task -Event $Event -Message $Message -Url $Url
    $directory = Get-NotificationHome
    $statePath = Join-Path $directory 'state.json'
    $fingerprint = Get-NotificationFingerprint $Project $Task $Event $EventId
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $mutex = [Threading.Mutex]::new($false, "Local\AgentToolkitPushover-$sid")
    $locked = $false
    try {
        try { $locked = $mutex.WaitOne(1000) } catch [Threading.AbandonedMutexException] { $locked = $true }
        if (-not $locked) { throw 'Notification sender is busy. Try later with the same EventId.' }
        $state = @()
        if (Test-Path -LiteralPath $statePath) {
            try {
                $decoded = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
                $state = @($decoded)
                foreach ($item in $state) {
                    if (-not $item -or -not $item.PSObject.Properties['id'] -or -not $item.PSObject.Properties['time'] -or -not $item.PSObject.Properties['outcome']) { throw 'invalid ledger' }
                }
            }
            catch { throw 'Notification state cannot be read. Preserve it and investigate before sending.' }
        }
        $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        # Seven-day deduplication; no message content or credentials in the ledger.
        $state = @($state | Where-Object { $_.time -gt ($now - 604800) })
        $existing = @($state | Where-Object { $_.id -eq $fingerprint })
        if ($existing.Count) { return @{ outcome = 'suppressed'; reason = 'duplicate_or_uncertain_event' } }
        if (@($state | Where-Object { $_.time -gt ($now - 3600) }).Count -ge 10) {
            return @{ outcome = 'suppressed'; reason = 'hourly_limit'; retry_after = 'later; nothing queued' }
        }
        $credentials = Read-NotificationConfig
        try {
            foreach ($key in @('user', 'token')) { $body[$key] = $credentials[$key] }
            # Write intent first so a timeout/crash cannot cause a silent duplicate resend.
            $entry = [pscustomobject]@{ id = $fingerprint; time = $now; outcome = 'uncertain' }
            $state = @($state) + @($entry)
            Initialize-PrivateDirectory $directory
            ConvertTo-Json -InputObject @($state) -Depth 3 | Set-Content -LiteralPath $statePath -Encoding UTF8
            $null = Invoke-PushoverRequest -Endpoint 'messages' -Body $body
            $entry.outcome = 'queued'
            ConvertTo-Json -InputObject @($state) -Depth 3 | Set-Content -LiteralPath $statePath -Encoding UTF8
            @{ outcome = 'queued'; device_delivery = 'not_verified'; event_fingerprint = $fingerprint }
        } finally { $body.Clear(); $credentials.Clear() }
    } finally {
        if ($locked) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

Export-ModuleMember -Function Write-NotificationConfig, Get-NotificationStatus, Test-NotificationAccount, New-NotificationPayload, Send-ProjectNotification
