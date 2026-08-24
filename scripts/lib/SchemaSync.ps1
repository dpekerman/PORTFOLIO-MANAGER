# Portfolio Manager - Generic schema-sync library
# Dot-source this file, then call Sync-TableSchema for each table.
# Diffs the LIVE schema of a source SQL Server table against a target and
# auto-generates CREATE TABLE / ALTER TABLE ADD COLUMN statements to close
# any gap. This removes the need to hand-maintain column lists (which drift).

function Get-SqlTypeDef {
    param($col)
    $t = $col.TypeName
    switch -Wildcard ($t) {
        { $_ -in @('nvarchar', 'nchar') } {
            if ($col.MaxLength -eq -1) { return "$t(MAX)" }
            return "$t($([int]($col.MaxLength / 2)))"
        }
        { $_ -in @('varchar', 'char', 'varbinary', 'binary') } {
            if ($col.MaxLength -eq -1) { return "$t(MAX)" }
            return "$t($($col.MaxLength))"
        }
        { $_ -in @('decimal', 'numeric') } { return "$t($($col.Precision),$($col.Scale))" }
        { $_ -in @('datetime2', 'datetimeoffset', 'time') } { return "$t($($col.Scale))" }
        default { return $t }
    }
}

function Get-DefaultFallback {
    param($typeName)
    switch -Wildcard ($typeName) {
        "bit" { return "0" }
        "int*" { return "0" }
        "bigint" { return "0" }
        "smallint" { return "0" }
        "tinyint" { return "0" }
        "decimal" { return "0" }
        "numeric" { return "0" }
        "float" { return "0" }
        "real" { return "0" }
        "money" { return "0" }
        "date" { return "'1900-01-01'" }
        "datetime*" { return "GETUTCDATE()" }
        "nvarchar" { return "N''" }
        "varchar" { return "''" }
        default { return $null }
    }
}

function Get-TableColumns {
    param($conn, [string]$table)
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT c.name AS ColumnName, ty.name AS TypeName, c.max_length AS MaxLength,
       c.precision AS Precision, c.scale AS Scale, c.is_nullable AS IsNullable,
       c.is_identity AS IsIdentity, dc.definition AS DefaultDef, c.column_id AS ColumnId
FROM sys.columns c
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('$table')
ORDER BY c.column_id
"@
    $reader = $cmd.ExecuteReader()
    $cols = @()
    while ($reader.Read()) {
        $cols += [pscustomobject]@{
            Name        = $reader["ColumnName"]
            TypeName    = $reader["TypeName"]
            MaxLength   = $reader["MaxLength"]
            Precision   = $reader["Precision"]
            Scale       = $reader["Scale"]
            IsNullable  = [bool]$reader["IsNullable"]
            IsIdentity  = [bool]$reader["IsIdentity"]
            DefaultDef  = if ($reader["DefaultDef"] -is [DBNull]) { $null } else { $reader["DefaultDef"] }
        }
    }
    $reader.Close()
    return $cols
}

function Get-PrimaryKeyColumns {
    param($conn, [string]$table)
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT col.name
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('$table') AND i.is_primary_key = 1
ORDER BY ic.key_ordinal
"@
    $reader = $cmd.ExecuteReader()
    $pk = @()
    while ($reader.Read()) { $pk += $reader.GetString(0) }
    $reader.Close()
    return $pk
}

function Test-TableExists {
    param($conn, [string]$table)
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = '$table'"
    return ([int]$cmd.ExecuteScalar()) -gt 0
}

# Diffs one table's schema on $sourceConn against $targetConn and applies
# whatever DDL is needed on the target so it matches the source structure.
function Sync-TableSchema {
    param($sourceConn, $targetConn, [string]$table)

    $sourceCols = Get-TableColumns $sourceConn $table

    if (-not (Test-TableExists $targetConn $table)) {
        $pkCols = Get-PrimaryKeyColumns $sourceConn $table
        $colDefs = @()
        foreach ($c in $sourceCols) {
            $typeDef = Get-SqlTypeDef $c
            $parts = @("[$($c.Name)]", $typeDef)
            if ($c.IsIdentity) { $parts += "IDENTITY(1,1)" }
            $parts += ($(if ($c.IsNullable) { "NULL" } else { "NOT NULL" }))
            if ($c.DefaultDef) { $parts += "DEFAULT $($c.DefaultDef)" }
            $colDefs += ($parts -join " ")
        }
        $pkDef = ""
        if ($pkCols.Count -gt 0) {
            $pkDef = ", CONSTRAINT [PK_$table] PRIMARY KEY (" + (($pkCols | ForEach-Object { "[$_]" }) -join ", ") + ")"
        }
        $createSql = "CREATE TABLE [$table] (" + ($colDefs -join ", ") + "$pkDef)"
        Write-Host "  CREATE TABLE [$table]" -ForegroundColor Yellow
        $c2 = $targetConn.CreateCommand(); $c2.CommandTimeout = 120; $c2.CommandText = $createSql
        $c2.ExecuteNonQuery() | Out-Null
        return
    }

    $targetCols = Get-TableColumns $targetConn $table
    $targetNames = @($targetCols | ForEach-Object { $_.Name })
    foreach ($c in $sourceCols) {
        if ($targetNames -contains $c.Name) { continue }
        $typeDef = Get-SqlTypeDef $c
        $nullDef = if ($c.IsNullable) { "NULL" } else { "NOT NULL" }
        $defDef = ""
        if ($c.DefaultDef) {
            $defDef = "DEFAULT $($c.DefaultDef)"
        } elseif (-not $c.IsNullable) {
            $fallback = Get-DefaultFallback $c.TypeName
            if ($fallback) { $defDef = "DEFAULT $fallback" }
        }
        $alterSql = ("ALTER TABLE [$table] ADD [$($c.Name)] $typeDef $nullDef $defDef").Trim() -replace '\s+', ' '
        Write-Host "  ALTER TABLE [$table] ADD [$($c.Name)]" -ForegroundColor Yellow
        $c2 = $targetConn.CreateCommand(); $c2.CommandTimeout = 120; $c2.CommandText = $alterSql
        $c2.ExecuteNonQuery() | Out-Null
    }
}

# Runs Sync-TableSchema for every table in $tables, in order.
function Sync-AllSchemas {
    param($sourceConn, $targetConn, [string[]]$tables)
    foreach ($table in $tables) {
        Sync-TableSchema $sourceConn $targetConn $table
    }
}
