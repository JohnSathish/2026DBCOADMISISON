import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PaymentApiService } from '@client/shared/data';
import { ApplicantPortalStore } from '../dashboard/applicant-portal.store';
import { ToastService } from '../shared/toast.service';
import { buildRazorpayStandardOptions, isMobileBrowser } from '../shared/razorpay-checkout.util';
import { finalize } from 'rxjs/operators';

declare global {
  interface Window {
    Razorpay: any;
  }
}

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './payment.component.html',
  styleUrls: ['./payment.component.scss'],
})
export class PaymentComponent {
  private readonly paymentApi = inject(PaymentApiService);
  private readonly store = inject(ApplicantPortalStore);
  private readonly toast = inject(ToastService);

  readonly isProcessing = signal(false);
  readonly isOpen = signal(false);
  readonly amount = signal<number>(0);

  paymentCheckoutHint(): string {
    return isMobileBrowser()
      ? 'Pay via UPI app (GPay, PhonePe, …) or Card / Netbanking. On phones, UPI opens first when your email and mobile are on file.'
      : 'Pay with UPI (scan QR), Card, Netbanking, or Wallet in the secure window.';
  }

  open(amount: number): void {
    this.amount.set(amount);
    this.isOpen.set(true);
    this.loadRazorpayScript();
  }

  close(): void {
    this.isOpen.set(false);
  }

  private loadRazorpayScript(): void {
    if (window.Razorpay) {
      this.initiatePayment();
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://checkout.razorpay.com/v1/checkout.js';
    script.onload = () => this.initiatePayment();
    script.onerror = () => {
      this.toast.show(
        'Failed to load payment gateway. Please refresh the page and try again.',
        'error'
      );
      this.isProcessing.set(false);
    };
    document.body.appendChild(script);
  }

  initiatePayment(): void {
    if (this.isProcessing()) {
      return;
    }

    this.isProcessing.set(true);

    this.paymentApi
      .createOrder()
      .pipe(finalize(() => this.isProcessing.set(false)))
      .subscribe({
        next: (order) => {
          this.openRazorpayCheckout(order);
        },
        error: (error) => {
          const message =
            error?.error?.message ??
            'Failed to create payment order. Please try again.';
          this.toast.show(message, 'error');
        },
      });
  }

  private openRazorpayCheckout(order: {
    orderId: string;
    amount: number;
    currency: string;
    keyId: string;
    checkoutLogoUrl?: string | null;
  }): void {
    const profile = this.store.dashboard()?.profile;
    const options = buildRazorpayStandardOptions({
      key: order.keyId,
      amountPaise: Math.round(order.amount * 100),
      currency: order.currency,
      name: 'College Admission Application Fee',
      description: 'Application Fee Payment',
      orderId: order.orderId,
      handler: (response) => {
        this.verifyPayment(
          {
            razorpay_payment_id: response['razorpay_payment_id'] ?? '',
            razorpay_order_id: response['razorpay_order_id'] ?? '',
            razorpay_signature: response['razorpay_signature'] ?? '',
          },
          order.orderId
        );
      },
      prefill: {
        name: profile?.fullName ?? '',
        email: profile?.email ?? '',
        contact: profile?.mobileNumber ?? '',
      },
      themeColor: '#1b5e9d',
      onDismiss: () => this.isProcessing.set(false),
      checkoutLogoUrl: order.checkoutLogoUrl,
    });

    const razorpay = new window.Razorpay(options);
    razorpay.open();
  }

  private verifyPayment(
    response: { razorpay_payment_id: string; razorpay_order_id: string; razorpay_signature: string },
    orderId: string
  ): void {
    this.isProcessing.set(true);

    this.paymentApi
      .verifyPayment({
        orderId: response.razorpay_order_id,
        paymentId: response.razorpay_payment_id,
        signature: response.razorpay_signature,
      })
      .pipe(finalize(() => this.isProcessing.set(false)))
      .subscribe({
        next: (result) => {
          if (result.success) {
            this.toast.show('Payment completed successfully!', 'success');
            this.close();
            // Reload dashboard to refresh payment status
            this.store.loadDashboard();
          } else {
            this.toast.show(
              result.message || 'Payment verification failed.',
              'error'
            );
          }
        },
        error: (error) => {
          const message =
            error?.error?.message ??
            'Payment verification failed. Please contact support.';
          this.toast.show(message, 'error');
        },
      });
  }
}

