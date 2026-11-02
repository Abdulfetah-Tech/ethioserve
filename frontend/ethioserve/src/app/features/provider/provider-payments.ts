import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, map, switchMap } from 'rxjs';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { PaymentApi } from '../../core/api/payment.api';
import { EtbPipe } from '../../shared/etb.pipe';
import { Pager } from '../../shared/pager';
import { paymentStatusBadge, paymentStatusLabel } from '../../shared/payment-format';
import { ProviderNav } from './provider-nav';

@Component({
  selector: 'app-provider-payments',
  imports: [DatePipe, EtbPipe, Pager, ProviderNav],
  template: `
    <div class="stack">
      <header>
        <h1>Payments and earnings</h1>
        <p class="muted">Payments for your completed jobs. Amounts are shown in Ethiopian birr.</p>
      </header>

      <app-provider-nav />

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading payment history...</p>
      } @else if (res.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ res.message }}</p>
      } @else {
        <section class="card" aria-labelledby="earnings-heading">
          <h2 id="earnings-heading">Earnings summary</h2>
          <dl class="details">
            <dt>Payments received</dt>
            <dd>{{ res.data.earnings.totalReceived | etb }} ({{ res.data.earnings.paymentsReceived }})</dd>
            <dt>Awaiting payment</dt>
            <dd>{{ res.data.earnings.awaitingPaymentAmount | etb }} ({{ res.data.earnings.awaitingPaymentCount }})</dd>
          </dl>
          <p class="hint">This is a payment record, not a payout or bank-transfer balance.</p>
        </section>

        <section class="stack" aria-labelledby="payments-heading">
          <h2 id="payments-heading">Payment history</h2>
          @if (res.data.payments.totalCount === 0) {
            <div class="card">
              <p><strong>No payments yet.</strong></p>
              <p class="muted">A payment appears here after a customer pays for a completed job.</p>
            </div>
          } @else {
            <ul class="provider-grid" role="list">
              @for (payment of res.data.payments.items; track payment.id) {
                <li>
                  <article class="card provider-card">
                    <h3>{{ payment.requestTitle }}</h3>
                    <p class="muted">{{ payment.customerName }} &middot; {{ payment.createdAt | date: 'medium' }}</p>
                    <p class="provider-meta">
                      <strong>{{ payment.amount | etb }}</strong>
                      <span [class]="badge(payment.status)">{{ label(payment.status) }}</span>
                    </p>
                    <p class="muted">Reference: {{ payment.reference }}</p>
                  </article>
                </li>
              }
            </ul>
          }
          <app-pager
            [page]="res.data.payments.page"
            [totalPages]="res.data.payments.totalPages"
            (pageChange)="goToPage($event)"
          />
        </section>
      }
    </div>
  `,
})
export class ProviderPayments {
  private readonly api = inject(PaymentApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly badge = paymentStatusBadge;
  protected readonly label = paymentStatusLabel;
  private readonly page$ = this.route.queryParamMap.pipe(
    map((query) => Math.max(1, Number.parseInt(query.get('page') ?? '1', 10) || 1)),
  );
  protected readonly results = toSignal(
    this.page$.pipe(
      switchMap((page) =>
        toLoadState(forkJoin({
          earnings: this.api.providerEarnings(),
          payments: this.api.providerList(page),
        })),
      ),
    ),
    { initialValue: LOADING },
  );

  protected goToPage(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page > 1 ? page : null },
    });
  }
}
