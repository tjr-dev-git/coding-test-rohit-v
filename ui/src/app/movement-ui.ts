import { MovementPriority, MovementStatus } from './models';

const STATUS_LABELS: Record<MovementStatus, string> = {
  Requested: 'Requested',
  Assigned: 'Assigned',
  InProgress: 'In progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled'
};

const STATUS_ICONS: Record<MovementStatus, string> = {
  Requested: 'inbox',
  Assigned: 'assignment_ind',
  InProgress: 'local_shipping',
  Completed: 'check_circle',
  Cancelled: 'cancel'
};

const TRANSITIONS: Record<MovementStatus, readonly MovementStatus[]> = {
  Requested: ['Requested', 'Assigned', 'Cancelled'],
  Assigned: ['Assigned', 'InProgress', 'Cancelled'],
  InProgress: ['InProgress', 'Completed', 'Cancelled'],
  Completed: [],
  Cancelled: []
};

export interface ShiftWindow {
  shiftDate: string;
  shift: 'Day' | 'Night';
}

export function allowedTransitions(status: MovementStatus): readonly MovementStatus[] {
  return TRANSITIONS[status];
}

export function isTerminal(status: MovementStatus): boolean {
  return status === 'Completed' || status === 'Cancelled';
}

export function requiresEquipment(status: MovementStatus): boolean {
  return status === 'Assigned' || status === 'InProgress';
}

export function statusLabel(status: MovementStatus): string {
  return STATUS_LABELS[status];
}

export function statusIcon(status: MovementStatus): string {
  return STATUS_ICONS[status];
}

export function priorityIcon(priority: MovementPriority): string {
  return priority === 'Urgent' ? 'priority_high' : 'schedule';
}

export function formatSiteTime(iso: string, timeZoneId: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: timeZoneId,
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false
  }).format(new Date(iso));
}

/**
 * The API stores requestedAtUtc in UTC, but the shift is a site-local concept.
 * The day shift runs from 06:00 to 18:00 and the night shift runs from 18:00
 * to 06:00 on the following calendar day.
 */
export function siteShiftForInstant(iso: string, timeZoneId: string): ShiftWindow {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: timeZoneId,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    hour12: false
  }).formatToParts(new Date(iso));

  const part = (type: Intl.DateTimeFormatPartTypes): number =>
    Number(parts.find(item => item.type === type)?.value ?? '0');

  const year = part('year');
  const month = part('month');
  const day = part('day');
  const hour = part('hour') % 24;

  if (hour >= 18) {
    return { shiftDate: toDateString(year, month, day), shift: 'Night' };
  }

  if (hour >= 6) {
    return { shiftDate: toDateString(year, month, day), shift: 'Day' };
  }

  return { shiftDate: toDateString(year, month, day, -1), shift: 'Night' };
}

function toDateString(year: number, month: number, day: number, offsetDays = 0): string {
  const date = new Date(Date.UTC(year, month - 1, day + offsetDays));
  return date.toISOString().slice(0, 10);
}
