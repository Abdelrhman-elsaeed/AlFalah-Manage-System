param(
    [Parameter(Mandatory = $true)][string]$TrialDirectory,
    [ValidateRange(1, 4)][int]$Hours = 2
)

# Operator-run synthetic trial, not a deployment or a production launcher.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$trial = (Resolve-Path -LiteralPath $TrialDirectory).Path
if ($trial -eq $repo -or $trial.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Private trial settings must be outside the repository.'
}
$configPath = Join-Path $trial 'appsettings.json'
$settingsPath = Join-Path $trial 'trial-settings.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
# PowerShell adapts this dictionary-backed object: assigning .ConnectionString is
# treated as a SQL keyword. Parse through the constructor instead (also on PS 5.1).
$sqlBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$config.ConnectionStrings.DefaultConnection)
if ($settings.databaseName -notmatch '^AlFalahN2DeviceE2E_[a-f0-9]+$' -or
    $sqlBuilder.InitialCatalog -cne $settings.databaseName -or
    $sqlBuilder.DataSource -ine '(localdb)\mssqllocaldb' -or !$sqlBuilder.IntegratedSecurity -or
    $config.ALFALAH_E2E_PASSWORD.Length -lt 24 -or
    $settings.apiOrigin -cne 'http://127.0.0.1:5487') { throw 'Unsafe trial configuration.' }
