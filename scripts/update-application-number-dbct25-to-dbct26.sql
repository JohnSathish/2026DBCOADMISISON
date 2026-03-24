-- One-off: rename application DBCT25-0002 -> DBCT26-0001 for 2026 admissions year.
-- Run against the same database as the API (e.g. erp_dev).
-- Fails if DBCT26-0001 already exists.

BEGIN;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM admissions."StudentApplicantAccounts" WHERE "UniqueId" = 'DBCT26-0001'
  ) THEN
    RAISE EXCEPTION 'Target UniqueId DBCT26-0001 already exists; aborting.';
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM admissions."StudentApplicantAccounts" WHERE "UniqueId" = 'DBCT25-0002'
  ) THEN
    RAISE NOTICE 'No row with UniqueId DBCT25-0002; nothing to rename.';
  END IF;
END $$;

UPDATE admissions."StudentApplicantAccounts"
SET "UniqueId" = 'DBCT26-0001'
WHERE "UniqueId" = 'DBCT25-0002';

UPDATE admissions."AdmissionOffers"
SET "ApplicationNumber" = 'DBCT26-0001'
WHERE "ApplicationNumber" = 'DBCT25-0002';

UPDATE admissions."MeritScores"
SET "ApplicationNumber" = 'DBCT26-0001'
WHERE "ApplicationNumber" = 'DBCT25-0002';

UPDATE admissions."OfflineFormIssuances"
SET "FormNumber" = 'DBCT26-0001'
WHERE "FormNumber" = 'DBCT25-0002';

UPDATE admissions."Applicants"
SET "ApplicationNumber" = 'DBCT26-0001'
WHERE "ApplicationNumber" = 'DBCT25-0002';

UPDATE admissions."ExamRegistrations"
SET "HallTicketNumber" = REPLACE("HallTicketNumber", 'DBCT25-0002', 'DBCT26-0001')
WHERE "HallTicketNumber" IS NOT NULL AND "HallTicketNumber" LIKE '%DBCT25-0002%';

UPDATE students."Students"
SET "AdmissionNumber" = 'DBCT26-0001'
WHERE "AdmissionNumber" = 'DBCT25-0002';

COMMIT;
