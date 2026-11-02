import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { PaymentApi } from '../../core/api/payment.api';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { EtbPipe } from '../../shared/etb.pipe';
import { Pager } from '../../shared/pager';
import { paymentStatusBadge, paymentStatusLabel } from '../../shared/payment-format';
import { CustomerNav } from './customer-nav';

@Component({
  selector: 'app-customer-payments',
  imports: [DatePipe, RouterLink, EtbPipe, Pager, CustomerNav],
  template: `
    <div class="stack">
      <header>
        <h1>Payments</h1>
        <p class="muted">Your payment history and checkout status.</p>
      </header>

      <app-customer-nav />

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading payments...</p>
      } @else if (res.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ res.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ res.data.totalCount }} {{ res.data.totalCount === 1 ? 'payment' : 'payments' }}
        </p>

        @if (res.data.items.length === 0) {
          <section class="card">
            <h2>No payments yet</h2>
            <p class="muted">When a provider completes a job, you can start checkout from that request.</p>
            <a routerLink="/customer/requests" class="btn btn-primary">Go to my requests</a>
          </section>
        } @else {
          <ul class="provider-grid" role="list">
            @for (payment of res.data.items; track payment.id) {
              <li>
                <article class="card provider-card">
                  <h2><a [routerLink]="['/customer/requests', payment.requestId]">{{ payment.requestTitle }}</a></h2>
                  <p class="muted">{{ payment.providerName }} &middot; {{ payment.createdAt | date: 'medium' }}</p>
                  <p class="provider-meta">
                    <strong>{{ payment.amount | etb }}</strong>
                    <span [class]="badge(payment.status)">{{ label(payment.status) }}</span>
                  </p>
                  <p class="muted">Reference: {{ payment.reference }}</p>
                  @if (payment.failureReason) {
                    <p class="alert alert-error" role="note">{{ payment.failureReason }}</p>
                  }
                  @if (payment.status === 'Pending' || payment.status === 'Processing') {
                    <a [routerLink]="['/pay', payment.id]" class="btn btn-primary">Continue checkout</a>
                  }
                </article>
              </li>
            }
          </ul>
        }

        <app-pager [page]="res.data.page" [totalPages]="res.data.totalPages" (pageChange)="goToPage($event)" />
      }
    </div>
  `,
})
export class CustomerPayments {
  private readonly api = inject(PaymentApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly badge = paymentStatusBadge;
  protected readonly label = paymentStatusLabel;
  private readonly page$ = this.route.queryParamMap.pipe(
    map((query) => Math.max(1, Number.parseInt(query.get('page') ?? '1', 10) || 1)),
  );
  protected readonly page = toSignal(this.page$, { initialValue: 1 });
  protected readonly results = toSignal(
    this.page$.pipe(switchMap((page) => toLoadState(this.api.customerList({ page })))),
    { initialValue: LOADING },
  );

  protected goToPage(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page > 1 ? page : null },
    });
  }
}