foreach ($port in @(4487, 5487)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Port $port is already occupied; no existing process will be stopped."
    }
}
$apiDll = Join-Path $repo 'backend/AlFalah.Api/bin/Debug/net8.0/AlFalah.Api.dll'
$proxyScript = Join-Path $PSScriptRoot 'serve-n2-device-trial.mjs'
$cloudflared = Join-Path $env:LOCALAPPDATA 'AlFalah/Tools/cloudflared.exe'
if (!(Test-Path -LiteralPath $apiDll) -or !(Test-Path (Join-Path $settings.assetRoot 'ngsw.json'))) {
    throw 'Build the Debug API and optimized pwa-verification frontend first; see the trial guide.'
}
if (!(Test-Path -LiteralPath $cloudflared) -or
    (Get-FileHash -LiteralPath $cloudflared -Algorithm SHA256).Hash -ine
    '4d2490e74082c5d48dbfedaf92576fa5bd1702379ab88eb7faa0cfbe8dc891b6') {
    throw 'Expected official cloudflared 2026.10.1 Windows amd64 digest is missing; see the trial guide.'
}
$oldEnvironment = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'DOTNET_ROLL_FORWARD', 'ALFALAH_N2_TRIAL_SETTINGS')) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
# Refuse inherited app overrides rather than accidentally exposing a different database/key ring.
if (Get-ChildItem Env: | Where-Object { $_.Name -match '^(ConnectionStrings|Jwt|DataProtection|E2E|Inbox|WebPush|Storage)__|^ALFALAH_E2E_PASSWORD$' }) {
    throw 'Use a clean PowerShell session without application configuration environment overrides.'
}
$children = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function Save-Config { $config | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $configPath -Encoding utf8 }
function Start-TrialApi([string]$mode) {
    $env:ASPNETCORE_ENVIRONMENT = $mode
    $env:DOTNET_ENVIRONMENT = $mode
    $env:DOTNET_ROLL_FORWARD = 'Major'
    $process = Start-Process dotnet -ArgumentList @(('"{0}"' -f $apiDll), '--urls', $settings.apiOrigin) `
        -WorkingDirectory $trial -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $trial ('api-' + $mode + '.stdout.log')) `
        -RedirectStandardError (Join-Path $trial ('api-' + $mode + '.stderr.log'))
    $children.Add($process)
    for ($attempt = 0; $attempt -lt 180; $attempt++) {
        if ($process.HasExited) { throw "Trial API exited. Inspect private api-$mode logs locally." }
        try { return Invoke-RestMethod ($settings.apiOrigin + '/api/v1/auth/schools') -TimeoutSec 3 }
        catch { Start-Sleep -Seconds 1 }
    }
    throw 'Trial API startup timed out. Inspect private logs locally.'
}
try {
    $config.WebPush.RegistrationEnabled = $false
    $config.WebPush.DispatchEnabled = $false
    $config.WebPush.PwaUiEnabled = $false
    Save-Config
    [DateTimeOffset]::UtcNow.ToString('O') | Set-Content -LiteralPath $config.E2E.ClockFile -Encoding ascii
    $schools = Start-TrialApi 'E2E'
    $seedProcess = $children[$children.Count - 1]
    Stop-Process -Id $seedProcess.Id
    $seedProcess.WaitForExit()

    # Only this newly generated E2E database is writable here. Disable every default/weak account.
    $connection = New-Object System.Data.SqlClient.SqlConnection($sqlBuilder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        # AlFalahDbContext explicitly maps ApplicationUser to dbo.Users.
        $command.CommandText = "UPDATE dbo.Users SET IsActive = CASE WHEN UserName IN ('parent.test','officer.test') THEN 1 ELSE 0 END; SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1;"
        if ([int]$command.ExecuteScalar() -ne 2) { throw 'Expected exactly two synthetic actors.' }
    } finally { $connection.Dispose() }
    $config | Add-Member -NotePropertyName Database -NotePropertyValue @{ InitializeOnStartup = $false } -Force
    Save-Config
    $schools = Start-TrialApi 'Development'
    $school = @($schools.data | Where-Object name -eq 'Al-Falah E2E Test School')
    if ($school.Count -ne 1) { throw 'Synthetic school was not found.' }
    foreach ($username in @('parent.test', 'officer.test')) {
        $body = @{ username = $username; password = $config.ALFALAH_E2E_PASSWORD; schoolId = $school[0].id } | ConvertTo-Json
        $login = Invoke-RestMethod ($settings.apiOrigin + '/api/v1/auth/school-login') -Method Post -ContentType 'application/json' -Body $body
        if (!$login.data.accessToken) { throw 'Synthetic login preflight failed.' }
    }

    $settings.expiresAtUtc = [DateTimeOffset]::UtcNow.AddHours($Hours).AddSeconds(-30).ToString('O')
    $settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
    $env:ALFALAH_N2_TRIAL_SETTINGS = $settingsPath
    $proxy = Start-Process node -ArgumentList @(('"{0}"' -f $proxyScript)) -WorkingDirectory $repo -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $trial 'proxy.stdout.log') -RedirectStandardError (Join-Path $trial 'proxy.stderr.log')
    $children.Add($proxy)
    Start-Sleep -Seconds 2
    if ($proxy.HasExited) { throw 'Trial proxy failed. Inspect private proxy logs.' }
    $tunnel = Start-Process $cloudflared -ArgumentList @('tunnel', '--no-autoupdate', '--url', 'http://127.0.0.1:4487') `
        -WorkingDirectory $trial -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $trial 'tunnel.stdout.log') `
        -RedirectStandardError (Join-Path $trial 'tunnel.stderr.log')
    $children.Add($tunnel)
    $origin = $null
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($tunnel.HasExited) { throw 'Temporary tunnel failed. Inspect private tunnel logs.' }
        $logs = Get-Content (Join-Path $trial 'tunnel.stderr.log') -Raw -ErrorAction SilentlyContinue
        if ($logs -match 'https://[a-z0-9-]+\.trycloudflare\.com') { $origin = $Matches[0]; break }
        Start-Sleep -Seconds 1
    }
    if (!$origin) { throw 'No temporary HTTPS origin was assigned.' }
    Stop-Process -Id $children[1].Id
    $children[1].WaitForExit()
    $config.WebPush.RegistrationEnabled = $true
    $config.WebPush.DispatchEnabled = $true
    $config.WebPush.PwaUiEnabled = $true
    $config.WebPush.Subject = $origin
    $config.Inbox.AllowedOrigins = @($origin)
    Save-Config
    $null = Start-TrialApi 'Development'
    $manifest = Invoke-RestMethod ($origin + '/manifest.webmanifest') -TimeoutSec 30
    $worker = Invoke-RestMethod ($origin + '/ngsw.json') -TimeoutSec 30
    if ($manifest.start_url -ne '/' -or !$worker.assetGroups) { throw 'Public HTTPS PWA preflight failed.' }
    $settings | Add-Member -NotePropertyName publicOrigin -NotePropertyValue $origin -Force
    $settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
    $loginText = "Synthetic trial only`r`nHTTPS: $origin`r`nSchool: Al-Falah E2E Test School`r`nAndroid username: parent.test`r`nDesktop sender: officer.test`r`nPassword: $($config.ALFALAH_E2E_PASSWORD)`r`nExpires: $($settings.expiresAtUtc)`r`n"
    $loginText | Set-Content -LiteralPath (Join-Path $trial 'device-login.txt') -Encoding utf8
    Write-Host "HTTPS trial: $origin"
    Write-Host "Private login instructions: $(Join-Path $trial 'device-login.txt')"
    Write-Host 'Keep this window open. Ctrl+C stops only the trial processes. Real-phone evidence is still pending.'
    while ([DateTimeOffset]::UtcNow -lt [DateTimeOffset]::Parse($settings.expiresAtUtc)) {
        if ($proxy.HasExited -or $tunnel.HasExited -or $children[$children.Count - 1].HasExited) { throw 'Trial process stopped.' }
        Start-Sleep -Seconds 2
    }
} finally {
    foreach ($child in $children) { if (!$child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue } }
    $config.WebPush.RegistrationEnabled = $false
    $config.WebPush.DispatchEnabled = $false
    $config.WebPush.PwaUiEnabled = $false
    Save-Config
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process') }
}
