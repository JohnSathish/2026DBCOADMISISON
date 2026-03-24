# Importing Class XII `subjects_master` data

## 1. Apply database migration

After pulling changes, run (from `src/server/Api` or your host project):

```bash
dotnet ef database update --project ../Infrastructure/ERP.Infrastructure.csproj --startup-project ERP.Api.csproj
```

This creates `admissions.subjects_master`.

## 2. Prepare CSV (UTF-8)

Use `docs/subjects_master_import_template.csv` and `docs/subjects_master_IMPORT_INSTRUCTIONS.md`.

Columns: `boardCode`, `streamCode`, `subjectName`, `sortOrder`  
Codes: **MBOSE / CBSE / ISC** and **ARTS / SCIENCE / COMMERCE** (uppercase in file is recommended).

## 3. Import via Admin API (full replace)

**Endpoint:** `POST /api/admissions/admin/class-xii-subjects`  
**Auth:** Admin Bearer token  
**Effect:** Deletes all existing catalog rows and inserts the request body (full replace).

### Option A — PowerShell script (easiest)

1. Start the API (e.g. `dotnet run --launch-profile http` from `src/server/Api` → usually `http://localhost:5227`).
2. Get a JWT: `POST /api/auth/admin/login` with `{ "username": "...", "password": "..." }`. Copy `token` from the JSON response.
3. From the repo root:

```powershell
cd scripts
.\import-subjects-master-from-csv.ps1 `
  -CsvPath "..\docs\subjects_master_import_template.csv" `
  -BaseUrl "http://localhost:5227" `
  -LoginUsername "admin" `
  -LoginPassword "Admin@123"
```

Or pass `-AdminToken "eyJ..."` from a fresh login. You should see `importedRowCount` in the output. The script skips blank rows.

**401 Unauthorized:** The import requires an **Admin** JWT. If you truncated `admissions` (including `AdminUsers`), **restart the API** so the seed recreates the default admin (`admin` / `Admin@123`), then log in again — old tokens are invalid.

### Option B — Swagger

1. Open `http://localhost:5227/swagger`.
2. Authorize with your admin Bearer token (or log in via `/api/auth/admin/login` and paste the token).
3. Use `POST /api/admissions/admin/class-xii-subjects` with a JSON body built from your CSV (see below).

### Option C — Manual JSON

**Example body:**

```json
{
  "rows": [
    { "boardCode": "MBOSE", "streamCode": "ARTS", "subjectName": "English", "sortOrder": 10 },
    { "boardCode": "MBOSE", "streamCode": "ARTS", "subjectName": "Education", "sortOrder": 20 }
  ]
}
```

Convert each CSV line to one object in `rows`. Duplicate `(boardCode, streamCode, subjectName)` in the same payload returns `400`.

## 4. Applicant dropdown API

**GET** `/api/admissions/class-xii-subjects?board=MBOSE&stream=ARTS`  
**Auth:** none (`AllowAnonymous`)

**Response:**

```json
{
  "items": [
    { "id": "…", "subjectName": "English", "sortOrder": 10 }
  ]
}
```

## 5. Draft JSON (backward compatibility)

Applicant drafts support:

- **Legacy:** `academics.classXII[]` with `{ subject, marks }`
- **New:** `academics.classXiiBoardCode`, `academics.classXiiStreamCode`, `academics.classXiiSubjects[]` with optional `subjectMasterId` and `entryMode`

The API syncs `classXiiSubjects` ↔ `classXII` on load/save so PDFs and exports keep working.

When **both** `classXiiBoardCode` and `classXiiStreamCode` are set, the dashboard step **Academic Records** requires **at least five** subject rows with non-empty subject and marks.
