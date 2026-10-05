param([string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)

$ErrorActionPreference = 'Stop'
$api = Join-Path (Resolve-Path -LiteralPath $Repository).Path 'backend/AlFalah.Api'
if (Get-NetTCPConnection -LocalPort 5264 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 5264 is already running. Stop your existing API from its terminal, then run this script again.'
}
$settings = Get-Content -LiteralPath (Join-Path $api 'appsettings.Development.json') -Raw | ConvertFrom-Json
if ($settings.ConnectionStrings.DefaultConnection -notmatch '(?i)\(localdb\)\\') {
    throw 'The Drive pilot requires the local Development database.'
}
$pilotEnvironment = @{
    DOTNET_ROLL_FORWARD = 'LatestMajor'
    ASPNETCORE_ENVIRONMENT = 'Development'
    DOTNET_ENVIRONMENT = 'Development'
    Database__InitializeOnStartup = 'false'
    GoogleOAuth__RedirectUri = 'http://localhost:5264/api/v1/school-google-drive/callback'
    GoogleOAuth__CompletionRedirectUri = 'http://localhost:4200/school-manager/evidence-settings'
    SchoolFileStorage__AdministrationEnabled = 'false'
    SchoolFileStorage__ReadModelEnabled = 'false'
    SchoolFileStorage__ArchiveWorkerEnabled = 'false'
    SchoolFileStorage__ArchiveExternalWritesEnabled = 'false'
}
$previousEnvironment = @{}
foreach ($key in $pilotEnvironment.Keys) {
    $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $pilotEnvironment[$key], 'Process')
}
try {
    Write-Host 'Local Drive pilot on http://localhost:5264. Startup migrations and storage rollout flags are disabled.'
    & dotnet run --project (Join-Path $api 'AlFalah.Api.csproj') --no-launch-profile --urls http://localhost:5264
    if ($LASTEXITCODE -ne 0) { throw "API exited with code $LASTEXITCODE." }
} finally {
    foreach ($key in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key], 'Process')
    }
}
