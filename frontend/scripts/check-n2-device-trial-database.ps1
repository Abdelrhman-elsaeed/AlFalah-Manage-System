param([Parameter(Mandatory = $true)][string]$TrialDirectory)

# Executes the launcher's actual account-restriction SQL in its guarded E2E DB,
# always rolling back. No API processes, public tunnel or push provider requests.
$ErrorActionPreference = 'Stop'
$trial = (Resolve-Path -LiteralPath $TrialDirectory).Path
$config = Get-Content (Join-Path $trial 'appsettings.json') -Raw | ConvertFrom-Json
$settings = Get-Content (Join-Path $trial 'trial-settings.json') -Raw | ConvertFrom-Json
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$config.ConnectionStrings.DefaultConnection)
if ($builder.InitialCatalog -notmatch '^AlFalahN2DeviceE2E_[a-f0-9]+$' -or
    $builder.InitialCatalog -cne $settings.databaseName -or
    $builder.DataSource -ine '(localdb)\mssqllocaldb' -or !$builder.IntegratedSecurity) {
    throw 'Only the isolated local trial database can be tested.'
}
$tokens = $null
$parseErrors = $null
$launcher = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'start-n2-device-trial.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Launcher syntax is invalid.' }
$assignments = @($launcher.FindAll({ param($node)
    $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $node.Left -is [System.Management.Automation.Language.MemberExpressionAst] -and
    $node.Left.Member.Value -eq 'CommandText'
}, $true))
if ($assignments.Count -ne 1) { throw 'Cannot identify the actual account-restriction SQL.' }
$literal = $assignments[0].Right.Find({ param($node)
    $node -is [System.Management.Automation.Language.StringConstantExpressionAst]
}, $true)
if ($null -eq $literal) { throw 'Account-restriction SQL must be a literal for this test.' }
$accountSql = $literal.Value

function Read-AccountState($connection) {
    $command = $connection.CreateCommand()
    try {
        $command.CommandText = 'SELECT Id, IsActive FROM dbo.Users ORDER BY Id'
        $reader = $command.ExecuteReader()
        try {
            $state = [System.Collections.Generic.List[string]]::new()
            while ($reader.Read()) { $state.Add($reader.GetString(0) + ':' + $reader.GetBoolean(1)) }
            return [string]::Join('|', $state)
        } finally { $reader.Dispose() }
    } finally { $command.Dispose() }
}

$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$transaction = $null
try {
    $connection.Open()
    $before = Read-AccountState $connection
    $transaction = $connection.BeginTransaction()
    $command = $connection.CreateCommand()
    try {
        $command.Transaction = $transaction
        $command.CommandText = $accountSql
        if ([int]$command.ExecuteScalar() -ne 2) { throw 'Expected exactly two active synthetic actors.' }
        $command.CommandText = "SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1 AND UserName NOT IN ('parent.test','officer.test')"
        if ([int]$command.ExecuteScalar() -ne 0) { throw 'Default accounts remained enabled.' }
        $command.CommandText = "SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1 AND IsDeleted = 0 AND UserName IN ('parent.test','officer.test')"
        if ([int]$command.ExecuteScalar() -ne 2) { throw 'Both synthetic actors must remain usable.' }
    } finally { $command.Dispose() }
    $transaction.Rollback()
    $transaction.Dispose()
    $transaction = $null
    if ((Read-AccountState $connection) -cne $before) { throw 'Account states changed after rollback.' }
    Write-Output 'PASS: actual launcher SQL restricts the real isolated database to both synthetic actors; all account states restored by rollback. No API/tunnel/provider action.'
} finally {
    if ($null -ne $transaction) { try { $transaction.Rollback() } finally { $transaction.Dispose() } }
    $connection.Dispose()
}
