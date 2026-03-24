import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '@client/shared/util';

export interface CreateOrderResponse {
  orderId: string;
  amount: number;
  currency: string;
  keyId: string;
  /** Absolute URL for Razorpay checkout header logo; optional. */
  checkoutLogoUrl?: string | null;
}

export interface VerifyPaymentRequest {
  orderId: string;
  paymentId: string;
  signature: string;
}

export interface VerifyPaymentResponse {
  success: boolean;
  message: string;
}

export interface PaymentStatusResponse {
  isApplicationSubmitted: boolean;
  isPaymentCompleted: boolean;
  /** Application / registration reference (same as UniqueId). */
  applicationReference: string | null;
  paymentOrderId: string | null;
  paymentTransactionId: string | null;
  paymentAmount: number | null;
  paymentCompletedOnUtc: string | null;
}

@Injectable({ providedIn: 'root' })
export class PaymentApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  createOrder(): Observable<CreateOrderResponse> {
    return this.http.post<CreateOrderResponse>(`${this.apiBaseUrl}/payments/create-order`, {});
  }

  verifyPayment(request: VerifyPaymentRequest): Observable<VerifyPaymentResponse> {
    return this.http.post<VerifyPaymentResponse>(`${this.apiBaseUrl}/payments/verify`, request);
  }

  getPaymentStatus(): Observable<PaymentStatusResponse> {
    return this.http.get<PaymentStatusResponse>(`${this.apiBaseUrl}/payments/status`);
  }
}

