import { addMonths } from './expirations.component'

describe('addMonths', () => {
  it('keeps the day of the month', () => {
    expect(addMonths('2026-11-22', 12)).toBe('2027-11-22')
    expect(addMonths('2026-11-22', 6)).toBe('2027-05-22')
  })

  it('uses the last day when the month is shorter', () => {
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-28')
    expect(addMonths('2028-02-29', 12)).toBe('2029-02-28')
  })
})
