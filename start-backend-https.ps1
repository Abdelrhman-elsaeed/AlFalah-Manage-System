Set-Location 'D:\AlFalah-Manage-System\backend\AlFalah.Api'
$proc = Start-Process -FilePath 'cmd.exe' `
    -ArgumentList '/c','set DOTNET_ROLL_FORWARD=Major&& set ASPNETCORE_ENVIRONMENT=Development&& dotnet run --no-build --launch-profile https' `
    -WindowStyle Hidden `
    -RedirectStandardOutput 'D:\AlFalah-Manage-System\backend\backend.log' `
    -RedirectStandardError 'D:\AlFalah-Manage-System\backend\backend.err.log'
Start-Sleep -Seconds 1
Write-Host "Started backend PID=$($proc.Id)"
