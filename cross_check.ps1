Add-Type @"
using System;
using System.Net;
public static class CertHelper {
  public static void Ignore() {
    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
  }
}
"@
[CertHelper]::Ignore()

$BASE = 'https://localhost:7002'

function ResetPwd($username, $newPwd) {
  $r = Invoke-WebRequest -Uri "$BASE/api/v1/auth/forgot-password" -Method POST -ContentType 'application/json' -Body (@{ username = $username } | ConvertTo-Json) -UseBasicParsing
  $j = $r.Content | ConvertFrom-Json
  $tok = $j.data.resetToken
  if (-not $tok) { Write-Host ("no token for $username"); return $false }
  $r2 = Invoke-WebRequest -Uri "$BASE/api/v1/auth/reset-password" -Method POST -ContentType 'application/json' -Body (@{ username = $username; token = $tok; newPassword = $newPwd } | ConvertTo-Json) -UseBasicParsing
  $j2 = $r2.Content | ConvertFrom-Json
  return $j2.isSuccess
}
function Login($path, $body) {
  $r = Invoke-WebRequest -Uri "$BASE$path" -Method POST -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Depth 8) -UseBasicParsing
  return ($r.Content | ConvertFrom-Json)
}

# Reset passwords for moderator_2 and mgr_test_1783640498257
$r1 = ResetPwd 'moderator_2' 'AlFalah@Moderator2024!'
$r2 = ResetPwd 'mgr_test_1783640498257' 'AlFalah@Manager2024!'
Write-Host ("moderator_2 reset ok=" + $r1)
Write-Host ("mgr_test_1783640498257 reset ok=" + $r2)

function GetCount($token, $path) {
  $h = @{ Authorization = "Bearer $token" }
  try {
    $r = Invoke-WebRequest -Uri "$BASE$path" -Headers $h -UseBasicParsing
    $j = $r.Content | ConvertFrom-Json
    return @{ status = [int]$r.StatusCode; count = if ($j.data) { @($j.data).Count } else { -1 }; body = $r.Content.Substring(0, [Math]::Min(200, $r.Content.Length)) }
  } catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    $reader = if ($_.Exception.Response) { (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ status = $code; count = -1; body = $reader.Substring(0, [Math]::Min(200, $reader.Length)) }
  }
}

# Cross-school SM (school 2)
try {
  $sm2 = Login '/api/v1/auth/school-login' @{ schoolId = 2; username = 'mgr_test_1783640498257'; password = 'AlFalah@Manager2024!' }
  if ($sm2.isSuccess) {
    $r = GetCount $sm2.data.accessToken '/api/v1/complaints'
    Write-Host ("SM school 2 -> " + $r.status + " count=" + $r.count + " (expect 0)")
  } else { Write-Host ("SM school 2 login failed: " + $sm2.message) }
} catch { Write-Host ("SM school 2 login threw: " + $_.Exception.Message) }

# Moderator (NOT visit creator) - still scoped to school 1, should see 0
try {
  $mod2 = Login '/api/v1/auth/school-login' @{ schoolId = 1; username = 'moderator_2'; password = 'AlFalah@Moderator2024!' }
  if ($mod2.isSuccess) {
    $r = GetCount $mod2.data.accessToken '/api/v1/complaints'
    Write-Host ("Moderator moderator_2 (NOT creator) -> " + $r.status + " count=" + $r.count + " (expect 0)")
  } else { Write-Host ("mod2 login failed: " + $mod2.message) }
} catch { Write-Host ("mod2 login threw: " + $_.Exception.Message) }

# Cross-school detail fetch (SM school 2 trying to GET /complaints/1) - expect 403/404
try {
  $sm2b = Login '/api/v1/auth/school-login' @{ schoolId = 2; username = 'mgr_test_1783640498257'; password = 'AlFalah@Manager2024!' }
  if ($sm2b.isSuccess) {
    $r = GetCount $sm2b.data.accessToken '/api/v1/complaints/1'
    Write-Host ("SM school 2 GET /complaints/1 -> " + $r.status + " (expect 403)")
  }
} catch {}