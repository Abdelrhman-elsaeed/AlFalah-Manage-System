param([Parameter(Mandatory=$true)][string]$Database,[string]$Output='')
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($Output)){$Output=Join-Path $PSScriptRoot '../../../../.audit/sfs-s6'}
if($Database -notmatch '^AlFalahSFS_S6_[a-zA-Z0-9_]+$'){throw 'An isolated S6 clone is required.'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$settings=Get-Content -LiteralPath (Join-Path $root 'backend/AlFalah.Api/appsettings.Development.json') -Encoding UTF8 -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.ConnectionStrings.DefaultConnection)
if($builder.DataSource -notmatch '^\(localdb\)\\' -or $builder.InitialCatalog -eq $Database){throw 'Isolated LocalDB required.'}
$builder['Initial Catalog']='master';$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$outputPath=(Resolve-Path -LiteralPath $Output).Path;$suffix=[Guid]::NewGuid().ToString('N');$restored=$Database+'_Retained_'+$suffix;$backup=Join-Path $outputPath ($restored+'.bak')
function Query([string]$Sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$Sql;$cmd.CommandTimeout=180;$reader=$cmd.ExecuteReader();$rows=[System.Collections.Generic.List[object]]::new();try{while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$rows.Add([pscustomobject]$row)}}finally{$reader.Dispose();$cmd.Dispose()};return $rows.ToArray()}
function Run([string]$Sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$Sql;$cmd.CommandTimeout=180;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
function Quote([string]$Value){return "'"+$Value.Replace("'","''")+"'"}
try{
 $connection.Open();if((Query "SELECT name FROM sys.databases WHERE name=$(Quote $restored)").Count -gt 0){throw 'Existing restore target rejected.'}
 Run "BACKUP DATABASE [$Database] TO DISK=$(Quote $backup) WITH COPY_ONLY,CHECKSUM";Run "RESTORE VERIFYONLY FROM DISK=$(Quote $backup) WITH CHECKSUM"
 $files=@(Query "RESTORE FILELISTONLY FROM DISK=$(Quote $backup)");$moves=for($i=0;$i -lt $files.Count;$i++){"MOVE $(Quote $files[$i].LogicalName) TO $(Quote (Join-Path $outputPath ($restored+'_'+$i+$(if($files[$i].Type -eq 'L'){'.ldf'}else{'.mdf'}))))"}
 Run "RESTORE DATABASE [$restored] FROM DISK=$(Quote $backup) WITH $($moves -join ',')";Run "DBCC CHECKDB ([$restored]) WITH NO_INFOMSGS"
 $tables=@('StoredFiles','StoredFileVersions','EvidenceLinks','EvidenceReviewDecisions','PrototypeImportBatches','PrototypeImportRows','AuditLogs','VisitArchiveOperations','VisitArchiveArtifacts')
 $counts=foreach($table in $tables){(Query "SELECT '$table' AS entity,(SELECT COUNT(*) FROM [$Database].dbo.[$table]) AS sourceRows,(SELECT COUNT(*) FROM [$restored].dbo.[$table]) AS restoredRows")[0]}
 if(@($counts|Where-Object {$_.sourceRows -ne $_.restoredRows}).Count -gt 0){throw 'Retained row counts differ.'}
 $diff=(Query "SELECT COUNT(*) AS differences FROM (SELECT Id,SchoolId,StoredFileId,DriveId,DriveItemId,SHA256 FROM [$Database].dbo.StoredFileVersions EXCEPT SELECT Id,SchoolId,StoredFileId,DriveId,DriveItemId,SHA256 FROM [$restored].dbo.StoredFileVersions) d")[0].differences
 if($diff -ne 0){throw 'Retained version identities or hashes differ.'}
 [ordered]@{capturedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');isolatedSource=$Database;isolatedRestore=$restored;postSyntheticWrites=$true;checksumVerified=$true;checkDbPassed=$true;counts=$counts;versionIdentityOrHashDifferences=$diff;originalDatabaseTouched=$false;driveFilesDeleted=$false;downExecuted=$false}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $outputPath 'retained-restore.json') -Encoding UTF8
}finally{$connection.Dispose()}
