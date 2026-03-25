import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  CUSTOM_ELEMENTS_SCHEMA,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdmissionsAdminApiService } from '@client/shared/data';
import { ToastService } from '../../../shared/toast.service';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-admin-send-offer',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-send-offer.component.html',
  styleUrls: ['./admin-send-offer.component.scss'],
})
export class AdminSendOfferComponent {
  private readonly api = inject(AdmissionsAdminApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);

  readonly loading = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly offerForm = this.fb.group({
    applicationNumber: ['', [Validators.required, Validators.pattern(/^[A-Z0-9-]+$/)]],
    admissionFeeAmount: [9500.0, [Validators.required, Validators.min(0)]],
    expiryDays: [2, [Validators.required, Validators.min(1), Validators.max(30)]],
    remarks: [''],
  });

  async sendOffer(): Promise<void> {
    this.successMessage.set(null);
    this.errorMessage.set(null);

    this.offerForm.markAllAsTouched();
    if (this.offerForm.invalid) {
      this.errorMessage.set('Please correct the form errors.');
      return;
    }

    this.loading.set(true);
    try {
      const formValue = this.offerForm.getRawValue();
      const result = await firstValueFrom(
        this.api.sendIndividualAdmissionOffer({
          applicationNumber: formValue.applicationNumber!,
          admissionFeeAmount: formValue.admissionFeeAmount!,
          expiryDays: formValue.expiryDays!,
          remarks: formValue.remarks || undefined,
        })
      );

      this.successMessage.set(
        `Admission offer sent successfully to ${result.fullName} (${result.applicationNumber}). ` +
        `Email sent to: ${result.email}. Offer expires on ${new Date(result.expiryDate).toLocaleDateString()}.`
      );
      this.toast.show('Admission offer sent successfully!', 'success');
      this.offerForm.reset({
        admissionFeeAmount: 10.0,
        expiryDays: 2,
        remarks: '',
      });
    } catch (error: any) {
      console.error('Error sending admission offer:', error);
      this.errorMessage.set(
        error.error?.message || error.message || 'Failed to send admission offer. Please try again.'
      );
      this.toast.show('Failed to send admission offer.', 'error');
    } finally {
      this.loading.set(false);
    }
  }

  resetForm(): void {
    this.offerForm.reset({
      applicationNumber: '',
      admissionFeeAmount: 10.0,
      expiryDays: 2,
      remarks: '',
    });
    this.successMessage.set(null);
    this.errorMessage.set(null);
    this.offerForm.markAsPristine();
    this.offerForm.markAsUntouched();
  }
}




