import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map, switchMap } from 'rxjs';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { AuthService } from '../../core/auth/auth.service';
import { QuoteApi } from '../../core/api/quote.api';
import { ProviderQuote } from '../../core/models/quote.models';
import { ProviderNav } from './provider-nav';
import { ProviderJob } from './provider-job';
import { ProviderQuoteView } from './provider-quote-view';
import { RequestMessages } from '../../shared/request-messages';

@Component({
  selector: 'app-provider-quote-detail',
  imports: [RouterLink, ProviderNav, ProviderQuoteView, ProviderJob, RequestMessages],
  template: `
    <div class="stack">
      <app-provider-nav />

      <p><a routerLink="/provider/quotes">&larr; My quotes</a></p>

      @let s = detail();
      @if (s.kind === 'loading') {
        <p class="muted" role="status">Loading your quote...</p>
      } @else if (s.kind === 'error') {
        <section class="card narrow">
          <h1>Quote not found</h1>
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
          <a routerLink="/provider/quotes" class="btn btn-primary">Back to my quotes</a>
        </section>
      } @else {
        <header class="card">
          <h1>{{ s.data.requestTitle }}</h1>
          <p class="muted">{{ s.data.serviceName }} &middot; {{ s.data.cityName }}</p>
          @if (s.data.requestStatus === 'Pending' || s.data.requestStatus === 'Quoted') {
            <p><a [routerLink]="['/provider/requests', s.data.requestId]">View the request</a></p>
          }
        </header>

        <section class="card" aria-labelledby="quote-heading">
          <h2 id="quote-heading">Your quote</h2>
          <app-provider-quote-view [quote]="s.data" (updated)="onUpdated($event)" />
        </section>

        @if (providerId(); as id) {
          <section class="card" aria-label="Private conversation">
            <app-request-messages
              [requestId]="s.data.requestId"
              [providerId]="id"
              otherParticipantName="Customer"
            />
          </section>
        }

        @if (s.data.status === 'Accepted') {
          <section class="card" aria-labelledby="job-heading">
            <h2 id="job-heading">The job</h2>
            <app-provider-job [requestId]="s.data.requestId" />
          </section>
        }
      }
    </div>
  `,
})
export class ProviderQuoteDetail {
  private readonly api = inject(QuoteApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  protected readonly providerId = computed(() => this.auth.currentUser()?.providerId ?? '');
  protected readonly detail = signal<LoadState<ProviderQuote>>(LOADING);

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        distinctUntilChanged(),
        switchMap((id) => toLoadState(this.api.get(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.detail.set(state));
  }

  protected onUpdated(quote: ProviderQuote): void {
    this.detail.set({ kind: 'ready', data: quote });
  }
}