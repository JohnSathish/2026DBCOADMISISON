-- Truncate all tables in schema "admissions" (ERP applicant portal + admin catalog).
-- WARNING: Deletes ALL admissions data: applicants, accounts, exams, programs, courses,
-- fee structures, merit, offline forms, Class XII subject catalog, and admin users.
-- After this, restart the API so SeedAdminUser can recreate the default admin (if configured).
--
-- If this fails with a foreign-key error from another schema (e.g. students."Students"
-- referencing admissions."StudentApplicantAccounts"), clear those dependent rows first
-- or run the optional block at the bottom.

BEGIN;

TRUNCATE TABLE
  admissions."AdmissionOffers",
  admissions."ApplicantApplicationDrafts",
  admissions."ApplicantRefreshTokens",
  admissions."MeritScores",
  admissions."ExamRegistrations",
  admissions."OfflineFormIssuances",
  admissions."Applicants",
  admissions."FeeComponents",
  admissions."FeeStructures",
  admissions."Courses",
  admissions."EntranceExams",
  admissions.subjects_master,
  admissions."Programs",
  admissions."StudentApplicantAccounts",
  admissions."AdmissionWorkflowSettings",
  admissions."AdminUsers"
RESTART IDENTITY CASCADE;

COMMIT;

-- Optional: if TRUNCATE fails because students."Students" references StudentApplicantAccounts,
-- truncate dependent student tables first (order depends on your FKs), then re-run the block above.
