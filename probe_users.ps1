$ErrorActionPreference = 'Stop'
$conn = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AlFalahDb;Trusted_Connection=True;')
$conn.Open()

function Run($sql) {
  $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql
  $dt = New-Object System.Data.DataTable
  (New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($dt) | Out-Null
  $dt
}

Write-Host "=== Users (Id, UserName, Email) ==="
Run "SELECT Id, UserName, Email FROM dbo.Users ORDER BY UserName" | Format-Table -AutoSize

Write-Host "=== UserSchoolRoles (Id, UserId, SchoolId, RoleId) ==="
Run "SELECT usr.Id, usr.UserId, usr.SchoolId, r.Name AS RoleName FROM dbo.UserSchoolRoles usr JOIN dbo.Roles r ON r.Id=usr.RoleId ORDER BY usr.Id" | Format-Table -AutoSize

Write-Host "=== Roles ==="
Run "SELECT Id, Name, NormalizedName FROM dbo.Roles" | Format-Table -AutoSize

Write-Host "=== Visits (Id, SchoolId, InstructorId, CreatedByUserId, Status, ApprovedAt) ==="
Run "SELECT Id, SchoolId, InstructorId, CreatedByUserId, Status, ApprovedAt FROM dbo.Visits ORDER BY Id DESC" | Format-Table -AutoSize

Write-Host "=== Complaints ==="
Run "SELECT Id, SchoolId, VisitId, InstructorUserId, ModeratorUserId, Status FROM dbo.Complaints" | Format-Table -AutoSize

Write-Host "=== Schools ==="
Run "SELECT Id, Name, City FROM dbo.Schools" | Format-Table -AutoSize

$conn.Close()