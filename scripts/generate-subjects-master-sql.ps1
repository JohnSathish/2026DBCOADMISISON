# Generates scripts/seed-subjects-master.sql from docs/subjects_master_import_template.csv
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csv = Join-Path $root "docs\subjects_master_import_template.csv"
$out = Join-Path $PSScriptRoot "seed-subjects-master.sql"

$rows = Import-Csv -Path $csv -Encoding UTF8
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("-- Auto-generated from docs/subjects_master_import_template.csv")
[void]$sb.AppendLine("-- Restore: psql -f scripts/seed-subjects-master.sql  (requires pgcrypto for gen_random_uuid on older PG)")
[void]$sb.AppendLine("DELETE FROM admissions.subjects_master;")
foreach ($r in $rows) {
    $b = [string]$r.boardCode
    $s = [string]$r.streamCode
    $n = [string]$r.subjectName
    $b = $b.Replace("'", "''")
    $s = $s.Replace("'", "''")
    $n = $n.Replace("'", "''")
    $so = [int]$r.sortOrder
    [void]$sb.AppendLine(
        "INSERT INTO admissions.subjects_master (""Id"", ""BoardCode"", ""StreamCode"", ""SubjectName"", ""SortOrder"", ""IsActive"") VALUES (gen_random_uuid(), '$b', '$s', '$n', $so, true);"
    )
}
Set-Content -Path $out -Value $sb.ToString() -Encoding utf8
Write-Host "Wrote $out ($($rows.Count) rows)"
