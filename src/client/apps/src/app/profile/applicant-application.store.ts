import { inject, Injectable, signal } from '@angular/core';
import {
  ApplicantApplicationApiService,
  ApplicantApplicationDraft,
  ApplicantApplicationDraftResponse,
} from '@client/shared/data';
import { ToastService } from '../shared/toast.service';
import { firstValueFrom } from 'rxjs';

export interface ApplicantApplicationSubmitResult {
  blob: Blob;
  fileName: string;
}

@Injectable()
export class ApplicantApplicationStore {
  private readonly api = inject(ApplicantApplicationApiService);
  private readonly toast = inject(ToastService);

  private readonly draftSignal = signal<ApplicantApplicationDraft | null>(null);
  private readonly updatedOnSignal = signal<string | null>(null);
  private readonly loadingSignal = signal(false);
  private readonly savingSignal = signal(false);
  private readonly errorSignal = signal<string | null>(null);

  readonly draft = this.draftSignal.asReadonly();
  readonly updatedOnUtc = this.updatedOnSignal.asReadonly();
  readonly loading = this.loadingSignal.asReadonly();
  readonly saving = this.savingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  async loadDraft(): Promise<void> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);
    try {
      const response = await firstValueFrom(this.api.loadDraft());
      this.setDraftResponse(response);
      console.log('Draft loaded successfully:', response.payload ? 'Has data' : 'Empty');
    } catch (error: any) {
      console.error('Failed to load application draft', error);
      // Don't set error signal for 404 - it just means no draft exists yet
      if (error?.status !== 404) {
        this.errorSignal.set('Unable to load your application at this time.');
      } else {
        console.log('No draft found (404) - this is normal for new applications');
        // Set empty draft so effects know there's no data
        this.draftSignal.set(null);
      }
    } finally {
      this.loadingSignal.set(false);
    }
  }

  async saveDraft(
    payload: ApplicantApplicationDraft,
    options?: { silent?: boolean }
  ): Promise<ApplicantApplicationDraftResponse | null> {
    this.savingSignal.set(true);
    this.errorSignal.set(null);
    try {
      const response = await firstValueFrom(this.api.saveDraft(payload));
      this.setDraftResponse(response);
      if (!options?.silent) {
        this.toast.show('Application saved successfully.', 'success');
      }
      return response;
    } catch (error) {
      console.error('Failed to save application draft', error);
      this.errorSignal.set('Unable to save your application. Please try again.');
      this.toast.show('Unable to save your application.', 'error');
      return null;
    } finally {
      this.savingSignal.set(false);
    }
  }

  async submitApplication(payload: ApplicantApplicationDraft): Promise<ApplicantApplicationSubmitResult | null> {
    this.savingSignal.set(true);
    this.errorSignal.set(null);
    try {
      const response = await firstValueFrom(this.api.submitApplication(payload));
      const blob = response.body ?? new Blob();
      const fileName = this.extractFileName(response.headers.get('Content-Disposition')) ??
        `admission-application-${new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14)}.pdf`;

      this.toast.show('Application submitted successfully.', 'success');
      return { blob, fileName };
    } catch (error) {
      console.error('Failed to submit application', error);
      this.errorSignal.set('Unable to submit your application. Please try again.');
      this.toast.show('Unable to submit your application.', 'error');
      return null;
    } finally {
      this.savingSignal.set(false);
    }
  }

  private setDraftResponse(response: ApplicantApplicationDraftResponse): void {
    this.draftSignal.set(response.payload);
    this.updatedOnSignal.set(response.updatedOnUtc);
  }

  private extractFileName(contentDisposition: string | null): string | null {
    if (!contentDisposition) {
      return null;
    }
    const fileNameMatch = contentDisposition.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i);
    if (fileNameMatch && fileNameMatch[1]) {
      return decodeURIComponent(fileNameMatch[1]);
    }
    return null;
  }
}

