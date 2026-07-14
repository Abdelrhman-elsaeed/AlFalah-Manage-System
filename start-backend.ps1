Set-Location 'D:\AlFalah-Manage-System\backend'
$proc = Start-Process -FilePath 'cmd.exe' `
    -ArgumentList '/c','dotnet run --project AlFalah.Api' `
    -WindowStyle Hidden `
    -RedirectStandardOutput 'D:\AlFalah-Manage-System\backend\backend.log' `
    -RedirectStandardError 'D:\AlFalah-Manage-System\backend\backend.err.log'
Start-Sleep -Seconds 1
Write-Host "Started backend PID=$($proc.Id)"
