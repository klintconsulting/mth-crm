# Dataverse Web API helper for development (browser login with PKCE; -DeviceCode for the old device code flow).
# Token cache is stored outside the repo in %LOCALAPPDATA%\mth-crm\.
# Usage:
#   Import-Module .\scripts\Dataverse.psm1 -Force
#   Connect-Dataverse -Url https://orgxxxx.crm4.dynamics.com
#   Invoke-Dataverse GET 'WhoAmI'

$script:ClientId = '51f81489-12ee-4a9e-aaae-a2591f45987d'   # Microsoft public client used in Dataverse samples
$script:Authority = 'https://login.microsoftonline.com/organizations/oauth2/v2.0'
$script:CacheDir = Join-Path $env:LOCALAPPDATA 'mth-crm'
$script:Url = $null
$script:Token = $null

function Get-CacheFile([string]$Url) {
    $name = ([uri]$Url).Host -replace '[^a-z0-9]', '_'
    Join-Path $script:CacheDir "dv-token-$name.json"
}

function Save-Token($resp) {
    if (-not (Test-Path $script:CacheDir)) { New-Item -ItemType Directory $script:CacheDir | Out-Null }
    $script:Token = [pscustomobject]@{
        access_token  = $resp.access_token
        refresh_token = $resp.refresh_token
        expires       = (Get-Date).AddSeconds([int]$resp.expires_in - 120)
    }
    $script:Token | ConvertTo-Json | Set-Content (Get-CacheFile $script:Url) -Encoding UTF8
}

function ConvertTo-Base64Url([byte[]]$b) { [Convert]::ToBase64String($b).TrimEnd('=').Replace('+', '-').Replace('/', '_') }

# Browser login: authorization code + PKCE with a loopback redirect (http://localhost:<port>/).
# Default because Entra security defaults block the device code flow (AADSTS530035).
function Connect-Browser([string]$Scope, [int]$TimeoutMinutes) {
    $l = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0); $l.Start(); $port = $l.LocalEndpoint.Port; $l.Stop()
    $redirect = "http://localhost:$port/"
    $verifierBytes = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($verifierBytes)
    $verifier = ConvertTo-Base64Url $verifierBytes
    $challenge = ConvertTo-Base64Url ([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::ASCII.GetBytes($verifier)))
    $state = [guid]::NewGuid().ToString('N')
    $authUrl = "$($script:Authority)/authorize?client_id=$($script:ClientId)&response_type=code&redirect_uri=$([uri]::EscapeDataString($redirect))" +
        "&scope=$([uri]::EscapeDataString($Scope))&code_challenge=$challenge&code_challenge_method=S256&state=$state&prompt=select_account"
    $http = [Net.HttpListener]::new(); $http.Prefixes.Add($redirect); $http.Start()
    try {
        Write-Host "Opening browser for sign-in. If it does not open, browse to:`n$authUrl"
        Start-Process $authUrl
        $task = $http.GetContextAsync()
        if (-not $task.Wait([TimeSpan]::FromMinutes($TimeoutMinutes))) { throw 'Browser login timed out.' }
        $ctx = $task.Result
        $q = [Web.HttpUtility]::ParseQueryString($ctx.Request.Url.Query)
        $ok = $q['code'] -and $q['state'] -eq $state
        $msg = if ($ok) { 'Signed in. You can close this tab.' } else { "Sign-in failed: $($q['error']) $($q['error_description'])" }
        $bytes = [Text.Encoding]::UTF8.GetBytes("<html><body style='font-family:sans-serif'><h3>$([Net.WebUtility]::HtmlEncode($msg))</h3></body></html>")
        $ctx.Response.ContentType = 'text/html; charset=utf-8'; $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length); $ctx.Response.Close()
        if (-not $ok) { throw $msg }
        Invoke-RestMethod -Method Post -Uri "$($script:Authority)/token" -Body @{
            grant_type = 'authorization_code'; client_id = $script:ClientId; code = $q['code']
            redirect_uri = $redirect; code_verifier = $verifier; scope = $Scope }
    } finally { $http.Stop(); $http.Close() }
}

