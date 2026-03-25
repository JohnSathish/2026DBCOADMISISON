import { Component, inject, signal, CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdmissionsAdminApiService,
  CreateDirectAdmissionOffersResponse,
} from '@client/shared/data';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-admin-direct-admission',
  standalone: true,
  imports: [CommonModule, FormsModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './admin-direct-admission.component.html',
  styleUrls: ['./admin-direct-admission.component.scss'],
})
export class AdminDirectAdmissionComponent {
  private readonly api = inject(AdmissionsAdminApiService);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly result = signal<CreateDirectAdmissionOffersResponse | null>(null);

  readonly formData = signal({
    minimumPercentage: 60.0,
    admissionFeeAmount: 9500.0,
    expiryDate: '',
  });

  createDirectAdmissionOffers(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);
    this.result.set(null);

    const payload: {
      minimumPercentage?: number;
      admissionFeeAmount?: number;
      expiryDate?: string | null;
    } = {
      minimumPercentage: this.formData().minimumPercentage,
      admissionFeeAmount: this.formData().admissionFeeAmount,
    };

    if (this.formData().expiryDate) {
      payload.expiryDate = new Date(this.formData().expiryDate).toISOString();
    } else {
      payload.expiryDate = null;
    }

    this.api
      .createDirectAdmissionOffers(payload)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          if (response.totalOffersCreated > 0) {
            this.success.set(
              `Successfully created ${response.totalOffersCreated} direct admission offer(s). Notifications have been sent to the selected students.`
            );
          } else {
            this.error.set(
              response.errors.length > 0
                ? response.errors.join(', ')
                : 'No applicants found matching the criteria.'
            );
          }
        },
        error: (err) => {
          const errorMessage =
            err.error?.message ||
            err.message ||
            'Failed to create direct admission offers. Please try again.';
          this.error.set(errorMessage);
        },
      });
  }

  resetForm(): void {
    this.formData.set({
      minimumPercentage: 60.0,
      admissionFeeAmount: 9500.0,
      expiryDate: '',
    });
    this.error.set(null);
    this.success.set(null);
    this.result.set(null);
  }

  getDefaultExpiryDate(): string {
    const date = new Date();
    date.setDate(date.getDate() + 30); // 30 days from now
    return date.toISOString().split('T')[0];
  }
}

