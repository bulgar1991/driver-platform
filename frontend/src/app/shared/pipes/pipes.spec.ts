import { LOCALE_ID } from '@angular/core'
import { TestBed } from '@angular/core/testing'
import { MeetingPoint } from '@/core/models'
import { DurationPipe } from './duration.pipe'
import { InstructionsPipe } from './instructions.pipe'
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

  it('shows meeting point instructions in the site language', () => {
    const point: MeetingPoint = {
      name: 'Piazzale Roma',
      address: 'Piazzale Roma, Venezia',
      latitude: 45.4381,
      longitude: 12.318,
      clientInstructions: 'Walk to Piazzale Roma.',
      clientInstructionsTranslations: { de: 'Gehen Sie zum Piazzale Roma.' },
    }
    const pipe = (locale: string) => {
      TestBed.resetTestingModule()
      TestBed.configureTestingModule({
        providers: [{ provide: LOCALE_ID, useValue: locale }],
      })
      return TestBed.runInInjectionContext(() => new InstructionsPipe())
    }

    expect(pipe('de').transform(point)).toBe('Gehen Sie zum Piazzale Roma.')
    // Not translated into French: the English text.
    expect(pipe('fr').transform(point)).toBe('Walk to Piazzale Roma.')
    expect(pipe('en').transform(null)).toBeNull()
  })
})
