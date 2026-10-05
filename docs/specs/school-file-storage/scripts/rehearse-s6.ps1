param(
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string]$Output = (Join-Path $Repository '.audit/sfs-s6'),
    [string]$Database = ('AlFalahSFS_S6_' + [Guid]::NewGuid().ToString('N'))
)
$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^AlFalahSFS_S6_[a-zA-Z0-9_]+$') { throw 'Use a fresh isolated S6 database name.' }
$root = (Resolve-Path -LiteralPath $Repository).Path
$settings = Get-Content -LiteralPath (Join-Path $root 'backend/AlFalah.Api/appsettings.Development.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.ConnectionStrings.DefaultConnection)
if ($builder.DataSource -notmatch '^\(localdb\)\\' -or !$builder.IntegratedSecurity -or $builder.InitialCatalog -eq $Database) { throw 'LocalDB isolated rehearsal required.' }
$source = $builder.InitialCatalog
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$outputPath = (Resolve-Path -LiteralPath $Output).Path
$builder['Initial Catalog'] = 'master'
$sql = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Rows([string]$Text) {
    $command=$sql.CreateCommand();$command.CommandText=$Text;$command.CommandTimeout=180
    $reader=$command.ExecuteReader();$result=[System.Collections.Generic.List[object]]::new()
    try { while($reader.Read()) { $row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};$result.Add([pscustomobject]$row) } }
    finally {$reader.Dispose();$command.Dispose()};return $result.ToArray()
}
function Execute([string]$Text) { $command=$sql.CreateCommand();$command.CommandText=$Text;$command.CommandTimeout=180;try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()} }
function Quote([string]$Value) {return "'"+$Value.Replace("'","''")+"'"}
function Restore([string]$Name,[string]$Backup) {
    if((Rows "SELECT name FROM sys.databases WHERE name=$(Quote $Name)").Count -gt 0){throw 'Rehearsal refuses to overwrite an existing database.'}
    $files=@(Rows "RESTORE FILELISTONLY FROM DISK=$(Quote $Backup)")
    $moves=for($i=0;$i -lt $files.Count;$i++){"MOVE $(Quote $files[$i].LogicalName) TO $(Quote (Join-Path $outputPath ($Name+'_'+$i+$(if($files[$i].Type -eq 'L'){'.ldf'}else{'.mdf'}))))"}
    Execute "RESTORE DATABASE [$Name] FROM DISK=$(Quote $Backup) WITH $($moves -join ',')"
}
try {
    $sql.Open();$backup=Join-Path $outputPath ($Database+'-source.bak')
    $timer=[Diagnostics.Stopwatch]::StartNew()
    Execute "BACKUP DATABASE [$source] TO DISK=$(Quote $backup) WITH COPY_ONLY, CHECKSUM"
    Execute "RESTORE VERIFYONLY FROM DISK=$(Quote $backup) WITH CHECKSUM"
    Restore $Database $backup
    $builder['Initial Catalog']=$Database
    $env:ALFALAH_MIGRATIONS_CONNECTION=$builder.ConnectionString
    & dotnet ef database update --configuration Release --no-build --project (Join-Path $root 'backend/AlFalah.Infrastructure') --startup-project (Join-Path $root 'backend/AlFalah.Api') *> (Join-Path $outputPath 'clone-migration.log')
    if($LASTEXITCODE -ne 0){throw 'Clone migration failed; see local log.'}
    & dotnet build (Join-Path $PSScriptRoot 'StorageBackfill') --configuration Release --verbosity quiet *> (Join-Path $outputPath 'backfill-build.log')
    if($LASTEXITCODE -ne 0){throw 'Backfill build failed.'}
    $dll=Join-Path $PSScriptRoot 'StorageBackfill/bin/Release/net8.0/StorageBackfill.dll'
    foreach($run in @('dry','first','second')) {
        $mode=if($run -eq 'dry'){'--dry-run'}else{'--apply'}
        & dotnet --roll-forward Major $dll --repository $root --database $Database $mode --report (Join-Path $outputPath ('backfill-'+$run+'.json'))
        if($LASTEXITCODE -notin @(0,2)){throw 'Backfill did not complete.'}
    }
    $post=Join-Path $outputPath ($Database+'-retained.bak');Execute "BACKUP DATABASE [$Database] TO DISK=$(Quote $post) WITH COPY_ONLY, CHECKSUM"
    $restored=$Database+'_Restore';Restore $restored $post;Execute "DBCC CHECKDB ([$restored]) WITH NO_INFOMSGS"
    $counts=@(Rows "SELECT 'clone' AS kind,(SELECT COUNT(*) FROM [$Database].dbo.StoredFiles) AS files,(SELECT COUNT(*) FROM [$Database].dbo.StoredFileVersions) AS versions,(SELECT COUNT(*) FROM [$Database].dbo.EvidenceLinks) AS links,(SELECT COUNT(*) FROM [$Database].dbo.EvidenceReviewDecisions) AS decisions UNION ALL SELECT 'restored',(SELECT COUNT(*) FROM [$restored].dbo.StoredFiles),(SELECT COUNT(*) FROM [$restored].dbo.StoredFileVersions),(SELECT COUNT(*) FROM [$restored].dbo.EvidenceLinks),(SELECT COUNT(*) FROM [$restored].dbo.EvidenceReviewDecisions)")
    $credentials=@(Rows "SELECT COUNT(*) AS differences FROM [$source].dbo.SchoolGoogleDrives a FULL JOIN [$Database].dbo.SchoolGoogleDrives b ON a.SchoolId=b.SchoolId WHERE a.SchoolId IS NULL OR b.SchoolId IS NULL OR ISNULL(a.ProtectedCredential,'')<>ISNULL(b.ProtectedCredential,'')")
    [ordered]@{capturedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');isolatedDatabase=$Database;restoreDatabase=$restored;durationMs=$timer.Elapsed.TotalMilliseconds;sourceBackupCopyOnly=$true;backupChecksumVerified=$true;restoreCheckDbPassed=$true;counts=$counts;credentialDifferences=$credentials[0].differences;originalMigrated=$false;originalFlagsChanged=$false;downExecuted=$false;driveDeleted=$false;rollback='All four flags stay OFF; preserve new files/versions/links/review history and reconcile before reopening legacy writes'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputPath 'restore-rehearsal.json') -Encoding UTF8
    Write-Host "Rehearsal retained in $Database and $restored. Original database not migrated."
} finally {$sql.Dispose();Remove-Item Env:ALFALAH_MIGRATIONS_CONNECTION -ErrorAction SilentlyContinue}
