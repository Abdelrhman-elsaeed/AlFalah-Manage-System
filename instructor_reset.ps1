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

$ErrorActionPreference = 'Stop'
$BASE = 'https://localhost:7002'

function PostPlain($path, $body) {
  $r = Invoke-WebRequest -Uri "$BASE$path" -Method POST -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 8) -UseBasicParsing
  return $r.Content
}

# 1. Request forgot-password for the instructor (returns token in dev)
$resp = PostPlain '/api/v1/auth/forgot-password' @{ username = 'nasser_unicode_test' }
Write-Host ("forgot-password raw: " + $resp)
$j = $resp | ConvertFrom-Json
$token = $j.data.resetToken
Write-Host ("token: " + $token)

# 2. Reset password
$resp2 = PostPlain '/api/v1/auth/reset-password' @{ username = 'nasser_unicode_test'; token = $token; newPassword = 'AlFalah@Instructor2024!' }
Write-Host ("reset: " + $resp2)

# 3. Login
$login = PostPlain '/api/v1/auth/school-login' @{ schoolId = 1; username = 'nasser_unicode_test'; password = 'AlFalah@Instructor2024!' }
Write-Host ("login: " + $login)
$j2 = $login | ConvertFrom-Json
$instTok = $j2.data.accessToken

# 4. GET /complaints as instructor
$headers = @{ Authorization = "Bearer $instTok" }
$r = Invoke-WebRequest -Uri "$BASE/api/v1/complaints" -Headers $headers -UseBasicParsing
Write-Host ("Inst GET /complaints -> " + $r.StatusCode + " body=" + $r.Content)

$instTok | Out-File -FilePath 'D:\AlFalah-Manage-System\.inst.tok' -Encoding ASCII