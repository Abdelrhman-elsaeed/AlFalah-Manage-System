$apiExe = "D:\AlFalah-Manage-System\backend\AlFalah.Api\bin\Debug\net8.0\AlFalah.Api.exe"
if (-not (Test-Path $apiExe)) {
    Write-Host "Building backend..."
    $env:DOTNET_ROLL_FORWARD = 'Major'
    dotnet build "D:\AlFalah-Manage-System\backend\AlFalah.Api"
}

$proc = Start-Process -FilePath $apiExe `
    -ArgumentList "--urls http://localhost:5264 --environment Development" `
    -WorkingDirectory "D:\AlFalah-Manage-System\backend\AlFalah.Api" `
    -WindowStyle Hidden `
    -RedirectStandardOutput "D:\AlFalah-Manage-System\backend\backend.log" `
    -RedirectStandardError "D:\AlFalah-Manage-System\backend\backend.err.log" `
    -PassThru

Start-Sleep -Seconds 2
Write-Host "Started backend PID=$($proc.Id) on http://localhost:5264"

