try {
  $r = Invoke-WebRequest -Uri 'http://localhost:5264/swagger/index.html' -UseBasicParsing -TimeoutSec 8
  Write-Host ("HTTP OK: " + $r.StatusCode)
} catch {
  Write-Host ("HTTP ERR: " + $_.Exception.Message)
}
try {
  $r = Invoke-WebRequest -Uri 'https://localhost:7002/swagger/index.html' -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8
  Write-Host ("HTTPS OK: " + $r.StatusCode)
} catch {
  Write-Host ("HTTPS ERR: " + $_.Exception.Message)
}
try {
  $r = Invoke-WebRequest -Uri 'http://localhost:5264/api/Auth/schools' -UseBasicParsing -TimeoutSec 8
  Write-Host ("SCHOOLS HTTP: " + $r.StatusCode + " body=" + $r.Content.Substring(0, [Math]::Min(200, $r.Content.Length)))
} catch {
  Write-Host ("SCHOOLS HTTP ERR: " + $_.Exception.Message)
}