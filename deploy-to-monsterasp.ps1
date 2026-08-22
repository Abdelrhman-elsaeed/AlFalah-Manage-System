<#
.SYNOPSIS
  One-click build and deploy script for Al-Falah System to MonsterASP.net via WebDeploy.
#>

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Al-Falah System - Deploy to MonsterASP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$WorkspaceRoot = $PSScriptRoot
$FrontendPath = Join-Path $WorkspaceRoot "frontend"
$BackendPath = Join-Path $WorkspaceRoot "backend\AlFalah.Api"
$PublishOut = Join-Path $WorkspaceRoot "publish_out"

# 1. Build Frontend
Write-Host "`n[1/4] Building Angular Frontend..." -ForegroundColor Yellow
Push-Location $FrontendPath
try {
    npm run build
} finally {
    Pop-Location
}

# 2. Copy Frontend to Backend wwwroot
Write-Host "`n[2/4] Copying Frontend files to wwwroot..." -ForegroundColor Yellow
$WwwRoot = Join-Path $BackendPath "wwwroot"
if (Test-Path $WwwRoot) {
    Remove-Item -Path "$WwwRoot\*" -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $WwwRoot -Force | Out-Null
}
Copy-Item -Path "$FrontendPath\dist\al-falah-app\browser\*" -Destination $WwwRoot -Recurse -Force

# 3. Publish Backend (Self-Contained win-x86)
Write-Host "`n[3/4] Publishing .NET Backend..." -ForegroundColor Yellow
if (Test-Path $PublishOut) {
    Remove-Item -Path $PublishOut -Recurse -Force
}
dotnet publish "$BackendPath\AlFalah.Api.csproj" -c Release -r win-x86 --self-contained true -o $PublishOut

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
    -dest:contentPath="site86674",ComputerName="https://site86674.siteasp.net:8172/msdeploy.axd?site=site86674",UserName="site86674",Password="Je4-!Qh3o2Z@",AuthType="Basic" `
    -allowUntrusted `
    -enableRule:AppOffline

Write-Host "`n Deployment Successful! Visit: http://alfalahtest.runasp.net/" -ForegroundColor Green
