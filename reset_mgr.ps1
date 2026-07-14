Add-Type @"
using System; using System.Net;
public static class C { public static void Init() {
  ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
  ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
}}
"@
[C]::Init()
$BASE = 'https://localhost:7002'
$USER = 'mgr_test_1783640498257'
$NEW  = 'AlFalah@Mgr2024!'

$r = Invoke-WebRequest -Uri "$BASE/api/v1/auth/forgot-password" -Method POST -ContentType 'application/json' -Body (@{ username = $USER } | ConvertTo-Json -Compress) -UseBasicParsing
$j = $r.Content | ConvertFrom-Json
$tok = $j.data.resetToken
Write-Host ("TOK: " + $tok)

$payload = @{ username = $USER; token = $tok; newPassword = $NEW } | ConvertTo-Json -Compress
$r2 = Invoke-WebRequest -Uri "$BASE/api/v1/auth/reset-password" -Method POST -ContentType 'application/json; charset=utf-8' -Body $payload -UseBasicParsing
Write-Host ("RESET: " + $r2.Content)

$r3 = Invoke-WebRequest -Uri "$BASE/api/v1/auth/school-login" -Method POST -ContentType 'application/json; charset=utf-8' -Body (@{ schoolId = 2; username = $USER; password = $NEW } | ConvertTo-Json -Compress) -UseBasicParsing
Write-Host ("LOGIN: " + $r3.Content)