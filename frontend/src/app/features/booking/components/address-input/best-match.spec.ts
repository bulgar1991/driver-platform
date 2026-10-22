import { Place } from '@/core/models'
import { bestMatch } from './address-input.component'

const place = (label: string, kind: Place['kind'] = 'Address'): Place => ({
  label,
  kind,
  address: label,
  latitude: 0,
  longitude: 0,
})

describe('bestMatch', () => {
  const forRoma = [
    place('Piazzale Roma · Venice historic centre', 'MeetingPoint'),
    place(
      'Fiumicino Terminal 3 arrivals · Rome Fiumicino Airport (FCO)',
      'MeetingPoint'
    ),
    place('Rome, Rome'),
    place('Roma Capitale'),
  ]

  it('takes the city, not a meeting point that only mentions it', () => {
    expect(bestMatch('Roma', forRoma)?.label).toBe('Rome, Rome')
  })

  it('takes the meeting point when its name is what was typed', () => {
    expect(bestMatch('fiumicino', forRoma)?.label).toContain(
      'Fiumicino Terminal 3'
    )
    expect(bestMatch('Piazzale Roma', forRoma)?.label).toContain(
      'Piazzale Roma'
    )
  })

  it('ignores accents and case, and falls back to the first result', () => {
    const places = [place('Forlì airport', 'MeetingPoint')]
    expect(bestMatch('FORLI', places)?.label).toBe('Forlì airport')
    expect(bestMatch('nothing', [])).toBeUndefined()
  })
})