function Connect-Dataverse {
    param([Parameter(Mandatory)][string]$Url, [int]$TimeoutMinutes = 15, [switch]$DeviceCode)
    $script:Url = $Url.TrimEnd('/')
    $scope = "$($script:Url)/user_impersonation offline_access"
    $cache = Get-CacheFile $script:Url
    if (Test-Path $cache) {
        $script:Token = Get-Content $cache -Raw | ConvertFrom-Json
        try { [void](Get-DataverseToken); Write-Host "Connected (cached token) to $($script:Url)"; return } catch { Write-Host 'Cached token invalid, logging in again.' }
    }
    if (-not $DeviceCode) {
        Add-Type -AssemblyName System.Web
        Save-Token (Connect-Browser $scope $TimeoutMinutes)
        Write-Host "Connected to $($script:Url)"
        return
    }
    $dc =Invoke-RestMethod -Method Post -Uri "$($script:Authority)/devicecode" -Body @{ client_id = $script:ClientId; scope = $scope }
    Write-Host $dc.message
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds ([int]$dc.interval)
        try {
            $resp = Invoke-RestMethod -Method Post -Uri "$($script:Authority)/token" -Body @{
                grant_type = 'urn:ietf:params:oauth:grant-type:device_code'; client_id = $script:ClientId; device_code = $dc.device_code }
            Save-Token $resp
            Write-Host "Connected to $($script:Url)"
            return
        } catch {
            $err = $null; try { $err = ($_.ErrorDetails.Message | ConvertFrom-Json).error } catch {}
            if ($err -ne 'authorization_pending' -and $err -ne 'slow_down') { throw }
        }
    }
    throw 'Device code login timed out.'
}

function Get-DataverseToken {
    if (-not $script:Token) { throw 'Not connected. Run Connect-Dataverse first.' }
    if ((Get-Date) -ge [datetime]$script:Token.expires) {
        $resp = Invoke-RestMethod -Method Post -Uri "$($script:Authority)/token" -Body @{
            grant_type = 'refresh_token'; client_id = $script:ClientId; refresh_token = $script:Token.refresh_token
            scope = "$($script:Url)/user_impersonation offline_access" }
        Save-Token $resp
    }
    $script:Token.access_token
}

function Invoke-Dataverse {
    param(
        [Parameter(Mandatory, Position = 0)][ValidateSet('GET', 'POST', 'PATCH', 'PUT', 'DELETE')][string]$Method,
        [Parameter(Mandatory, Position = 1)][string]$Path,
        [Parameter(Position = 2)]$Body,
        [string]$Solution,
        [hashtable]$Headers = @{}
    )
    $h = @{
        Authorization      = "Bearer $(Get-DataverseToken)"
        'OData-MaxVersion' = '4.0'; 'OData-Version' = '4.0'
        Accept             = 'application/json'
        'Content-Type'     = 'application/json; charset=utf-8'
    }
    if ($Solution) { $h['MSCRM.SolutionUniqueName'] = $Solution }
    foreach ($k in $Headers.Keys) { $h[$k] = $Headers[$k] }
    $uri = "$($script:Url)/api/data/v9.2/$Path"
    $params = @{ Method = $Method; Uri = $uri; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $Body) {
        $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 30 -Compress }
        $params.Body = [Text.Encoding]::UTF8.GetBytes($json)
    }
    try {
        $r = Invoke-WebRequest @params
        if (-not $r.Content) { return $r.Headers['OData-EntityId'] }
        # Decode as UTF-8 explicitly (PowerShell 5.1 may guess the wrong charset)
        $text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()).TrimStart([char]0xFEFF)
        if ($text -match '^\s*[\{\[]') { $text | ConvertFrom-Json } else { $text.Trim() }   # e.g. $count returns plain text
    } catch {
        $msg = $_.ErrorDetails.Message
        if (-not $msg) { $msg = $_.Exception.Message }
        throw "Dataverse $Method $Path failed: $msg"
    }
}

Export-ModuleMember -Function Connect-Dataverse, Invoke-Dataverse, Get-DataverseToken
