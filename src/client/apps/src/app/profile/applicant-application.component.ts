import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  DestroyRef,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  FormArray,
  FormBuilder,
  FormGroup,
  FormControl,
  ReactiveFormsModule,
  Validators,
  AbstractControl,
  ValidatorFn,
} from '@angular/forms';
import {
  AddressInformation,
  ApplicantApplicationDraft,
  BoardExaminationDetail,
  ClassXiiSubjectOptionDto,
  ClassXiiSubjectRow,
  AcademicInformation,
  ContactInformation,
  CuetDetail,
  CoursePreferences,
  draftForSubmitRequest,
  FileAttachment,
  ParentOrGuardian,
  PersonalInformation,
  SubjectMark,
  UploadSection,
} from '@client/shared/data';
import { ApplicantApplicationApiService } from '@client/shared/data';
import {
  ApplicantApplicationStore,
  ApplicantApplicationSubmitResult,
} from './applicant-application.store';
import { ApplicantPortalStore } from '../dashboard/applicant-portal.store';
import { ToastService } from '../shared/toast.service';
import {
  ApplicantApplicationNavigationService,
  ApplicantApplicationStep,
} from './applicant-application-navigation.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PaymentApiService, PaymentStatusResponse } from '@client/shared/data';
import { AuthService } from '../auth/auth.service';
import {
  buildRazorpayStandardOptions,
  isMobileBrowser,
} from '../shared/razorpay-checkout.util';

type ShiftCode = 'ShiftI' | 'ShiftII';

interface ShiftOption {
  value: ShiftCode;
  label: string;
}

interface CourseOfferingConfig {
  majors: Record<string, string[]>;
}

const SHIFT_OPTIONS: readonly ShiftOption[] = [
  {
    value: 'ShiftI',
    label: 'Shift - I (Timing : 6:30 am to 9:30 am)',
  },
  {
    value: 'ShiftII',
    label: 'Shift - II (Timing : 9.45 am - 3.30 pm)',
  },
];

const COURSE_OFFERINGS: Record<ShiftCode, CourseOfferingConfig> = {
  ShiftI: {
    majors: {
      ECONOMICS: ['GEOGRAPHY', 'HISTORY', 'POLITICAL SCIENCE', 'SOCIOLOGY'],
      EDUCATION: ['GARO', 'HISTORY', 'PHILOSOPHY'],
      ENGLISH: ['EDUCATION', 'GEOGRAPHY', 'PHILOSOPHY', 'POLITICAL SCIENCE'],
      GARO: ['EDUCATION', 'GEOGRAPHY', 'PHILOSOPHY', 'SOCIOLOGY'],
      GEOGRAPHY: ['ECONOMICS', 'GARO'],
      HISTORY: ['ECONOMICS', 'PHILOSOPHY', 'POLITICAL SCIENCE', 'SOCIOLOGY'],
      PHILOSOPHY: ['EDUCATION', 'GARO', 'GEOGRAPHY'],
      'POLITICAL SCIENCE': ['ECONOMICS', 'EDUCATION', 'HISTORY', 'SOCIOLOGY'],
      SOCIOLOGY: ['ECONOMICS', 'GARO', 'HISTORY', 'POLITICAL SCIENCE'],
    },
  },
  ShiftII: {
    majors: {
      ECONOMICS: ['GEOGRAPHY', 'HISTORY', 'POLITICAL SCIENCE', 'SOCIOLOGY'],
      EDUCATION: ['GARO', 'HISTORY', 'PHILOSOPHY'],
      ENGLISH: ['EDUCATION', 'GEOGRAPHY', 'PHILOSOPHY', 'POL. SCIENCE'],
      GARO: ['EDUCATION', 'GEOGRAPHY', 'PHILOSOPHY', 'SOCIOLOGY'],
      GEOGRAPHY: ['ECONOMICS', 'GARO'],
      HISTORY: ['ECONOMICS', 'PHILOSOPHY', 'POL. SCIENCE', 'SOCIOLOGY'],
      PHILOSOPHY: ['EDUCATION', 'GARO', 'GEOGRAPHY'],
      'POL. SCIENCE': ['ECONOMICS', 'EDUCATION', 'HISTORY', 'SOCIOLOGY'],
      SOCIOLOGY: ['ECONOMICS', 'GARO', 'HISTORY', 'POL. SCIENCE'],
      BOTANY: ['ZOOLOGY', 'CHEMISTRY'],
      CHEMISTRY: ['MATHEMATICS', 'PHYSICS'],
      MATHEMATICS: ['PHYSICS', 'CHEMISTRY'],
      ZOOLOGY: ['BOTANY', 'CHEMISTRY'],
      PHYSICS: ['CHEMISTRY', 'MATHEMATICS'],
      'ACCOUNTING FOR BUSINESS': ['ECONOMICS'],
    },
  },
};

const LEGACY_SHIFT_ALIASES: Record<string, ShiftCode> = {
  Morning: 'ShiftI',
  Day: 'ShiftII',
  'SHIFT - I (TIMING : 7.30 AM - 1.15 PM)': 'ShiftI',
  'SHIFT - II (TIMING : 9.45 AM - 3.30 PM)': 'ShiftII',
  'Shift - I': 'ShiftI',
  'Shift - II': 'ShiftII',
};

/** Legacy stored values that referred to discontinued Shift III — must not map to a selectable shift. */
const LEGACY_SHIFT_III_INPUTS = new Set<string>([
  'ShiftIII',
  'Evening',
  'SHIFT - III (TIMING : 1.30 PM - 6.15 PM)',
  'Shift - III',
]);

function isLegacyShiftIIIRaw(raw: string | undefined | null): boolean {
  const t = (raw ?? '').trim();
  if (!t) {
    return false;
  }
  if (t === 'ShiftIII') {
    return true;
  }
  if (LEGACY_SHIFT_III_INPUTS.has(t)) {
    return true;
  }
  return false;
}

type OptionGroup = readonly { value: string; label: string; }[];

const SHIFT_SUPPLEMENTS: Record<ShiftCode, {
  mdc: OptionGroup;
  aec: OptionGroup;
  sec: OptionGroup;
}> = {
  ShiftI: {
    mdc: [
      { value: 'MDC 111', label: 'MDC 111 — Culture and Society' },
      { value: 'MDC 116', label: 'MDC 116 — Introduction to National Cadet Corps' },
      { value: 'MDC 118', label: 'MDC 118 — Mathematics in Daily Life' },
      { value: 'MDC 119', label: 'MDC 119 — Philosophy of Culture' },
    ],
    aec: [
      { value: 'AEC 120', label: 'AEC 120 — Alternative English' },
      { value: 'AEC 123', label: 'AEC 123 — MIL-I: Garo' },
    ],
    sec: [
      { value: 'SEC 131', label: 'SEC 131 — Motivation' },
      { value: 'SEC 132', label: 'SEC 132 — Personality Development' },
      { value: 'SEC 133', label: 'SEC 133 — Public Speaking' },
    ],
  },
  ShiftII: {
    mdc: [
      { value: 'MDC 111', label: 'MDC 111 — Culture and Society' },
      { value: 'MDC 118', label: 'MDC 118 — Mathematics in Daily Life' },
      { value: 'MDC 119', label: 'MDC 119 — Philosophy of Culture' },
      { value: 'MDC 116', label: 'MDC 116 — Introduction to National Cadet Corps' },
      { value: 'MDC 112', label: 'MDC 112 — Fundamentals of Computer Systems' },
      { value: 'MDC 115', label: 'MDC 115 — Introduction to Life Sciences' },
      { value: 'MDC 110', label: 'MDC 110 — Commercial Arithmetic & Elementary Statistics' },
    ],
    aec: [
      { value: 'AEC 120', label: 'AEC 120 — Alternative English' },
      { value: 'AEC 123', label: 'AEC 123 — MIL-I: Garo' },
    ],
    sec: [
      { value: 'SEC 131', label: 'SEC 131 — Motivation' },
      { value: 'SEC 132', label: 'SEC 132 — Personality Development' },
      { value: 'SEC 133', label: 'SEC 133 — Public Speaking' },
    ],
  },
};

/** Context for MDC 111 / 115 / 119 eligibility (Class XII + major). */
interface MdcEligibilityContext {
  classXiiSubjectNames: string[];
  classXiiStreamCode: string;
  majorSubject: string;
}

type MdcOption = Readonly<{
  value: string;
  label: string;
  disabled?: boolean;
  disabledReason?: string;
}>;

function normalizeAcademicSubjectToken(s: string): string {
  return s.toLowerCase().replace(/\s+/g, ' ').trim();
}

function subjectMatchesKeyword(normalizedSubject: string, keyword: string): boolean {
  const k = normalizeAcademicSubjectToken(keyword);
  if (!k || !normalizedSubject) {
    return false;
  }
  return normalizedSubject === k || normalizedSubject.includes(k);
}

function hasScienceBackground(streamCode: string, normalizedSubjects: string[]): boolean {
  const sc = (streamCode ?? '').trim().toUpperCase();
  if (sc === 'SCIENCE') {
    return true;
  }
  const scienceKeywords = [
    'physics',
    'chemistry',
    'biology',
    'botany',
    'zoology',
    'biotechnology',
    'microbiology',
    'environmental science',
    'life science',
  ];
  for (const n of normalizedSubjects) {
    for (const k of scienceKeywords) {
      if (subjectMatchesKeyword(n, k)) {
        return true;
      }
    }
  }
  return false;
}

/** Returns a user-facing reason when the chosen MDC is not allowed, or null if allowed. */
function getMdcIneligibilityReason(mdcValue: string, ctx: MdcEligibilityContext): string | null {
  const v = (mdcValue ?? '').trim();
  if (!v) {
    return null;
  }
  const subjects = ctx.classXiiSubjectNames
    .map((s) => normalizeAcademicSubjectToken(s))
    .filter(Boolean);
  const stream = (ctx.classXiiStreamCode ?? '').trim();
  const major = (ctx.majorSubject ?? '').trim().toUpperCase();

  if (v.startsWith('MDC 111')) {
    const blocked = subjects.some(
      (s) => subjectMatchesKeyword(s, 'geography') || subjectMatchesKeyword(s, 'sociology')
    );
    if (blocked) {
      return 'This MDC is not available because you studied Geography and/or Sociology in Class XII.';
    }
  }
  if (v.startsWith('MDC 119')) {
    if (subjects.some((s) => subjectMatchesKeyword(s, 'philosophy'))) {
      return 'This MDC is not available because you studied Philosophy in Class XII.';
    }
    if (major === 'PHILOSOPHY') {
      return 'This MDC is not available when Philosophy is your major subject.';
    }
  }
  if (v.startsWith('MDC 115')) {
    if (hasScienceBackground(stream, subjects)) {
      return 'This MDC is not available for Science stream students or those with Class XII Science subjects (e.g. Physics, Chemistry, Biology).';
    }
  }
  return null;
}

const DEFAULT_VAC = { value: 'VAC 140', label: 'VAC 140 — Environment Studies' };

interface Step {
  title: string;
  description: string;
  formGroup: FormGroup;
}

