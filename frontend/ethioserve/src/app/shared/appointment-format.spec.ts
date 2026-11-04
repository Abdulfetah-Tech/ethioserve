import { FormControl } from '@angular/forms';
import { AppointmentStatus } from '../core/models/appointment.models';
import { RequestStatus } from '../core/models/request.models';
import {
  appointmentExplanation,
  appointmentLength,
  appointmentStatusBadge,
  appointmentStatusLabel,
  canStartWork,
  customerActions,
  dateTimeText,
  fromLocalInput,
  hasAppointmentSection,
  isActiveAppointment,
  minLocalInput,
  providerActions,
  slotText,
  startProblem,
  startRule,
  summarizeJob,
  timeZoneName,
  toLocalInput,
} from './appointment-format';

const ALL: AppointmentStatus[] = ['Scheduled', 'Confirmed', 'InProgress', 'Completed', 'Cancelled', 'NoShow'];
const MINUTE = 60_000;
const NOW = Date.parse('2026-12-01T08:00:00Z');
const at = (ms: number): string => new Date(NOW + ms).toISOString();
const asInput = (ms: number): string => toLocalInput(at(ms));

describe('status helpers', () => {
  it('have a label, a badge and an explanation for every status and both audiences', () => {
    for (const status of ALL) {
      expect(appointmentStatusLabel(status).length).toBeGreaterThan(0);
      expect(appointmentStatusBadge(status)).toContain('badge');
      expect(appointmentExplanation(status, 'provider').length).toBeGreaterThan(0);
      expect(appointmentExplanation(status, 'customer').length).toBeGreaterThan(0);
    }
  });

  it('colour waiting appointments amber and cancelled ones red', () => {
    expect(appointmentStatusBadge('Scheduled')).toBe('badge badge-warn');
    expect(appointmentStatusBadge('Confirmed')).toBe('badge badge-ok');
    expect(appointmentStatusBadge('Cancelled')).toBe('badge badge-danger');
  });

  it('treat only scheduled, confirmed and in progress appointments as active', () => {
    expect(ALL.filter((status) => isActiveAppointment(status))).toEqual(['Scheduled', 'Confirmed', 'InProgress']);
  });

  it('explain the same step differently to each side', () => {
    expect(appointmentExplanation('Scheduled', 'provider')).not.toBe(appointmentExplanation('Scheduled', 'customer'));
  });
});

describe('hasAppointmentSection', () => {
  it('is true from acceptance onwards and false before it or after cancelling', () => {
    const all: RequestStatus[] = ['Pending', 'Quoted', 'Accepted', 'Scheduled', 'InProgress', 'Completed', 'Cancelled', 'Rejected'];

    expect(all.filter((status) => hasAppointmentSection(status))).toEqual(['Accepted', 'Scheduled', 'InProgress', 'Completed']);
  });
});

describe('slotText and dateTimeText', () => {
  const zone = 'Africa/Addis_Ababa';

  it('shows the day and the start and end times in the chosen time zone', () => {
    const text = slotText('2026-12-01T11:00:00Z', '2026-12-01T13:00:00Z', zone);

    expect(text).toContain('1 Dec 2026');
    expect(text).toContain('14:00');
    expect(text).toContain('16:00');
    expect(text).toContain(' to ');
  });

  it('shows both dates when the job ends on another day', () => {
    const text = slotText('2026-12-01T20:00:00Z', '2026-12-01T22:00:00Z', zone); // 23:00 to 01:00 in Addis Ababa

    expect(text).toContain('1 Dec 2026');
    expect(text).toContain('2 Dec 2026');
    expect(text).toContain('23:00');
    expect(text).toContain('01:00');
  });

  it('is empty for an unreadable date', () => {
    expect(slotText('nope', '2026-12-01T13:00:00Z')).toBe('');
    expect(dateTimeText('nope')).toBe('');
  });

  it('formats a single moment', () => {
    const text = dateTimeText('2026-12-01T11:00:00Z', zone);

    expect(text).toContain('1 Dec 2026');
    expect(text).toContain('14:00');
  });
});

describe('appointmentLength', () => {
  it('describes the length of the slot', () => {
    expect(appointmentLength('2026-12-01T11:00:00Z', '2026-12-01T13:00:00Z')).toBe('2 hours');
    expect(appointmentLength('2026-12-01T11:00:00Z', '2026-12-01T12:30:00Z')).toBe('1 h 30 min');
  });
});

describe('local input conversion', () => {
  it('gives the shape a date-and-time input expects', () => {
    expect(toLocalInput('2026-12-01T11:00:00Z')).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);
    expect(toLocalInput('nope')).toBe('');
  });

  it('survives a round trip to the exact minute', () => {
    const iso = '2026-12-01T11:30:00.000Z';

    expect(fromLocalInput(toLocalInput(iso))).toBe(iso);
  });

  it('interprets appointment form values in Addis Ababa regardless of device time zone', () => {
    expect(timeZoneName()).toBe('Africa/Addis_Ababa');
    expect(toLocalInput('2026-12-01T11:30:00Z')).toBe('2026-12-01T14:30');
    expect(fromLocalInput('2026-12-01T14:30')).toBe('2026-12-01T11:30:00.000Z');
  });

  it('refuses anything that is not a date and time', () => {
    expect(fromLocalInput('')).toBeNull();
    expect(fromLocalInput('2026-12-01')).toBeNull();
    expect(fromLocalInput('2026-12-01T25:99')).toBeNull();
    expect(fromLocalInput('tomorrow')).toBeNull();
  });
});

