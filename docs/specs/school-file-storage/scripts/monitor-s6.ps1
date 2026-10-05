param([int]$SchoolId=18,[string]$Database,[string]$OutputPath=(Join-Path $PSScriptRoot '../verification/s6-monitor.json'))
$ErrorActionPreference='Stop'
if($SchoolId -le 0){throw 'SchoolId must be positive.'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$settings=Get-Content -LiteralPath (Join-Path $root 'backend/AlFalah.Api/appsettings.Development.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.ConnectionStrings.DefaultConnection)
if($builder.DataSource -notmatch '^\(localdb\)\\'){throw 'Local observation only.'}
if($Database){if($Database -notmatch '^AlFalahSFS_S6_[a-zA-Z0-9_]+$'){throw 'An isolated S6 database is required.'};$builder['Initial Catalog']=$Database}
$builder['ApplicationIntent']='ReadOnly'
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Count([string]$Query){$command=$connection.CreateCommand();$command.CommandText=$Query;$command.CommandTimeout=20;try{return [int]$command.ExecuteScalar()}finally{$command.Dispose()}}
try{
 $connection.Open();$schema=Count "SELECT COUNT(*) FROM sys.tables WHERE name IN ('StorageOperations','StoredFiles','StoredFileVersions','EvidenceLinks','VisitArchiveOperations','PrototypeImportRows')"
 $report=[ordered]@{capturedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');school=$SchoolId;schemaAvailable=($schema -eq 6);readOnly=$true;googleObserved=$false;unindexedDriveFiles=$null;countDrift=$null;metrics=$null}
 if($schema -eq 6){$report.metrics=[ordered]@{
  uploadsNeedingAttention=(Count "SELECT COUNT(*) FROM StorageOperations WHERE SchoolId=$SchoolId AND Status IN ('Failed','NeedsAttention')")
  uploadsOlderThanTenMinutes=(Count "SELECT COUNT(*) FROM StorageOperations WHERE SchoolId=$SchoolId AND Status='Pending' AND CreatedAtUtc<DATEADD(MINUTE,-10,SYSDATETIMEOFFSET())")
  archiveNeedingAttention=(Count "SELECT COUNT(*) FROM VisitArchiveOperations WHERE SchoolId=$SchoolId AND Status=5")
  expiredArchiveLeases=(Count "SELECT COUNT(*) FROM VisitArchiveOperations WHERE SchoolId=$SchoolId AND Status=2 AND LeaseExpiresAtUtc<SYSDATETIMEOFFSET()")
  archiveQueueOlderThanTenMinutes=(Count "SELECT COUNT(*) FROM VisitArchiveOperations WHERE SchoolId=$SchoolId AND Status IN (1,4) AND CreatedAtUtc<DATEADD(MINUTE,-10,SYSDATETIMEOFFSET())")
  approvedLinksWithUnavailableBytes=(Count "SELECT COUNT(*) FROM EvidenceLinks l JOIN StoredFileVersions v ON v.Id=l.VersionId AND v.SchoolId=l.SchoolId JOIN StoredFiles f ON f.Id=l.StoredFileId AND f.SchoolId=l.SchoolId WHERE l.SchoolId=$SchoolId AND l.IsActive=1 AND l.Status=3 AND (v.Availability<>2 OR f.IsDeleted=1)")
  importExceptions=(Count "SELECT COUNT(*) FROM PrototypeImportRows WHERE SchoolId=$SchoolId AND (Status=4 OR Classification IN ('Missing','New','Conflict'))")
 }}
 $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $OutputPath -Encoding UTF8
}finally{$connection.Dispose()}
