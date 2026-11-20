import { TestBed } from '@angular/core/testing'
import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { ImageAsset } from '../models'
import { SiteContentService } from './site-content.service'

describe('SiteContentService', () => {
  const builtInImage: ImageAsset = {
    src: '/assets/images/hero-1254.webp',
    width: 1254,
    height: 470,
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
    return {
      site: TestBed.inject(SiteContentService),
      http: TestBed.inject(HttpTestingController),
    }
  }

  it('shows the built-in content until something was changed in the panel', () => {
    const { site, http } = create()
    expect(site.text('welcome.title', 'Arrive like you belong here')).toBe(
      'Arrive like you belong here'
    )

    site.load()
    http.expectOne('/api/content').flush({
      texts: {
        'welcome.title': {
          en: 'Arrive in Zurich like a local',
          it: null,
          de: null,
          fr: null,
        },
        'welcome.eyebrow': { en: null, it: 'Benvenuti', de: null, fr: null },
      },
      images: {
        'welcome.image': { url: '/api/photos/abc', width: 1600, height: 600 },
      },
    })

    // The test build is English.
    expect(site.text('welcome.title', 'x')).toBe(
      'Arrive in Zurich like a local'
    )
    // Only Italian was changed: English keeps the built-in text.
    expect(site.text('welcome.eyebrow', 'Welcome')).toBe('Welcome')
    expect(site.image('welcome.image', builtInImage)).toEqual({
      src: '/api/photos/abc',
      width: 1600,
      height: 600,
    })
    expect(site.image('vehicle.bus.image', builtInImage)).toBe(builtInImage)
  })

  it('keeps the built-in content when the API is down', () => {
    const { site, http } = create()
    site.load()
    http
      .expectOne('/api/content')
      .flush(null, { status: 503, statusText: 'Unavailable' })
    expect(site.text('welcome.title', 'Built in')).toBe('Built in')
  })
})