describe('startProblem', () => {
  it('asks for a date and time when there is none', () => {
    expect(startProblem('', NOW)).toBe('Choose a date and time.');
  });

  it('needs at least one hour of notice', () => {
    expect(startProblem(asInput(59 * MINUTE), NOW)).toContain('at least 1 hour');
    expect(startProblem(asInput(-3 * 60 * MINUTE), NOW)).toContain('at least 1 hour');
    expect(startProblem(asInput(60 * MINUTE), NOW)).toBeNull();
    expect(startProblem(asInput(30 * 60 * MINUTE), NOW)).toBeNull();
  });

  it('allows at most 90 days ahead', () => {
    expect(startProblem(asInput(90 * 24 * 60 * MINUTE), NOW)).toBeNull();
    expect(startProblem(asInput(91 * 24 * 60 * MINUTE), NOW)).toContain('90 days');
  });
});

describe('minLocalInput', () => {
  it('is always a time the notice rule accepts, even when the clock has seconds', () => {
    for (const now of [NOW, NOW + 30_000, NOW + 59_999]) {
      expect(startProblem(minLocalInput(now), now)).toBeNull();
    }
  });
});

describe('startRule', () => {
  it('reports a problem on the control', () => {
    expect(startRule(new FormControl(''))).toEqual({ problem: 'Choose a date and time.' });
    expect(startRule(new FormControl(toLocalInput(new Date(Date.now() + 5 * 3_600_000).toISOString())))).toBeNull();
  });
});

describe('canStartWork', () => {
  it('opens two hours before the agreed time', () => {
    expect(canStartWork(at(120 * MINUTE), NOW)).toBe(true);
    expect(canStartWork(at(30 * MINUTE), NOW)).toBe(true);
    expect(canStartWork(at(-60 * MINUTE), NOW)).toBe(true);
    expect(canStartWork(at(121 * MINUTE), NOW)).toBe(false);
    expect(canStartWork(at(72 * 60 * MINUTE), NOW)).toBe(false);
  });
});

describe('providerActions', () => {
  const soon = at(60 * MINUTE);
  const far = at(72 * 60 * MINUTE);

  it('offers changing and cancelling a waiting appointment, but not starting', () => {
    expect(providerActions('Scheduled', soon, NOW)).toEqual({ reschedule: true, cancel: true, start: false, startLater: false, complete: false });
  });

  it('offers starting a confirmed appointment only inside the two-hour window', () => {
    expect(providerActions('Confirmed', soon, NOW)).toEqual({ reschedule: true, cancel: true, start: true, startLater: false, complete: false });
    expect(providerActions('Confirmed', far, NOW)).toEqual({ reschedule: true, cancel: true, start: false, startLater: true, complete: false });
  });

  it('offers only completing a job that is under way', () => {
    expect(providerActions('InProgress', soon, NOW)).toEqual({ reschedule: false, cancel: false, start: false, startLater: false, complete: true });
  });

  it('offers nothing once the appointment is over', () => {
    for (const status of ['Completed', 'Cancelled', 'NoShow'] as AppointmentStatus[]) {
      expect(providerActions(status, soon, NOW)).toEqual({ reschedule: false, cancel: false, start: false, startLater: false, complete: false });
    }
  });
});

describe('customerActions', () => {
  it('lets the customer confirm only a proposed time and cancel an open appointment', () => {
    expect(customerActions('Scheduled')).toEqual({ confirm: true, cancel: true });
    expect(customerActions('Confirmed')).toEqual({ confirm: false, cancel: true });

    for (const status of ['InProgress', 'Completed', 'Cancelled', 'NoShow'] as AppointmentStatus[]) {
      expect(customerActions(status)).toEqual({ confirm: false, cancel: false });
    }
  });
});

describe('summarizeJob', () => {
  const item = (status: AppointmentStatus) => ({ status });

  it('shows the active appointment first', () => {
    expect(summarizeJob([item('Confirmed')], [item('Cancelled')])).toEqual({ current: item('Confirmed'), lastCancelled: false });
  });

  it('shows a completed appointment when nothing is active', () => {
    expect(summarizeJob([], [item('Completed'), item('Cancelled')]).current).toEqual(item('Completed'));
  });

  it('reports a cancelled appointment when there is nothing to show', () => {
    expect(summarizeJob([], [item('Cancelled')])).toEqual({ current: null, lastCancelled: true });
  });

  it('has nothing to show for a job without appointments', () => {
    expect(summarizeJob([], [])).toEqual({ current: null, lastCancelled: false });
  });
});