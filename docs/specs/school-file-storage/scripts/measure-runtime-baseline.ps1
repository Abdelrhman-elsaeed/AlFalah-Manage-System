param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../baseline/runtime-baseline.json')
)

$ErrorActionPreference = 'Stop'
# Fixed SELECT statements only. Do not start the API: startup migrates/seeds the database.
$configPath = Join-Path $RepositoryRoot 'backend/AlFalah.Api/appsettings.Development.json'
$config = Get-Content -LiteralPath $configPath -Encoding UTF8 -Raw | ConvertFrom-Json
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$config.ConnectionStrings.DefaultConnection)
if ($builder.DataSource -notmatch '^(\(localdb\)\\|localhost([\\,]|$)|\.([\\,]|$))') {
    throw 'This baseline collector only accepts the explicitly configured local Development database.'
}
$builder['ApplicationIntent'] = 'ReadOnly'
$builder['Application Name'] = 'SFS-S0-ReadOnly'
$builder['Connect Timeout'] = 5
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)

function Read-Aggregate([string]$Sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 20
    $reader = $command.ExecuteReader()
    $rows = [System.Collections.Generic.List[object]]::new()
    try {
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($i=0; $i -lt $reader.FieldCount; $i++) {
                $row[$reader.GetName($i)] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
            }
            $rows.Add([pscustomobject]$row)
        }
    } finally { $reader.Dispose(); $command.Dispose() }
    return $rows.ToArray()
}

try {
    $connection.Open()
    $report = [ordered]@{
        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        environment = 'Development local only; not production and not E2E'
        database = $builder.InitialCatalog
        method = 'Direct SqlClient fixed SELECT aggregates; no API, migrations, seed, or Drive calls'
        totals = @(Read-Aggregate @'
SELECT
 (SELECT COUNT(*) FROM dbo.Schools) AS Schools,
 (SELECT COUNT(*) FROM dbo.Schools WHERE IsDeleted=0) AS NonDeletedSchools,
 (SELECT COUNT(*) FROM dbo.SchoolGoogleDrives) AS DriveConfigurations,
 (SELECT COUNT(*) FROM dbo.SchoolGoogleDrives WHERE IsEnabled=1) AS EnabledDriveConfigurations,
 (SELECT COUNT(*) FROM dbo.SchoolGoogleDrives WHERE IsEnabled=1 AND LEN(ProtectedCredential)>0) AS EnabledWithStoredCredential,
 (SELECT COUNT(*) FROM dbo.TeacherDriveFolders) AS TeacherFolderGrants,
 (SELECT COUNT(*) FROM dbo.TeacherDriveFolders WHERE IsActive=1) AS ActiveTeacherFolderGrants,
 (SELECT COUNT(*) FROM dbo.TeacherEvidenceSubmissions) AS Submissions,
 (SELECT COUNT(*) FROM dbo.TeacherEvidenceSubmissions WHERE IsDeleted=0) AS NonDeletedSubmissions,
 (SELECT COUNT(*) FROM dbo.TeacherEvidenceSubmissions WHERE IsMissingFromDrive=1) AS FlaggedMissingFromDrive,
 (SELECT COUNT(*) FROM dbo.TeacherEvidenceSubmissions WHERE TaskId IS NULL OR AcademicYearId IS NULL) AS LegacyUnlinkedSubmissions,
 (SELECT COUNT(*) FROM (SELECT SchoolId,DriveId,DriveItemId FROM dbo.TeacherEvidenceSubmissions GROUP BY SchoolId,DriveId,DriveItemId) x) AS DistinctRecordedDriveItems,
 (SELECT COUNT(*) FROM dbo.EvidenceUploadOperations WHERE Status=1) AS PendingUploadOperations,
 (SELECT COUNT(*) FROM dbo.EvidenceTasks) AS EvidenceTasks,
 (SELECT COUNT(*) FROM dbo.AcademicYears) AS AcademicYears,
 (SELECT COUNT(*) FROM dbo.TeacherTaskStatuses) AS TaskStatusRows;
'@)
        schoolGroups = @(Read-Aggregate @'
SELECT s.Id AS SchoolKey,
 (SELECT COUNT(*) FROM dbo.SchoolGoogleDrives d WHERE d.SchoolId=s.Id AND d.IsEnabled=1) AS EnabledDrives,
 (SELECT COUNT(*) FROM dbo.TeacherDriveFolders g WHERE g.SchoolId=s.Id AND g.IsActive=1) AS ActiveGrants,
 (SELECT COUNT(*) FROM dbo.TeacherEvidenceSubmissions f WHERE f.SchoolId=s.Id) AS Submissions,
 (SELECT COUNT(*) FROM dbo.EvidenceUploadOperations o WHERE o.SchoolId=s.Id AND o.Status=1) AS PendingOperations
FROM dbo.Schools s WHERE s.IsDeleted=0 ORDER BY s.Id;
'@)
        teacherYearTaskGroups = @(Read-Aggregate @'
SELECT SchoolId AS SchoolKey,TeacherId AS TeacherKey,AcademicYearId AS YearKey,TaskId AS TaskKey,
 COUNT(*) AS Submissions,SUM(CASE WHEN IsDeleted=0 THEN 1 ELSE 0 END) AS NonDeleted,
 SUM(CASE WHEN ReviewStatus=3 AND IsDeleted=0 AND IsMissingFromDrive=0 THEN 1 ELSE 0 END) AS ApprovedPresentInLedger
FROM dbo.TeacherEvidenceSubmissions GROUP BY SchoolId,TeacherId,AcademicYearId,TaskId
ORDER BY SchoolId,TeacherId,AcademicYearId,TaskId;
'@)
        reviewStates = @(Read-Aggregate 'SELECT ReviewStatus,UploadStatus,IsDeleted,IsMissingFromDrive,COUNT(*) AS Records FROM dbo.TeacherEvidenceSubmissions GROUP BY ReviewStatus,UploadStatus,IsDeleted,IsMissingFromDrive;')
        uploadStates = @(Read-Aggregate 'SELECT Status,COUNT(*) AS Records FROM dbo.EvidenceUploadOperations GROUP BY Status;')
        taskCatalog = @(Read-Aggregate 'SELECT Id,Code,NameAr,Category,IsActive FROM dbo.EvidenceTasks ORDER BY Id;')
        yearGroups = @(Read-Aggregate 'SELECT Id AS YearKey,Code,IsActive FROM dbo.AcademicYears ORDER BY Id;')
        driveMetadataOnly = @(Read-Aggregate 'SELECT SchoolId AS SchoolKey,CredentialType,IsEnabled,CASE WHEN LEN(ProtectedCredential)>0 THEN 1 ELSE 0 END AS HasCredential,CASE WHEN LEN(RootFolderId)>0 THEN 1 ELSE 0 END AS HasRoot,CASE WHEN LEN(SharedDriveId)>0 THEN 1 ELSE 0 END AS UsesSharedDrive FROM dbo.SchoolGoogleDrives ORDER BY SchoolId;')
        limitation = 'Drive contents and unindexed Drive items have not been measured. Stored metadata is not proof of connectivity or file existence.'
    }
    $outputAbsolute = [IO.Path]::GetFullPath($OutputPath)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputAbsolute)) | Out-Null
    [IO.File]::WriteAllText($outputAbsolute, ($report | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $report.totals | ConvertTo-Json -Compress
    Write-Output 'Local baseline saved. No names, emails, Drive IDs, file names, or credentials exported.'
} finally { $connection.Dispose() }
