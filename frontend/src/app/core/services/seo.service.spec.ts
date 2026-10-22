import { Component, DestroyRef, inject, LOCALE_ID } from '@angular/core'
import { TestBed } from '@angular/core/testing'
import { DOCUMENT } from '@angular/common'
import { provideRouter, Router } from '@angular/router'
import { SITE_URL_TOKEN } from '../tokens/site-url.token'
import { SeoService } from './seo.service'

@Component({ template: '' })
class PageComponent {}

describe('SeoService', () => {
  let document: Document
  const meta = (attr: string) =>
    document.head.querySelector(`meta[${attr}]`)?.getAttribute('content')

  const setUp = (locale: string) => {
    TestBed.configureTestingModule({
      providers: [
        { provide: LOCALE_ID, useValue: locale },
        { provide: SITE_URL_TOKEN, useValue: 'https://magnus.example' },
        provideRouter([
          {
            path: '',
            title: 'Home · Magnus Drive',
            component: PageComponent,
            data: {
              seo: { description: 'Transfers in Italy.', schema: 'business' },
            },
          },
          {
            path: 'tariffs',
            title: 'Tariffs · Magnus Drive',
            component: PageComponent,
            data: { seo: { description: 'Prices.' } },
          },
          { path: 'admin', title: 'Panel', component: PageComponent },
        ]),
      ],
    })
    document = TestBed.inject(DOCUMENT)
    TestBed.runInInjectionContext(() =>
      inject(SeoService).start(inject(DestroyRef))
    )
  }

  // The Italian site (the default, at the root) unless a test says otherwise.
  beforeEach(() => setUp('it'))

  it('describes public pages for search engines and link previews', async () => {
    await TestBed.inject(Router).navigateByUrl('/tariffs?ref=ad')

    expect(meta('name="description"')).toBe('Prices.')
    expect(meta('name="robots"')).toContain('index, follow')
    expect(
      document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')
    ).toBe('https://magnus.example/tariffs')
    expect(meta('property="og:title"')).toBe('Tariffs · Magnus Drive')
    expect(meta('property="og:url"')).toBe('https://magnus.example/tariffs')
    expect(meta('property="og:image"')).toBe(
      'https://magnus.example/assets/images/og.jpg'
    )
    expect(document.getElementById('structured-data')).toBeNull()
  })

  it('adds the business data on the home page only', async () => {
    await TestBed.inject(Router).navigateByUrl('/')
    const data = JSON.parse(
      document.getElementById('structured-data')!.textContent!
    )
    expect(data['@graph'][0]['@type']).toBe('TaxiService')
    expect(data['@graph'][0].url).toBe('https://magnus.example/')

    await TestBed.inject(Router).navigateByUrl('/tariffs')
    expect(document.getElementById('structured-data')).toBeNull()
  })

  it('links every language version of the page, with Italian as the default', async () => {
    TestBed.resetTestingModule()
    setUp('de')
    await TestBed.inject(Router).navigateByUrl('/tariffs')

    const alternates = Object.fromEntries(
      [
        ...document.head.querySelectorAll('link[rel="alternate"][hreflang]'),
      ].map(l => [l.getAttribute('hreflang'), l.getAttribute('href')])
    )
    expect(
      document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')
    ).toBe('https://magnus.example/de/tariffs')
    expect(meta('property="og:locale"')).toBe('de_DE')
    expect(alternates).toEqual({
      it: 'https://magnus.example/tariffs',
      en: 'https://magnus.example/en/tariffs',
      de: 'https://magnus.example/de/tariffs',
      fr: 'https://magnus.example/fr/tariffs',
      'x-default': 'https://magnus.example/tariffs',
    })
  })

  it('keeps pages without SEO data out of search results', async () => {
    await TestBed.inject(Router).navigateByUrl('/tariffs')
    await TestBed.inject(Router).navigateByUrl('/admin')

    expect(meta('name="robots"')).toBe('noindex, nofollow')
    expect(document.head.querySelector('link[rel="canonical"]')).toBeNull()
    expect(meta('name="description"')).toBeUndefined()
  })
})
