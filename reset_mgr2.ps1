Add-Type @"
using System; using System.Net;
public static class C { public static void Init() {
  ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
  ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
}}
"@
[C]::Init()
$BASE = 'https://localhost:7002'

# Reset password to a clean known value and immediately login
$USER = 'mgr_test_1783640498257'
$NEW  = 'Passw0rd!XYZ'

$r = Invoke-WebRequest -Uri "$BASE/api/v1/auth/forgot-password" -Method POST -ContentType 'application/json; charset=utf-8' -Body (@{ username = $USER } | ConvertTo-Json -Compress) -UseBasicParsing
$j = $r.Content | ConvertFrom-Json
$tok = $j.data.resetToken
Write-Host ("TOK len: " + $tok.Length)

$payload = @{ username = $USER; token = $tok; newPassword = $NEW } | ConvertTo-Json -Compress
$r2 = Invoke-WebRequest -Uri "$BASE/api/v1/auth/reset-password" -Method POST -ContentType 'application/json; charset=utf-8' -Body $payload -UseBasicParsing
Write-Host ("RESET: " + $r2.Content)

# Login
$loginBody = @{ schoolId = 2; username = $USER; password = $NEW } | ConvertTo-Json -Compress
try {
  $r3 = Invoke-WebRequest -Uri "$BASE/api/v1/auth/school-login" -Method POST -ContentType 'application/json; charset=utf-8' -Body $loginBody -UseBasicParsing
  Write-Host ("LOGIN STATUS: " + $r3.StatusCode + " body=" + $r3.Content.Substring(0, [Math]::Min(300, $r3.Content.Length)))
} catch {
  Write-Host ("LOGIN THREW: " + $_.Exception.Message)
  if ($_.Exception.Response) {
    $code = [int]$_.Exception.Response.StatusCode
    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    $body = $reader.ReadToEnd()
    Write-Host ("LOGIN STATUS: " + $code + " body=" + $body.Substring(0, [Math]::Min(300, $body.Length)))
  }
}