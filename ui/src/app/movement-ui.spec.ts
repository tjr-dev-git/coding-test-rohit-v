import {
  allowedTransitions,
  isTerminal,
  requiresEquipment,
  siteShiftForInstant
} from './movement-ui';

describe('movement UI helpers', () => {
  it('maps an instant to the site-local night shift', () => {
    expect(siteShiftForInstant('2026-08-17T18:30:00Z', 'Asia/Kolkata'))
      .toEqual({ shiftDate: '2026-08-17', shift: 'Night' });
  });

  it('maps an early-morning instant to the previous night shift date', () => {
    expect(siteShiftForInstant('2026-08-17T00:29:00Z', 'Asia/Kolkata'))
      .toEqual({ shiftDate: '2026-08-16', shift: 'Night' });
  });

  it('maps the 06:00 site-local boundary to the day shift', () => {
    expect(siteShiftForInstant('2026-08-17T00:30:00Z', 'Asia/Kolkata'))
      .toEqual({ shiftDate: '2026-08-17', shift: 'Day' });
  });

  it('maps the 18:00 site-local boundary to the night shift', () => {
    expect(siteShiftForInstant('2026-08-17T12:30:00Z', 'Asia/Kolkata'))
      .toEqual({ shiftDate: '2026-08-17', shift: 'Night' });
  });

  it('returns no transitions for terminal statuses', () => {
    expect(allowedTransitions('Completed')).toEqual([]);
    expect(allowedTransitions('Cancelled')).toEqual([]);
    expect(isTerminal('Completed')).toBeTrue();
    expect(isTerminal('Cancelled')).toBeTrue();
  });

  it('allows only the forward lifecycle transitions', () => {
    expect(allowedTransitions('Requested')).toEqual(['Requested', 'Assigned', 'Cancelled']);
    expect(allowedTransitions('Assigned')).toEqual(['Assigned', 'InProgress', 'Cancelled']);
    expect(allowedTransitions('InProgress')).toEqual(['InProgress', 'Completed', 'Cancelled']);
  });

  it('requires equipment for assigned and in-progress movements', () => {
    expect(requiresEquipment('Assigned')).toBeTrue();
    expect(requiresEquipment('InProgress')).toBeTrue();
    expect(requiresEquipment('Requested')).toBeFalse();
    expect(requiresEquipment('Completed')).toBeFalse();
    expect(requiresEquipment('Cancelled')).toBeFalse();
  });
});
