$headers = @{
  'Origin' = 'http://localhost:4200'
  'Access-Control-Request-Method' = 'GET'
  'Access-Control-Request-Headers' = 'content-type'
}
try {
  $r = Invoke-WebRequest -Uri 'http://localhost:5264/api/v1/auth/schools' -Method OPTIONS -Headers $headers -UseBasicParsing -TimeoutSec 8
  Write-Host ("PREFLIGHT OK: " + $r.StatusCode)
  Write-Host ("ACAO: " + $r.Headers['Access-Control-Allow-Origin'])
  Write-Host ("ACAC: " + $r.Headers['Access-Control-Allow-Credentials'])
} catch {
  Write-Host ("PREFLIGHT ERR: " + $_.Exception.Message)
}
try {
  $r = Invoke-WebRequest -Uri 'http://localhost:5264/api/v1/auth/schools' -Headers @{ 'Origin' = 'http://localhost:4200' } -UseBasicParsing -TimeoutSec 8
  Write-Host ("GET OK: " + $r.StatusCode)
  Write-Host ("ACAO: " + $r.Headers['Access-Control-Allow-Origin'])
} catch {
  Write-Host ("GET ERR: " + $_.Exception.Message)
}