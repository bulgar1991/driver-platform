import { delta, heatLevel } from './statistics.component'

describe('statistics helpers', () => {
  it('compares with the previous period', () => {
    expect(delta(120, 100)).toEqual({ change: 0.2, up: true })
    expect(delta(50, 100)).toEqual({ change: -0.5, up: false })
    expect(delta(10, 0)).toBeNull()
  })

  it('grades the busy hours in five steps', () => {
    expect(heatLevel(0, 10)).toBe(0)
    expect(heatLevel(1, 10)).toBe(1)
    expect(heatLevel(10, 10)).toBe(5)
    expect(heatLevel(3, 0)).toBe(0)
  })
})
