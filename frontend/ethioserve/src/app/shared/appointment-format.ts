import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { AppointmentStatus } from '../core/models/appointment.models';
import { RequestStatus } from '../core/models/request.models';
import { durationText } from './quote-format';

const MINUTE = 60_000;
const DAY = 24 * 60 * MINUTE;
const ADDIS_OFFSET_MINUTES = 3 * 60;
const ADDIS_TIME_ZONE = 'Africa/Addis_Ababa';

/** These mirror the API, which still decides. */
export const MIN_LEAD_MINUTES = 60;
export const MAX_ADVANCE_DAYS = 90;
export const EARLY_START_MINUTES = 120;

const LABELS: Record<AppointmentStatus, string> = {
  Scheduled: 'Waiting for confirmation',
  Confirmed: 'Confirmed',
  InProgress: 'In progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  NoShow: 'Missed',
};

const BADGES: Record<AppointmentStatus, string> = {
  Scheduled: 'badge badge-warn',
  Confirmed: 'badge badge-ok',
  InProgress: 'badge badge-ok',
  Completed: 'badge badge-ok',
  Cancelled: 'badge badge-danger',
  NoShow: 'badge badge-danger',
};

export type Audience = 'provider' | 'customer';

export function appointmentStatusLabel(status: AppointmentStatus): string {
  return LABELS[status];
}

export function appointmentStatusBadge(status: AppointmentStatus): string {
  return BADGES[status];
}

export function appointmentExplanation(status: AppointmentStatus, audience: Audience): string {
  switch (status) {
    case 'Scheduled':
      return audience === 'provider'
        ? 'Waiting for the customer to confirm this time. Changing the time asks them to confirm again.'
        : 'The provider proposed this time. Confirm it, or cancel the appointment to ask for another.';
    case 'Confirmed':
      return audience === 'provider' ? 'The customer confirmed this time.' : 'You confirmed this time.';
    case 'InProgress':
      return 'The job is under way.';
    case 'Completed':
      return 'The job is finished.';
    case 'Cancelled':
      return 'This appointment was cancelled.';
    default:
      return 'This appointment was missed.';
  }
}

export function isActiveAppointment(status: AppointmentStatus): boolean {
  return status === 'Scheduled' || status === 'Confirmed' || status === 'InProgress';
}

/** The sections of a customer's request page that deal with an appointment. */
export function hasAppointmentSection(status: RequestStatus): boolean {
  return status === 'Accepted' || status === 'Scheduled' || status === 'InProgress' || status === 'Completed';
}

export function timeZoneName(): string {
  return ADDIS_TIME_ZONE;
}

function dateText(date: Date, timeZone: string = ADDIS_TIME_ZONE): string {
  const options: Intl.DateTimeFormatOptions = { timeZone };

  return date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', ...options });
}

function timeText(date: Date, timeZone: string = ADDIS_TIME_ZONE): string {
  const options: Intl.DateTimeFormatOptions = { timeZone };

  return date.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', ...options });
}

/** For example "1 Dec 2026, 14:00". Appointment times use Addis Ababa unless another zone is requested. */
export function dateTimeText(iso: string, timeZone?: string): string {
  const date = new Date(iso);

  return Number.isNaN(date.getTime()) ? '' : `${dateText(date, timeZone)}, ${timeText(date, timeZone)}`;
}

/** For example "1 Dec 2026, 14:00 to 16:00". A job that ends on another day shows both dates. */
export function slotText(startIso: string, endIso: string, timeZone?: string): string {
  const start = new Date(startIso);
  const end = new Date(endIso);

  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
    return '';
  }

  const sameDay = dateText(start, timeZone) === dateText(end, timeZone);

  return sameDay
    ? `${dateText(start, timeZone)}, ${timeText(start, timeZone)} to ${timeText(end, timeZone)}`
    : `${dateText(start, timeZone)}, ${timeText(start, timeZone)} to ${dateText(end, timeZone)}, ${timeText(end, timeZone)}`;
}

