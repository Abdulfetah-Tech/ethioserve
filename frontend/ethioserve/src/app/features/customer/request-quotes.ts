import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject, combineLatest, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { QuoteApi } from '../../core/api/quote.api';
import { CustomerQuote } from '../../core/models/quote.models';
import { RequestStatus } from '../../core/models/request.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { cheapestOpenId, durationText, expiryText, quoteStatusBadge, quoteStatusLabel } from '../../shared/quote-format';
import { Rating } from '../../shared/rating';
import { RequestMessages } from '../../shared/request-messages';

/** The quotes on one of the customer's requests: compare them, accept one, or decline the rest. */
@Component({
  selector: 'app-request-quotes',
  imports: [RouterLink, EtbPipe, Rating, RequestMessages],
  template: `
    @let s = quotes();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading quotes...</p>
    } @else if (s.kind === 'error') {
      <p class="alert alert-error" role="alert">{{ s.message }}</p>
    } @else {
      @if (notice(); as text) {
        <p class="alert alert-success" role="status">{{ text }}</p>
      }
      @if (errorMessage(); as text) {
        <p class="alert alert-error" role="alert">{{ text }}</p>
      }

      @if (s.data.length === 0) {
        <p class="muted">{{ emptyText() }}</p>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <caption class="visually-hidden">Quotes from providers, cheapest first</caption>
            <thead>
              <tr>
                <th scope="col">Provider</th>
                <th scope="col">Price</th>
                <th scope="col">Time</th>
                <th scope="col">Their offer</th>
                <th scope="col">Status</th>
                <th scope="col"><span class="visually-hidden">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              @for (q of s.data; track q.id) {
                <tr>
                  <th scope="row">
                    <a [routerLink]="['/providers', q.providerId]">{{ q.providerName }}</a>
                    <div><app-rating [value]="q.providerRating" [count]="q.providerReviews" /></div>
                    <div class="muted">{{ q.providerYearsOfExperience }} years of experience</div>
                  </th>
                  <td>
                    <strong>{{ q.amount | etb }}</strong>
                    @if (q.id === bestId()) {
                      <div><span class="badge badge-ok">Lowest price</span></div>
                    }
                  </td>
                  <td>{{ duration(q.estimatedDurationMinutes) }}</td>
                  <td>
                    {{ q.description }}
                    @if (q.status === 'Pending') {
                      <div class="muted">{{ expiry(q.validUntil) }}</div>
                    }
                  </td>
                  <td><span [class]="badge(q.status)">{{ label(q.status) }}</span></td>
                  <td>
                    <button
                      type="button"
                      class="btn btn-secondary"
                      [attr.aria-expanded]="conversationProviderId() === q.providerId"
                      (click)="toggleConversation(q.providerId)"
                    >
                      {{ conversationProviderId() === q.providerId ? 'Close messages' : 'Message provider' }}
                    </button>
                    @if (q.status === 'Pending' && decidable()) {
                      @if (accepting() === q.id) {
                        <p>
                          Accept <strong>{{ q.providerName }}</strong>'s quote of {{ q.amount | etb }}? This closes your
                          request, declines the other quotes, and shares your name, phone number and address with
                          {{ q.providerName }}.
                        </p>
                        <div class="row-actions">
                          <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="accept(q)">Yes, accept</button>
                          <button type="button" class="btn btn-secondary" (click)="accepting.set(null)">Keep looking</button>
                        </div>
                      } @else if (declining() === q.id) {
                        <div class="row-actions">
                          <span>Decline this quote?</span>
                          <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="decline(q)">Yes, decline</button>
                          <button type="button" class="btn btn-secondary" (click)="declining.set(null)">Keep</button>
                        </div>
                      } @else {
                        <div class="row-actions">
                          <button type="button" class="btn btn-primary" (click)="startAccept(q.id)">Accept</button>
                          <button type="button" class="btn btn-secondary" (click)="startDecline(q.id)">Decline</button>
                        </div>
                      }
                    } @else if (q.status === 'Accepted' && q.providerPhone) {
                      <p>Call <a [href]="'tel:' + q.providerPhone">{{ q.providerPhone }}</a></p>
                    }
                  </td>
                </tr>
                @if (conversationProviderId() === q.providerId) {
                  <tr>
                    <td colspan="6">
                      <app-request-messages
                        [requestId]="requestId()"
                        [providerId]="q.providerId"
                        [otherParticipantName]="q.providerName"
                      />
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      }
    }
  `,
})
export class RequestQuotes {
  private readonly api = inject(QuoteApi);
  private readonly refresh$ = new Subject<void>();

  readonly requestId = input.required<string>();
  readonly requestStatus = input.required<RequestStatus>();

  /** Emitted after an accept or a decline, so the page can reload the request itself. */
  readonly changed = output<void>();

  protected readonly badge = quoteStatusBadge;
  protected readonly label = quoteStatusLabel;
  protected readonly duration = durationText;
  protected readonly expiry = (validUntil: string): string => expiryText(validUntil);

  protected readonly quotes = signal<LoadState<CustomerQuote[]>>(LOADING);
  protected readonly accepting = signal<string | null>(null);
  protected readonly declining = signal<string | null>(null);
  protected readonly conversationProviderId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly bestId = computed(() => {
    const s = this.quotes();

    return s.kind === 'ready' ? cheapestOpenId(s.data) : null;
  });

  protected readonly decidable = computed(() => this.requestStatus() === 'Pending' || this.requestStatus() === 'Quoted');

  protected readonly emptyText = computed(() => {
    switch (this.requestStatus()) {
      case 'Pending':
        return 'No quotes yet. Verified providers in your city can see your request, and their quotes will appear here.';
      case 'Cancelled':
        return 'No quotes were received.';
      default:
        return 'There are no quotes to show.';
    }
  });

  constructor() {
    combineLatest([toObservable(this.requestId), this.refresh$.pipe(startWith(undefined))])
      .pipe(
        switchMap(([id]) => toLoadState(this.api.forRequest(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.quotes.set(state));
  }

  protected startAccept(id: string): void {
    this.declining.set(null);
    this.clearMessages();
    this.accepting.set(id);
  }

  protected toggleConversation(providerId: string): void {
    this.conversationProviderId.update((current) => current === providerId ? null : providerId);
  }

  protected startDecline(id: string): void {
    this.accepting.set(null);
    this.clearMessages();
    this.declining.set(id);
  }

  protected accept(quote: CustomerQuote): void {
    this.run(
      this.api.accept(this.requestId(), quote.id),
      `You accepted ${quote.providerName}'s quote. They now have your contact details, and their phone number is shown below.`,
    );
  }

  protected decline(quote: CustomerQuote): void {
    this.run(this.api.decline(this.requestId(), quote.id), `You declined ${quote.providerName}'s quote.`);
  }

  private run(call: ReturnType<QuoteApi['accept']>, doneText: string): void {
    this.clearMessages();
    this.busy.set(true);

    call.subscribe({
      next: () => {
        this.finish();
        this.notice.set(doneText);
      },
      error: (error: unknown) => {
        // Show what really happened, then reload so the table and the page match the stored state.
        this.finish();
        this.errorMessage.set(toApiError(error).message);
      },
    });
  }

  private finish(): void {
    this.accepting.set(null);
    this.declining.set(null);
    this.busy.set(false);
    this.refresh$.next();
    this.changed.emit();
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.errorMessage.set(null);
  }
}