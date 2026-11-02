import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/marketplace.models';
import {
  CustomerPayment,
  PaymentOutcome,
  ProviderEarnings,
  ProviderPayment,
} from '../models/payment.models';

export interface CustomerPaymentQuery {
  requestId?: string;
  page?: number;
}

@Injectable({ providedIn: 'root' })
export class PaymentApi {
  private readonly http = inject(HttpClient);

  start(
    requestId: string,
    idempotencyKey: string = globalThis.crypto.randomUUID(),
  ): Observable<CustomerPayment> {
    return this.http.post<CustomerPayment>(
      `/api/customer/payments/requests/${encodeURIComponent(requestId)}`,
      null,
      { headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }) },
    );
  }

  customerList(query: CustomerPaymentQuery = {}, pageSize = 10): Observable<PagedResult<CustomerPayment>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.requestId) {
      params = params.set('requestId', query.requestId);
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<CustomerPayment>>('/api/customer/payments', { params });
  }

  customerGet(id: string): Observable<CustomerPayment> {
    return this.http.get<CustomerPayment>(`/api/customer/payments/${encodeURIComponent(id)}`);
  }

  cancel(id: string): Observable<CustomerPayment> {
    return this.http.post<CustomerPayment>(`${this.customerUrl(id)}/cancel`, null);
  }

  simulate(id: string, outcome: PaymentOutcome, reason?: string | null): Observable<CustomerPayment> {
    return this.http.post<CustomerPayment>(`${this.customerUrl(id)}/simulate`, { outcome, reason: reason ?? null });
  }

  providerList(page = 1, pageSize = 10): Observable<PagedResult<ProviderPayment>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (page > 1) {
      params = params.set('page', String(page));
    }

    return this.http.get<PagedResult<ProviderPayment>>('/api/provider/payments', { params });
  }

  providerEarnings(): Observable<ProviderEarnings> {
    return this.http.get<ProviderEarnings>('/api/provider/payments/earnings');
  }

  private customerUrl(id: string): string {
    return `/api/customer/payments/${encodeURIComponent(id)}`;
  }
}
