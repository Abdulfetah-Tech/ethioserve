import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { combineLatest, startWith, Subject, switchMap } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { toApiError } from '../../core/api/api-error';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { AdminReview } from '../../core/models/admin.models';
import { Pager } from '../../shared/pager';
import { AdminNav } from './admin-nav';

type ReviewVisibility = 'All' | 'Visible' | 'Hidden';

const VISIBILITY_OPTIONS: ReviewVisibility[] = ['All', 'Visible', 'Hidden'];

@Component({
  selector: 'app-admin-reviews',
  imports: [DatePipe, AdminNav, Pager],
  template: `
    <div class="stack">
      <header>
        <h1>Review moderation</h1>
        <p class="muted">Hide reviews from public pages or restore them. Hidden reviews do not affect provider ratings.</p>
      </header>

      <app-admin-nav />

      @if (notice(); as text) {
        <p class="alert alert-success" role="status">{{ text }}</p>
      }
      @if (actionError(); as text) {
        <p class="alert alert-error" role="alert">{{ text }}</p>
      }

      <section class="card" aria-label="Review filters">
        <div class="field">
          <label for="visibility">Visibility</label>
          <select id="visibility" (change)="onVisibility($event)">
            @for (option of visibilityOptions; track option) {
              <option [value]="option" [selected]="option === visibility()">{{ option }} reviews</option>
            }
          </select>
        </div>
      </section>

      @let r = results();
      @if (r.kind === 'loading') {
        <p class="muted" role="status">Loading reviews...</p>
      } @else if (r.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ r.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ r.data.totalCount }} {{ r.data.totalCount === 1 ? 'review' : 'reviews' }} found
        </p>

        @if (r.data.items.length === 0) {
          <div class="card"><p class="muted">No reviews match this filter.</p></div>
        } @else {
          <div class="stack">
            @for (review of r.data.items; track review.id) {
              <article class="card">
                <header>
                  <div>
                    <h2>{{ review.providerBusinessName }}</h2>
                    <p class="muted">{{ review.rating }} / 5 · {{ review.createdAt | date: 'mediumDate' }}</p>
                  </div>
                  <span class="badge" [class.badge-ok]="review.isVisible">
                    {{ review.isVisible ? 'Visible' : 'Hidden' }}
                  </span>
                </header>

                @if (review.comment) {
                  <p>{{ review.comment }}</p>
                } @else {
                  <p class="muted">No written comment.</p>
                }

                <div class="form-actions">
                  @if (review.isVisible) {
                    <button
                      type="button"
                      class="btn btn-secondary"
                      [disabled]="pendingReviewId() !== null"
                      (click)="setVisibility(review)"
                    >
                      {{ pendingReviewId() === review.id ? 'Hiding...' : 'Hide review' }}
                    </button>
                  } @else {
                    <button
                      type="button"
                      class="btn btn-secondary"
                      [disabled]="pendingReviewId() !== null"
                      (click)="setVisibility(review)"
                    >
                      {{ pendingReviewId() === review.id ? 'Restoring...' : 'Restore review' }}
                    </button>
                  }
                </div>
              </article>
            }
          </div>
        }

        <app-pager [page]="r.data.page" [totalPages]="r.data.totalPages" (pageChange)="page.set($event)" />
      }
    </div>
  `,
})
export class AdminReviews {
  private readonly api = inject(AdminApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refresh$ = new Subject<void>();

  protected readonly visibilityOptions = VISIBILITY_OPTIONS;
  protected readonly visibility = signal<ReviewVisibility>('All');
  protected readonly page = signal(1);
  protected readonly pendingReviewId = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly results = toSignal(
    combineLatest([
      toObservable(this.visibility),
      toObservable(this.page),
      this.refresh$.pipe(startWith(undefined)),
    ]).pipe(
      switchMap(([visibility, page]) =>
        toLoadState(
          this.api.reviews(
            {
              isVisible: visibility === 'All' ? undefined : visibility === 'Visible',
              page,
            },
            15,
          ),
        ),
      ),
    ),
    { initialValue: LOADING },
  );

  protected onVisibility(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    const option = VISIBILITY_OPTIONS.find((candidate) => candidate === value);
    if (!option) {
      return;
    }

    this.visibility.set(option);
    this.page.set(1);
  }

  protected setVisibility(review: AdminReview): void {
    this.pendingReviewId.set(review.id);
    this.notice.set(null);
    this.actionError.set(null);

    const action = review.isVisible ? this.api.hideReview(review.id) : this.api.restoreReview(review.id);
    action.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.pendingReviewId.set(null);
        this.notice.set(review.isVisible ? 'Review hidden.' : 'Review restored.');
        this.refresh$.next();
      },
      error: (error: unknown) => {
        this.pendingReviewId.set(null);
        this.actionError.set(toApiError(error).message);
      },
    });
  }
}
