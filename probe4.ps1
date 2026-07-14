$r = Invoke-WebRequest -Uri 'http://localhost:5264/api/v1/auth/schools' -UseBasicParsing -TimeoutSec 8
Write-Host ("Status: " + $r.StatusCode)
Write-Host ("Content-Type: " + $r.Headers['Content-Type'])
[Text.Encoding]::UTF8.GetString([Text.Encoding]::GetEncoding('Windows-1252').GetBytes($r.Content))