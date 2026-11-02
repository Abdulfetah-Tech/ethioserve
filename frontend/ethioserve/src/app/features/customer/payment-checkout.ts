import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, map, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { PaymentApi } from '../../core/api/payment.api';
import { CustomerPayment, PaymentOutcome } from '../../core/models/payment.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { paymentStatusBadge, paymentStatusLabel } from '../../shared/payment-format';

@Component({
  selector: 'app-payment-checkout',
  imports: [DatePipe, RouterLink, EtbPipe],
  template: `
    <div class="stack narrow">
      <p><a routerLink="/customer/payments">&larr; My payments</a></p>
      @let s = payment();
      @if (s.kind === 'loading') {
        <p class="muted" role="status">Loading checkout...</p>
      } @else if (s.kind === 'error') {
        <section class="card">
          <h1>Checkout unavailable</h1>
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
        </section>
      } @else {
        <section class="card stack" aria-labelledby="checkout-heading">
          <div>
            <h1 id="checkout-heading">{{ s.data.requestTitle }}</h1>
            <p class="muted">Payment to {{ s.data.providerName }}</p>
          </div>
          <dl class="details">
            <dt>Amount</dt>
            <dd><strong>{{ s.data.amount | etb }}</strong></dd>
            <dt>Status</dt>
            <dd><span [class]="badge(s.data.status)">{{ label(s.data.status) }}</span></dd>
            <dt>Reference</dt>
            <dd>{{ s.data.reference }}</dd>
            <dt>Started</dt>
            <dd>{{ s.data.createdAt | date: 'medium' }}</dd>
          </dl>

          @if (s.data.status === 'Completed') {
            <p class="alert alert-success" role="status">Payment received. Keep your reference for your records.</p>
          } @else if (s.data.status === 'Failed') {
            <p class="alert alert-error" role="alert">{{ s.data.failureReason || 'The payment did not complete.' }}</p>
            <a [routerLink]="['/customer/requests', s.data.requestId]" class="btn btn-secondary">Return to request</a>
          } @else if (s.data.status === 'Refunded' || s.data.status === 'Cancelled') {
            <p class="muted">This payment attempt is closed. Return to your request if another payment is due.</p>
            <a [routerLink]="['/customer/requests', s.data.requestId]" class="btn btn-secondary">Return to request</a>
          } @else if (s.data.method !== 'Mock') {
            <p class="alert alert-error" role="alert">This payment method is not available in the demo checkout.</p>
          } @else if (!s.data.checkoutPath) {
            <p class="alert alert-error" role="alert">This checkout method is currently unavailable. Please contact support.</p>
          } @else {
            <p class="hint" role="note">
              Demo checkout only. No money will move; choose a result to test the payment workflow.
            </p>
            @if (errorMessage(); as message) {
              <p class="alert alert-error" role="alert">{{ message }}</p>
            }
            <div class="form-actions">
              <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="respond(s.data, 'Succeeded')">
                {{ busy() ? 'Processing...' : 'Simulate successful payment' }}
              </button>
              <button type="button" class="btn btn-secondary" [disabled]="busy()" (click)="respond(s.data, 'Failed')">
                Simulate decline
              </button>
              <button type="button" class="btn btn-secondary" [disabled]="busy()" (click)="cancel(s.data)">
                Cancel checkout
              </button>
            </div>
          }
        </section>
      }
    </div>
  `,
})
export class PaymentCheckout {
  private readonly api = inject(PaymentApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly badge = paymentStatusBadge;
  protected readonly label = paymentStatusLabel;
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly payment = signal<LoadState<CustomerPayment>>(LOADING);

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        switchMap((id) => toLoadState(this.api.customerGet(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.payment.set(state));
  }

  protected respond(payment: CustomerPayment, outcome: PaymentOutcome): void {
    this.run(this.api.simulate(payment.id, outcome));
  }

  protected cancel(payment: CustomerPayment): void {
    this.run(this.api.cancel(payment.id));
  }

  private run(request: Observable<CustomerPayment>): void {
    this.busy.set(true);
    this.errorMessage.set(null);

    request.subscribe({
      next: (updated) => {
        this.payment.set({ kind: 'ready', data: updated });
        this.busy.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.busy.set(false);
      },
    });
  }
}
