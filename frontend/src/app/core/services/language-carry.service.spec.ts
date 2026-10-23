import { TestBed } from '@angular/core/testing'
import { provideRouter } from '@angular/router'
import { LanguageCarryService } from './language-carry.service'

describe('LanguageCarryService', () => {
  /** A fresh service, as on the page loaded in the other language (it has read its carry). */
  const page = async () => {
    TestBed.resetTestingModule()
    TestBed.configureTestingModule({ providers: [provideRouter([])] })
    const service = TestBed.inject(LanguageCarryService)
    await service.take('')
    return service
  }

  beforeEach(() => {
    sessionStorage.clear()
    document.documentElement.removeAttribute('data-language-carried')
  })

  it('carries what the page provided over to the next page, once', async () => {
    const before = await page()
    before.provide('trip', () => ({ from: 'Fiumicino', pax: 2 }))
    before.provide('nothing', () => null)
    before.save()

    const after = await page()
    expect(await after.take('trip')).toEqual({ from: 'Fiumicino', pax: 2 })
    expect(await after.take('nothing')).toBeNull()
    expect(document.documentElement.hasAttribute('data-language-carried')).toBe(
      true
    )

    // A reload later in the same tab starts clean.
    expect(await (await page()).take('trip')).toBeNull()
  })

  it('ignores a switch from long ago', async () => {
    sessionStorage.setItem(
      'md.language-switch',
      JSON.stringify({
        at: Date.now() - 60_000,
        scrollY: 0,
        data: { trip: {} },
      })
    )

    expect(await (await page()).take('trip')).toBeNull()
  })

  it('stops carrying what a page no longer provides', async () => {
    const before = await page()
    const stop = before.provide('trip', () => ({ from: 'Fiumicino' }))
    stop()
    before.save()

    expect(await (await page()).take('trip')).toBeNull()
  })
})
