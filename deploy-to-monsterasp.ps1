<#
.SYNOPSIS
  One-click build and deploy script for Al-Falah System to MonsterASP.net via WebDeploy.

.PARAMETER PackageOnly
  Build the current Angular frontend and create publish_out without contacting MonsterASP.
#>

param(
    [string]$PublishSettingsPath,
    [switch]$PackageOnly
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Al-Falah System - Deploy to MonsterASP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$WorkspaceRoot = $PSScriptRoot
$FrontendPath = Join-Path $WorkspaceRoot "frontend"
$BackendPath = Join-Path $WorkspaceRoot "backend\AlFalah.Api"
$PublishOut = Join-Path $WorkspaceRoot "publish_out"
$FrontendOutput = Join-Path $FrontendPath "dist\al-falah-app\browser"
$StepCount = if ($PackageOnly) { 3 } else { 4 }
$WebDeployHost = $null
if ($PackageOnly) {
    $WebDeploySite = $null
    $WebDeployUser = $null
    $WebDeployPassword = $null
} elseif (-not [string]::IsNullOrWhiteSpace($PublishSettingsPath)) {
    if (-not (Test-Path -LiteralPath $PublishSettingsPath)) {
        throw "Publish settings file was not found: $PublishSettingsPath"
    }

    [xml]$PublishSettings = Get-Content -Raw -LiteralPath $PublishSettingsPath
    $PublishProfile = @($PublishSettings.publishData.publishProfile) |
        Where-Object { $_.publishMethod -eq "MSDeploy" } |
        Select-Object -First 1
    if ($null -eq $PublishProfile) {
        throw "The publish settings file does not contain an MSDeploy profile."
    }

    $WebDeploySite = $PublishProfile.msdeploySite
    $WebDeployHost = $PublishProfile.publishUrl
    $WebDeployUser = $PublishProfile.userName
    $WebDeployPassword = $PublishProfile.userPWD
} else {
    $WebDeploySite = if ($env:MONSTERASP_WEBDEPLOY_SITE) { $env:MONSTERASP_WEBDEPLOY_SITE } else { "site86674" }
    $WebDeployHost = "$WebDeploySite.siteasp.net"
    $WebDeployUser = if ($env:MONSTERASP_WEBDEPLOY_USERNAME) { $env:MONSTERASP_WEBDEPLOY_USERNAME } else { $WebDeploySite }
    $WebDeployPassword = $env:MONSTERASP_WEBDEPLOY_PASSWORD
}

if (-not $PackageOnly -and [string]::IsNullOrWhiteSpace($WebDeployPassword)) {
    throw "Set MONSTERASP_WEBDEPLOY_PASSWORD before deploying. Deployment secrets must not be stored in source control."
}

# 1. Build Frontend
Write-Host "`n[1/$StepCount] Building Angular Frontend..." -ForegroundColor Yellow
Push-Location $FrontendPath
try {
    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "Angular build failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

# 2. Copy Frontend to Backend wwwroot
Write-Host "`n[2/$StepCount] Copying Frontend files to wwwroot..." -ForegroundColor Yellow
$WwwRoot = Join-Path $BackendPath "wwwroot"
if (-not (Test-Path -LiteralPath (Join-Path $FrontendOutput "index.html"))) {
    throw "Angular build output was not found at: $FrontendOutput"
}

if (Test-Path $WwwRoot) {
    $ResolvedWwwRoot = (Resolve-Path -LiteralPath $WwwRoot).Path
    $ExpectedWwwRoot = [System.IO.Path]::GetFullPath($WwwRoot)
    if (-not [string]::Equals($ResolvedWwwRoot, $ExpectedWwwRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected wwwroot path: $ResolvedWwwRoot"
    }

    Get-ChildItem -LiteralPath $ResolvedWwwRoot -Force | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $WwwRoot | Out-Null
}
Get-ChildItem -LiteralPath $FrontendOutput -Force | Copy-Item -Destination $WwwRoot -Recurse -Force

# 3. Publish Backend (Self-Contained win-x86)
Write-Host "`n[3/$StepCount] Publishing .NET Backend..." -ForegroundColor Yellow
if (Test-Path $PublishOut) {
    $ResolvedPublishOut = (Resolve-Path -LiteralPath $PublishOut).Path
    $ExpectedPublishOut = [System.IO.Path]::GetFullPath($PublishOut)
    if (-not [string]::Equals($ResolvedPublishOut, $ExpectedPublishOut, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected publish output path: $ResolvedPublishOut"
    }

    Remove-Item -LiteralPath $ResolvedPublishOut -Recurse -Force
}
dotnet publish "$BackendPath\AlFalah.Api.csproj" -c Release -r win-x86 --self-contained true -o $PublishOut
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if ($PackageOnly) {
    Write-Host "`n Production package created locally: $PublishOut" -ForegroundColor Green
    return
}

# 4. Sync via WebDeploy
Write-Host "`n[4/4] Deploying to MonsterASP via WebDeploy..." -ForegroundColor Yellow
$MsDeployPath = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
if (-not (Test-Path $MsDeployPath)) {
    $MsDeployPath = "C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\msdeploy.exe"
}

if (-not (Test-Path $MsDeployPath)) {
    Write-Host "msdeploy.exe not found. Please install WebDeploy or use Visual Studio Publish." -ForegroundColor Red
    exit 1
}

& $MsDeployPath -verb:sync `
    -source:contentPath="$PublishOut" `
    -dest:contentPath="$WebDeploySite",ComputerName="https://${WebDeployHost}:8172/msdeploy.axd?site=$WebDeploySite",UserName="$WebDeployUser",Password="$WebDeployPassword",AuthType="Basic" `
    -allowUntrusted `
    '-skip:objectName=dirPath,absolutePath=.*\\App_Data(\\.*)?$' `
    -enableRule:AppOffline

if ($LASTEXITCODE -ne 0) {
    throw "WebDeploy failed with exit code $LASTEXITCODE."
}

Write-Host "`n Deployment Successful! Visit: http://alfalahtest.runasp.net/" -ForegroundColor Green
