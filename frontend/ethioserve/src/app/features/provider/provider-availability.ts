import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { toApiError } from '../../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { ProviderApi } from '../../core/api/provider.api';
import {
  AvailabilityDateException,
  AvailabilityWindow,
  ProviderAvailabilityCalendar,
  WeeklyAvailabilityDay,
} from '../../core/models/provider.models';

const DAYS = [
  { dayOfWeek: 0, name: 'Sunday' },
  { dayOfWeek: 1, name: 'Monday' },
  { dayOfWeek: 2, name: 'Tuesday' },
  { dayOfWeek: 3, name: 'Wednesday' },
  { dayOfWeek: 4, name: 'Thursday' },
  { dayOfWeek: 5, name: 'Friday' },
  { dayOfWeek: 6, name: 'Saturday' },
];

const DEFAULT_WINDOW: AvailabilityWindow = { startMinute: 540, endMinute: 1020 };

function blankCalendar(): ProviderAvailabilityCalendar {
  return { isConfigured: false, weeklyHours: [], dateExceptions: [] };
}

@Component({
  selector: 'app-provider-availability',
  template: `
    <section class="card" aria-labelledby="availability-heading">
      <h2 id="availability-heading">Availability calendar</h2>
      <p class="muted">
        All calendar and appointment times use Africa/Addis_Ababa (EAT, UTC+3). A proposed appointment must fit entirely
        inside one open time range.
      </p>

      @if (notice(); as message) {
        <p class="alert alert-success" role="status">{{ message }}</p>
      }

      @let state = calendar();
      @if (state.kind === 'loading') {
        <p class="muted" role="status">Loading your calendar...</p>
      } @else if (state.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ state.message }}</p>
        <button type="button" class="btn btn-secondary" (click)="load()">Try again</button>
      } @else if (!editing()) {
        <p class="muted">No calendar is set. Appointment proposals are not restricted by weekly hours.</p>
        <button type="button" class="btn btn-primary" (click)="startEditing()">Set calendar</button>
      } @else {
        @if (errorMessage(); as message) {
          <p class="alert alert-error" role="alert">{{ message }}</p>
        }

        <p class="hint">Days without hours are closed. Add up to four time ranges per day.</p>

        <div class="availability-days">
          @for (day of days; track day.dayOfWeek) {
            <fieldset class="availability-day">
              <legend>{{ day.name }}</legend>
              @for (window of windowsFor(day.dayOfWeek); track $index) {
                <div class="availability-window">
                  <label>
                    <span>From</span>
                    <input
                      type="time"
                      [value]="timeText(window.startMinute)"
                      [attr.aria-label]="day.name + ' opening time'"
                      (change)="changeWeeklyWindow(day.dayOfWeek, $index, 'startMinute', $event)"
                    />
                  </label>
                  <label>
                    <span>To</span>
                    <input
                      type="time"
                      [value]="timeText(window.endMinute)"
                      [attr.aria-label]="day.name + ' closing time'"
                      (change)="changeWeeklyWindow(day.dayOfWeek, $index, 'endMinute', $event)"
                    />
                  </label>
                  <button
                    type="button"
                    class="btn btn-ghost"
                    [attr.aria-label]="'Remove ' + day.name + ' hours'"
                    (click)="removeWeeklyWindow(day.dayOfWeek, $index)"
                  >
                    Remove
                  </button>
                </div>
              } @empty {
                <p class="muted">Closed</p>
              }
              <button
                type="button"
                class="btn btn-secondary"
                [disabled]="windowsFor(day.dayOfWeek).length >= 4"
                (click)="addWeeklyWindow(day.dayOfWeek)"
              >
                Add hours
              </button>
            </fieldset>
          }
        </div>

        <section class="availability-exceptions" aria-labelledby="exceptions-heading">
          <h3 id="exceptions-heading">Date exceptions</h3>
          <p class="muted">Exceptions replace that date's weekly hours, for example holidays or one-off openings.</p>
          @for (exception of draft().dateExceptions; track $index; let exceptionIndex = $index) {
            <fieldset class="availability-exception">
              <legend>Exception {{ exceptionIndex + 1 }}</legend>
              <label>
                <span>Date</span>
                <input
                  type="date"
                  [value]="exception.date"
                  [attr.aria-label]="'Exception ' + (exceptionIndex + 1) + ' date'"
                  (change)="changeExceptionDate(exceptionIndex, $event)"
                />
              </label>
              <label class="availability-closed">
                <input
                  type="checkbox"
                  [checked]="exception.isClosed"
                  (change)="toggleExceptionClosed(exceptionIndex, $event)"
                />
                Closed all day
              </label>
              @if (!exception.isClosed) {
                @for (window of exception.windows; track $index; let windowIndex = $index) {
                  <div class="availability-window">
                    <label>
                      <span>From</span>
                      <input
                        type="time"
                        [value]="timeText(window.startMinute)"
                        [attr.aria-label]="'Exception ' + (exceptionIndex + 1) + ' opening time'"
                        (change)="changeExceptionWindow(exceptionIndex, windowIndex, 'startMinute', $event)"
                      />
                    </label>
                    <label>
                      <span>To</span>
                      <input
                        type="time"
                        [value]="timeText(window.endMinute)"
                        [attr.aria-label]="'Exception ' + (exceptionIndex + 1) + ' closing time'"
                        (change)="changeExceptionWindow(exceptionIndex, windowIndex, 'endMinute', $event)"
                      />
                    </label>
                    <button
                      type="button"
                      class="btn btn-ghost"
                      [attr.aria-label]="'Remove exception ' + (exceptionIndex + 1) + ' hours'"
                      (click)="removeExceptionWindow(exceptionIndex, windowIndex)"
                    >
                      Remove hours
                    </button>
                  </div>
                } @empty {
                  <p class="muted">No hours set for this date.</p>
                }
                <button
                  type="button"
                  class="btn btn-secondary"
                  [disabled]="exception.windows.length >= 4"
                  (click)="addExceptionWindow(exceptionIndex)"
                >
                  Add exception hours
                </button>
              }
              <button type="button" class="btn btn-ghost" (click)="removeException(exceptionIndex)">Remove exception</button>
            </fieldset>
          } @empty {
            <p class="muted">No date exceptions.</p>
          }
          <button type="button" class="btn btn-secondary" [disabled]="draft().dateExceptions.length >= 365" (click)="addException()">
            Add date exception
          </button>
        </section>

        @if (validationError(); as message) {
          <p class="field-error" role="alert">{{ message }}</p>
        }

        <div class="form-actions">
          <button type="button" class="btn btn-primary" [disabled]="saving() || validationError() !== null" (click)="save()">
            {{ saving() ? 'Saving...' : 'Save calendar' }}
          </button>
          <button type="button" class="btn btn-secondary" [disabled]="saving()" (click)="discardChanges()">
            Discard changes
          </button>
          @if (state.data.isConfigured) {
            <button type="button" class="btn btn-danger" [disabled]="saving()" (click)="confirmClear.set(true)">
              Remove calendar
            </button>
          }
          @if (confirmClear()) {
            <span>Remove the calendar and allow proposals at any time?</span>
            <button type="button" class="btn btn-danger" [disabled]="saving()" (click)="clear()">Yes, remove</button>
            <button type="button" class="btn btn-secondary" (click)="confirmClear.set(false)">Keep calendar</button>
          }
        </div>
      }
    </section>
  `,
  styleUrl: './provider-availability.scss',
})
export class ProviderAvailability {
  private readonly api = inject(ProviderApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly days = DAYS;
  protected readonly calendar = signal<LoadState<ProviderAvailabilityCalendar>>(LOADING);
  protected readonly draft = signal<ProviderAvailabilityCalendar>(blankCalendar());
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly confirmClear = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly validationError = computed(() => this.validateDraft());

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api
      .availabilityCalendar()
      .pipe(toLoadState, takeUntilDestroyed(this.destroyRef))
      .subscribe((state) => {
        this.calendar.set(state);
        if (state.kind === 'ready') {
          if (state.data.isConfigured) {
            this.draft.set(state.data);
            this.editing.set(true);
          } else {
            this.draft.set(blankCalendar());
            this.editing.set(false);
          }
        }
      });
  }

