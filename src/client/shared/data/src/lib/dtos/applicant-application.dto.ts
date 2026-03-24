export interface ApplicantApplicationDraft {
  personalInformation: PersonalInformation;
  address: AddressInformation;
  contacts: ContactInformation;
  academics: AcademicInformation;
  courses: CoursePreferences;
  uploads: UploadSection;
  declarationAccepted: boolean;
  coursesLocked: boolean;
}

export interface PersonalInformation {
  nameAsPerAdmitCard: string;
  dateOfBirth: string;
  gender: string;
  shift?: string;
  maritalStatus: string;
  bloodGroup: string;
  category: string;
  raceOrTribe: string;
  religion: string;
  /** Set when `religion` is Christian; omit or null for other religions. */
  denomination?: string | null;
  isDifferentlyAbled: boolean;
  isEconomicallyWeaker: boolean;
}

export interface AddressInformation {
  addressInTura: string;
  homeAddress: string;
  sameAsTura: boolean;
  aadhaarNumber: string;
  state: string;
  email: string;
}

export interface ContactInformation {
  father: ParentOrGuardian;
  mother: ParentOrGuardian;
  localGuardian: ParentOrGuardian;
  householdAreaType: 'Urban' | 'Rural' | '';
}

export interface ParentOrGuardian {
  name: string;
  age: string;
  occupation: string;
  contactNumber1: string;
}

export interface AcademicInformation {
  /** MBOSE | CBSE | ISC | OTHER */
  classXiiBoardCode?: string;
  /** ARTS | SCIENCE | COMMERCE — not used when board is OTHER */
  classXiiStreamCode?: string;
  /** Canonical rows; server syncs legacy `classXII` from this on save. */
  classXiiSubjects?: ClassXiiSubjectRow[];
  /** Legacy; may still be returned for old drafts — prefer `classXiiSubjects`. */
  classXII: SubjectMark[];
  boardExamination: BoardExaminationDetail;
  cuet: CuetDetail;
  lastInstitutionAttended: string;
}

export interface ClassXiiSubjectRow {
  subject: string;
  marks: string;
  subjectMasterId?: string | null;
  /** Server expects `master` (catalog) or `manual` (OTHER / free text). */
  entryMode?: 'master' | 'manual' | string;
}

export interface SubjectMark {
  subject: string;
  marks: string;
}

/** Response item from GET /api/admissions/class-xii-subjects */
export interface ClassXiiSubjectOptionDto {
  id: string;
  subjectName: string;
  sortOrder: number;
}

export interface BoardExaminationDetail {
  rollNumber: string;
  year: string;
  totalMarks?: string;
  percentage: string;
  division: string;
  boardName: string;
  registrationType: string;
}

export interface CuetDetail {
  marks: string;
  rollNumber: string;
}

export interface CoursePreferences {
  shift: string;
  majorSubject: string;
  minorSubject: string;
  multidisciplinaryChoice: string;
  abilityEnhancementChoice: string;
  skillEnhancementChoice: string;
  valueAddedChoice: string;
}

export interface UploadSection {
  stdXMarksheet: FileAttachment | null;
  stdXIIMarksheet: FileAttachment | null;
  cuetMarksheet: FileAttachment | null;
  differentlyAbledProof: FileAttachment | null;
  economicallyWeakerProof: FileAttachment | null;
}

export interface FileAttachment {
  fileName: string;
  contentType: string;
  data: string;
}

export interface ApplicantApplicationDraftResponse {
  payload: ApplicantApplicationDraft;
  updatedOnUtc: string;
}

/**
 * Strips base64 file data for POST /submit so the HTTP body stays small (files already live in the server draft).
 * The API merges binaries from the stored draft before generating the PDF.
 */
export function draftForSubmitRequest(payload: ApplicantApplicationDraft): ApplicantApplicationDraft {
  const strip = (a: FileAttachment | null): FileAttachment | null => {
    if (!a) {
      return null;
    }
    return {
      fileName: a.fileName ?? '',
      contentType: a.contentType || 'application/octet-stream',
      data: '',
    };
  };
  return {
    ...payload,
    uploads: {
      stdXMarksheet: strip(payload.uploads.stdXMarksheet),
      stdXIIMarksheet: strip(payload.uploads.stdXIIMarksheet),
      cuetMarksheet: strip(payload.uploads.cuetMarksheet),
      differentlyAbledProof: strip(payload.uploads.differentlyAbledProof),
      economicallyWeakerProof: strip(payload.uploads.economicallyWeakerProof),
    },
  };
}

