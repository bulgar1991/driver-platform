import { splitLogo } from './logo.component'

describe('splitLogo', () => {
  it('draws the accent word in gold wherever it is in the name', () => {
    expect(splitLogo('MAGNUS DRIVE', 'DRIVE')).toEqual({
      before: 'MAGNUS ',
      accent: 'DRIVE',
      after: '',
    })
    expect(splitLogo('HELVETIC VIP TRANSFER', 'VIP')).toEqual({
      before: 'HELVETIC ',
      accent: 'VIP',
      after: ' TRANSFER',
    })
  })

  it('shows the plain name when the accent is missing', () => {
    expect(splitLogo('ACME', 'GOLD')).toEqual({
      before: 'ACME',
      accent: '',
      after: '',
    })
  })
})
