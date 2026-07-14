try {
  $r = Invoke-WebRequest -Uri 'http://localhost:5264/api/v1/auth/schools' -UseBasicParsing -TimeoutSec 8
  Write-Host ("OK: " + $r.StatusCode + " " + $r.Content.Substring(0, [Math]::Min(300, $r.Content.Length)))
} catch {
  Write-Host ("ERR: " + $_.Exception.Message)
  if ($_.Exception.Response) {
    Write-Host ("Status: " + [int]$_.Exception.Response.StatusCode)
    Write-Host ("Headers: " + ($_.Exception.Response.Headers | Out-String))
  }
}