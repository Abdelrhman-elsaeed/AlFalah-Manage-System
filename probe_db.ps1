$ErrorActionPreference = 'Stop'
$conn = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AlFalahDb;Trusted_Connection=True;')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME"
$da = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$da.Fill($dt) | Out-Null
Write-Host "=== TABLES ==="
$dt | Select-Object -ExpandProperty TABLE_NAME

$cmd.CommandText = "SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId"
$dt2 = New-Object System.Data.DataTable
$da.SelectCommand = $cmd
$da.Fill($dt2) | Out-Null
Write-Host "=== EF MIGRATIONS HISTORY ==="
if ($dt2.Rows.Count -eq 0) { Write-Host "(empty)" } else { $dt2 | Select-Object -ExpandProperty MigrationId }

$cmd.CommandText = "IF OBJECT_ID('dbo.Complaints','U') IS NULL SELECT 'MISSING: dbo.Complaints' AS Status ELSE SELECT 'OK: dbo.Complaints' AS Status"
$dt3 = New-Object System.Data.DataTable
$da.SelectCommand = $cmd
$da.Fill($dt3) | Out-Null
Write-Host "=== COMPLAINTS TABLE ==="
$dt3 | Select-Object -ExpandProperty Status

$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Complaints' ORDER BY ORDINAL_POSITION"
$dt4 = New-Object System.Data.DataTable
$da.SelectCommand = $cmd
$da.Fill($dt4) | Out-Null
Write-Host "=== COMPLAINTS COLUMNS ==="
if ($dt4.Rows.Count -eq 0) { Write-Host "(none)" } else { $dt4 | Format-Table -AutoSize }

$conn.Close()