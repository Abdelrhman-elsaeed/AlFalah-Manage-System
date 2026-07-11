Set-Location 'D:\AlFalah-Manage-System\frontend'
$proc = Start-Process -FilePath 'cmd.exe' `
    -ArgumentList '/c','npx ng serve --port 4200 --host 0.0.0.0' `
    -WindowStyle Hidden `
    -RedirectStandardOutput 'D:\AlFalah-Manage-System\frontend\frontend.log' `
    -RedirectStandardError 'D:\AlFalah-Manage-System\frontend\frontend.err.log'
Start-Sleep -Seconds 1
Write-Host "Started frontend"