export function appointmentLength(startIso: string, endIso: string): string {
  return durationText(Math.round((Date.parse(endIso) - Date.parse(startIso)) / MINUTE));
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** A moment as the value of a date-and-time input, in Addis Ababa time. */
export function toLocalInput(iso: string): string {
  const date = new Date(Date.parse(iso) + ADDIS_OFFSET_MINUTES * MINUTE);

  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}T${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`;
}

/** The value of a date-and-time input as an exact moment (UTC), or null when it is empty or malformed. */
export function fromLocalInput(value: string): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!match) {
    return null;
  }

  const [, yearText, monthText, dayText, hourText, minuteText] = match;
  const year = Number(yearText);
  const month = Number(monthText);
  const day = Number(dayText);
  const hour = Number(hourText);
  const minute = Number(minuteText);
  const local = new Date(0);
  local.setUTCFullYear(year, month - 1, day);
  local.setUTCHours(hour, minute, 0, 0);

  if (
    year < 1 ||
    local.getUTCFullYear() !== year ||
    local.getUTCMonth() !== month - 1 ||
    local.getUTCDate() !== day ||
    local.getUTCHours() !== hour ||
    local.getUTCMinutes() !== minute
  ) {
    return null;
  }

  return new Date(local.getTime() - ADDIS_OFFSET_MINUTES * MINUTE).toISOString();
}

/** The earliest value the picker should offer: one minute past the notice period, so rounding never lets a bad time in. */
export function minLocalInput(now: number = Date.now()): string {
  return toLocalInput(new Date(now + (MIN_LEAD_MINUTES + 1) * MINUTE).toISOString());
}

export function startProblem(value: string, now: number = Date.now()): string | null {
  const iso = fromLocalInput(value);

  if (iso === null) {
    return 'Choose a date and time.';
  }

  const start = Date.parse(iso);

  if (start < now + MIN_LEAD_MINUTES * MINUTE) {
    return 'Choose a time at least 1 hour from now.';
  }

  if (start > now + MAX_ADVANCE_DAYS * DAY) {
    return 'Choose a time within the next 90 days.';
  }

  return null;
}

export const startRule: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const problem = startProblem(String(control.value ?? ''));

  return problem ? { problem } : null;
};

export function canStartWork(startIso: string, now: number = Date.now()): boolean {
  return Date.parse(startIso) - now <= EARLY_START_MINUTES * MINUTE;
}

export interface ProviderActions {
  reschedule: boolean;
  cancel: boolean;
  start: boolean;
  /** The job can be started, but not yet: the early-start window has not opened. */
  startLater: boolean;
  complete: boolean;
}

/** Mirrors the API: only the steps it will accept are offered. */
export function providerActions(status: AppointmentStatus, startIso: string, now: number = Date.now()): ProviderActions {
  const open = status === 'Scheduled' || status === 'Confirmed';
  const startable = status === 'Confirmed' && canStartWork(startIso, now);

  return {
    reschedule: open,
    cancel: open,
    start: startable,
    startLater: status === 'Confirmed' && !startable,
    complete: status === 'InProgress',
  };
}

export interface CustomerActions {
  confirm: boolean;
  cancel: boolean;
}

export function customerActions(status: AppointmentStatus): CustomerActions {
  return {
    confirm: status === 'Scheduled',
    cancel: status === 'Scheduled' || status === 'Confirmed',
  };
}

export interface JobSummary<T> {
  /** The appointment to show: the active one, otherwise the completed one. */
  current: T | null;
  /** There is no appointment to show and the last one was cancelled. */
  lastCancelled: boolean;
}

/** Reduces a job's upcoming list and history (newest first) to what the screen needs. */
export function summarizeJob<T extends { status: AppointmentStatus }>(upcoming: readonly T[], history: readonly T[]): JobSummary<T> {
  const current = upcoming[0] ?? history.find((item) => item.status === 'Completed') ?? null;

  return { current, lastCancelled: current === null && history[0]?.status === 'Cancelled' };
}