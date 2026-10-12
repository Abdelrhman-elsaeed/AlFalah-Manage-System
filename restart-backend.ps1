param(
    [int]$Port = 5264,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$restartRoot = $PSScriptRoot
$restartProject = Join-Path $restartRoot 'backend\AlFalah.Api\AlFalah.Api.csproj'
$restartApiDirectory = (Resolve-Path -LiteralPath (Join-Path $restartRoot 'backend\AlFalah.Api')).Path
$restartDll = Join-Path $restartApiDirectory "bin\$Configuration\net8.0\AlFalah.Api.dll"
$restartLog = Join-Path $restartRoot 'backend\dashboard-restart.log'
$restartErrorLog = Join-Path $restartRoot 'backend\dashboard-restart.err.log'
$restartDotnet = (Get-Command dotnet -ErrorAction Stop).Source

Write-Host 'Building the current API...'
& $restartDotnet build $restartProject --no-restore -c $Configuration -v quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed. The existing API has been left running.'
}

$restartOwners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique)
$restartProcesses = @()
foreach ($restartOwner in $restartOwners) {
    $restartProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $restartOwner"
    $restartIsDotnetApi = $restartProcess.Name -eq 'dotnet.exe' -and
        $restartProcess.CommandLine.IndexOf($restartApiDirectory + '\bin\', [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $restartProcess.CommandLine.IndexOf('\AlFalah.Api.dll', [StringComparison]::OrdinalIgnoreCase) -ge 0
    $restartIsAppHost = $restartProcess.Name -eq 'AlFalah.Api.exe' -and
        $restartProcess.ExecutablePath.StartsWith($restartApiDirectory + '\bin\', [StringComparison]::OrdinalIgnoreCase)
    if (-not ($restartIsDotnetApi -or $restartIsAppHost)) {
        throw "Port $Port belongs to a different application. No processes were stopped."
    }
    $restartProcesses += $restartProcess
}

foreach ($restartProcess in $restartProcesses) {
    Write-Host "Stopping the old API (PID $($restartProcess.ProcessId))..."
    Stop-Process -Id $restartProcess.ProcessId -ErrorAction Stop
}

# Preserve the current local policy: restart without migrating or reseeding data.
$restartArguments = @('--roll-forward', 'Major', ('"' + $restartDll + '"'),
    '--urls', "http://localhost:$Port", '--environment', 'Development',
    '--Database:InitializeOnStartup=false')
$restartNewProcess = Start-Process -FilePath $restartDotnet -ArgumentList $restartArguments `
    -WorkingDirectory $restartApiDirectory -WindowStyle Hidden `
    -RedirectStandardOutput $restartLog -RedirectStandardError $restartErrorLog -PassThru

$restartReady = $false
for ($restartAttempt = 0; $restartAttempt -lt 20; $restartAttempt++) {
    $restartNewProcess.Refresh()
    if ($restartNewProcess.HasExited) { break }
    try {
        $restartSwagger = Invoke-RestMethod -Uri "http://localhost:$Port/swagger/v1/swagger.json" -TimeoutSec 2
        if ($restartSwagger.paths.PSObject.Properties.Name -contains '/api/v1/dashboard/school-manager/operations') {
            $restartReady = $true
            break
        }
    } catch {
        Start-Sleep -Milliseconds 500
    }
}
if (-not $restartReady) {
    throw "The new API did not become ready. Check $restartLog and $restartErrorLog."
}
Write-Host "API restarted on http://localhost:$Port (PID $($restartNewProcess.Id))."
Write-Host 'The dashboard operations endpoint is available. Refresh the browser.'