  protected startEditing(): void {
    this.draft.set(blankCalendar());
    this.editing.set(true);
    this.notice.set(null);
    this.errorMessage.set(null);
  }

  protected windowsFor(dayOfWeek: number): AvailabilityWindow[] {
    return this.draft().weeklyHours.find((day) => day.dayOfWeek === dayOfWeek)?.windows ?? [];
  }

  protected timeText(minute: number): string {
    if (minute < 0 || minute > 1439) {
      return '';
    }

    return `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
  }

  protected addWeeklyWindow(dayOfWeek: number): void {
    const windows = this.windowsFor(dayOfWeek);
    const newWindow = windows.length === 0 ? DEFAULT_WINDOW : { startMinute: -1, endMinute: -1 };
    this.updateWeekly(dayOfWeek, [...windows, newWindow]);
  }

  protected changeWeeklyWindow(
    dayOfWeek: number,
    index: number,
    field: keyof AvailabilityWindow,
    event: Event,
  ): void {
    const windows = [...this.windowsFor(dayOfWeek)];
    windows[index] = { ...windows[index], [field]: this.parseTime(event) };
    this.updateWeekly(dayOfWeek, windows);
  }

  protected removeWeeklyWindow(dayOfWeek: number, index: number): void {
    this.updateWeekly(dayOfWeek, this.windowsFor(dayOfWeek).filter((_, itemIndex) => itemIndex !== index));
  }

  protected addException(): void {
    this.draft.update((calendar) => ({
      ...calendar,
      dateExceptions: [...calendar.dateExceptions, { date: '', isClosed: false, windows: [{ ...DEFAULT_WINDOW }] }],
    }));
  }

  protected changeExceptionDate(index: number, event: Event): void {
    this.updateException(index, (exception) => ({ ...exception, date: this.inputValue(event) }));
  }

  protected toggleExceptionClosed(index: number, event: Event): void {
    const closed = (event.target as HTMLInputElement).checked;
    this.updateException(index, (exception) => ({
      ...exception,
      isClosed: closed,
      windows: closed ? [] : exception.windows.length ? exception.windows : [{ ...DEFAULT_WINDOW }],
    }));
  }

  protected addExceptionWindow(index: number): void {
    this.updateException(index, (exception) => ({
      ...exception,
      windows: [...exception.windows, { startMinute: -1, endMinute: -1 }],
    }));
  }

  protected changeExceptionWindow(
    exceptionIndex: number,
    windowIndex: number,
    field: keyof AvailabilityWindow,
    event: Event,
  ): void {
    this.updateException(exceptionIndex, (exception) => {
      const windows = [...exception.windows];
      windows[windowIndex] = { ...windows[windowIndex], [field]: this.parseTime(event) };

      return { ...exception, windows };
    });
  }

  protected removeExceptionWindow(exceptionIndex: number, windowIndex: number): void {
    this.updateException(exceptionIndex, (exception) => ({
      ...exception,
      windows: exception.windows.filter((_, index) => index !== windowIndex),
    }));
  }

  protected removeException(index: number): void {
    this.draft.update((calendar) => ({
      ...calendar,
      dateExceptions: calendar.dateExceptions.filter((_, exceptionIndex) => exceptionIndex !== index),
    }));
  }

  protected save(): void {
    if (this.validationError()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.notice.set(null);
    const { weeklyHours, dateExceptions } = this.draft();
    this.api.updateAvailabilityCalendar({ weeklyHours, dateExceptions })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (calendar) => {
          this.calendar.set({ kind: 'ready', data: calendar });
          this.draft.set(calendar);
          this.saving.set(false);
          this.notice.set('Your availability calendar has been saved.');
        },
        error: (error: unknown) => {
          this.errorMessage.set(toApiError(error).message);
          this.saving.set(false);
        },
      });
  }

  protected discardChanges(): void {
    const state = this.calendar();
    if (state.kind !== 'ready') {
      return;
    }

    this.draft.set(state.data.isConfigured ? state.data : blankCalendar());
    this.editing.set(state.data.isConfigured);
    this.confirmClear.set(false);
    this.notice.set(null);
    this.errorMessage.set(null);
  }

  protected clear(): void {
    this.saving.set(true);
    this.errorMessage.set(null);
    this.api.clearAvailabilityCalendar().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.calendar.set({ kind: 'ready', data: blankCalendar() });
        this.draft.set(blankCalendar());
        this.editing.set(false);
        this.confirmClear.set(false);
        this.saving.set(false);
        this.notice.set('The calendar was removed. Appointment proposals are no longer restricted by hours.');
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.saving.set(false);
      },
    });
  }

  private updateWeekly(dayOfWeek: number, windows: AvailabilityWindow[]): void {
    this.draft.update((calendar) => ({
      ...calendar,
      weeklyHours: [
        ...calendar.weeklyHours.filter((day) => day.dayOfWeek !== dayOfWeek),
        ...(windows.length ? [{ dayOfWeek, windows }] : []),
      ].sort((a, b) => a.dayOfWeek - b.dayOfWeek),
    }));
    this.notice.set(null);
  }

  private updateException(
    index: number,
    update: (exception: AvailabilityDateException) => AvailabilityDateException,
  ): void {
    this.draft.update((calendar) => ({
      ...calendar,
      dateExceptions: calendar.dateExceptions.map((exception, exceptionIndex) =>
        exceptionIndex === index ? update(exception) : exception,
      ),
    }));
    this.notice.set(null);
  }

  private parseTime(event: Event): number {
    const match = /^([01]\d|2[0-3]):([0-5]\d)$/.exec(this.inputValue(event));

    return match ? Number(match[1]) * 60 + Number(match[2]) : -1;
  }

  private inputValue(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  private validateDraft(): string | null {
    const calendar = this.draft();
    if (calendar.weeklyHours.some((day) => !this.validWindows(day.windows, true))) {
      return 'Each weekly day can have up to four valid, non-overlapping time ranges.';
    }

    if (calendar.dateExceptions.length > 365) {
      return 'You can add up to 365 date exceptions.';
    }

    const dates = calendar.dateExceptions.map((exception) => exception.date);
    if (dates.some((date) => !this.validDate(date)) || new Set(dates).size !== dates.length) {
      return 'Enter a unique date for every exception.';
    }

    if (
      calendar.dateExceptions.some(
        (exception) =>
          exception.isClosed
            ? exception.windows.length !== 0
            : !this.validWindows(exception.windows, false),
      )
    ) {
      return 'A closed date cannot have hours; an open exception needs up to four valid, non-overlapping ranges.';
    }

    return null;
  }

  private validWindows(windows: AvailabilityWindow[], mayBeEmpty: boolean): boolean {
    if ((!mayBeEmpty && windows.length === 0) || windows.length > 4) {
      return false;
    }

    const ordered = [...windows].sort((a, b) => a.startMinute - b.startMinute);

    return ordered.every(
      (window, index) =>
        window.startMinute >= 0 &&
        window.startMinute < 1439 &&
        window.endMinute > window.startMinute &&
        window.endMinute <= 1439 &&
        (index === 0 || ordered[index - 1].endMinute <= window.startMinute),
    );
  }

  private validDate(value: string): boolean {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
      return false;
    }

    const time = Date.parse(`${value}T00:00:00Z`);

    return Number.isFinite(time) && new Date(time).toISOString().slice(0, 10) === value;
  }
}
