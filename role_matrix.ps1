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

function Login($path, $body) {
  $r = Invoke-WebRequest -Uri "$BASE$path" -Method POST -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Depth 8) -UseBasicParsing
  return ($r.Content | ConvertFrom-Json)
}
function GetCount($token, $path) {
  $h = @{ Authorization = "Bearer $token" }
  try {
    $r = Invoke-WebRequest -Uri "$BASE$path" -Headers $h -UseBasicParsing
    $j = $r.Content | ConvertFrom-Json
    return @{ status = [int]$r.StatusCode; count = if ($j.data) { @($j.data).Count } else { -1 }; body = $r.Content }
  } catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    $reader = if ($_.Exception.Response) { (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ status = $code; count = -1; body = $reader }
  }
}

$sm = Login '/api/v1/auth/school-login' @{ schoolId = 1; username = 'school_manager_1'; password = 'AlFalah@Manager2024!' }
$sa = Login '/api/v1/auth/main-manager-login' @{ username = 'superadmin'; password = 'AlFalah@SuperAdmin2024!' }
$mod1 = Login '/api/v1/auth/school-login' @{ schoolId = 1; username = 'moderator_1'; password = 'AlFalah@Moderator2024!' }
$inst = Login '/api/v1/auth/school-login' @{ schoolId = 1; username = 'nasser_unicode_test'; password = 'AlFalah@Instructor2024!' }

Write-Host ""
Write-Host "=== EXPECTED: complaint is on visit 5 (school 1, moderator=moderator_1, instructor=nasser_unicode_test) ==="
Write-Host ""

# SM (school 1) - should see 1
$r = GetCount $sm.data.accessToken '/api/v1/complaints'
Write-Host ("SM school_manager_1 (school 1) -> " + $r.status + " count=" + $r.count)

# Moderator (visit creator for visit 5) - should see 1
$r = GetCount $mod1.data.accessToken '/api/v1/complaints'
Write-Host ("Moderator moderator_1 (visit creator for visit 5) -> " + $r.status + " count=" + $r.count)

# Instructor (own) - should see 1
$r = GetCount $inst.data.accessToken '/api/v1/complaints'
Write-Host ("Instructor nasser_unicode_test (own) -> " + $r.status + " count=" + $r.count)

# SuperAdmin - should see 1 (global)
$r = GetCount $sa.data.accessToken '/api/v1/complaints'
Write-Host ("SuperAdmin -> " + $r.status + " count=" + $r.count)

# SM of another school - should see 0 (scoping)
try {
  $sm2 = Login '/api/v1/auth/school-login' @{ schoolId = 2; username = 'mgr_test_1783640498257'; password = 'AlFalah@Manager2024!' }
  if ($sm2.isSuccess) {
    $r = GetCount $sm2.data.accessToken '/api/v1/complaints'
    Write-Host ("SM of school 2 (mgr_test_1783640498257) -> " + $r.status + " count=" + $r.count + " (expect 0)")
  } else { Write-Host ("SM school 2 login failed: " + $sm2.message) }
} catch { Write-Host ("SM school 2 login threw: " + $_.Exception.Message) }

# Moderator (NOT the visit creator) - filtered out (still 200, 0)
# moderator_2 was added to school 1 but did NOT create visit 5; he should see 0
try {
  $mod2 = Login '/api/v1/auth/school-login' @{ schoolId = 1; username = 'moderator_2'; password = 'AlFalah@Moderator2024!' }
  if ($mod2.isSuccess) {
    $r = GetCount $mod2.data.accessToken '/api/v1/complaints'
    Write-Host ("Moderator moderator_2 (NOT visit creator) -> " + $r.status + " count=" + $r.count + " (expect 0)")
  } else { Write-Host ("mod2 login failed: " + $mod2.message) }
} catch { Write-Host ("mod2 login threw: " + $_.Exception.Message) }

# MainManager - need to find one. Login as superadmin but MainManager role? Let's try main-manager-login as superadmin (it accepts any "main manager" cred).
# Actually main-manager-login endpoint just authenticates a user with MainManager role. Check if any seeded user has MainManager.
# If none, we rely on MainManager hard 403 logic — covered by EnsureNotMainManager + permission gating.
Write-Host ""
Write-Host "Note: no MainManager seeded in this DB (per seeder). The MainManager 403 is enforced by (a) Permission gate 'Complaint.View' not seeded for MainManager (controller 403) and (b) ComplaintService.EnsureNotMainManager() throwing 403 even if a permission leaks. Both gates are present in source."