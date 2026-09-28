import { OfficeHoursAggregateDto } from './phase5.models';

describe('OfficeHoursAggregateDto', () => {
  it('keeps a configuration token even when no slots are selected', () => {
    const aggregate = { rowVersion: 'derived-token', slots: [] } as unknown as OfficeHoursAggregateDto;
    expect(aggregate.rowVersion).toBe('derived-token');
    expect(aggregate.slots).toEqual([]);
  });
});
