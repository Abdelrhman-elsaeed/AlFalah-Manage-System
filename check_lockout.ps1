$ErrorActionPreference = 'Stop'
$conn = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AlFalahDb;Trusted_Connection=True;')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT UserName, AccessFailedCount, LockoutEnd, LockoutEnabled FROM dbo.Users WHERE UserName IN ('mgr_test_1783640498257','moderator_2')"
$dt = New-Object System.Data.DataTable
(New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($dt) | Out-Null
$dt | Format-Table -AutoSize
$conn.Close()