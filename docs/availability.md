# Provider availability

Providers can manage a recurring weekly calendar and date-specific exceptions from **Your profile**. Calendar times and appointment entry/display use `Africa/Addis_Ababa` (EAT, UTC+3), regardless of the device time zone.

## Rules

- A day omitted from weekly hours is closed. A day can have up to four non-overlapping time ranges.
- A date exception replaces that date's weekly hours. It can close the full day or define up to four open ranges.
- An unconfigured calendar leaves appointment proposals unrestricted for backward compatibility. Removing a calendar restores that behavior.
- New and rescheduled appointments must fit entirely inside one open range on the same Addis Ababa calendar date. Existing appointments are not changed when calendar hours are edited.
- The separate **Available** profile toggle controls provider discovery; the calendar controls appointment times only.

## API

- `GET /api/providers/me/availability-calendar` returns the configured schedule, or `isConfigured: false` when none exists.
- `PUT /api/providers/me/availability-calendar` replaces weekly hours and date exceptions.
- `DELETE /api/providers/me/availability-calendar` removes the schedule.

All three endpoints require the authenticated provider profile. The update is serialized with appointment proposals and reschedules so a booking cannot race a calendar change.
