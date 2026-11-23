import { addMonths, previousMonth } from './statements.component'

describe('statement months', () => {
  it('moves across the year', () => {
    expect(addMonths('2026-12', 1)).toBe('2027-01')
    expect(addMonths('2026-01', -1)).toBe('2025-12')
    expect(addMonths('2026-05', 0)).toBe('2026-05')
  })

  it('opens on the last month, the one usually billed', () => {
    expect(previousMonth('2026-11-23')).toBe('2026-10')
    expect(previousMonth('2026-01-05')).toBe('2025-12')
  })
})
