import { OfficeHourSlotDto, consistentOfficeHoursRowVersion } from './phase5.models';

function slot(id: number, rowVersion: string): OfficeHourSlotDto {
  return {
    id,
    dayOfWeek: 'Sunday',
    startsAt: '10:00:00',
    endsAt: '11:00:00',
    effectiveFrom: '2026-09-01',
    effectiveTo: null,
    source: 'TeacherSelected',
    isEligible: true,
    rowVersion
  };
}

describe('consistentOfficeHoursRowVersion', () => {
  it('returns the one shared configuration token', () => {
    expect(consistentOfficeHoursRowVersion([slot(1, 'AQID'), slot(2, 'AQID')])).toBe('AQID');
  });

  it('refuses to guess when current rows expose different tokens', () => {
    expect(consistentOfficeHoursRowVersion([slot(1, 'AQID'), slot(2, 'BAUG')])).toBeNull();
  });

  it('refuses to take a token from an empty current configuration', () => {
    expect(consistentOfficeHoursRowVersion([])).toBeNull();
  });
});
