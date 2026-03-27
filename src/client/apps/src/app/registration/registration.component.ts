import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  Validators,
  FormBuilder,
  AbstractControl,
  ValidatorFn,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { HttpErrorResponse } from '@angular/common/http';
import {
  RegisterStudentApplicantRequest,
  RegistrationApiService,
  RegisterStudentApplicantResponse,
} from '@client/shared/data';

interface RegistrationSuccess {
  uniqueId: string;
  temporaryPassword: string;
}

@Component({
  selector: 'app-registration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './registration.component.html',
  styleUrls: ['./registration.component.scss'],
})
export class RegistrationComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(RegistrationApiService);
  private readonly router = inject(Router);

  readonly genderOptions = ['Female', 'Male', 'Other', 'Prefer not to say'];

  photoPreview: string | null = null;
  photoError: string | null = null;

  isSubmitting = false;
  submitError: string | null = null;
  success: RegistrationSuccess | null = null;

  /** Mobile: instructions accordion expanded state */
  instructionsExpanded = false;

  /** Drag-over state for profile photo drop zone */
  photoDropZoneActive = false;

  /** India mobile: 10 digits, first digit 6–9. */
  private static readonly indiaMobilePattern = /^[6-9]\d{9}$/;
  /** Practical email format (RFC-style, not exhaustive). */
  private static readonly emailPattern =
    /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
  /** Name: letters and spaces; optional apostrophe, hyphen, dot within name (min 3 chars). */
  private static readonly fullNamePattern = /^[a-zA-Z][a-zA-Z\s.'-]{2,}$/;

  readonly form = this.fb.nonNullable.group({
    fullName: [
      '',
      [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(256),
        Validators.pattern(RegistrationComponent.fullNamePattern),
      ],
    ],
    dateOfBirth: [
      '',
      [Validators.required, this.dateFormatValidator(), this.pastDateValidator()],
    ],
    gender: ['', Validators.required],
    email: [
      '',
      [
        Validators.required,
        Validators.maxLength(256),
        Validators.pattern(RegistrationComponent.emailPattern),
      ],
    ],
    mobileNumber: [
      '',
      [Validators.required, Validators.pattern(RegistrationComponent.indiaMobilePattern)],
    ],
    profilePhoto: [null as File | null],
    agreedToTerms: [false, Validators.requiredTrue],
  });

  submit(): void {
    this.clearDuplicateError('email');
    this.clearDuplicateError('mobileNumber');
    this.success = null;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.submitError =
        'Please fix the highlighted fields above (scroll up if needed), then try again.';
      return;
    }

    const photo = this.form.get('profilePhoto')?.value as File | null;
    if (!photo) {
      this.photoError = 'Please upload a passport-style photograph (JPEG or PNG, max 2 MB).';
      this.submitError =
        'A profile photo is required. Choose a JPEG or PNG file (max 2 MB) in the upload area.';
      return;
    }

    this.isSubmitting = true;
    this.submitError = null;

    let payload: RegisterStudentApplicantRequest;
    try {
      payload = this.mapFormToPayload();
    } catch {
      this.isSubmitting = false;
      this.submitError = 'Date of birth is invalid. Use mm/dd/yyyy with a real calendar date.';
      return;
    }

    this.api
      .registerApplicant(payload, photo)
      .pipe(finalize(() => (this.isSubmitting = false)))
      .subscribe({
        next: (response: RegisterStudentApplicantResponse) => {
          this.success = {
            uniqueId: response.uniqueId,
            temporaryPassword: response.temporaryPassword,
          };
          this.form.reset();
          this.form.patchValue({ agreedToTerms: false });
          this.clearPhotoSelection();
          this.photoError = null;
        },
        error: (error: unknown) => {
          this.success = null;
          if (error instanceof HttpErrorResponse) {
            const message =
              (error.error as { message?: string })?.message ??
              'We were unable to process your registration at this time. Please try again in a few minutes.';

            if (error.status === 409) {
              this.submitError = message;
              if (message.toLowerCase().includes('email')) {
                this.setDuplicateError('email');
              }
              if (message.toLowerCase().includes('mobile')) {
                this.setDuplicateError('mobileNumber');
              }
              return;
            }

            this.submitError = message;
            return;
          }

          this.submitError =
            'We were unable to process your registration at this time. Please try again in a few minutes.';
        },
      });
  }

  onDateInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const control = this.form.get('dateOfBirth');
    if (!control) {
      return;
    }

    const digits = input.value.replace(/\D/g, '').slice(0, 8);
    let formatted = '';

    if (digits.length > 0) {
      formatted = digits.slice(0, 2);
    }
    if (digits.length >= 3) {
      formatted += '/' + digits.slice(2, 4);
    } else if (digits.length > 2) {
      formatted += '/' + digits.slice(2);
    }
    if (digits.length >= 5) {
      formatted += '/' + digits.slice(4, 8);
    } else if (digits.length > 4) {
      formatted += '/' + digits.slice(4);
    }

    control.setValue(formatted, { emitEvent: false });
    control.markAsDirty();
    control.updateValueAndValidity({ emitEvent: false });

    const caretPosition = this.calculateDateCaretPosition(input.selectionStart ?? formatted.length, formatted);
    requestAnimationFrame(() => {
      input.selectionStart = input.selectionEnd = caretPosition;
    });
  }

  onMobileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const control = this.form.get('mobileNumber');
    if (!control) {
      return;
    }

    const selectionStart = input.selectionStart ?? input.value.length;
    const digits = input.value.replace(/\D/g, '').slice(0, 10);

    control.setValue(digits, { emitEvent: false });
    control.markAsDirty();
    control.updateValueAndValidity({ emitEvent: false });

    const caret = Math.min(selectionStart, digits.length);
    requestAnimationFrame(() => {
      input.selectionStart = input.selectionEnd = caret;
    });
  }

  private calculateDateCaretPosition(currentPosition: number, formatted: string): number {
    return Math.min(currentPosition, formatted.length);
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  private clearDuplicateError(controlName: 'email' | 'mobileNumber'): void {
    const control = this.form.get(controlName);
    if (control?.errors?.['duplicate']) {
      const rest = { ...control.errors };
      delete rest['duplicate'];
      control.setErrors(Object.keys(rest).length ? rest : null);
    }
  }

  private setDuplicateError(controlName: 'email' | 'mobileNumber'): void {
    const control = this.form.get(controlName);
    if (!control) {
      return;
    }
    const existingErrors = control.errors ?? {};
    control.setErrors({ ...existingErrors, duplicate: true });
    control.markAsTouched();
  }

  private dateFormatValidator(): ValidatorFn {
    const regex =
      /^(0[1-9]|1[0-2])\/(0[1-9]|[12][0-9]|3[01])\/(19|20)\d{2}$/;
    return (control: AbstractControl): { [key: string]: unknown } | null => {
      const value = (control.value ?? '').toString().trim();
      if (!value) {
        return null;
      }
      return regex.test(value) ? null : { invalidFormat: true };
    };
  }

  private pastDateValidator(): ValidatorFn {
    return (control: AbstractControl): { [key: string]: unknown } | null => {
      const iso = this.normalizeToIsoDate(control.value ?? '');
      if (!iso) {
        return null;
      }
      const date = new Date(iso);
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      return date > today ? { futureDate: true } : null;
    };
  }

  private normalizeToIsoDate(value: string): string | null {
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }

    const asIsoMatch =
      /^(?<year>(19|20)\d{2})-(?<month>0[1-9]|1[0-2])-(?<day>0[1-9]|[12][0-9]|3[01])$/.exec(
        trimmed
      );
    if (asIsoMatch?.groups) {
      const { year, month, day } = asIsoMatch.groups;
      return this.validateCalendarDate(Number(year), Number(month), Number(day))
        ? `${year}-${month}-${day}`
        : null;
    }

    const asUsMatch =
      /^(?<month>0[1-9]|1[0-2])\/(?<day>0[1-9]|[12][0-9]|3[01])\/(?<year>(19|20)\d{2})$/.exec(
        trimmed
      );
    if (asUsMatch?.groups) {
      const { year, month, day } = asUsMatch.groups;
      return this.validateCalendarDate(Number(year), Number(month), Number(day))
        ? `${year}-${month}-${day}`
        : null;
    }

    return null;
  }

  openCalendar(nativeInput: HTMLInputElement): void {
    const current = this.form.get('dateOfBirth')?.value ?? '';
    const iso = this.normalizeToIsoDate(current);
    nativeInput.value = iso ?? '';
    const picker = nativeInput as HTMLInputElement & { showPicker?: () => void };
    if (typeof picker.showPicker === 'function') {
      picker.showPicker();
    } else {
      nativeInput.click();
    }
  }

  applyNativeDate(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.value) {
      return;
    }
    const formatted = this.formatIsoToDisplay(input.value);
    const control = this.form.get('dateOfBirth');
    control?.setValue(formatted);
    control?.markAsDirty();
    control?.updateValueAndValidity();
  }

  private formatIsoToDisplay(value: string): string {
    if (!value) {
      return '';
    }
    if (value.includes('/')) {
      return value;
    }
    const match =
      /^(?<year>(19|20)\d{2})-(?<month>0[1-9]|1[0-2])-(?<day>0[1-9]|[12][0-9]|3[01])$/.exec(
        value
      );
    if (!match?.groups) {
      return value;
    }
    const { year, month, day } = match.groups;
    if (!this.validateCalendarDate(Number(year), Number(month), Number(day))) {
      return value;
    }
    return `${month}/${day}/${year}`;
  }

  private validateCalendarDate(year: number, month: number, day: number): boolean {
    const date = new Date(year, month - 1, day);
    return (
      date.getFullYear() === year &&
      date.getMonth() === month - 1 &&
      date.getDate() === day
    );
  }

  private mapFormToPayload(): RegisterStudentApplicantRequest {
    const value = this.form.getRawValue();
    const isoDate = this.normalizeToIsoDate(value.dateOfBirth);
    if (!isoDate) {
      throw new Error('Invalid date of birth format.');
    }
    return {
      fullName: value.fullName.trim(),
      dateOfBirth: isoDate,
      gender: value.gender,
      email: value.email.trim(),
      mobileNumber: value.mobileNumber.trim(),
    };
  }

  controlInvalid(control: string): boolean {
    const ctrl = this.form.get(control);
    return !!ctrl && ctrl.invalid && (ctrl.dirty || ctrl.touched);
  }

  /** True when control is valid and has been touched (for success indicator). */
  controlValid(control: string): boolean {
    const ctrl = this.form.get(control);
    return !!ctrl && ctrl.valid && (ctrl.dirty || ctrl.touched);
  }

  onPhotoDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.photoDropZoneActive = true;
  }

  onPhotoDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.photoDropZoneActive = false;
  }

  onPhotoDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.photoDropZoneActive = false;
    const file = event.dataTransfer?.files?.[0];
    if (!file) return;
    this.photoError = null;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      this.photoError = 'Only JPEG or PNG files are allowed.';
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      this.photoError = 'File size must be 2 MB or less.';
      return;
    }
    const control = this.form.get('profilePhoto');
    control?.setValue(file);
    control?.markAsDirty();
    const reader = new FileReader();
    reader.onload = () => {
      this.photoPreview = reader.result as string;
    };
    reader.readAsDataURL(file);
  }

  onProfilePhotoSelected(event: Event): void {
    this.photoError = null;
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      this.clearPhotoSelection();
      return;
    }

    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      this.photoError = 'Only JPEG or PNG files are allowed.';
      this.clearPhotoSelection();
      input.value = '';
      return;
    }

    if (file.size > 2 * 1024 * 1024) {
      this.photoError = 'File size must be 2 MB or less.';
      this.clearPhotoSelection();
      input.value = '';
      return;
    }

    const control = this.form.get('profilePhoto');
    control?.setValue(file);
    control?.markAsDirty();

    const reader = new FileReader();
    reader.onload = () => {
      this.photoPreview = reader.result as string;
    };
    reader.readAsDataURL(file);
  }

  private clearPhotoSelection(): void {
    const control = this.form.get('profilePhoto');
    control?.setValue(null);
    control?.markAsPristine();
    control?.markAsUntouched();
    this.photoPreview = null;
  }

  triggerPhotoInput(input: HTMLInputElement): void {
    input.click();
  }
}