@Component({
  selector: 'app-applicant-application',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  providers: [ApplicantApplicationStore],
  templateUrl: './applicant-application.component.html',
  styleUrls: [
    './applicant-application.component.scss',
    './applicant-application-part2.scss',
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class ApplicantApplicationComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly applicationStore = inject(ApplicantApplicationStore);
  private readonly portalStore = inject(ApplicantPortalStore);
  private readonly toast = inject(ToastService);
  private readonly navigation = inject(ApplicantApplicationNavigationService);
  private readonly paymentApi = inject(PaymentApiService);
  private readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly applicantApi = inject(ApplicantApplicationApiService);
  private readonly destroyRef = inject(DestroyRef);

  /** Skips clearing Class XII rows when board/stream change comes from draft patch. */
  private draftPatchInProgress = false;

  readonly subjectsList = signal<ClassXiiSubjectOptionDto[]>([]);
  readonly subjectsLoading = signal(false);
  readonly subjectsLoadError = signal<string | null>(null);

  readonly loading = this.applicationStore.loading;
  readonly saving = this.applicationStore.saving;
  readonly error = this.applicationStore.error;
  readonly updatedOnUtc = this.applicationStore.updatedOnUtc;

  /** Avoid showing default/invalid dates (e.g. year 0001) in the header. */
  lastSavedDate(): Date | null {
    const raw = this.updatedOnUtc();
    if (!raw?.trim()) {
      return null;
    }
    const d = new Date(raw);
    if (Number.isNaN(d.getTime()) || d.getUTCFullYear() < 1900) {
      return null;
    }
    return d;
  }

  /** Shown when not in “just saved” flash and we have a real timestamp. */
  lastSavedDisplay(): Date | null {
    if (this.showJustSaved()) {
      return null;
    }
    return this.lastSavedDate();
  }

  submittedPdfUrl: string | null = null;
  submittedPdfFileName: string | null = null;
  coursesLocked = false;
  submissionSuccessful = false;
  paymentStatus = signal<PaymentStatusResponse | null>(null);
  isPaymentProcessing = signal(false);
  private isSavingDraft = false;

  readonly currentStepIndex = this.navigation.currentIndex;

  readonly personalForm = this.fb.group({
    nameAsPerAdmitCard: ['', Validators.required],
    dateOfBirth: ['', Validators.required],
    gender: ['', Validators.required],
    maritalStatus: ['', Validators.required],
    bloodGroup: ['', Validators.required],
    category: ['', Validators.required],
    raceOrTribe: ['', Validators.required],
    religion: ['', Validators.required],
    denomination: [''],
    isDifferentlyAbled: [false],
    isEconomicallyWeaker: [false],
  });

  private aadhaarValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    if (!/^\d+$/.test(value)) {
      return { aadhaarInvalid: { message: 'Aadhaar number must contain only digits.' } };
    }
    if (value.length < 12) {
      return { aadhaarInvalid: { message: 'Aadhaar number is not valid or less than 12 digits.' } };
    }
    // maxlength="12" prevents > 12, but validator check is still good
    if (value.length > 12) {
      return { aadhaarInvalid: { message: 'Aadhaar number must be exactly 12 digits.' } };
    }
    return null; // Valid
  };

  readonly addressForm = this.fb.group({
    addressInTura: ['', Validators.required],
    homeAddress: ['', Validators.required],
    sameAsTura: [false],
    aadhaarNumber: ['', [Validators.required, this.aadhaarValidator]],
    state: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
  });

  private nameValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    // Allow letters, spaces, hyphens, apostrophes (for names like "O'Brien" or "Mary-Jane")
    if (!/^[a-zA-Z\s\-']+$/.test(value)) {
      return { nameInvalid: { message: 'Name must contain only letters.' } };
    }
    return null; // Valid
  };

  private ageValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    if (!/^\d+$/.test(value)) {
      return { ageInvalid: { message: 'Age must contain only numbers.' } };
    }
    if (value.length > 2) {
      return { ageInvalid: { message: 'Age must be 2 digits or less.' } };
    }
    const ageNum = parseInt(value, 10);
    if (ageNum < 1 || ageNum > 99) {
      return { ageInvalid: { message: 'Age must be between 1 and 99.' } };
    }
    return null; // Valid
  };

  private occupationValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    // Allow letters, spaces, hyphens, apostrophes (for occupations like "IT Professional" or "Self-employed")
    if (!/^[a-zA-Z\s\-']+$/.test(value)) {
      return { occupationInvalid: { message: 'Occupation must contain only letters.' } };
    }
    return null; // Valid
  };

  private contactNumberValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    if (!/^\d+$/.test(value)) {
      return { contactInvalid: { message: 'Contact number must contain only digits.' } };
    }
    if (value.length !== 10) {
      return { contactInvalid: { message: 'Contact number must be exactly 10 digits.' } };
    }
    return null; // Valid
  };

  private numericValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    if (!/^\d+$/.test(value)) {
      return { numericInvalid: { message: 'This field must contain only numbers.' } };
    }
    return null; // Valid
  };

  private percentageValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    // Allow numbers with optional % symbol and decimal point (e.g., "85.5", "85.5%", "85%")
    if (!/^\d{1,3}(\.\d{1,2})?%?$/.test(value)) {
      return { percentageInvalid: { message: 'Percentage must be numeric (e.g., 85.5 or 85.5%).' } };
    }
    return null; // Valid
  };

  private alphanumericValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    // Allow letters and numbers
    if (!/^[a-zA-Z0-9]+$/.test(value)) {
      return { alphanumericInvalid: { message: 'This field must contain only letters and numbers.' } };
    }
    return null; // Valid
  };

  private subjectValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    // Allow letters, spaces, hyphens (for subject names like "English Literature" or "Physical Education")
    if (!/^[a-zA-Z\s\-]+$/.test(value)) {
      return { subjectInvalid: { message: 'Subject name must contain only letters.' } };
    }
    return null; // Valid
  };

  private marksValidator = (control: AbstractControl): { [key: string]: any } | null => {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    const value = control.value.toString().trim();
    if (value.length === 0) {
      return null; // Let required validator handle empty values
    }
    if (!/^\d+$/.test(value)) {
      return { marksInvalid: { message: 'Marks must contain only numbers.' } };
    }
    if (value.length > 2) {
      return { marksInvalid: { message: 'Marks must be 2 digits or less.' } };
    }
    const marksNum = parseInt(value, 10);
    if (marksNum < 0 || marksNum > 99) {
      return { marksInvalid: { message: 'Marks must be between 0 and 99.' } };
    }
    return null; // Valid
  };

  /** Rejects discontinued Shift III and any non–Shift I/II value. */
  private shiftAllowedValidator = (control: AbstractControl): { [key: string]: unknown } | null => {
    const raw = (control.value ?? '').toString().trim();
    if (!raw) {
      return null;
    }
    if (isLegacyShiftIIIRaw(raw)) {
      return { shiftDiscontinued: true };
    }
    if (raw !== 'ShiftI' && raw !== 'ShiftII') {
      return { shiftInvalid: true };
    }
    return null;
  };

  /** Class XII marks: 0–100 (required handled separately). */
  private classXiiMarksValidator = (control: AbstractControl): { [key: string]: unknown } | null => {
    const raw = control.value;
    if (raw === null || raw === undefined || raw === '') {
      return null;
    }
    const s = String(raw).trim();
    if (s === '') {
      return null;
    }
    const n = Number(s);
    if (Number.isNaN(n)) {
      return { classXiiMarksInvalid: { message: 'Marks must be a valid number.' } };
    }
    if (n < 0 || n > 100) {
      return { classXiiMarksInvalid: { message: 'Marks must be between 0 and 100.' } };
    }
    return null;
  };

  private readonly classXiiRowCrossValidator: ValidatorFn = (group: AbstractControl) => {
    const g = group as FormGroup;
    const board = this.academicsForm?.get('classXiiBoardCode')?.value;
    if (board === 'OTHER') {
      return null;
    }
    const mode = g.get('entryMode')?.value as 'master' | 'manual' | undefined;
    const subj = g.get('subject')?.value?.toString().trim() ?? '';
    const marks = g.get('marks')?.value;
    const id = g.get('subjectMasterId')?.value?.toString().trim() ?? '';
    const marksStr = marks === null || marks === undefined ? '' : String(marks).trim();
    const marksInvalid = !!g.get('marks')?.errors?.['classXiiMarksInvalid'];

    if (mode === 'manual') {
      const hasSubj = !!subj;
      const hasMarks = !!marksStr;
      if (!hasSubj && !hasMarks) {
        return null;
      }
      if (hasMarks && !hasSubj) {
        return { manualSubjectRequired: { message: 'Enter subject name (not listed).' } };
      }
      if (hasSubj && (!marksStr || marksInvalid)) {
        return { manualMarksRequired: { message: 'Enter marks between 0 and 100.' } };
      }
      return null;
    }

    const rowHasData = !!(subj || marksStr || id);
    if (!rowHasData) {
      return null;
    }
    if (!id) {
      return { masterPickRequired: { message: 'Choose a subject from the list.' } };
    }
    return null;
  };

  /** At least five complete core rows (indices 0–4); additional rows do not count toward the minimum. */
  /**
   * Required + non-empty base64 payload. Plain `Validators.required` treats any object as set, so
   * `{ fileName, data: '' }` (e.g. stale draft) incorrectly passed validation while the file input stayed empty.
   */
  private readonly attachmentUploadValidator: ValidatorFn = (control: AbstractControl) => {
    const v = control.value as FileAttachment | null | undefined;
    if (v === null || v === undefined) {
      return { required: true };
    }
    const data = (v.data ?? '').trim();
    if (!data) {
      return {
        attachmentEmpty: {
          message: 'This document has no file data. Please choose the file again.',
        },
      };
    }
    return null;
  };

  private readonly classXiiSubjectsArrayMinValidator: ValidatorFn = (control: AbstractControl) => {
    const fa = control as FormArray<FormGroup>;
    const board = (this.academicsForm?.get('classXiiBoardCode')?.value ?? '').toString().trim();
    if (!board) {
      return null;
    }
    let complete = 0;
    const n = Math.min(5, fa.length);
    for (let i = 0; i < n; i++) {
      const g = fa.at(i) as FormGroup;
      const subj = g.get('subject')?.value?.toString().trim() ?? '';
      const marksRaw = g.get('marks')?.value;
      const marks =
        marksRaw === null || marksRaw === undefined ? '' : String(marksRaw).trim();
      const id = g.get('subjectMasterId')?.value?.toString().trim() ?? '';
      const mode = g.get('entryMode')?.value as 'master' | 'manual' | undefined;
      if (!marks || g.get('marks')?.errors?.['classXiiMarksInvalid']) {
        continue;
      }
      if (board === 'OTHER') {
        if (subj) {
          complete++;
        }
      } else if (mode === 'master' && id) {
        complete++;
      } else if (mode === 'manual' && subj) {
        complete++;
      }
    }
    if (complete < 5) {
      return {
        classXiiMinSubjects: { message: 'Enter at least five subjects with marks (0–100).' },
      };
    }
    return null;
  };

  private readonly mdcEligibilityValidator: ValidatorFn = (control: AbstractControl) => {
    const mdc = (control.value as string)?.trim() ?? '';
    if (!mdc) {
      return null;
    }
    const ctx = this.buildMdcEligibilityContext();
    const reason = getMdcIneligibilityReason(mdc, ctx);
    return reason ? { mdcIneligible: { message: reason } } : null;
  };

  readonly contactsForm = this.fb.group({
    father: this.createParentGroup(),
    mother: this.createParentGroup(),
    localGuardian: this.createParentGroup(),
    householdAreaType: ['', Validators.required],
  });

  readonly academicsForm = this.fb.group({
    classXiiBoardCode: ['', Validators.required],
    classXiiStreamCode: [''],
    classXiiSubjects: this.fb.array(this.createDefaultClassXiiRows(), [this.classXiiSubjectsArrayMinValidator]),
    boardExamination: this.fb.group({
      rollNumber: ['', [Validators.required, this.numericValidator]],
      year: ['', Validators.required],
      totalMarks: ['', [Validators.required, this.numericValidator]],
      percentage: ['', [Validators.required, this.percentageValidator]],
      division: ['', Validators.required],
      registrationType: ['', Validators.required],
    }),
    cuet: this.fb.group({
      applied: ['Not Applied', Validators.required],
      marks: [{ value: '', disabled: true }, this.numericValidator],
      rollNumber: [{ value: '', disabled: true }, this.alphanumericValidator],
    }),
    lastInstitutionAttended: ['', Validators.required],
  });

  readonly coursesForm = this.fb.group({
     shift: ['', [Validators.required, this.shiftAllowedValidator]],
     majorSubject: ['', Validators.required],
     minorSubject: ['', Validators.required],
     multidisciplinaryChoice: ['', [Validators.required, this.mdcEligibilityValidator]],
     abilityEnhancementChoice: ['', Validators.required],
     skillEnhancementChoice: ['', Validators.required],
     valueAddedChoice: [''],
   });

  readonly uploadsForm = this.fb.group({
    stdXMarksheet: this.fb.control<FileAttachment | null>(null, this.attachmentUploadValidator),
    stdXIIMarksheet: this.fb.control<FileAttachment | null>(null, this.attachmentUploadValidator),
    cuetMarksheet: this.fb.control<FileAttachment | null>({ value: null, disabled: true }),
    differentlyAbledProof: this.fb.control<FileAttachment | null>({ value: null, disabled: true }),
    economicallyWeakerProof: this.fb.control<FileAttachment | null>({ value: null, disabled: true }),
  });

  readonly declarationForm = this.fb.group({
    declarationAccepted: [false, Validators.requiredTrue],
  });

  readonly steps: Step[] = [
    {
      title: 'Personal Information',
      description: 'Applicant basic details',
      formGroup: this.personalForm,
    },
    {
      title: 'Addresses & Identity',
      description: 'Contact information and identification',
      formGroup: this.addressForm,
    },
    {
      title: 'Family & Guardian',
      description: 'Parent and guardian contact information',
      formGroup: this.contactsForm,
    },
    {
      title: 'Academic Records',
      description: 'Class XI/XII subjects and examination details',
      formGroup: this.academicsForm,
    },
    {
      title: 'Course Preferences',
      description: 'Select first semester course combinations',
      formGroup: this.coursesForm,
    },
    {
      title: 'Uploads & Declaration',
      description: 'Upload marksheets and confirm declaration',
      formGroup: this.uploadsForm,
    },
  ];

  readonly declarationStepIndex = this.steps.length;

  readonly totalSteps = computed(() => this.steps.length + 1);

  readonly currentStepTitle = computed(() => {
    const i = this.currentStepIndex();
    if (i >= this.steps.length) return 'Declaration';
    return this.steps[i]?.title ?? '';
  });

  readonly progressPercent = computed(() => {
    const total = this.totalSteps();
    const current = this.currentStepIndex() + 1;
    return total > 0 ? Math.round((current / total) * 100) : 0;
  });

  readonly showJustSaved = signal(false);

  readonly hasLocalGuardian = signal(false);

  /** UX copy for Razorpay: UPI QR on desktop vs UPI intent-first on mobile. */
  paymentCheckoutHint(): string {
    return isMobileBrowser()
      ? 'On mobile, checkout opens with UPI first—choose Google Pay, PhonePe, Paytm, or BHIM to pay in your app. Use the same window for Card or Netbanking if you prefer.'
      : 'On desktop, select UPI and scan the QR with any UPI app, or use Card / Netbanking / Wallet in the same window.';
  }

  setHasLocalGuardian(checked: boolean): void {
    this.hasLocalGuardian.set(checked);
    const guardian = this.contactsForm.get('localGuardian') as FormGroup;
    if (checked) {
      guardian.enable({ emitEvent: false });
    } else {
      guardian.reset({ name: '', age: '', occupation: '', contactNumber1: '' }, { emitEvent: false });
      guardian.disable({ emitEvent: false });
    }
  }

  readonly categoryOptions = ['General', 'ST', 'SC', 'OBC', 'Other'];
  readonly genderOptions = ['Female', 'Male', 'Other', 'Prefer not to say'];
  readonly shiftOptions = SHIFT_OPTIONS;
  readonly stateOptions = [
    'Andhra Pradesh',
    'Arunachal Pradesh',
    'Assam',
    'Bihar',
    'Chhattisgarh',
    'Goa',
    'Gujarat',
    'Haryana',
    'Himachal Pradesh',
    'Jharkhand',
    'Karnataka',
    'Kerala',
    'Madhya Pradesh',
    'Maharashtra',
    'Manipur',
    'Meghalaya',
    'Mizoram',
    'Nagaland',
    'Odisha',
    'Punjab',
    'Rajasthan',
    'Sikkim',
    'Tamil Nadu',
    'Telangana',
    'Tripura',
    'Uttar Pradesh',
    'Uttarakhand',
    'West Bengal',
  ];
  readonly bloodGroupOptions = ['Not Checked', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
  readonly religionOptions = [
    'Hindu',
    'Muslim',
    'Christian',
    'Sikh',
    'Buddhist',
    'Jain',
    'Other',
  ] as const;
  readonly denominationOptions = ['Catholic', 'Baptist', 'Other'] as const;
  readonly availableMajors = signal<string[]>([]);
  readonly availableMinors = signal<string[]>([]);
  readonly currentShiftOption = signal<ShiftOption | null>(null);
  readonly availableMdc = signal<MdcOption[]>([]);
  readonly availableAec = signal<OptionGroup>([]);
  readonly availableSec = signal<OptionGroup>([]);
  readonly defaultVac = DEFAULT_VAC;

  /** True when user has selected a shift (enables Step 2). */
  get shiftSelected(): boolean {
    return !!(this.coursesForm.get('shift')?.value ?? '').toString().trim();
  }

  /** True when user has selected both major and minor (enables Step 3). */
  get majorMinorSelected(): boolean {
    const major = (this.coursesForm.get('majorSubject')?.value ?? '').toString().trim();
    const minor = (this.coursesForm.get('minorSubject')?.value ?? '').toString().trim();
    return !!major && !!minor;
  }

  /** One-line summary of course selection for preview. */
  get courseSelectionSummary(): string {
    const parts: string[] = [];
    const shiftOpt = this.currentShiftOption();
    if (shiftOpt) {
      const short = shiftOpt.label.split('(')[0]?.trim() ?? shiftOpt.label;
      parts.push(`Shift: ${short}`);
    }
    const major = (this.coursesForm.get('majorSubject')?.value ?? '').toString().trim();
    if (major) parts.push(`Major: ${major}`);
    const minor = (this.coursesForm.get('minorSubject')?.value ?? '').toString().trim();
    if (minor) parts.push(`Minor: ${minor}`);
    return parts.length ? parts.join(' | ') : '';
  }

  constructor() {
    this.setupPersonalInformationEffects();
    this.setupCoursePreferenceEffects();
    this.setupAddressSyncEffect();
    this.attachDraftEffects();
    this.setupFormSubmissionPrevention();
    this.setupConditionalUploads();
    this.setupReligionDenominationListener();
    (this.contactsForm.get('localGuardian') as FormGroup)?.disable({ emitEvent: false });
    this.syncDenominationWithReligion(false);
  }

  private setupFormSubmissionPrevention(): void {
    // Prevent any form submission globally for this component
    if (typeof document !== 'undefined') {
      // Prevent form submission - CRITICAL: This must be in capture phase to catch early
      const preventFormSubmission = (e: Event): void => {
        const target = e.target as HTMLElement;
        const form = target?.closest('form') || target?.closest('.application__form');
        if (form) {
          e.preventDefault();
          e.stopPropagation();
          e.stopImmediatePropagation();
          console.log('Form submission prevented');
        }
      };
      
      // Add listener in capture phase (before it bubbles) - use once: false to keep it active
      document.addEventListener('submit', preventFormSubmission, { capture: true, passive: false });
      
      // Prevent page reload on beforeunload if form has unsaved changes
      window.addEventListener('beforeunload', (e) => {
        // Only warn if form has been modified
        const hasChanges = this.personalForm.dirty || 
                           this.addressForm.dirty || 
                           this.contactsForm.dirty ||
                           this.academicsForm.dirty ||
                           this.coursesForm.dirty;
        if (hasChanges) {
          // Modern browsers ignore custom messages, but we can still prevent default
          e.preventDefault();
          // Save draft before leaving
          void this.saveDraft();
        }
      });
      
      // Prevent Enter key from submitting form
      document.addEventListener('keydown', (e) => {
        const target = e.target as HTMLElement;
        if (target && target.closest('.application__form')) {
          if (e.key === 'Enter') {
            const input = target as HTMLInputElement;
            // Only prevent if it's a file input, submit button, or button element
            if (input.type === 'file' || input.type === 'submit' || target.tagName === 'BUTTON') {
              e.preventDefault();
              e.stopPropagation();
            }
            // For text inputs, allow Enter but don't submit form
            if (input.type === 'text' || input.tagName === 'INPUT') {
              // Don't prevent - let it work normally, but form won't submit due to other handlers
            }
          }
        }
      }, { capture: true, passive: false });
    }
  }

  async ngOnInit(): Promise<void> {
    // Preserve current step before any initialization
    const preservedStep = this.navigation.currentIndex();
    
    this.registerNavigation();
    
    // Restore preserved step if it was valid
    if (preservedStep > 0 && preservedStep <= this.steps.length) {
      this.navigation.setCurrentIndex(preservedStep);
    }
    
    // Load draft with error handling
    try {
      await this.applicationStore.loadDraft();
      
      // Patch form immediately after loading draft if draft exists
      // This ensures form is populated even if effects don't run
      const draft = this.applicationStore.draft();
      if (draft) {
        // Always patch from draft - it will merge with any existing data
        // The patchForm method has logic to preserve user input if needed
        console.log('Patching form from draft on initial load');
        this.patchForm(draft);
      } else {
        console.log('No draft found, form will remain empty');
      }
    } catch (error) {
      console.error('Error loading draft:', error);
      // Don't throw - allow user to continue filling form
    }
    
    // Set up CUET form field enable/disable based on applied status
    this.setupCuetFieldsToggle();
    this.setupClassXiiBoardStreamListeners();
    this.applyClassXiiStreamValidators(this.academicsForm.get('classXiiBoardCode')?.value);
    
    // Load payment status
    try {
      await this.loadPaymentStatus();
    } catch (error) {
      console.error('Error loading payment status:', error);
      // Don't throw - allow user to continue
    }
    
    // Restore step after init — unless server says application+fee are complete (then stay on declaration for success UI)
    if (
      !this.applicationFullyComplete() &&
      preservedStep > 0 &&
      preservedStep <= this.steps.length &&
      this.currentStepIndex() !== preservedStep
    ) {
      this.navigation.setCurrentIndex(preservedStep);
    }
  }

  private setupReligionDenominationListener(): void {
    this.personalForm
      .get('religion')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.syncDenominationWithReligion(true);
        this.cdr.markForCheck();
      });
  }

  /**
   * Clears denomination when religion is not Christian (when `clearDenomWhenNotChristian`)
   * and applies required validators only for Christian.
   */
  private syncDenominationWithReligion(clearDenomWhenNotChristian: boolean): void {
    const r = (this.personalForm.get('religion')?.value ?? '').toString().trim();
    const denomCtrl = this.personalForm.get('denomination');
    if (!denomCtrl) {
      return;
    }
    if (r !== 'Christian') {
      if (clearDenomWhenNotChristian) {
        denomCtrl.setValue('', { emitEvent: false });
      }
      denomCtrl.clearValidators();
    } else {
      denomCtrl.setValidators([Validators.required]);
    }
    denomCtrl.updateValueAndValidity({ emitEvent: false });
  }

  isReligionChristian(): boolean {
    return (this.personalForm.get('religion')?.value ?? '').toString().trim() === 'Christian';
  }

  private setupCuetFieldsToggle(): void {
    const cuetForm = this.cuetForm;
    const appliedControl = cuetForm.get('applied');
    const marksControl = cuetForm.get('marks');
    const rollNumberControl = cuetForm.get('rollNumber');

    if (appliedControl && marksControl && rollNumberControl) {
      // Initial state: disable fields if "Not Applied"
      const cuetMarksheetControl = this.uploadsForm.get('cuetMarksheet');
      const isApplied = appliedControl.value === 'Applied';
      this.updateCuetFields(isApplied, marksControl, rollNumberControl);
      this.updateCuetMarksheetUpload(isApplied, cuetMarksheetControl);

      // Listen for changes
      appliedControl.valueChanges.subscribe((value) => {
        const applied = value === 'Applied';
        this.updateCuetFields(applied, marksControl, rollNumberControl);
        this.updateCuetMarksheetUpload(applied, cuetMarksheetControl);
      });
    }
  }

  ngOnDestroy(): void {
    // Don't clear navigation - preserve it for when component is recreated
    // Only clear if explicitly needed (e.g., on logout)
    // this.navigation.clear();
    this.revokeSubmittedPdf();
  }

  get classXiiSubjectsArray(): FormArray<FormGroup> {
    return this.academicsForm.get('classXiiSubjects') as FormArray<FormGroup>;
  }

  get boardExaminationForm(): FormGroup {
    return this.academicsForm.get('boardExamination') as FormGroup;
  }

  get cuetForm(): FormGroup {
    return this.academicsForm.get('cuet') as FormGroup;
  }

  get isCuetApplied(): boolean {
    const cuetForm = this.cuetForm;
    const appliedControl = cuetForm.get('applied');
    return appliedControl?.value === 'Applied';
  }

  private updateCuetFields(enabled: boolean, marksControl: AbstractControl, rollNumberControl: AbstractControl): void {
    if (enabled) {
      marksControl.enable({ emitEvent: false });
      rollNumberControl.enable({ emitEvent: false });
    } else {
      marksControl.disable({ emitEvent: false });
      rollNumberControl.disable({ emitEvent: false });
      marksControl.setValue('', { emitEvent: false });
      rollNumberControl.setValue('', { emitEvent: false });
    }
  }


  private updateCuetMarksheetUpload(enabled: boolean, cuetMarksheetControl: AbstractControl | null): void {
    if (!cuetMarksheetControl) return;

    if (enabled) {
      cuetMarksheetControl.setValidators([this.attachmentUploadValidator]);
      cuetMarksheetControl.enable({ emitEvent: false });
    } else {
      cuetMarksheetControl.clearValidators();
      cuetMarksheetControl.disable({ emitEvent: false });
      if (cuetMarksheetControl.value) {
        cuetMarksheetControl.setValue(null, { emitEvent: false });
      }
    }
    cuetMarksheetControl.updateValueAndValidity({ emitEvent: false });
  }
  preventFormSubmit(event: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
      event.stopImmediatePropagation();
    }
  }

  nextStep(event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    
    if (this.currentStepIndex() < this.steps.length) {
      const currentGroup = this.getCurrentFormGroup();
      if (currentGroup.invalid) {
        currentGroup.markAllAsTouched();
        const messages = this.getCurrentFormErrors();
        const summary =
          messages.length > 0
            ? messages.length === 1
              ? messages[0]
              : `Please complete the following: ${messages.join('; ')}`
            : 'Please review the highlighted fields before continuing.';
        this.toast.show(summary, 'error');
        this.scrollFirstValidationAnchorForGroup(currentGroup);
        return;
      }

      this.navigation.setCurrentIndex(this.currentStepIndex() + 1);
      void this.saveDraft();
    }
  }

  previousStep(event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    
    if (this.currentStepIndex() > 0) {
      this.navigation.setCurrentIndex(this.currentStepIndex() - 1);
    }
  }

  async saveDraft(options?: { silent?: boolean }): Promise<void> {
    // Preserve current step before saving
    const currentStep = this.currentStepIndex();
    
    this.isSavingDraft = true;
    try {
      // Sync totalMarks from DOM input to form control before saving
      // This ensures we capture the value even if the input handler hasn't synced yet
      const boardExaminationForm = this.academicsForm.get('boardExamination') as FormGroup;
      if (boardExaminationForm) {
        const totalMarksControl = boardExaminationForm.get('totalMarks');
        // Try to find the input element and sync its value
        const totalMarksInput = document.querySelector('input[formControlName="totalMarks"]') as HTMLInputElement;
        if (totalMarksInput && totalMarksControl) {
          const inputValue = totalMarksInput.value.trim();
          // Always sync if input has a value (even if it matches control, to ensure it's set)
          if (inputValue) {
            totalMarksControl.setValue(inputValue, { emitEvent: false });
            totalMarksControl.markAsDirty();
            totalMarksControl.markAsTouched();
          }
        }
      }
      
      const payload = this.toDraft();
      // Debug: Log the payload to verify totalMarks is included
      console.log('Saving draft with totalMarks:', payload.academics.boardExamination.totalMarks);
      await this.applicationStore.saveDraft(payload, options);
      this.showJustSaved.set(true);
      setTimeout(() => this.showJustSaved.set(false), 3000);
    } catch (error) {
      console.error('Error saving draft:', error);
      // Don't throw - just log the error
    } finally {
      // Restore current step after saving (in case it was reset)
      if (this.currentStepIndex() !== currentStep) {
        this.navigation.setCurrentIndex(currentStep);
      }
      
      // Use setTimeout to prevent effect from patching immediately after save
      setTimeout(() => {
        this.isSavingDraft = false;
      }, 100);
    }
  }

  async submit(): Promise<void> {
    if (this.applicationFullyComplete()) {
      return;
    }
    this.submissionSuccessful = false;
    this.revokeSubmittedPdf();
    for (let i = 0; i < this.steps.length; i++) {
      const step = this.steps[i];
      step.formGroup.markAllAsTouched();
      if (step.formGroup.invalid) {
        this.navigation.setCurrentIndex(i);
        const messages = this.collectErrorsFromGroup(step.formGroup, step.title);
        const summary =
          messages.length > 0
            ? messages.length === 1
              ? messages[0]
              : `Please complete the following: ${messages.join('; ')}`
            : 'Please complete all sections before submitting.';
        this.toast.show(summary, 'error');
        setTimeout(() => this.scrollFirstValidationAnchorForGroup(step.formGroup), 0);
        return;
      }
    }
    if (this.declarationForm.invalid) {
      this.declarationForm.markAllAsTouched();
      this.toast.show('Please accept the declaration to submit.', 'error');
      return;
    }

    const payload = this.toDraft();
    payload.coursesLocked = true;
    this.coursesLocked = true;
    this.applyCourseLock();
    // Persist latest uploads so submit can omit heavy base64 (server merges from draft).
    await this.saveDraft({ silent: true });
    const submissionResult = await this.applicationStore.submitApplication(draftForSubmitRequest(payload));
    if (!submissionResult) {
      this.coursesLocked = false;
      this.applyCourseLock();
      return;
    }

    this.setSubmissionPdf(submissionResult);
    this.submissionSuccessful = true;
    this.toast.show('Application submitted successfully. A confirmation email has been sent. Please complete the payment to download your application form.', 'success');
    
    // Load payment status after submission
    await this.loadPaymentStatus();
  }


  async payNow(): Promise<void> {
    if (this.isPaymentProcessing()) {
      return;
    }

    try {
      this.isPaymentProcessing.set(true);

      // Create payment order
      const orderResponse = await this.paymentApi.createOrder().toPromise();
      if (!orderResponse) {
        throw new Error('Failed to create payment order');
      }

      // Load Razorpay script if not already loaded
      await this.loadRazorpayScript();

      const profile = this.portalStore.dashboard()?.profile;
      const options = buildRazorpayStandardOptions({
        key: orderResponse.keyId,
        amountPaise: Math.round(orderResponse.amount * 100),
        currency: orderResponse.currency,
        name: 'Don Bosco College Tura',
        description: 'Admission Application Fee',
        orderId: orderResponse.orderId,
        handler: async (response) => {
          await this.handlePaymentSuccess(response);
        },
        prefill: {
          name: profile?.fullName ?? '',
          email: profile?.email ?? '',
          contact: profile?.mobileNumber ?? '',
        },
        themeColor: '#1b5e9d',
        onDismiss: () => this.isPaymentProcessing.set(false),
        checkoutLogoUrl: orderResponse.checkoutLogoUrl,
      });

      const razorpay = new (window as any).Razorpay(options);
      razorpay.open();
    } catch (error: any) {
      console.error('Payment error:', error);
      // Extract detailed error message from HttpErrorResponse
      let errorMessage = 'Failed to initiate payment. Please try again.';
      
      // Try to extract error message from various possible locations
      if (error?.error) {
        // Check if error.error is a string (text/plain response)
        if (typeof error.error === 'string') {
          errorMessage = error.error || errorMessage;
        }
        // Check if error.error is an object with message property
        else if (error.error?.message) {
          errorMessage = error.error.message;
        }
        // Check if error.error is an object with error property
        else if (error.error?.error) {
          errorMessage = error.error.error;
        }
      }
      // Fallback to error.message
      else if (error?.message) {
        errorMessage = error.message;
      }
      
      // Log full error details for debugging
      console.error('Payment error details:', {
        error,
        errorString: JSON.stringify(error),
        errorError: error?.error,
        errorErrorString: typeof error?.error === 'string' ? error.error : JSON.stringify(error?.error),
        message: errorMessage,
        status: error?.status,
        statusText: error?.statusText,
        url: error?.url
      });
      
      this.toast.show(errorMessage, 'error');
      this.isPaymentProcessing.set(false);
    }
  }

  private async handlePaymentSuccess(response: any): Promise<void> {
    try {
      this.isPaymentProcessing.set(true);
      
      const verifyResponse = await this.paymentApi.verifyPayment({
        orderId: response.razorpay_order_id,
        paymentId: response.razorpay_payment_id,
        signature: response.razorpay_signature
      }).toPromise();

      if (verifyResponse?.success) {
        // Reload payment status to get updated payment information
        await this.loadPaymentStatus();
        
        // Ensure PDF is available for download
        const status = this.paymentStatus();
        if (status?.isPaymentCompleted) {
          // Reload PDF if not already loaded
          if (!this.submittedPdfUrl) {
            await this.reloadSubmittedPdf();
          }
          
          this.toast.show('Payment completed successfully! You can now download your application form.', 'success');
          
          // Reload dashboard to reflect updated payment status
          await this.portalStore.loadDashboard();
        } else {
          this.toast.show('Payment completed successfully!', 'success');
        }
      } else {
        this.toast.show(verifyResponse?.message || 'Payment verification failed.', 'error');
      }
    } catch (error: any) {
      console.error('Payment verification error:', error);
      this.toast.show('Payment verification failed. Please contact support.', 'error');
    } finally {
      this.isPaymentProcessing.set(false);
    }
  }

  private async loadPaymentStatus(): Promise<void> {
    try {
      const status = await this.paymentApi.getPaymentStatus().toPromise();
      if (status) {
        this.paymentStatus.set(status);
        if (status.isPaymentCompleted && !this.submittedPdfUrl) {
          // Reload PDF if payment is completed but PDF not loaded
          await this.reloadSubmittedPdf();
        }
        this.syncPostPaymentUiState();
      } else {
        // Initialize with default status if not found
        this.paymentStatus.set({
          isApplicationSubmitted: false,
          isPaymentCompleted: false,
          applicationReference: null,
          paymentOrderId: null,
          paymentTransactionId: null,
          paymentAmount: null,
          paymentCompletedOnUtc: null
        });
      }
    } catch (error) {
      console.error('Failed to load payment status:', error);
      // Initialize with default status on error
      this.paymentStatus.set({
        isApplicationSubmitted: false,
        isPaymentCompleted: false,
        applicationReference: null,
        paymentOrderId: null,
        paymentTransactionId: null,
        paymentAmount: null,
        paymentCompletedOnUtc: null
      });
    }
  }

  /** True when application is submitted and fee payment is verified (server state). */
  applicationFullyComplete(): boolean {
    const p = this.paymentStatus();
    return !!(p?.isApplicationSubmitted && p?.isPaymentCompleted);
  }

  /** After refresh: jump to declaration step and align local flags so the success card can show. */
  private syncPostPaymentUiState(): void {
    const p = this.paymentStatus();
    if (p?.isApplicationSubmitted && p?.isPaymentCompleted) {
      this.submissionSuccessful = true;
      this.navigation.setCurrentIndex(this.declarationStepIndex);
      this.cdr.markForCheck();
    }
  }

  downloadPaymentReceipt(): void {
    const s = this.paymentStatus();
    if (!s?.isPaymentCompleted) {
      return;
    }
    const dateStr = s.paymentCompletedOnUtc
      ? new Date(s.paymentCompletedOnUtc).toLocaleString()
      : '—';
    const lines = [
      'Don Bosco College Tura — Admission application fee',
      '========================================',
      `Application reference: ${s.applicationReference ?? '—'}`,
      `Amount paid: ₹${s.paymentAmount ?? '—'}`,
      `Razorpay transaction ID: ${s.paymentTransactionId ?? '—'}`,
      `Paid on: ${dateStr}`,
      '',
      'This is a summary of your online payment. Keep for your records.',
      'A confirmation email has also been sent to your registered email address.',
    ];
    const blob = new Blob([lines.join('\n')], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `payment-receipt-${s.applicationReference ?? 'application'}.txt`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  signOut(): void {
    this.auth.logout();
  }

  private async reloadSubmittedPdf(): Promise<void> {
    // Re-submit to get PDF after payment
    const payload = this.toDraft();
    payload.coursesLocked = true;
    await this.saveDraft({ silent: true });
    const result = await this.applicationStore.submitApplication(draftForSubmitRequest(payload));
    if (result) {
      this.setSubmissionPdf(result);
    }
  }

  private loadRazorpayScript(): Promise<void> {
    return new Promise((resolve, reject) => {
      if ((window as any).Razorpay) {
        resolve();
        return;
      }

      const script = document.createElement('script');
      script.src = 'https://checkout.razorpay.com/v1/checkout.js';
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Failed to load Razorpay script'));
      document.body.appendChild(script);
    });
  }
  /** Drop attachments with missing base64 so validation matches the empty file input. */
  private sanitizeUploadSection(uploads: UploadSection): UploadSection {
    const pick = (a: FileAttachment | null | undefined): FileAttachment | null => {
      if (!a || typeof a !== 'object') {
        return null;
      }
      const data = (a.data ?? '').trim();
      if (!data) {
        return null;
      }
      return {
        fileName: a.fileName ?? 'document',
        contentType: a.contentType || 'application/octet-stream',
        data,
      };
    };
    return {
      stdXMarksheet: pick(uploads.stdXMarksheet),
      stdXIIMarksheet: pick(uploads.stdXIIMarksheet),
      cuetMarksheet: pick(uploads.cuetMarksheet),
      differentlyAbledProof: pick(uploads.differentlyAbledProof),
      economicallyWeakerProof: pick(uploads.economicallyWeakerProof),
    };
  }

  /** Faster than readAsDataURL for large images (no data: URL prefix / extra copy). */
  private arrayBufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    const chunk = 0x8000;
    let binary = '';
    for (let i = 0; i < bytes.length; i += chunk) {
      binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk) as unknown as number[]);
    }
    return btoa(binary);
  }

  onFileSelected(controlName: keyof UploadSection, event: Event): void {
    // CRITICAL: Prevent form submission but allow file selection
    // DO NOT preventDefault on change event - it prevents file selection!
    // Only stop propagation to prevent bubbling to form
    event.stopPropagation();
    event.stopImmediatePropagation();
    
    // Prevent form submission at multiple levels
    const form = (event.target as HTMLElement)?.closest('form');
    if (form) {
      // Disable form submission completely
      const preventSubmit = (e: Event) => {
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
        return false;
      };
      
      // Add multiple layers of prevention
      form.addEventListener('submit', preventSubmit, { capture: true });
      const originalOnSubmit = form.onsubmit;
      form.onsubmit = preventSubmit;
      
      // Remove after a delay to allow file processing
      setTimeout(() => {
        try {
          form.removeEventListener('submit', preventSubmit, { capture: true } as any);
          form.onsubmit = originalOnSubmit;
        } catch (e) {
          // Ignore errors when removing listener
        }
      }, 1000);
    }
    
    const input = event.target as HTMLInputElement;
    if (!input || !input.files || !input.files.length) {
      return;
    }
    
    const control = this.uploadsForm.get(controlName);
    if (!control || control.disabled) {
      input.value = '';
      return;
    }
    
    const file = input.files[0];
    if (!file) {
      return;
    }
    
    // Validate file type
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'application/pdf'];
    if (!allowedTypes.includes(file.type)) {
      this.toast.show('Only JPEG, PNG, or PDF files are allowed.', 'error');
      input.value = '';
      return;
    }
    
    // Validate file size (max 5MB)
    const maxSize = 5 * 1024 * 1024; // 5MB
    if (file.size > maxSize) {
      this.toast.show('File size must be less than 5MB.', 'error');
      input.value = '';
      return;
    }
    
    void this.readFileAsAttachment(file)
      .then((attachment) => {
        const currentStep = this.currentStepIndex();
        control.setValue(attachment, { emitEvent: false });
        control.markAsDirty();
        control.markAsTouched();
        control.updateValueAndValidity({ emitEvent: false });
        this.uploadsForm.updateValueAndValidity({ emitEvent: false });
        this.cdr.markForCheck();

        if (this.currentStepIndex() !== currentStep) {
          this.navigation.setCurrentIndex(currentStep);
        }

        this.toast.show(`File "${file.name}" uploaded successfully.`, 'success');
      })
      .catch((error) => {
        console.error('Error processing file:', error);
        this.toast.show('Failed to process file. Please try again.', 'error');
        input.value = '';
      });
  }

  private async readFileAsAttachment(file: File): Promise<FileAttachment> {
    const buffer = await file.arrayBuffer();
    const data = this.arrayBufferToBase64(buffer);
    return {
      fileName: file.name,
      contentType: file.type || 'application/octet-stream',
      data,
    };
  }

  removeAttachment(controlName: keyof UploadSection): void {
    const control = this.uploadsForm.get(controlName);
    if (!control || control.disabled) {
      return;
    }
    control.setValue(null);
    control.markAsDirty();
    control.markAsTouched();
    control.updateValueAndValidity({ emitEvent: false });
    this.uploadsForm.updateValueAndValidity({ emitEvent: false });
    this.cdr.markForCheck();
  }

  getAttachment(controlName: keyof UploadSection): FileAttachment | null {
    return (this.uploadsForm.get(controlName)?.value as FileAttachment | null) ?? null;
  }

  /** Display label for an uploaded file (normalizes legacy DBCT25 in stored names to DBCT26). */
  displayUploadFileName(fileName: string | undefined | null): string {
    if (!fileName) {
      return '';
    }
    return fileName.replace(/DBCT25/gi, 'DBCT26');
  }

  downloadAttachment(controlName: keyof UploadSection): void {
    const attachment = this.getAttachment(controlName);
    if (!attachment) {
      return;
    }

    const byteCharacters = atob(attachment.data ?? '');
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
      byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], {
      type: attachment.contentType || 'application/octet-stream',
    });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = attachment.fileName || 'document';
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private setupConditionalUploads(): void {
    const differentlyAbled = this.personalForm.get('isDifferentlyAbled');
    const differentlyAbledProof = this.uploadsForm.get('differentlyAbledProof');

    if (differentlyAbled && differentlyAbledProof) {
      differentlyAbled.valueChanges
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((value) =>
          this.toggleConditionalUpload(differentlyAbledProof, Boolean(value))
        );
      this.toggleConditionalUpload(differentlyAbledProof, Boolean(differentlyAbled.value));
    }

    const economicallyWeaker = this.personalForm.get('isEconomicallyWeaker');
    const economicallyWeakerProof = this.uploadsForm.get('economicallyWeakerProof');

    if (economicallyWeaker && economicallyWeakerProof) {
      economicallyWeaker.valueChanges
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((value) =>
          this.toggleConditionalUpload(economicallyWeakerProof, Boolean(value))
        );
      this.toggleConditionalUpload(
        economicallyWeakerProof,
        Boolean(economicallyWeaker.value)
      );
    }
  }

  private setupCoursePreferenceDependencies(): void {
     const shiftControl = this.coursesForm.get('shift');
     const majorControl = this.coursesForm.get('majorSubject');
 
     shiftControl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => {
      this.updateMajorOptions(value, false);
      this.updateMinorOptions(false);
      this.updateSupplementOptions(this.toShiftCode(value), false);
    });
 
     majorControl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
       this.updateMinorOptions(false);
     });
   }
 
  private updateMajorOptions(rawShift: unknown, preserveSelection: boolean): void {
    const shift = this.toShiftCode(rawShift);
    const majors = shift ? Object.keys(COURSE_OFFERINGS[shift]?.majors ?? {}) : [];
    this.availableMajors.set(majors);
    this.currentShiftOption.set(
      shift ? SHIFT_OPTIONS.find((option) => option.value === shift) ?? null : null
    );
 
    const majorControl = this.coursesForm.get('majorSubject');
    const currentMajor = (majorControl?.value as string) ?? '';
 
    if (majors.length === 0) {
      majorControl?.disable({ emitEvent: false });
    } else {
      majorControl?.enable({ emitEvent: false });
    }
 
    if (!preserveSelection || !majors.includes(currentMajor)) {
      majorControl?.setValue('', { emitEvent: false });
    }
 
    this.updateSupplementOptions(shift, preserveSelection);
    this.updateMinorOptions(preserveSelection);
  }
 
  private updateMinorOptions(preserveSelection: boolean): void {
    const shift = this.toShiftCode(this.coursesForm.get('shift')?.value);
    const major = (this.coursesForm.get('majorSubject')?.value as string) ?? '';
    const minorControl = this.coursesForm.get('minorSubject');
 
    const minors =
      shift && major ? COURSE_OFFERINGS[shift]?.majors[major] ?? [] : [];
 
    this.availableMinors.set(minors);
 
    if (minors.length > 0) {
      minorControl?.setValidators([Validators.required]);
      minorControl?.enable({ emitEvent: false });
    } else {
      minorControl?.clearValidators();
      minorControl?.disable({ emitEvent: false });
    }
 
    const currentMinor = (minorControl?.value as string) ?? '';
    if (!preserveSelection || !minors.includes(currentMinor)) {
      minorControl?.setValue('', { emitEvent: false });
    }
 
    minorControl?.updateValueAndValidity({ emitEvent: false });
  }
 
  private toShiftCode(value: unknown): ShiftCode | '' {
    if (!value) {
      return '';
    }

    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (!trimmed) {
        return '';
      }

      if (isLegacyShiftIIIRaw(trimmed)) {
        return '';
      }

      const direct = SHIFT_OPTIONS.find((option) => option.value === trimmed);
      if (direct) {
        return direct.value;
      }

      return LEGACY_SHIFT_ALIASES[trimmed] ?? '';
    }

    return '';
  }

  private buildMdcEligibilityContext(): MdcEligibilityContext {
    const subjects = this.buildClassXiiSubjectsPayload()
      .map((r) => r.subject)
      .filter((s) => !!s?.trim());
    const stream = (this.academicsForm.get('classXiiStreamCode')?.value ?? '').toString().trim();
    const major = (this.coursesForm.get('majorSubject')?.value ?? '').toString().trim();
    return {
      classXiiSubjectNames: subjects,
      classXiiStreamCode: stream,
      majorSubject: major,
    };
  }

  private enrichMdcOptionsWithEligibility(mdcRaw: OptionGroup): MdcOption[] {
    const ctx = this.buildMdcEligibilityContext();
    return mdcRaw.map((opt) => {
      const reason = getMdcIneligibilityReason(opt.value, ctx);
      if (reason) {
        return { ...opt, disabled: true, disabledReason: reason };
      }
      return { ...opt };
    });
  }

  /** Re-run MDC eligibility when Class XII or major changes (shift unchanged). */
  private refreshMdcEligibilityFromCurrentShift(): void {
    const shift = this.toShiftCode(this.coursesForm.get('shift')?.value);
    const supplements = shift ? SHIFT_SUPPLEMENTS[shift] : null;
    const mdcRaw = supplements?.mdc ?? [];
    const mdcEnriched = this.enrichMdcOptionsWithEligibility(mdcRaw);
    this.availableMdc.set(mdcEnriched);
    this.syncSupplementControl(
      this.coursesForm.get('multidisciplinaryChoice') as FormControl | null,
      mdcEnriched,
      true
    );
    this.coursesForm.get('multidisciplinaryChoice')?.updateValueAndValidity({ emitEvent: false });
    this.cdr.markForCheck();
  }

  private updateSupplementOptions(shift: ShiftCode | '', preserveSelection: boolean): void {
    const supplements = shift ? SHIFT_SUPPLEMENTS[shift] : null;

    const mdcOptions = supplements?.mdc ?? [];
    const aecOptions = supplements?.aec ?? [];
    const secOptions = supplements?.sec ?? [];

    const mdcEnriched = this.enrichMdcOptionsWithEligibility(mdcOptions);
    this.availableMdc.set(mdcEnriched);
    this.availableAec.set(aecOptions);
    this.availableSec.set(secOptions);

    this.syncSupplementControl(
      this.coursesForm.get('multidisciplinaryChoice') as FormControl | null,
      mdcEnriched,
      preserveSelection
    );

    this.syncSupplementControl(
      this.coursesForm.get('abilityEnhancementChoice') as FormControl | null,
      aecOptions,
      preserveSelection
    );

    this.syncSupplementControl(
      this.coursesForm.get('skillEnhancementChoice') as FormControl | null,
      secOptions,
      preserveSelection
    );
  }

  private syncSupplementControl(
    control: FormControl | null,
    options: ReadonlyArray<{ value: string; label: string; disabled?: boolean }>,
    preserveSelection: boolean
  ): void {
    if (!control) {
      return;
    }

    const current = (control.value as string) ?? '';

    if (options.length === 0) {
      control.disable({ emitEvent: false });
    } else {
      control.enable({ emitEvent: false });
    }

    const match = options.find((option) => option.value === current);
    const selectionAllowed = preserveSelection && !!match && !match.disabled;

    if (!selectionAllowed) {
      control.setValue('', { emitEvent: false });
    }
  }

  private toggleConditionalUpload(control: AbstractControl | null, enabled: boolean): void {
    if (!control) {
      return;
    }
    if (enabled) {
      control.setValidators([this.attachmentUploadValidator]);
      control.enable({ emitEvent: false });
    } else {
      control.clearValidators();
      control.disable({ emitEvent: false });
      control.setValue(null, { emitEvent: false });
      control.markAsPristine();
      control.markAsUntouched();
    }
    control.updateValueAndValidity({ emitEvent: false });
  }

  /** Keeps upload controls aligned with personal flags after patch (patchValue uses emitEvent: false). */
  private syncConditionalUploadControls(): void {
    const differentlyAbled = this.personalForm.get('isDifferentlyAbled');
    const economicallyWeaker = this.personalForm.get('isEconomicallyWeaker');
    this.toggleConditionalUpload(
      this.uploadsForm.get('differentlyAbledProof'),
      Boolean(differentlyAbled?.value)
    );
    this.toggleConditionalUpload(
      this.uploadsForm.get('economicallyWeakerProof'),
      Boolean(economicallyWeaker?.value)
    );
  }

  onAadhaarInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-digit characters
    const value = input.value.replace(/\D/g, '');
    // Limit to 12 digits
    const limitedValue = value.slice(0, 12);
    if (input.value !== limitedValue) {
      input.value = limitedValue;
      this.addressForm.get('aadhaarNumber')?.setValue(limitedValue, { emitEvent: true });
    }
  }

  onNameInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-letter characters (keep spaces, hyphens, apostrophes)
    const value = input.value.replace(/[^a-zA-Z\s\-']/g, '');
    if (input.value !== value) {
      // Find the form control name and parent fieldset
      const fieldset = input.closest('fieldset');
      const controlName = input.getAttribute('formControlName');
      if (fieldset && controlName) {
        const fieldsetName = fieldset.getAttribute('formGroupName');
        if (fieldsetName) {
          const control = this.contactsForm.get(`${fieldsetName}.${controlName}`) as AbstractControl | null;
          if (control) {
            (control as FormControl<string>).setValue(value, { emitEvent: true });
          }
        }
      }
    }
  }

  onAgeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-digit characters
    const value = input.value.replace(/\D/g, '');
    // Limit to 2 digits
    const limitedValue = value.slice(0, 2);
    if (input.value !== limitedValue) {
      const fieldset = input.closest('fieldset');
      const controlName = input.getAttribute('formControlName');
      if (fieldset && controlName) {
        const fieldsetName = fieldset.getAttribute('formGroupName');
        if (fieldsetName) {
          const control = this.contactsForm.get(`${fieldsetName}.${controlName}`) as AbstractControl | null;
          if (control) {
            (control as FormControl<string>).setValue(limitedValue, { emitEvent: true });
          }
        }
      }
    }
  }

  onOccupationInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-letter characters (keep spaces, hyphens, apostrophes)
    const value = input.value.replace(/[^a-zA-Z\s\-']/g, '');
    if (input.value !== value) {
      const fieldset = input.closest('fieldset');
      const controlName = input.getAttribute('formControlName');
      if (fieldset && controlName) {
        const fieldsetName = fieldset.getAttribute('formGroupName');
        if (fieldsetName) {
          const control = this.contactsForm.get(`${fieldsetName}.${controlName}`) as AbstractControl | null;
          if (control) {
            (control as FormControl<string>).setValue(value, { emitEvent: true });
          }
        }
      }
    }
  }

  onContactNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-digit characters
    const value = input.value.replace(/\D/g, '');
    // Limit to 10 digits
    const limitedValue = value.slice(0, 10);
    if (input.value !== limitedValue) {
      const fieldset = input.closest('fieldset');
      const controlName = input.getAttribute('formControlName');
      if (fieldset && controlName) {
        const fieldsetName = fieldset.getAttribute('formGroupName');
        if (fieldsetName) {
          const control = this.contactsForm.get(`${fieldsetName}.${controlName}`) as AbstractControl | null;
          if (control) {
            (control as FormControl<string>).setValue(limitedValue, { emitEvent: true });
          }
        }
      }
    }
  }

  onNumericInput(event: Event, formGroup: FormGroup, controlName: string): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-digit characters
    const value = input.value.replace(/\D/g, '');
    const control = formGroup.get(controlName);
    if (control) {
      // Always update the form control value to ensure it's synced
      // Use setValue with emitEvent: true to trigger validation
      (control as FormControl<string>).setValue(value, { emitEvent: true, onlySelf: false });
      // If the input value differs from the cleaned value, update the input
      if (input.value !== value) {
        input.value = value;
      }
      // Mark as touched and dirty to ensure validation works
      control.markAsTouched();
      control.markAsDirty();
      // Update parent form validity
      control.updateValueAndValidity({ emitEvent: true });
    }
  }

  onPercentageInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    // Allow digits, decimal point, and % symbol
    const value = input.value.replace(/[^\d.%]/g, '');
    // Ensure only one decimal point and one % symbol
    const parts = value.split('.');
    let cleanedValue = parts[0];
    if (parts.length > 1) {
      cleanedValue += '.' + parts.slice(1).join('').replace(/\./g, '');
    }
    // Remove % if it appears before the end
    const percentIndex = cleanedValue.indexOf('%');
    if (percentIndex >= 0 && percentIndex < cleanedValue.length - 1) {
      cleanedValue = cleanedValue.replace(/%/g, '') + '%';
    }
    if (input.value !== cleanedValue) {
      const control = this.academicsForm.get('boardExamination.percentage');
      if (control) {
        (control as FormControl<string>).setValue(cleanedValue, { emitEvent: true });
      }
    }
  }

  onAlphanumericInput(event: Event, formGroup: FormGroup, controlName: string): void {
    const input = event.target as HTMLInputElement;
    // Remove any non-alphanumeric characters
    const value = input.value.replace(/[^a-zA-Z0-9]/g, '');
    if (input.value !== value) {
      const control = formGroup.get(controlName);
      if (control) {
        (control as FormControl<string>).setValue(value, { emitEvent: true });
      }
    }
  }

  private createParentGroup(): FormGroup {
    return this.fb.group({
      name: ['', [Validators.required, this.nameValidator]],
      age: ['', [Validators.required, this.ageValidator]],
      occupation: ['', [Validators.required, this.occupationValidator]],
      contactNumber1: ['', [Validators.required, this.contactNumberValidator]],
    });
  }

  private createDefaultClassXiiRows(): FormGroup[] {
    return Array.from({ length: 5 }, (_, i) => this.createClassXiiRowGroupForIndex(i));
  }

  private createClassXiiRowGroupShell(): FormGroup {
    return this.fb.group(
      {
        entryMode: ['master' as 'master' | 'manual'],
        subjectMasterId: [''],
        subject: [''],
        marks: [''],
      },
      { validators: this.classXiiRowCrossValidator }
    );
  }

  private computeClassXiiEntryMode(rowIndex: number): 'master' | 'manual' {
    const board = (this.academicsForm?.get('classXiiBoardCode')?.value ?? '').toString().trim();
    if (board === 'OTHER' || rowIndex >= 5) {
      return 'manual';
    }
    return 'master';
  }

  private resolveEntryModeForLoadedRow(
    r: ClassXiiSubjectRow,
    index: number,
    board: string
  ): 'master' | 'manual' {
    if (board === 'OTHER') {
      return 'manual';
    }
    if (index >= 5) {
      return 'manual';
    }
    const em = r.entryMode?.toString().trim().toLowerCase();
    if (em === 'manual') {
      return 'manual';
    }
    if (em === 'dropdown') {
      return 'master';
    }
    const id = r.subjectMasterId?.toString().trim() ?? '';
    if (!id && (r.subject?.trim() ?? '')) {
      return 'manual';
    }
    return 'master';
  }

  private applyClassXiiRowFieldValidators(g: FormGroup, index: number): void {
    const board = (this.academicsForm?.get('classXiiBoardCode')?.value ?? '').toString().trim();
    const isOther = board === 'OTHER';
    const isAdditional = index >= 5;

    const subjectCtrl = g.get('subject');
    const marksCtrl = g.get('marks');

    if (isOther && !isAdditional) {
      subjectCtrl?.setValidators([Validators.required]);
    } else {
      subjectCtrl?.clearValidators();
    }
    subjectCtrl?.updateValueAndValidity({ emitEvent: false });

    if (!isAdditional) {
      marksCtrl?.setValidators([Validators.required, this.classXiiMarksValidator]);
    } else {
      marksCtrl?.setValidators([this.classXiiMarksValidator]);
    }
    marksCtrl?.updateValueAndValidity({ emitEvent: false });
  }

  private initClassXiiRowAtIndex(g: FormGroup, index: number): void {
    g.get('entryMode')?.setValue(this.computeClassXiiEntryMode(index), { emitEvent: false });
    this.applyClassXiiRowFieldValidators(g, index);
  }

  /** Row index drives core (0–4) vs additional (5+), board drives master vs manual. */
  private createClassXiiRowGroupForIndex(index: number): FormGroup {
    const g = this.createClassXiiRowGroupShell();
    this.initClassXiiRowAtIndex(g, index);
    return g;
  }

  private syncClassXiiEntryModesAndValidators(): void {
    this.classXiiSubjectsArray.controls.forEach((ctrl, i) => {
      this.initClassXiiRowAtIndex(ctrl as FormGroup, i);
    });
  }

  isClassXiiOtherBoard(): boolean {
    return (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim() === 'OTHER';
  }

  boardAndStreamReadyForCatalog(): boolean {
    const b = (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim();
    const s = (this.academicsForm.get('classXiiStreamCode')?.value ?? '').toString().trim();
    return !!b && b !== 'OTHER' && !!s;
  }

  /** Shown under Board Examination header — board name comes from Class XII step (no duplicate field). */
  boardExaminationBoardHint(): string | null {
    const code = (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim();
    if (!code) {
      return null;
    }
    const labels: Record<string, string> = {
      MBOSE: 'MBOSE',
      CBSE: 'CBSE',
      ISC: 'ISC',
      OTHER: 'Other',
    };
    return `Board: ${labels[code] ?? code} (from your Class XII selection)`;
  }

  /** Persisted on `boardExamination.boardName` for PDF/API — derived from Class XII board code. */
  private resolveBoardExaminationBoardName(): string {
    const code = (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim();
    const map: Record<string, string> = {
      MBOSE: 'MBOSE',
      CBSE: 'CBSE',
      ISC: 'ISC',
      OTHER: 'Other',
    };
    return map[code] ?? code ?? '';
  }

  /** Standard divisions + any legacy free-text value from an old draft. */
  divisionOptionsForSelect(): readonly string[] {
    const d = (this.boardExaminationForm.get('division')?.value ?? '').toString().trim();
    const opts = [...this.divisionOptions];
    if (d && !opts.includes(d)) {
      return [d, ...opts];
    }
    return opts;
  }

  /** Years 2020–2026 plus any other year from an older draft. */
  boardExamYearOptionsForSelect(): readonly string[] {
    const y = (this.boardExaminationForm.get('year')?.value ?? '').toString().trim();
    const base = [...this.boardExamYearOptions];
    if (y && !base.includes(y)) {
      return [y, ...base].sort();
    }
    return base;
  }

  setCuetApplied(value: 'Applied' | 'Not Applied'): void {
    this.cuetForm.get('applied')?.setValue(value);
    this.cdr.markForCheck();
  }

  setPersonalBool(key: 'isDifferentlyAbled' | 'isEconomicallyWeaker', value: boolean): void {
    const c = this.personalForm.get(key);
    if (c) {
      c.setValue(value);
      c.markAsDirty();
      c.markAsTouched();
    }
    this.cdr.markForCheck();
  }

  setHouseholdAreaType(value: 'Urban' | 'Rural'): void {
    const c = this.contactsForm.get('householdAreaType');
    if (c) {
      c.setValue(value);
      c.markAsDirty();
      c.markAsTouched();
    }
    this.cdr.markForCheck();
  }

  /** Class XII examination year of passing (dropdown). */
  readonly boardExamYearOptions = ['2020', '2021', '2022', '2023', '2024', '2025', '2026'] as const;

  readonly divisionOptions = ['First', 'Second', 'Third', 'Distinction'] as const;

  readonly classXiiMinimumSubjects = 5;

  /** Complete core rows (0–4) for progress — aligned with minimum-subjects validator. */
  classXiiCompleteCount(): number {
    const board = (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim();
    if (!board) {
      return 0;
    }
    let complete = 0;
    const n = Math.min(5, this.classXiiSubjectsArray.length);
    for (let i = 0; i < n; i++) {
      const g = this.classXiiSubjectsArray.at(i) as FormGroup;
      const subj = g.get('subject')?.value?.toString().trim() ?? '';
      const marksRaw = g.get('marks')?.value;
      const marks =
        marksRaw === null || marksRaw === undefined ? '' : String(marksRaw).trim();
      const id = g.get('subjectMasterId')?.value?.toString().trim() ?? '';
      const mode = g.get('entryMode')?.value as 'master' | 'manual' | undefined;
      if (!marks || g.get('marks')?.errors?.['classXiiMarksInvalid']) {
        continue;
      }
      if (board === 'OTHER') {
        if (subj) {
          complete++;
        }
      } else if (mode === 'master' && id) {
        complete++;
      } else if (mode === 'manual' && subj) {
        complete++;
      }
    }
    return complete;
  }

  classXiiUseMasterDropdown(group: AbstractControl): boolean {
    if (this.isClassXiiOtherBoard()) {
      return false;
    }
    return (group as FormGroup).get('entryMode')?.value === 'master';
  }

  classXiiUseManualSubjectField(group: AbstractControl): boolean {
    return this.isClassXiiOtherBoard() || (group as FormGroup).get('entryMode')?.value === 'manual';
  }

  availableOptionsForRow(rowIndex: number): ClassXiiSubjectOptionDto[] {
    const row = this.classXiiSubjectsArray.at(rowIndex);
    if (!row || !this.classXiiUseMasterDropdown(row)) {
      return [];
    }
    const list = this.subjectsList();
    const currentId =
      row.get('subjectMasterId')?.value?.toString().trim() ?? '';
    const taken = new Set<string>();
    this.classXiiSubjectsArray.controls.forEach((ctrl, idx) => {
      if (idx === rowIndex) {
        return;
      }
      const id = ctrl.get('subjectMasterId')?.value?.toString().trim();
      if (id) {
        taken.add(id);
      }
    });
    return list.filter((opt) => !taken.has(opt.id) || opt.id === currentId);
  }

  onClassXiiSubjectSelected(rowIndex: number, subjectId: string): void {
    const row = this.classXiiSubjectsArray.at(rowIndex);
    if (!row) {
      return;
    }
    if (!subjectId?.trim()) {
      row.patchValue({ subjectMasterId: '', subject: '' }, { emitEvent: true });
      row.updateValueAndValidity({ emitEvent: false });
      this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
      this.cdr.markForCheck();
      return;
    }
    const item = this.subjectsList().find((x) => x.id === subjectId);
    row.patchValue(
      {
        subjectMasterId: subjectId,
        subject: item?.subjectName ?? '',
      },
      { emitEvent: true }
    );
    row.updateValueAndValidity({ emitEvent: false });
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
    this.cdr.markForCheck();
  }

  onClassXiiManualSubjectInput(event: Event, rowIndex: number): void {
    const input = event.target as HTMLInputElement;
    const row = this.classXiiSubjectsArray.at(rowIndex);
    if (!row) {
      return;
    }
    row.get('subject')?.setValue(input.value, { emitEvent: true });
    row.get('subjectMasterId')?.setValue('', { emitEvent: false });
  }

  onClassXiiMarksBlurFocusNext(rowIndex: number): void {
    const next = document.getElementById(`class-xii-marks-${rowIndex + 1}`) as HTMLInputElement | null;
    next?.focus();
  }

  addClassXiiSubjectRow(): void {
    const idx = this.classXiiSubjectsArray.length;
    this.classXiiSubjectsArray.push(this.createClassXiiRowGroupForIndex(idx));
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
    this.cdr.markForCheck();
  }

  removeClassXiiSubjectRow(index: number): void {
    if (index < 5) {
      return;
    }
    if (this.classXiiSubjectsArray.length <= 5) {
      this.toast.show('At least five subjects are required.', 'error');
      return;
    }
    this.classXiiSubjectsArray.removeAt(index);
    this.syncClassXiiEntryModesAndValidators();
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
    this.cdr.markForCheck();
  }

  private setupClassXiiBoardStreamListeners(): void {
    const boardCtrl = this.academicsForm.get('classXiiBoardCode');
    const streamCtrl = this.academicsForm.get('classXiiStreamCode');
    boardCtrl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((board) => {
      this.applyClassXiiStreamValidators(board);
      if (!this.draftPatchInProgress) {
        this.clearClassXiiSubjectFields();
      }
      this.reloadClassXiiSubjectCatalog();
    });
    streamCtrl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (!this.draftPatchInProgress) {
        this.clearClassXiiSubjectFields();
      }
      this.reloadClassXiiSubjectCatalog();
    });
  }

  private applyClassXiiStreamValidators(board: string | null | undefined): void {
    const streamControl = this.academicsForm.get('classXiiStreamCode');
    const b = (board ?? '').toString().trim();
    if (b && b !== 'OTHER') {
      streamControl?.enable({ emitEvent: false });
      streamControl?.setValidators([Validators.required]);
    } else {
      streamControl?.clearValidators();
      if (b === 'OTHER') {
        streamControl?.setValue('', { emitEvent: false });
        streamControl?.disable({ emitEvent: false });
      } else {
        streamControl?.enable({ emitEvent: false });
      }
    }
    streamControl?.updateValueAndValidity({ emitEvent: false });
  }

  private clearClassXiiSubjectFields(): void {
    this.classXiiSubjectsArray.controls.forEach((ctrl) => {
      ctrl.patchValue({ subjectMasterId: '', subject: '', marks: '' }, { emitEvent: false });
      ctrl.markAsPristine();
    });
    this.syncClassXiiEntryModesAndValidators();
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
  }

  private reloadClassXiiSubjectCatalog(): void {
    const board = this.academicsForm.get('classXiiBoardCode')?.value?.toString().trim() ?? '';
    const stream = this.academicsForm.get('classXiiStreamCode')?.value?.toString().trim() ?? '';
    if (!board || board === 'OTHER' || !stream) {
      this.subjectsList.set([]);
      this.subjectsLoading.set(false);
      this.subjectsLoadError.set(null);
      this.cdr.markForCheck();
      return;
    }
    this.subjectsLoading.set(true);
    this.subjectsLoadError.set(null);
    this.applicantApi.getClassXiiSubjects(board, stream).subscribe({
      next: (res) => {
        const items = [...(res.items ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
        this.subjectsList.set(items);
        this.subjectsLoading.set(false);
        if (items.length === 0) {
          this.subjectsLoadError.set('No subjects are configured for this board and stream yet.');
        } else {
          this.subjectsLoadError.set(null);
        }
        this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
        this.cdr.markForCheck();
      },
      error: () => {
        this.subjectsLoading.set(false);
        this.subjectsList.set([]);
        this.subjectsLoadError.set('Unable to load subjects. Try again or verify board and stream.');
        this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
        this.cdr.markForCheck();
      },
    });
  }

  private patchClassXiiSection(draft: ApplicantApplicationDraft): void {
    const ac = draft.academics;
    const board = ac.classXiiBoardCode?.trim() ?? '';
    const stream = ac.classXiiStreamCode?.trim() ?? '';
    this.applyClassXiiStreamValidators(board || undefined);
    this.academicsForm.patchValue(
      {
        classXiiBoardCode: board,
        classXiiStreamCode: stream,
      },
      { emitEvent: false }
    );

    const fromNew = ac.classXiiSubjects?.filter(
      (r) => (r.subject?.trim() ?? '') || (r.marks?.toString().trim() ?? '') || r.subjectMasterId
    );
    if (fromNew && fromNew.length > 0) {
      this.setClassXiiSubjectsFromDto(fromNew);
    } else if (ac.classXII?.some((r) => (r.subject?.trim() ?? '') || (r.marks?.trim() ?? ''))) {
      this.setClassXiiSubjectsFromLegacy(ac.classXII);
    } else {
      this.resetClassXiiSubjectsArrayToFiveRows();
    }

    this.reloadClassXiiSubjectCatalog();
  }

  private setClassXiiSubjectsFromDto(rows: ClassXiiSubjectRow[]): void {
    const board = (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim();
    while (this.classXiiSubjectsArray.length) {
      this.classXiiSubjectsArray.removeAt(0);
    }
    rows.forEach((r, i) => {
      const g = this.createClassXiiRowGroupShell();
      const mode = this.resolveEntryModeForLoadedRow(r, i, board);
      g.get('entryMode')?.setValue(mode, { emitEvent: false });
      g.patchValue(
        {
          subjectMasterId: r.subjectMasterId?.toString() ?? '',
          subject: r.subject ?? '',
          marks: r.marks ?? '',
        },
        { emitEvent: false }
      );
      this.applyClassXiiRowFieldValidators(g, i);
      this.classXiiSubjectsArray.push(g);
    });
    while (this.classXiiSubjectsArray.length < 5) {
      const i = this.classXiiSubjectsArray.length;
      this.classXiiSubjectsArray.push(this.createClassXiiRowGroupForIndex(i));
    }
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
  }

  private setClassXiiSubjectsFromLegacy(legacy: SubjectMark[]): void {
    const board = (this.academicsForm.get('classXiiBoardCode')?.value ?? '').toString().trim();
    const filled = legacy.filter((x) => (x.subject?.trim() ?? '') || (x.marks?.trim() ?? ''));
    while (this.classXiiSubjectsArray.length) {
      this.classXiiSubjectsArray.removeAt(0);
    }
    filled.forEach((r, i) => {
      const g = this.createClassXiiRowGroupShell();
      const mode = this.resolveEntryModeForLoadedRow(
        { subject: r.subject, marks: r.marks, subjectMasterId: undefined },
        i,
        board
      );
      g.get('entryMode')?.setValue(mode, { emitEvent: false });
      g.patchValue(
        {
          subjectMasterId: '',
          subject: r.subject ?? '',
          marks: r.marks ?? '',
        },
        { emitEvent: false }
      );
      this.applyClassXiiRowFieldValidators(g, i);
      this.classXiiSubjectsArray.push(g);
    });
    while (this.classXiiSubjectsArray.length < 5) {
      const i = this.classXiiSubjectsArray.length;
      this.classXiiSubjectsArray.push(this.createClassXiiRowGroupForIndex(i));
    }
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
  }

  private resetClassXiiSubjectsArrayToFiveRows(): void {
    while (this.classXiiSubjectsArray.length) {
      this.classXiiSubjectsArray.removeAt(0);
    }
    for (let i = 0; i < 5; i++) {
      this.classXiiSubjectsArray.push(this.createClassXiiRowGroupForIndex(i));
    }
    this.classXiiSubjectsArray.updateValueAndValidity({ emitEvent: false });
  }

  private buildClassXiiSubjectsPayload(): ClassXiiSubjectRow[] {
    const out: ClassXiiSubjectRow[] = [];
    for (const ctrl of this.classXiiSubjectsArray.controls) {
      const v = ctrl.getRawValue() as {
        entryMode?: 'master' | 'manual';
        subjectMasterId: string;
        subject: string;
        marks: string | number | null;
      };
      const subject = (v.subject ?? '').toString().trim();
      const marks =
        v.marks === null || v.marks === undefined ? '' : String(v.marks).trim();
      const idTrim = v.subjectMasterId?.toString().trim() ?? '';
      if (!subject && !marks && !idTrim) {
        continue;
      }
      const mode: 'master' | 'manual' = v.entryMode === 'manual' ? 'manual' : 'master';
      let subjectOut = subject;
      if (mode === 'master' && !subjectOut && idTrim) {
        const item = this.subjectsList().find((x) => x.id === idTrim);
        subjectOut = item?.subjectName ?? '';
      }
      const row: ClassXiiSubjectRow = {
        subject: subjectOut,
        marks,
        entryMode: mode,
      };
      if (mode === 'master' && idTrim) {
        row.subjectMasterId = idTrim;
      }
      out.push(row);
    }
    return out;
  }

  getCurrentFormGroup(): FormGroup {
    if (this.currentStepIndex() === this.steps.length) {
      return this.declarationForm;
    }
    return this.steps[this.currentStepIndex()].formGroup;
  }

  private patchForm(draft: ApplicantApplicationDraft): void {
    this.coursesLocked = draft.coursesLocked ?? false;

    const preserveStringField = (
      target: Record<string, unknown>,
      key: string,
      existing: string | null | undefined
    ): void => {
      const current = (target[key] as string | null | undefined) ?? '';
      const currentTrimmed = current?.trim() ?? '';
      const existingTrimmed = existing?.trim() ?? '';
      
      // If draft has empty value and form has a value, preserve form value
      // If draft has a value and form is empty, use draft value
      // If both have values, prefer draft value (it's the saved data)
      if (currentTrimmed === '' && existingTrimmed !== '') {
        // Draft is empty, form has value - preserve form value
        target[key] = existing;
      } else if (currentTrimmed !== '' && existingTrimmed === '') {
        // Draft has value, form is empty - use draft value (already set)
        // No action needed
      } else if (currentTrimmed !== '' && existingTrimmed !== '') {
        // Both have values - prefer draft value (saved data takes precedence)
        // No action needed, draft value is already in target
      }
      // If both are empty, leave as is
    };

    const personal = { ...draft.personalInformation };
    preserveStringField(
      personal,
      'nameAsPerAdmitCard',
      this.personalForm.get('nameAsPerAdmitCard')?.value as string | null
    );
    preserveStringField(
      personal,
      'dateOfBirth',
      this.personalForm.get('dateOfBirth')?.value as string | null
    );
    preserveStringField(
      personal,
      'gender',
      this.personalForm.get('gender')?.value as string | null
    );
    preserveStringField(
      personal,
      'maritalStatus',
      this.personalForm.get('maritalStatus')?.value as string | null
    );
    preserveStringField(
      personal,
      'bloodGroup',
      this.personalForm.get('bloodGroup')?.value as string | null
    );
    preserveStringField(
      personal,
      'category',
      this.personalForm.get('category')?.value as string | null
    );
    preserveStringField(
      personal,
      'raceOrTribe',
      this.personalForm.get('raceOrTribe')?.value as string | null
    );
    preserveStringField(
      personal,
      'religion',
      this.personalForm.get('religion')?.value as string | null
    );
    preserveStringField(
      personal,
      'denomination',
      this.personalForm.get('denomination')?.value as string | null
    );
    personal.dateOfBirth = this.formatDateForDisplay(personal.dateOfBirth);
    
    // Filter out empty strings - only patch fields that have actual values
    const personalToPatch: Partial<PersonalInformation> = {};
    
    if (personal.nameAsPerAdmitCard?.trim()) personalToPatch.nameAsPerAdmitCard = personal.nameAsPerAdmitCard.trim();
    if (personal.dateOfBirth?.trim()) personalToPatch.dateOfBirth = personal.dateOfBirth;
    if (personal.gender?.trim()) personalToPatch.gender = personal.gender.trim();
    if (personal.maritalStatus?.trim()) personalToPatch.maritalStatus = personal.maritalStatus.trim();
    if (personal.bloodGroup?.trim()) personalToPatch.bloodGroup = personal.bloodGroup.trim();
    if (personal.category?.trim()) personalToPatch.category = personal.category.trim();
    if (personal.raceOrTribe?.trim()) personalToPatch.raceOrTribe = personal.raceOrTribe.trim();
    if (personal.religion?.trim()) personalToPatch.religion = personal.religion.trim();
    if ((personal.religion ?? '').trim() === 'Christian') {
      personalToPatch.denomination = (personal.denomination ?? '').toString().trim();
    }
    if (personal.isDifferentlyAbled !== undefined) personalToPatch.isDifferentlyAbled = personal.isDifferentlyAbled;
    if (personal.isEconomicallyWeaker !== undefined) personalToPatch.isEconomicallyWeaker = personal.isEconomicallyWeaker;
    
    this.personalForm.patchValue(personalToPatch, { emitEvent: false });
    this.syncDenominationWithReligion(true);

    const address = { ...draft.address };
    preserveStringField(
      address,
      'email',
      this.addressForm.get('email')?.value as string | null
    );
    // Filter out empty strings - only patch fields that have actual values
    const addressToPatch: Partial<AddressInformation> = {
      sameAsTura: address.sameAsTura ?? false,
    };
    
    if (address.addressInTura?.trim()) addressToPatch['addressInTura'] = address.addressInTura.trim();
    if (address.homeAddress?.trim()) addressToPatch['homeAddress'] = address.homeAddress.trim();
    if (address.aadhaarNumber?.trim()) addressToPatch['aadhaarNumber'] = address.aadhaarNumber.trim();
    if (address.state?.trim()) addressToPatch['state'] = address.state.trim();
    if (address.email?.trim()) addressToPatch['email'] = address.email.trim();
    
    this.addressForm.patchValue(addressToPatch, { emitEvent: false });

    if (this.addressForm.get('sameAsTura')?.value) {
      this.addressForm.get('homeAddress')?.disable({ emitEvent: false });
    } else {
      this.addressForm.get('homeAddress')?.enable({ emitEvent: false });
    }

    this.lockRegistrationFields();
    this.contactsForm.patchValue(draft.contacts);
    const lg = draft.contacts?.localGuardian;
    const hasGuardian = !!(lg && ((lg as { name?: string }).name?.trim() || (lg as { contactNumber1?: string }).contactNumber1?.trim()));
    this.hasLocalGuardian.set(hasGuardian);
    const guardianGroup = this.contactsForm.get('localGuardian') as FormGroup;
    if (!hasGuardian && guardianGroup) {
      guardianGroup.disable({ emitEvent: false });
    } else if (hasGuardian && guardianGroup) {
      guardianGroup.enable({ emitEvent: false });
    }

    this.draftPatchInProgress = true;
    try {
      this.patchClassXiiSection(draft);
    } finally {
      this.draftPatchInProgress = false;
    }
    
    // Patch boardExamination with explicit totalMarks handling
    const boardExaminationForm = this.academicsForm.get('boardExamination') as FormGroup;
    if (boardExaminationForm) {
      const draftTotalMarks = draft.academics.boardExamination.totalMarks;

      const existingFormValue = boardExaminationForm.get('totalMarks')?.value;
      let totalMarksValue = '';
      
      const draftTrimmed = draftTotalMarks?.trim() ?? '';
      const formTrimmed = existingFormValue?.toString().trim() ?? '';
      
      if (draftTrimmed !== '') {
        totalMarksValue = draftTrimmed;
      } else if (formTrimmed !== '') {
        totalMarksValue = formTrimmed;
      } else {
        totalMarksValue = '';
      }
      
      const boardExaminationData: Partial<BoardExaminationDetail> = {};
      
      if (draft.academics.boardExamination.rollNumber?.trim()) {
        boardExaminationData['rollNumber'] = draft.academics.boardExamination.rollNumber.trim();
      }
      if (draft.academics.boardExamination.year?.trim()) {
        boardExaminationData['year'] = draft.academics.boardExamination.year.trim();
      }
      if (totalMarksValue) {
        boardExaminationData['totalMarks'] = totalMarksValue;
      }
      if (draft.academics.boardExamination.percentage?.trim()) {
        boardExaminationData['percentage'] = draft.academics.boardExamination.percentage.trim();
      }
      if (draft.academics.boardExamination.division?.trim()) {
        boardExaminationData['division'] = draft.academics.boardExamination.division.trim();
      }
      if (draft.academics.boardExamination.registrationType?.trim()) {
        boardExaminationData['registrationType'] = draft.academics.boardExamination.registrationType.trim();
      }
      
      // Only patch if we have at least one field with a value
      if (Object.keys(boardExaminationData).length > 0) {
        boardExaminationForm.patchValue(boardExaminationData, { emitEvent: false });
      }
    }
    
    this.academicsForm.patchValue({
      cuet: draft.academics.cuet,
      lastInstitutionAttended: draft.academics.lastInstitutionAttended,
    });
    const draftShift =
       draft.courses.shift || draft.personalInformation.shift || '';
     const normalizedShift = this.toShiftCode(draftShift);

     if (isLegacyShiftIIIRaw(draftShift)) {
       this.toast.show(
         'Shift III is no longer offered. Please select Shift I or Shift II and confirm your course preferences.',
         'info'
       );
     }

     this.coursesForm.patchValue({ shift: normalizedShift }, { emitEvent: false });
     this.updateMajorOptions(normalizedShift, true);
     this.coursesForm.patchValue(
       {
         majorSubject: draft.courses.majorSubject ?? '',
         minorSubject: draft.courses.minorSubject ?? '',
         multidisciplinaryChoice: draft.courses.multidisciplinaryChoice ?? '',
         abilityEnhancementChoice: draft.courses.abilityEnhancementChoice ?? '',
         skillEnhancementChoice: draft.courses.skillEnhancementChoice ?? '',
         valueAddedChoice: draft.courses.valueAddedChoice || DEFAULT_VAC.value,
       },
       { emitEvent: false }
     );
    this.updateMinorOptions(true);
    this.updateSupplementOptions(normalizedShift, true);
    this.uploadsForm.patchValue(this.sanitizeUploadSection(draft.uploads));
    this.declarationForm.patchValue({
      declarationAccepted: draft.declarationAccepted,
    });

    this.coursesForm
      .get('valueAddedChoice')
      ?.setValue(draft.courses.valueAddedChoice || DEFAULT_VAC.value, {
        emitEvent: false,
      });

    this.syncConditionalUploadControls();
    const appliedControl = this.cuetForm.get('applied');
    const isCuetApplied = appliedControl?.value === 'Applied';
    const marksControl = this.cuetForm.get('marks');
    const rollNumberControl = this.cuetForm.get('rollNumber');
    const cuetMarksheetControl = this.uploadsForm.get('cuetMarksheet');
    if (marksControl && rollNumberControl) {
      this.updateCuetFields(isCuetApplied, marksControl, rollNumberControl);
    }
    this.updateCuetMarksheetUpload(isCuetApplied, cuetMarksheetControl);

    // If courses are empty after patching, unlock them even if coursesLocked was true
    // This allows users to fill in courses that were deleted or never saved
    if (this.areCoursesEmpty() && this.coursesLocked) {
      console.warn('Courses are empty but locked. Unlocking to allow user to fill in courses.');
      this.coursesLocked = false;
    }

    this.applyCourseLock();
  }

  private toDraft(): ApplicantApplicationDraft {
    const personalRaw = this.personalForm.getRawValue();
    const religion = (personalRaw.religion ?? '').toString().trim();
    const personal: PersonalInformation = {
      nameAsPerAdmitCard: personalRaw.nameAsPerAdmitCard ?? '',
      dateOfBirth: this.normalizeDateForStorage(personalRaw.dateOfBirth ?? ''),
      gender: personalRaw.gender ?? '',
      maritalStatus: personalRaw.maritalStatus ?? '',
      bloodGroup: personalRaw.bloodGroup ?? '',
      category: personalRaw.category ?? '',
      raceOrTribe: personalRaw.raceOrTribe ?? '',
      religion,
      denomination:
        religion === 'Christian'
          ? (personalRaw.denomination ?? '').toString().trim() || null
          : null,
      isDifferentlyAbled: Boolean(personalRaw.isDifferentlyAbled),
      isEconomicallyWeaker: Boolean(personalRaw.isEconomicallyWeaker),
    };

    const addressRaw = this.addressForm.getRawValue();
    const address: AddressInformation = {
      addressInTura: addressRaw.addressInTura ?? '',
      homeAddress: addressRaw.homeAddress ?? '',
      sameAsTura: Boolean(addressRaw.sameAsTura),
      aadhaarNumber: addressRaw.aadhaarNumber ?? '',
      state: addressRaw.state ?? '',
      email: addressRaw.email ?? '',
    };

    const contactsRaw = this.contactsForm.getRawValue();
    const toGuardian = (raw: Record<string, unknown> | undefined): ParentOrGuardian => ({
      name: (raw?.['name'] as string) ?? '',
      age: (raw?.['age'] as string) ?? '',
      occupation: (raw?.['occupation'] as string) ?? '',
      contactNumber1: (raw?.['contactNumber1'] as string) ?? '',
    });
    const contacts: ContactInformation = {
      father: toGuardian(contactsRaw.father as Record<string, unknown> | undefined),
      mother: toGuardian(contactsRaw.mother as Record<string, unknown> | undefined),
      localGuardian: toGuardian(
        contactsRaw.localGuardian as Record<string, unknown> | undefined
      ),
      householdAreaType: (contactsRaw.householdAreaType as 'Urban' | 'Rural' | '') ?? '',
    };

    const boardExaminationGroup = this.academicsForm.get('boardExamination') as FormGroup | null;
    const cuetGroup = this.academicsForm.get('cuet') as FormGroup | null;
    const lastInstitutionControl = this.academicsForm.get('lastInstitutionAttended');
    const declarationControl = this.declarationForm.get('declarationAccepted');

    // Get raw value and ensure totalMarks is included
    const boardRaw = boardExaminationGroup?.getRawValue() ?? this.defaultBoardExamination();
    // Also try to get totalMarks directly from the control if it's missing
    const totalMarksControl = boardExaminationGroup?.get('totalMarks');
    
    // Try multiple sources to get totalMarks value:
    // 1. Direct from form control value
    // 2. From raw value
    // 3. From DOM input element (as last resort)
    let totalMarksValue = '';
    
    // First, try form control value
    if (totalMarksControl) {
      const controlValue = totalMarksControl.value;
      if (controlValue !== null && controlValue !== undefined && controlValue !== '') {
        totalMarksValue = controlValue.toString().trim();
      }
    }
    
    // If still empty, try raw value
    if (!totalMarksValue && boardRaw.totalMarks) {
      const rawValue = boardRaw.totalMarks.toString().trim();
      if (rawValue) {
        totalMarksValue = rawValue;
      }
    }
    
    // If still empty, try reading from DOM input element directly
    if (!totalMarksValue) {
      const totalMarksInput = document.querySelector('input[formControlName="totalMarks"]') as HTMLInputElement;
      if (totalMarksInput && totalMarksInput.value) {
        const inputValue = totalMarksInput.value.trim();
        if (inputValue) {
          totalMarksValue = inputValue;
          // Also update the form control for consistency
          if (totalMarksControl) {
            totalMarksControl.setValue(inputValue, { emitEvent: false });
          }
        }
      }
    }
    
    const boardExamination: BoardExaminationDetail = {
      rollNumber: boardRaw.rollNumber ?? '',
      year: boardRaw.year ?? '',
      // Always include totalMarks in the payload, even if empty (to ensure it's saved)
      totalMarks: totalMarksValue || '',
      percentage: boardRaw.percentage ?? '',
      division: boardRaw.division ?? '',
      boardName: this.resolveBoardExaminationBoardName(),
      registrationType: boardRaw.registrationType ?? '',
    };

    const cuetRaw = cuetGroup?.getRawValue() ?? this.defaultCuet();
    const cuet: CuetDetail = {
      marks: cuetRaw.marks ?? '',
      rollNumber: cuetRaw.rollNumber ?? '',
    };

    const coursesRaw = this.coursesForm.getRawValue();
     const shiftCode = this.toShiftCode(coursesRaw.shift);
     const courses: CoursePreferences = {
       shift: shiftCode || '',
       majorSubject: coursesRaw.majorSubject ?? '',
       minorSubject: coursesRaw.minorSubject ?? '',
       multidisciplinaryChoice: coursesRaw.multidisciplinaryChoice ?? '',
       abilityEnhancementChoice: coursesRaw.abilityEnhancementChoice ?? '',
       skillEnhancementChoice: coursesRaw.skillEnhancementChoice ?? '',
       valueAddedChoice: coursesRaw.valueAddedChoice ?? DEFAULT_VAC.value,
     };

    const uploads = this.uploadsForm.getRawValue() as UploadSection;

    const classXiiSubjects = this.buildClassXiiSubjectsPayload();
    const classXII: SubjectMark[] = classXiiSubjects.map((r) => ({
      subject: r.subject,
      marks: r.marks,
    }));
    const acRaw = this.academicsForm.getRawValue() as {
      classXiiBoardCode?: string;
      classXiiStreamCode?: string;
    };
    const boardCode = (acRaw.classXiiBoardCode ?? '').toString().trim();
    const streamRaw = (acRaw.classXiiStreamCode ?? '').toString().trim();
    const academics: AcademicInformation = {
      classXiiBoardCode: boardCode || undefined,
      classXiiStreamCode: boardCode === 'OTHER' ? undefined : streamRaw || undefined,
      classXiiSubjects,
      classXII,
      boardExamination,
      cuet,
      lastInstitutionAttended: (lastInstitutionControl?.value as string | null) ?? '',
    };

    return {
      personalInformation: personal,
      address,
      contacts,
      academics,
      courses,
      uploads,
      declarationAccepted: Boolean(declarationControl?.value),
      coursesLocked: this.coursesLocked,
    };
  }

  private formatDateForDisplay(value: string | undefined): string {
    if (!value) {
      return '';
    }
    if (value.includes('/')) {
      return value;
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    const day = date.getUTCDate().toString().padStart(2, '0');
    const month = (date.getUTCMonth() + 1).toString().padStart(2, '0');
    const year = date.getUTCFullYear();
    return `${day}/${month}/${year}`;
  }

  private normalizeDateForStorage(value: string): string {
    const trimmed = value.trim();
    if (!trimmed) {
      return '';
    }
    if (trimmed.includes('-')) {
      return trimmed;
    }
    const match =
      /^(0[1-9]|[12][0-9]|3[01])\/(0[1-9]|1[0-2])\/(19|20)\d{2}$/.exec(trimmed);
    if (!match) {
      return trimmed;
    }
    const [, day, month, year] = match;
    return `${year}-${month}-${day}`;
  }

  private defaultBoardExamination(): BoardExaminationDetail {
    return {
      rollNumber: '',
      year: '',
      totalMarks: '',
      percentage: '',
      division: '',
      boardName: '',
      registrationType: '',
    };
  }

  private defaultCuet(): CuetDetail {
    return {
      marks: '',
      rollNumber: '',
    };
  }

  private registerNavigation(): void {
    const steps: ApplicantApplicationStep[] = this.steps.map((step, index) => ({
      index,
      title: step.title,
      description: step.description,
    }));

    steps.push({
      index: this.declarationStepIndex,
      title: 'Declaration',
      description: 'Review and submit',
    });

    // Preserve current step index before registering
    const currentIndex = this.navigation.currentIndex();
    const hasExistingSteps = this.navigation.steps().length > 0;
    
    // Only register if steps haven't been registered yet, or if the step count changed
    const existingStepsCount = this.navigation.steps().length;
    if (hasExistingSteps && existingStepsCount === steps.length) {
      // Steps already registered with same count, just preserve current step
      if (currentIndex > 0 && currentIndex <= steps.length) {
        this.navigation.setCurrentIndex(currentIndex);
      }
      return;
    }
    
    this.navigation.registerSteps(steps);
    
    // Only reset to step 0 if this is the first time registering (no existing steps)
    // Otherwise, preserve the current step or clamp it to valid range
    if (!hasExistingSteps) {
      this.navigation.setCurrentIndex(0);
    } else if (currentIndex > 0 && currentIndex <= steps.length) {
      // Preserve the current step if it's valid
      this.navigation.setCurrentIndex(currentIndex);
    }
  }

  downloadSubmittedPdf(): void {
    if (!this.submittedPdfUrl) {
      return;
    }

    // Check if payment is completed
    const status = this.paymentStatus();
    if (!status?.isPaymentCompleted) {
      this.toast.show('Please complete the payment before downloading the form.', 'info');
      return;
    }

    const link = document.createElement('a');
    link.href = this.submittedPdfUrl;
    link.download = this.submittedPdfFileName ?? 'admission-application.pdf';
    link.click();
  }

  private setSubmissionPdf(result: ApplicantApplicationSubmitResult): void {
    this.revokeSubmittedPdf();
    this.submittedPdfUrl = URL.createObjectURL(result.blob);
    this.submittedPdfFileName = result.fileName;
  }

  private revokeSubmittedPdf(): void {
    if (this.submittedPdfUrl) {
      URL.revokeObjectURL(this.submittedPdfUrl);
      this.submittedPdfUrl = null;
    }
  }

  private lockRegistrationFields(): void {
    const lock = (form: FormGroup, controlName: string) => {
      const control = form.get(controlName);
      if (!control) {
        return;
      }
      const value = control.value;
      if (value !== null && value !== undefined && `${value}`.trim() !== '') {
        control.disable({ emitEvent: false });
      }
    };

    lock(this.personalForm, 'nameAsPerAdmitCard');
    lock(this.personalForm, 'dateOfBirth');
    lock(this.personalForm, 'gender');
    lock(this.personalForm, 'maritalStatus');
    lock(this.personalForm, 'bloodGroup');
    lock(this.personalForm, 'category');
    lock(this.personalForm, 'raceOrTribe');
    // Religion & denomination stay editable so users can correct demographics from the list after draft save.
    // isDifferentlyAbled and isEconomicallyWeaker should remain editable
    // Email stays editable (correspondence may differ from login account)
  }

  private setupPersonalInformationEffects(): void {
    // Track if we've already patched from dashboard once
    let hasPatchedFromDashboard = false;
    
    effect(() => {
      const dashboard = this.portalStore.dashboard();
      if (!dashboard) {
        return;
      }

      // CRITICAL: Only patch ONCE on initial load
      // After that, never patch again - let the user fill the form without interference
      if (hasPatchedFromDashboard) {
        return;
      }

      // Only patch if form fields are completely empty (initial load)
      const nameValue = this.personalForm.get('nameAsPerAdmitCard')?.value;
      const emailValue = this.addressForm.get('email')?.value;
      
      // CRITICAL: If form already has ANY data, don't patch and mark as patched
      // This prevents the effect from running again when dashboard refreshes
      if (nameValue || emailValue) {
        hasPatchedFromDashboard = true; // Mark as patched to prevent future runs
        return;
      }

      // Only patch on initial load when form is empty
      this.personalForm.patchValue({
        nameAsPerAdmitCard: dashboard.profile.fullName,
        dateOfBirth: this.formatDateForDisplay(dashboard.profile.dateOfBirth),
        gender: dashboard.profile.gender,
      }, { emitEvent: false });

      this.addressForm.patchValue({
        email: dashboard.profile.email,
      }, { emitEvent: false });

      const shiftControl = this.coursesForm.get('shift');
      if (shiftControl && !shiftControl.value) {
        const rawProf = dashboard.profile.shift ?? '';
        if (isLegacyShiftIIIRaw(rawProf)) {
          this.toast.show(
            'Shift III is no longer offered. Please select Shift I or Shift II and confirm your course preferences.',
            'info'
          );
        }
        const normalizedShift = this.toShiftCode(rawProf);
        if (normalizedShift) {
          shiftControl.patchValue(normalizedShift, { emitEvent: false });
          this.updateMajorOptions(normalizedShift, true);
          this.updateMinorOptions(true);
        }
      }
      
      hasPatchedFromDashboard = true;
    });
  }

  private setupCoursePreferenceEffects(): void {
    const shiftControl = this.coursesForm.get('shift');
    const majorControl = this.coursesForm.get('majorSubject');

    shiftControl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => {
      this.updateMajorOptions(value, false);
      this.updateMinorOptions(false);
      this.updateSupplementOptions(this.toShiftCode(value), false);
    });

    majorControl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.updateMinorOptions(false);
      this.refreshMdcEligibilityFromCurrentShift();
    });

    this.academicsForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshMdcEligibilityFromCurrentShift());
  }

  private setupAddressSyncEffect(): void {
    this.addressForm
      .get('sameAsTura')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((isSame) => {
        const turaAddress = this.addressForm.get('addressInTura')?.value ?? '';

        if (isSame) {
          this.addressForm.get('homeAddress')?.setValue(turaAddress);
          this.addressForm.get('homeAddress')?.disable({ emitEvent: false });
        } else {
          this.addressForm.get('homeAddress')?.enable({ emitEvent: false });
        }
      });

    this.addressForm
      .get('addressInTura')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        if (this.addressForm.get('sameAsTura')?.value) {
          this.addressForm.get('homeAddress')?.setValue(value ?? '', {
            emitEvent: false,
          });
        }
      });
  }

  private attachDraftEffects(): void {
    // Track if we've already patched the form once
    let hasPatchedFromDraft = false;
    let isInitializing = true;
    
    effect(() => {
      // Don't patch form if we're currently saving (to prevent overwriting user input)
      if (this.isSavingDraft) {
        return;
      }
      
      const draft = this.applicationStore.draft();
      if (!draft) {
        // If no draft and we're past initialization, mark as done
        if (!isInitializing) {
          hasPatchedFromDraft = true;
        }
        return;
      }

      // CRITICAL: Only patch form ONCE on initial load
      // After that, never patch again - let the user fill the form without interference
      if (hasPatchedFromDraft) {
        return;
      }

      // Only patch if form is completely empty (initial load)
      // Check multiple fields to ensure form is truly empty
      const hasAnyData = 
        (this.personalForm.get('nameAsPerAdmitCard')?.value?.trim() ?? '') ||
        (this.addressForm.get('email')?.value?.trim() ?? '') ||
        (this.contactsForm.get('father.name')?.value?.trim() ?? '') ||
        (this.academicsForm.get('boardExamination.rollNumber')?.value?.trim() ?? '') ||
        (this.coursesForm.get('shift')?.value?.trim() ?? '');
      
      // If form has any data, don't patch (user might be actively filling)
      if (hasAnyData) {
        hasPatchedFromDraft = true; // Mark as patched to prevent future attempts
        isInitializing = false;
        return;
      }

      // Only patch on initial load when form is empty
      // Use setTimeout to ensure this runs after ngOnInit has completed
      if (isInitializing) {
        setTimeout(() => {
          if (!hasPatchedFromDraft && !hasAnyData) {
            console.log('Effect: Patching form from draft');
            this.patchForm(draft);
            hasPatchedFromDraft = true;
          }
          isInitializing = false;
        }, 100);
      }
    });
  }

  private applyCourseLock(): void {
    const controls = [
      'shift',
      'majorSubject',
      'minorSubject',
      'multidisciplinaryChoice',
      'abilityEnhancementChoice',
      'skillEnhancementChoice',
    ];

    // Check if courses are empty - if so, allow editing even if locked
    const areCoursesEmpty = this.areCoursesEmpty();
    
    if (this.coursesLocked && !areCoursesEmpty) {
      // Lock courses only if they are not empty
      controls.forEach((name) => this.coursesForm.get(name)?.disable({ emitEvent: false }));
    } else {
      // Enable courses if:
      // 1. Not locked, OR
      // 2. Locked but courses are empty (allow user to fill in missing data)
      controls.forEach((name) => this.coursesForm.get(name)?.enable({ emitEvent: false }));
      this.updateMajorOptions(this.coursesForm.get('shift')?.value, true);
    }
  }

  /**
   * Check if all course selections are empty
   * If courses are empty, user should be able to edit even if coursesLocked is true
   */
  private areCoursesEmpty(): boolean {
    const major = (this.coursesForm.get('majorSubject')?.value as string)?.trim() ?? '';
    const minor = (this.coursesForm.get('minorSubject')?.value as string)?.trim() ?? '';
    const mdc = (this.coursesForm.get('multidisciplinaryChoice')?.value as string)?.trim() ?? '';
    const aec = (this.coursesForm.get('abilityEnhancementChoice')?.value as string)?.trim() ?? '';
    const sec = (this.coursesForm.get('skillEnhancementChoice')?.value as string)?.trim() ?? '';
    
    // Consider courses empty if all required fields are empty
    return !major && !minor && !mdc && !aec && !sec;
  }

  private readonly uploadFieldLabels: Record<keyof UploadSection, string> = {
    stdXMarksheet: 'STD X Marksheet',
    stdXIIMarksheet: 'STD XII Marksheet',
    cuetMarksheet: 'CUET Marksheet',
    differentlyAbledProof: 'Disability Certificate',
    economicallyWeakerProof: 'Economically Weaker Section Proof',
  };

  private getCurrentFormErrors(): string[] {
    const currentGroup = this.getCurrentFormGroup();
    if (currentGroup === this.personalForm) {
      return this.collectErrorsFromGroup(this.personalForm, 'Personal Information');
    }
    if (currentGroup === this.addressForm) {
      return this.collectErrorsFromGroup(this.addressForm, 'Address & Identity');
    }
    if (currentGroup === this.contactsForm) {
      return this.collectErrorsFromGroup(this.contactsForm, 'Family & Guardian');
    }
    if (currentGroup === this.academicsForm) {
      return this.collectErrorsFromGroup(this.academicsForm, 'Academic Records');
    }
    if (currentGroup === this.coursesForm) {
      return this.collectErrorsFromGroup(this.coursesForm, 'Course Preferences');
    }
    if (currentGroup === this.uploadsForm) {
      return this.collectErrorsFromGroup(this.uploadsForm, 'Uploads');
    }
    return [];
  }

  /** Inline + summary copy for upload controls (file inputs are not bound with formControlName). */
  uploadControlInvalid(controlName: keyof UploadSection): boolean {
    const c = this.uploadsForm.get(controlName);
    return !!(c && !c.disabled && c.invalid && c.touched);
  }

  uploadControlErrorText(controlName: keyof UploadSection): string | null {
    const c = this.uploadsForm.get(controlName);
    if (!c || c.disabled || !c.invalid || !c.touched) {
      return null;
    }
    const label = this.uploadFieldLabels[controlName];
    const empty = c.errors?.['attachmentEmpty'] as { message?: string } | undefined;
    if (empty?.message) {
      return empty.message;
    }
    if (c.errors?.['required']) {
      return `${label} is required. Choose a JPEG, PNG, or PDF (max 5 MB).`;
    }
    return `${label} is invalid.`;
  }

  /** Shown under the uploads step heading when validation failed (markAllAsTouched). */
  uploadStepErrorSummary(): string[] {
    if (!this.uploadsForm.touched || this.uploadsForm.valid) {
      return [];
    }
    return this.collectErrorsFromGroup(this.uploadsForm, 'Uploads');
  }

  private collectErrorsFromGroup(group: FormGroup, sectionLabel: string): string[] {
    const errors: string[] = [];

    const grpErr = group.errors?.['masterPickRequired'] as { message?: string } | undefined;
    if (grpErr?.message) {
      errors.push(grpErr.message);
    }
    const manualSubErr = group.errors?.['manualSubjectRequired'] as { message?: string } | undefined;
    if (manualSubErr?.message) {
      errors.push(manualSubErr.message);
    }
    const manualMarksErr = group.errors?.['manualMarksRequired'] as { message?: string } | undefined;
    if (manualMarksErr?.message) {
      errors.push(manualMarksErr.message);
    }

    Object.entries(group.controls).forEach(([key, control]) => {
      if (control instanceof FormGroup) {
        errors.push(
          ...this.collectErrorsFromGroup(control, `${sectionLabel} (${this.toTitleCase(key)})`)
        );
        return;
      }

      if (control instanceof FormArray) {
        const minErr = control.errors?.['classXiiMinSubjects'] as { message?: string } | undefined;
        if (minErr?.message) {
          errors.push(minErr.message);
        }
        control.controls.forEach((child, index) => {
          if (child instanceof FormGroup) {
            errors.push(
              ...this.collectErrorsFromGroup(
                child,
                `${sectionLabel} — Class XII subject row ${index + 1}`
              )
            );
          } else if (child.invalid) {
            errors.push(`${sectionLabel}: Row ${index + 1} is incomplete.`);
          }
        });
        return;
      }

      if (control.disabled) {
        return;
      }

      if (!control.invalid) {
        return;
      }

      errors.push(...this.leafControlErrorMessages(control, key, sectionLabel));
    });

    return errors;
  }

  private leafControlErrorMessages(
    control: AbstractControl,
    fieldKey: string,
    sectionLabel: string
  ): string[] {
    const errs = control.errors;
    if (!errs) {
      return [`${this.fieldLabelForKey(fieldKey, sectionLabel)} is invalid.`];
    }

    const label = this.fieldLabelForKey(fieldKey, sectionLabel);

    if (errs['required']) {
      if (fieldKey === 'declarationAccepted') {
        return ['You must accept the declaration to submit.'];
      }
      if (this.uploadFieldLabels[fieldKey as keyof UploadSection]) {
        return [`${label} is required. Choose a file (JPEG, PNG, or PDF).`];
      }
      return [`${label} is required.`];
    }
    if (errs['requiredTrue']) {
      return [`${label} is required.`];
    }

    const customKeys = [
      'aadhaarInvalid',
      'nameInvalid',
      'ageInvalid',
      'occupationInvalid',
      'contactInvalid',
      'numericInvalid',
      'percentageInvalid',
      'alphanumericInvalid',
      'subjectInvalid',
      'marksInvalid',
      'classXiiMarksInvalid',
      'attachmentEmpty',
      'mdcIneligible',
    ] as const;
    for (const k of customKeys) {
      const err = errs[k] as { message?: string } | undefined;
      if (err?.message) {
        return [err.message];
      }
    }

    return [`${label} is invalid.`];
  }

  private fieldLabelForKey(fieldKey: string, sectionLabel: string): string {
    if (this.uploadFieldLabels[fieldKey as keyof UploadSection]) {
      return this.uploadFieldLabels[fieldKey as keyof UploadSection];
    }
    return this.toTitleCase(fieldKey);
  }

  private scrollFirstValidationAnchorForGroup(group: FormGroup): void {
    const order = this.validationAnchorFieldOrder(group);
    requestAnimationFrame(() => {
      for (const field of order) {
        const c = group.get(field);
        if (c && !c.disabled && c.invalid) {
          const el = document.querySelector(`[data-validation-anchor="${field}"]`);
          el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
          const focusEl =
            (el as HTMLElement | null)?.querySelector<HTMLElement>(
              'input,select,textarea,button:not([disabled])'
            ) ?? (el as HTMLElement | null);
          focusEl?.focus({ preventScroll: true });
          return;
        }
      }
      const root = document.querySelector('.application__form-card');
      const firstInvalid = root?.querySelector<HTMLElement>(
        '.ng-invalid[formControlName], select.ng-invalid, textarea.ng-invalid, input.ng-invalid:not([type="file"])'
      );
      firstInvalid?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      firstInvalid?.focus({ preventScroll: true });
    });
  }

  /** Breadth-first field names matching template `data-validation-anchor` order per step. */
  private validationAnchorFieldOrder(group: FormGroup): string[] {
    if (group === this.uploadsForm) {
      return [
        'stdXMarksheet',
        'stdXIIMarksheet',
        'cuetMarksheet',
        'differentlyAbledProof',
        'economicallyWeakerProof',
      ];
    }
    return Object.keys(group.controls);
  }

  private toTitleCase(value: string): string {
    return value
      .replace(/([A-Z])/g, ' $1')
      .replace(/_/g, ' ')
      .replace(/\s+/g, ' ')
      .replace(/^\w|\s\w/g, (match) => match.toUpperCase())
      .trim();
  }
}

