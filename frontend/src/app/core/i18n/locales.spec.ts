import { siteLocale, stripLocalePrefix } from './locales'

describe('locales', () => {
  it('drops stray language prefixes, however many', () => {
    expect(stripLocalePrefix('/de/fr/en/de/de/en/tariffs')).toBe('/tariffs')
    expect(stripLocalePrefix('/fr')).toBe('/')
    expect(stripLocalePrefix('/en?x=1')).toBe('/?x=1')
    expect(stripLocalePrefix('/tariffs')).toBe('/tariffs')
    // Only whole segments: /drivers is not "/d" + "rivers".
    expect(stripLocalePrefix('/drivers')).toBe('/drivers')
    expect(stripLocalePrefix('/frontier')).toBe('/frontier')
  })

  it('maps Angular locale ids to site languages', () => {
    expect(siteLocale('de').prefix).toBe('/de')
    expect(siteLocale('en-US').code).toBe('en')
    expect(siteLocale('es').code).toBe('it')
  })
})
