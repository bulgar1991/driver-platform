import { CalendarRide } from '@/core/models'
import {
  periodRange,
  place,
  shiftAnchor,
  ticks,
  wallMinutes,
} from './calendar-layout'

const ride = (id: string, start: string, minutes: number): CalendarRide =>
  ({ id, start, durationMinutes: minutes }) as CalendarRide

describe('calendar layout', () => {
  it('stacks overlapping rides in lanes and reuses a lane once it is free', () => {
    const start = wallMinutes('2026-11-21')
    const { bars, lanes } = place(
      [
        ride('a', '2026-11-21T08:00:00', 60),
        ride('b', '2026-11-21T08:30:00', 60),
        ride('c', '2026-11-21T09:00:00', 30),
      ],
      start,
      24 * 60
    )
    expect(lanes).toBe(2)
    expect(bars.find(b => b.ride.id === 'a')!.lane).toBe(0)
    expect(bars.find(b => b.ride.id === 'b')!.lane).toBe(1)
    expect(bars.find(b => b.ride.id === 'c')!.lane).toBe(0)
    expect(bars.find(b => b.ride.id === 'a')!.left).toBeCloseTo((8 / 24) * 100)
  })

  it('clips a ride that started the evening before', () => {
    const { bars } = place(
      [ride('x', '2026-11-20T23:00:00', 120)],
      wallMinutes('2026-11-21'),
      24 * 60
    )
    expect(bars[0].left).toBe(0)
    expect(bars[0].clippedStart).toBe(true)
    expect(bars[0].width).toBeCloseTo((60 / (24 * 60)) * 100)
  })

  it('builds weeks from Monday and months from the first', () => {
    expect(periodRange('2026-11-21', 'week')).toEqual({
      from: '2026-11-16',
      to: '2026-11-22',
    })
    expect(periodRange('2026-02-10', 'month')).toEqual({
      from: '2026-02-01',
      to: '2026-02-28',
    })
    expect(shiftAnchor('2026-01-31', 'month', 1)).toBe('2026-02-01')
    expect(shiftAnchor('2026-11-21', 'days3', -1)).toBe('2026-11-18')
  })

  it('ticks hours for a day and days for a week', () => {
    expect(ticks('2026-11-21', '2026-11-21')).toHaveLength(24)
    const week = ticks('2026-11-16', '2026-11-22')
    expect(week).toHaveLength(7)
    expect(week[5].weekend).toBe(true)
  })
})
