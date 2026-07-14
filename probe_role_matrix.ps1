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

function Login($url, $body) {
  $r = Invoke-WebRequest -Uri $url -Method POST -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 8) -UseBasicParsing
  return ($r.Content | ConvertFrom-Json)
}

function Call($token, $method, $path, $body = $null) {
  $headers = @{ Authorization = "Bearer $token" }
  $params = @{ Uri = "$BASE$path"; Method = $method; Headers = $headers; UseBasicParsing = $true }
  if ($body -ne $null) {
    $params['ContentType'] = 'application/json'
    $params['Body'] = ($body | ConvertTo-Json -Depth 8 -Compress)
  }
  try {
    $r = Invoke-WebRequest @params
    return @{ status = [int]$r.StatusCode; body = $r.Content }
  } catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    $reader = if ($_.Exception.Response) { (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ status = $code; body = $reader }
  }
}

Write-Host "=== SM (school_manager_1, school 1) login + GET /complaints ==="
$sm = Login "$BASE/api/v1/auth/school-login" @{ schoolId = 1; username = 'school_manager_1'; password = 'AlFalah@Manager2024!' }
$smTok = $sm.data.accessToken
Write-Host ("login.isSuccess: " + $sm.isSuccess)
$smList = Call $smTok 'GET' '/api/v1/complaints'
Write-Host ("SM GET /complaints -> " + $smList.status + " body=" + $smList.body)

Write-Host ""
Write-Host "=== Instructor (nasser_unicode_test) login + GET /complaints ==="
try {
  $inst = Login "$BASE/api/v1/auth/school-login" @{ schoolId = 1; username = 'nasser_unicode_test'; password = 'AlFalah@Instructor2024!' }
  if (-not $inst.isSuccess) { Write-Host ("inst login failed: " + $inst.message); $instTok = $null } else {
    $instTok = $inst.data.accessToken
    $instList = Call $instTok 'GET' '/api/v1/complaints'
    Write-Host ("Inst GET /complaints -> " + $instList.status + " body=" + $instList.body)
  }
} catch { Write-Host ("inst login threw: " + $_.Exception.Message) }

Write-Host ""
Write-Host "=== Moderator (moderator_1) login + GET /complaints ==="
try {
  $mod = Login "$BASE/api/v1/auth/school-login" @{ schoolId = 1; username = 'moderator_1'; password = 'AlFalah@Moderator2024!' }
  if (-not $mod.isSuccess) { Write-Host ("mod login failed: " + $mod.message); $modTok = $null } else {
    $modTok = $mod.data.accessToken
    $modList = Call $modTok 'GET' '/api/v1/complaints'
    Write-Host ("Mod GET /complaints -> " + $modList.status + " body=" + $modList.body)
  }
} catch { Write-Host ("mod login threw: " + $_.Exception.Message) }

Write-Host ""
Write-Host "=== SuperAdmin login + GET /complaints ==="
$sa = Login "$BASE/api/v1/auth/main-manager-login" @{ username = 'superadmin'; password = 'AlFalah@SuperAdmin2024!' }
Write-Host ("SA login ok=" + $sa.isSuccess)
$saTok = $sa.data.accessToken
$saList = Call $saTok 'GET' '/api/v1/complaints'
Write-Host ("SA GET /complaints -> " + $saList.status + " body=" + $saList.body)

Write-Host ""
Write-Host "=== MainManager login + GET /complaints (expect 403) ==="
try {
  $mm = Login "$BASE/api/v1/auth/main-manager-login" @{ username = 'superadmin'; password = 'AlFalah@SuperAdmin2024!' }
  # superadmin IS a SuperAdmin not MainManager; need a MainManager user. Try elrahman642? check users table
} catch {}

$smTok | Out-File -FilePath 'D:\AlFalah-Manage-System\.sm.tok' -Encoding ASCII
$instTok | Out-File -FilePath 'D:\AlFalah-Manage-System\.inst.tok' -Encoding ASCII -ErrorAction SilentlyContinue
$modTok | Out-File -FilePath 'D:\AlFalah-Manage-System\.mod.tok' -Encoding ASCII -ErrorAction SilentlyContinue
$saTok | Out-File -FilePath 'D:\AlFalah-Manage-System\.sa.tok' -Encoding ASCII