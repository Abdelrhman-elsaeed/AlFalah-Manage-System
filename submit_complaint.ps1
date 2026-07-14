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
$instTok = (Get-Content 'D:\AlFalah-Manage-System\.inst.tok' -Raw).Trim()

$subject = [char]0x0634 + [char]0x0643 + [char]0x0648 + [char]0x0649 + ' ' + [char]0x062A + [char]0x062C + [char]0x0631 + [char]0x064A + [char]0x0628 + [char]0x064A + [char]0x0629 + ' ' + [char]0x0645 + [char]0x0646 + ' ' + [char]0x0627 + [char]0x0644 + [char]0x0645 + [char]0x0639 + [char]0x0644 + [char]0x0645
$bodyText = [char]0x0647 + [char]0x0630 + [char]0x0627 + ' ' + [char]0x0646 + [char]0x0635 + ' ' + [char]0x0627 + [char]0x0644 + [char]0x0634 + [char]0x0643 + [char]0x0648 + [char]0x0649 + ' ' + [char]0x0627 + [char]0x0644 + [char]0x062A + [char]0x062C + [char]0x0631 + [char]0x064A + [char]0x0628 + [char]0x064A + ' ' + [char]0x0644 + [char]0x0644 + [char]0x062A + [char]0x062D + [char]0x0642 + [char]0x0642 + ' ' + [char]0x0645 + [char]0x0646 + ' ' + [char]0x0639 + [char]0x0645 + [char]0x0644 + ' ' + [char]0x0627 + [char]0x0644 + [char]0x0645 + [char]0x0631 + [char]0x062D + [char]0x0644 + [char]0x0629 + ' ' + [char]0x0627 + [char]0x0644 + [char]0x062B + [char]0x0627 + [char]0x0645 + [char]0x0646 + [char]0x0629 + ' ' + [char]0x0628 + [char]0x0634 + [char]0x0643 + [char]0x0644 + ' ' + [char]0x0635 + [char]0x062D + [char]0x0629 + '.'
$payload = @{ subject = $subject; body = $bodyText } | ConvertTo-Json -Depth 8 -Compress

$h = @{ Authorization = "Bearer $instTok" }
Write-Host ("payload: " + $payload)

# Step A: ensure ReportViewLog exists
try {
  $r = Invoke-WebRequest -Uri "$BASE/api/v1/visits/5/report" -Headers $h -UseBasicParsing
  Write-Host ("Inst GET /visits/5/report -> " + $r.StatusCode + " body_len=" + $r.Content.Length)
} catch {
  Write-Host ("GET report failed: " + $_.Exception.Message)
}

# Step B: POST complaint
try {
  $r = Invoke-WebRequest -Uri "$BASE/api/v1/visits/5/complaints" -Method POST -ContentType 'application/json; charset=utf-8' -Body $payload -Headers $h -UseBasicParsing
  Write-Host ("Inst POST /visits/5/complaints -> " + $r.StatusCode + " body=" + $r.Content)
} catch {
  $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
  $reader = if ($_.Exception.Response) { (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
  Write-Host ("Inst POST failed: " + $code + " body=" + $reader)
}