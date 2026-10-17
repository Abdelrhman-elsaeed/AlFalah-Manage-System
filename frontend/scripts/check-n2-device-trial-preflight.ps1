# Runs the launcher's actual SQL-configuration preflight, never its process/SQL/network operations.
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$launcher = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'start-n2-device-trial.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Launcher syntax is invalid.' }
$statements = @($launcher.EndBlock.Statements)
$start = @($statements | Where-Object {
    $_ -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $_.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
    $_.Left.VariablePath.UserPath -eq 'sqlBuilder'
})[0].Extent.StartOffset
$end = @($statements | Where-Object {
    $_ -is [System.Management.Automation.Language.ForEachStatementAst]
})[0].Extent.StartOffset
if (!$start -or $end -le $start) { throw 'Cannot locate the actual launcher preflight.' }
$validate = [scriptblock]::Create($launcher.Extent.Text.Substring($start, $end - $start))

function Check-Preflight([string]$name, [string]$connection, [string]$database, [bool]$allowed,
    [string]$password = 'synthetic-regression-password-only', [string]$origin = 'http://127.0.0.1:5487') {
    $config = [pscustomobject]@{
        ConnectionStrings = [pscustomobject]@{ DefaultConnection = $connection }
        ALFALAH_E2E_PASSWORD = $password
    }
    $settings = [pscustomobject]@{ databaseName = $database; apiOrigin = $origin }
    try { & $validate }
    catch {
        if (!$allowed -and $_.Exception.Message -eq 'Unsafe trial configuration.') {
            Write-Output "PASS: $name rejected before SQL/process/network operations"
            return
        }
        throw
    }
    if (!$allowed) { throw "Unsafe configuration accepted: $name" }
    Write-Output "PASS: $name parsed without a SQL connection"
}

$database = 'AlFalahN2DeviceE2E_abcdef'
$valid = 'Server=(localdb)\mssqllocaldb;Database=' + $database + ';Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
Check-Preflight 'Windows PowerShell 5.1 reported ConnectionString regression' $valid $database $true
Check-Preflight 'SQL connection keyword aliases' ('Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=' + $database + ';Integrated Security=SSPI') $database $true
Check-Preflight 'operational database name' ($valid.Replace($database, 'AlFalahDb')) 'AlFalahDb' $false
Check-Preflight 'mismatched database identity' $valid 'AlFalahN2DeviceE2E_123456' $false
Check-Preflight 'remote SQL host' ($valid.Replace('(localdb)\mssqllocaldb', 'remote.invalid')) $database $false
Check-Preflight 'SQL authentication' ($valid.Replace('Trusted_Connection=True', 'Integrated Security=False;User ID=synthetic;Password=synthetic')) $database $false
Check-Preflight 'weak fixture password' $valid $database $false -password 'weak'
Check-Preflight 'operational API port' $valid $database $false -origin 'http://127.0.0.1:5264'
