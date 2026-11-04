import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { combineLatest, distinctUntilChanged, startWith, Subject, switchMap } from 'rxjs';
import { toApiError } from '../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../core/api/load-state';
import { MessageApi } from '../core/api/message.api';
import { AuthService } from '../core/auth/auth.service';
import { RequestMessage, RequestMessagePage } from '../core/models/message.models';

@Component({
  selector: 'app-request-messages',
  imports: [DatePipe, ReactiveFormsModule],
  template: `
    <section class="conversation" aria-label="Private conversation">
      <header class="conversation-header">
        <div>
          <h3>Conversation with {{ otherParticipantName() }}</h3>
          <p class="muted">Only you and this provider can see these messages.</p>
        </div>
        <button type="button" class="btn btn-secondary" [disabled]="sending()" (click)="refresh()">
          Refresh
        </button>
      </header>

      @let state = messages();
      @if (state.kind === 'loading') {
        <p class="muted" role="status">Loading messages...</p>
      } @else if (state.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ state.message }}</p>
        <button type="button" class="btn btn-secondary" (click)="refresh()">Try again</button>
      } @else if (state.data.items.length === 0) {
        <p class="muted">No messages yet. Send a message to start the conversation.</p>
      } @else {
        <ol class="conversation-messages" aria-live="polite">
          @for (message of oldestFirst(state.data.items); track message.id) {
            <li class="conversation-message" [class.conversation-message-own]="isOwn(message)">
              <p class="conversation-message-author">{{ isOwn(message) ? 'You' : otherParticipantName() }}</p>
              <p class="conversation-message-body">{{ message.body }}</p>
              <time class="conversation-message-time" [attr.datetime]="message.createdAt">
                {{ message.createdAt | date: 'medium' }}
              </time>
            </li>
          }
        </ol>
      }
      @if (state.kind === 'ready' && state.data.totalPages > 1) {
        <nav class="conversation-pages" aria-label="Message history pages">
          <button
            type="button"
            class="btn btn-secondary"
            [disabled]="state.data.page >= state.data.totalPages"
            (click)="loadPage(state.data.page + 1)"
          >
            Older messages
          </button>
          <span class="muted">Page {{ state.data.page }} of {{ state.data.totalPages }}</span>
          <button
            type="button"
            class="btn btn-secondary"
            [disabled]="state.data.page <= 1"
            (click)="loadPage(state.data.page - 1)"
          >
            Newer messages
          </button>
        </nav>
      }

      @if (sendError(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      <form class="conversation-compose" (ngSubmit)="send()">
        <label [for]="inputId">Message</label>
        <textarea
          [id]="inputId"
          [formControl]="body"
          rows="3"
          maxlength="2000"
          [attr.aria-invalid]="body.touched && body.invalid"
          [attr.aria-describedby]="body.invalid && body.touched ? inputId + '-hint' : null"
        ></textarea>
        <div class="conversation-compose-footer">
          <p class="hint" [id]="inputId + '-hint'">{{ body.value.length }}/2000 characters</p>
          <button type="submit" class="btn btn-primary" [disabled]="sending() || !body.value.trim() || body.invalid">
            {{ sending() ? 'Sending...' : 'Send message' }}
          </button>
        </div>
      </form>
    </section>
  `,
  styleUrl: './request-messages.scss',
})
export class RequestMessages {
  private static nextId = 0;

  private readonly api = inject(MessageApi);
  private readonly auth = inject(AuthService);

  readonly requestId = input.required<string>();
  readonly providerId = input.required<string>();
  readonly otherParticipantName = input.required<string>();

  protected readonly inputId = `message-${RequestMessages.nextId++}`;
  private readonly page$ = new Subject<number>();
  protected readonly messages = signal<LoadState<RequestMessagePage>>(LOADING);
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);
  protected readonly currentPage = signal(1);
  protected readonly body = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(2000)],
  });

  constructor() {
    combineLatest([toObservable(this.requestId), toObservable(this.providerId)])
      .pipe(
        distinctUntilChanged(
          ([requestId, providerId], [nextRequestId, nextProviderId]) =>
            requestId === nextRequestId && providerId === nextProviderId,
        ),
        switchMap(([requestId, providerId]) => {
          this.currentPage.set(1);

          return this.page$.pipe(
            startWith(1),
            switchMap((page) => toLoadState(this.api.list(requestId, providerId, page))),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((state) => {
        this.messages.set(state);
        if (state.kind === 'ready') {
          this.currentPage.set(state.data.page);
        }
      });
  }

  protected refresh(): void {
    this.page$.next(this.currentPage());
  }

  protected loadPage(page: number): void {
    this.page$.next(page);
  }

  protected oldestFirst(items: RequestMessage[]): RequestMessage[] {
    return [...items].reverse();
  }

  protected isOwn(message: RequestMessage): boolean {
    return message.senderUserId === this.auth.currentUser()?.id;
  }

  protected send(): void {
    const text = this.body.value.trim();
    if (!text || this.body.invalid || this.sending()) {
      this.body.markAsTouched();
      return;
    }

    this.sending.set(true);
    this.sendError.set(null);
    this.api.send(this.requestId(), { providerId: this.providerId(), body: text }).subscribe({
      next: () => {
        this.body.reset('');
        this.sending.set(false);
        this.page$.next(1);
      },
      error: (error: unknown) => {
        this.sendError.set(toApiError(error).message);
        this.sending.set(false);
      },
    });
  }
}
