import { DurationPipe } from './duration.pipe'
import { MoneyPipe } from './money.pipe'

describe('pipes', () => {
  it('formats euros', () => {
    expect(new MoneyPipe().transform(58)).toBe('€58.00')
    expect(new MoneyPipe().transform(-6.38)).toBe('-€6.38')
    expect(new MoneyPipe().transform(null)).toBe('')
  })

  it('formats durations', () => {
    const pipe = new DurationPipe()
    expect(pipe.transform(35)).toBe('35 min')
    expect(pipe.transform(60)).toBe('1 h')
    expect(pipe.transform(95)).toBe('1 h 35 min')
  })
})
