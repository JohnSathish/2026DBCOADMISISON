# How to fill `subjects_master_import_template.csv`

Use **Excel**, **Google Sheets**, or any editor. Save as **CSV UTF-8** (Excel: *Save As* → *CSV UTF-8 (Comma delimited)* on Windows).

## Columns

| Column | Allowed values | Required |
|--------|----------------|----------|
| `boardCode` | `MBOSE`, `CBSE`, `ISC` only | Yes |
| `streamCode` | `ARTS`, `SCIENCE`, `COMMERCE` | Yes |
| `subjectName` | Exact label shown to students (e.g. `Political Science`) | Yes |
| `sortOrder` | Integer; **lower = higher in list** (use 10, 20, 30 … to allow inserts) | Yes |

Do **not** put `OTHER` in this file — “Other” board uses free-text subjects in the form, not the master table.

## Rules

1. **One row per subject** for each **board + stream** where that subject should appear in the dropdown.
2. If the same subject exists for multiple streams (e.g. English), add **one row per stream** with the same `subjectName` if appropriate.
3. Remove the example rows and replace with your full lists.
4. Do not leave blank cells in the four columns.

## After you return the file

We will validate codes, remove duplicates, and import into `admissions.subjects_master` (or provide a seed script).

---

## Production: fix “No subjects are configured for this board and stream yet”

That message means `admissions.subjects_master` has **no rows** for the student’s **board** (MBOSE / CBSE / ISC) and **stream** (ARTS / SCIENCE / COMMERCE). The live database must be loaded once (or again after a reset).

### Option A — SQL on the server (fast)

1. Ensure `docs/subjects_master_import_template.csv` is complete (or edit and save as UTF-8 CSV).
2. Regenerate SQL (from repo root):  
   `powershell -File scripts/generate-subjects-master-sql.ps1`
3. Copy `scripts/seed-subjects-master.sql` to the server and run against your Postgres DB (replace connection):

   `psql "Host=...;Database=...;Username=...;Password=..." -f seed-subjects-master.sql`

   Or: `psql -U ... -d ... -h ... -f seed-subjects-master.sql`

4. Reload the applicant form and pick board + stream again — subjects should load.

### Option B — Admin API (full replace)

From a machine that can reach the API:

```powershell
.\scripts\import-subjects-master-from-csv.ps1 `
  -CsvPath "docs\subjects_master_import_template.csv" `
  -BaseUrl "https://admissionsdbctura.com" `
  -LoginUsername "admin" `
  -LoginPassword "YOUR_ADMIN_PASSWORD"
```

Use a real admin user. The script calls `POST /api/admissions/admin/class-xii-subjects` and **replaces** the entire catalog.

### Verify

Open (anonymous):  
`GET /api/admissions/class-xii-subjects?board=MBOSE&stream=SCIENCE`  
Expect a JSON `items` array with subject names — not empty.
