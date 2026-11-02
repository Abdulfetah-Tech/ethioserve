import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { combineLatest, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { PaymentApi } from '../../core/api/payment.api';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { CustomerPayment, CustomerPaymentPage } from '../../core/models/payment.models';
import { RequestStatus } from '../../core/models/request.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { paymentStatusBadge, paymentStatusLabel } from '../../shared/payment-format';

@Component({
  selector: 'app-customer-payment-box',
  imports: [RouterLink, EtbPipe],
  template: `
    @let s = state();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading payment status...</p>
    } @else if (s.kind === 'error') {
      <p class="alert alert-error" role="alert">{{ s.message }}</p>
    } @else {
      @if (errorMessage(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      @if (latest(); as payment) {
        <dl class="details">
          <dt>Status</dt>
          <dd><span [class]="badge(payment.status)">{{ label(payment.status) }}</span></dd>
          <dt>Amount</dt>
          <dd>{{ payment.amount | etb }}</dd>
          <dt>Reference</dt>
          <dd>{{ payment.reference }}</dd>
          @if (payment.failureReason) {
            <dt>Update</dt>
            <dd>{{ payment.failureReason }}</dd>
          }
        </dl>

        @if (payment.status === 'Pending' || payment.status === 'Processing') {
          <a [routerLink]="['/pay', payment.id]" class="btn btn-primary">Continue to checkout</a>
        }
      } @else if (requestStatus() === 'Completed') {
        <p>Your job is complete. Start checkout to pay the amount agreed with your provider.</p>
      } @else {
        <p class="muted">Payment becomes available after the provider marks the job complete.</p>
      }

      @if (canStart()) {
        <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="startPayment()">
          {{ busy() ? 'Opening checkout...' : 'Start payment' }}
        </button>
      }
    }
  `,
})
export class CustomerPaymentBox {
  private readonly api = inject(PaymentApi);
  private readonly router = inject(Router);

  readonly requestId = input.required<string>();
  readonly requestStatus = input.required<RequestStatus>();

  protected readonly state = signal<LoadState<CustomerPaymentPage>>(LOADING);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly badge = paymentStatusBadge;
  protected readonly label = paymentStatusLabel;
  protected readonly latest = computed(() => {
    const current = this.state();
    return current.kind === 'ready' ? current.data.items[0] ?? null : null;
  });
  protected readonly canStart = computed(() => {
    const payment = this.latest();
    return this.requestStatus() === 'Completed'
      && (payment === null || payment.status === 'Failed' || payment.status === 'Cancelled');
  });

  constructor() {
    combineLatest([toObservable(this.requestId), toObservable(this.requestStatus)])
      .pipe(
        switchMap(([requestId]) => toLoadState(this.api.customerList({ requestId }, 5))),
        takeUntilDestroyed(),
      )
      .subscribe((result) => this.state.set(result));
  }

  protected startPayment(): void {
    this.busy.set(true);
    this.errorMessage.set(null);

    this.api.start(this.requestId()).subscribe({
      next: (payment) => {
        this.busy.set(false);
        if (payment.checkoutPath === null) {
          this.errorMessage.set('Checkout is not available right now. Please try again later.');
          return;
        }

        void this.router.navigateByUrl(payment.checkoutPath);
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.busy.set(false);
      },
    });
  }
}
