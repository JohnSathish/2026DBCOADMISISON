<#
.SYNOPSIS
  Converts subjects_master CSV to JSON and POSTs to POST /api/admissions/admin/class-xii-subjects (full replace).

.PARAMETER CsvPath
  Path to UTF-8 CSV with columns: boardCode, streamCode, subjectName, sortOrder

.PARAMETER BaseUrl
  API root, e.g. http://localhost:5227

.PARAMETER AdminToken
  JWT Bearer token for an Admin user. After DB truncate or password change, old tokens are invalid — use -LoginUsername/-LoginPassword or login again.

.PARAMETER LoginUsername / LoginPassword
  If -AdminToken is omitted, the script calls POST /api/auth/admin/login to obtain a token (default seed user after API restart: admin / Admin@123).

.EXAMPLE
  .\import-subjects-master-from-csv.ps1 -CsvPath "..\docs\subjects_master_import_template.csv" -BaseUrl "http://localhost:5227" -AdminToken "eyJhbG..."

.EXAMPLE
  .\import-subjects-master-from-csv.ps1 -CsvPath "..\docs\subjects_master_import_template.csv" -BaseUrl "http://localhost:5227" -LoginUsername "admin" -LoginPassword "Admin@123"
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $CsvPath,

    [Parameter(Mandatory = $false)]
    [string] $BaseUrl = "http://localhost:5227",

    [Parameter(Mandatory = $false)]
    [string] $AdminToken = "",

    [Parameter(Mandatory = $false)]
    [string] $LoginUsername = "",

    [Parameter(Mandatory = $false)]
    [string] $LoginPassword = ""
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')

# Resolve bearer token: explicit token, or login
if ([string]::IsNullOrWhiteSpace($AdminToken)) {
    if ([string]::IsNullOrWhiteSpace($LoginUsername) -or [string]::IsNullOrWhiteSpace($LoginPassword)) {
        throw @"
Provide either:
  -AdminToken '<JWT>'   (from a fresh admin login), or
  -LoginUsername 'admin' -LoginPassword 'Admin@123'

After truncating admissions tables, restart ERP.Api so the seed recreates the admin user, then use login or a new token.
API login endpoint: POST $base/api/auth/admin/login
"@
    }
    $loginUri = "$base/api/auth/admin/login"
    $loginBody = @{ username = $LoginUsername; password = $LoginPassword } | ConvertTo-Json -Compress
    Write-Host "POST $loginUri (admin login)..."
    try {
        $loginResp = Invoke-RestMethod -Uri $loginUri -Method Post -Body $loginBody -ContentType "application/json; charset=utf-8"
    }
    catch {
        Write-Host "Login failed. Ensure ERP.Api is running and AdminUsers was re-seeded (restart API after truncate). Default user: admin / Admin@123" -ForegroundColor Yellow
        throw
    }
    $AdminToken = $loginResp.token
    if ([string]::IsNullOrWhiteSpace($AdminToken)) {
        throw "Login response did not include 'token'."
    }
}

$resolved = Resolve-Path -Path $CsvPath
$rows = Import-Csv -Path $resolved -Encoding UTF8

$payloadRows = New-Object System.Collections.Generic.List[object]
foreach ($r in $rows) {
    $b = [string](@($r.boardCode, $r.BoardCode) | Where-Object { $_ } | Select-Object -First 1)
    $s = [string](@($r.streamCode, $r.StreamCode) | Where-Object { $_ } | Select-Object -First 1)
    $n = [string](@($r.subjectName, $r.SubjectName) | Where-Object { $_ } | Select-Object -First 1)
    $so = if ($null -ne $r.sortOrder -and $r.sortOrder -ne "") { $r.sortOrder } else { $r.SortOrder }
    $b = $b.Trim()
    $s = $s.Trim()
    $n = $n.Trim()
    if ([string]::IsNullOrWhiteSpace($b) -and [string]::IsNullOrWhiteSpace($s) -and [string]::IsNullOrWhiteSpace($n)) {
        continue
    }
    if ([string]::IsNullOrWhiteSpace($b) -or [string]::IsNullOrWhiteSpace($s) -or [string]::IsNullOrWhiteSpace($n)) {
        Write-Warning "Skipping incomplete row: board=$b stream=$s subject=$n"
        continue
    }
    $sortInt = 0
    if (-not [int]::TryParse([string]$so, [ref]$sortInt)) {
        throw "Invalid sortOrder for row: $b / $s / $n (value: $so)"
    }
    $payloadRows.Add([ordered]@{
        boardCode   = $b
        streamCode  = $s
        subjectName = $n
        sortOrder   = $sortInt
    })
}

if ($payloadRows.Count -eq 0) {
    throw "No data rows found in CSV."
}

$bodyObj = @{ rows = $payloadRows.ToArray() }
$json = $bodyObj | ConvertTo-Json -Depth 5 -Compress
$uri = "$base/api/admissions/admin/class-xii-subjects"
Write-Host "POST $uri ($($payloadRows.Count) rows)..."

$headers = @{
    Authorization = "Bearer $AdminToken"
    "Content-Type" = "application/json"
}

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $json -ContentType "application/json; charset=utf-8"
    Write-Host "OK: importedRowCount = $($response.importedRowCount)" -ForegroundColor Green
    $response
}
catch {
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -eq 401) {
        Write-Host "401 Unauthorized: JWT missing, expired, or not an Admin token. Log in again (POST $base/api/auth/admin/login) or use -LoginUsername/-LoginPassword." -ForegroundColor Red
    }
    $err = $_.ErrorDetails.Message
    if ($err) { Write-Host "Details: $err" -ForegroundColor Red }
    throw
}
