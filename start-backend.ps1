Set-Location 'D:\AlFalah-Manage-System\backend\AlFalah.Api'
$proc = Start-Process -FilePath "powershell.exe" `
    -ArgumentList "-NoProfile -WindowStyle Hidden -Command `"`$env:DOTNET_ROLL_FORWARD='Major'; `$env:ASPNETCORE_ENVIRONMENT='Development'; Set-Location 'D:\AlFalah-Manage-System\backend\AlFalah.Api'; dotnet run --no-build --launch-profile http *>&1 | Out-File -FilePath 'D:\AlFalah-Manage-System\backend\backend.log' -Encoding utf8`"" `
    -PassThru
Start-Sleep -Seconds 1
Write-Host "Started backend PID=$($proc.Id)"
