import { DOCUMENT } from '@angular/common'
import { DestroyRef, inject, Injectable, LOCALE_ID } from '@angular/core'
import { Meta, Title } from '@angular/platform-browser'
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  TitleStrategy,
} from '@angular/router'
import { filter } from 'rxjs'
import { IMAGES } from '../images.generated'
import { PageSeo } from '../models'
import { DEFAULT_LOCALE, SITE_LOCALES, siteLocale } from '../i18n/locales'
import { SITE_URL_TOKEN } from '../tokens/site-url.token'
import { CONTACT } from '@/features/home/home.content'

const SITE_NAME = 'Magnus Drive'
const SCHEMA_ID = 'structured-data'

/**
 * Keeps the head in step with the page: description, robots, canonical link, the same page in the
 * other languages (hreflang), Open Graph / Twitter preview and, on the home page, schema.org data. Runs while prerendering too, so crawlers and
 * link previews (WhatsApp, Facebook…) get them without running scripts.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly router = inject(Router)
  private readonly meta = inject(Meta)
  private readonly title = inject(Title)
  private readonly titleStrategy = inject(TitleStrategy)
  private readonly document = inject(DOCUMENT)
  private readonly siteUrl = inject(SITE_URL_TOKEN)
  private readonly locale = siteLocale(inject(LOCALE_ID))

  /** Called once at startup. */
  start(destroyRef: DestroyRef): void {
    const subscription = this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => this.update())
    destroyRef.onDestroy(() => subscription.unsubscribe())
  }

  private update(): void {
    const snapshot = this.router.routerState.snapshot
    const seo = deepestData<PageSeo>(snapshot.root, 'seo')
    const path = this.router.url.split(/[?#]/)[0].replace(/\/+$/, '')
    const url = this.absolute(this.locale.prefix + path || '/')
    // From the route itself: the router may set the document title only after this runs.
    const title =
      this.titleStrategy.buildTitle(snapshot) ?? this.title.getTitle()

    this.meta.updateTag({
      name: 'robots',
      content: seo
        ? 'index, follow, max-image-preview:large'
        : 'noindex, nofollow',
    })
    this.setLink('canonical', seo ? url : null)
    this.setAlternates(seo ? path : null)
    if (!seo) {
      for (const tag of ['name="description"', 'property="og:url"']) {
        this.meta.removeTag(tag)
      }
      this.setSchema(null)
      return
    }

    const image = this.absolute(IMAGES.og.src)
    const tags: Record<string, string> = {
      'og:type': 'website',
      'og:site_name': SITE_NAME,
      'og:locale': this.locale.og,
      'og:title': title,
      'og:description': seo.description,
      'og:url': url,
      'og:image': image,
      'og:image:width': `${IMAGES.og.width}`,
      'og:image:height': `${IMAGES.og.height}`,
      'og:image:alt': $localize`Black chauffeur-driven car on an Italian road`,
    }
    this.meta.updateTag({ name: 'description', content: seo.description })
    for (const [property, content] of Object.entries(tags)) {
      this.meta.updateTag({ property, content })
    }
    this.meta.updateTag({
      name: 'twitter:card',
      content: 'summary_large_image',
    })
    this.meta.updateTag({ name: 'twitter:title', content: title })
    this.meta.updateTag({
      name: 'twitter:description',
      content: seo.description,
    })
    this.meta.updateTag({ name: 'twitter:image', content: image })
    this.setSchema(
      seo.schema === 'business' ? this.businessSchema(seo.description) : null
    )
  }

  /** Only facts the site states: no invented address, phone or ratings. */
  private businessSchema(description: string): object {
    const home = this.absolute(this.locale.prefix + '/')
    // One organisation whatever the language: its id is the Italian (default) home page.
    const organization = `${this.absolute('/')}#organization`
    return {
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'TaxiService',
          '@id': `${home}#service`,
          name: `${SITE_NAME} · Private Driver Services`,
          description,
          url: home,
          image: this.absolute(IMAGES.og.src),
          serviceType: 'Private transfers with driver',
          areaServed: ['Italy', 'France', 'Switzerland', 'Germany'].map(
            name => ({ '@type': 'Country', name })
          ),
          provider: { '@id': organization },
          potentialAction: {
            '@type': 'ReserveAction',
            target: this.absolute(this.locale.prefix + '/book'),
          },
        },
        {
          '@type': 'Organization',
          '@id': organization,
          name: SITE_NAME,
          url: home,
          logo: this.absolute('/assets/brand/icon-512.png'),
          ...(CONTACT.email ? { email: CONTACT.email } : {}),
          ...(CONTACT.phone ? { telephone: CONTACT.phone } : {}),
          ...(CONTACT.address ? { address: CONTACT.address } : {}),
        },
        {
          '@type': 'WebSite',
          '@id': `${home}#website`,
          name: SITE_NAME,
          url: home,
          publisher: { '@id': organization },
          inLanguage: this.locale.code,
        },
      ],
    }
  }

  /** The same page in every language, plus x-default (Italian) for everyone else. */
  private setAlternates(path: string | null): void {
    this.document.head
      .querySelectorAll('link[rel="alternate"][hreflang]')
      .forEach(link => link.remove())
    if (path === null) {
      return
    }
    const links = [
      ...SITE_LOCALES.map(l => [l.code, l.prefix] as const),
      ['x-default', DEFAULT_LOCALE.prefix] as const,
    ]
    for (const [hreflang, prefix] of links) {
      const link = this.document.createElement('link')
      link.setAttribute('rel', 'alternate')
      link.setAttribute('hreflang', hreflang)
      link.setAttribute('href', this.absolute(prefix + path || '/'))
      this.document.head.appendChild(link)
    }
  }

  private absolute(path: string): string {
    return `${this.siteUrl}${path}`
  }

  private setLink(rel: string, href: string | null): void {
    let link = this.document.head.querySelector<HTMLLinkElement>(
      `link[rel="${rel}"]`
    )
    if (!href) {
      link?.remove()
      return
    }
    if (!link) {
      link = this.document.createElement('link')
      link.setAttribute('rel', rel)
      this.document.head.appendChild(link)
    }
    link.setAttribute('href', href)
  }

  private setSchema(data: object | null): void {
    let script = this.document.getElementById(SCHEMA_ID)
    if (!data) {
      script?.remove()
      return
    }
    if (!script) {
      script = this.document.createElement('script')
      script.id = SCHEMA_ID
      script.setAttribute('type', 'application/ld+json')
      this.document.head.appendChild(script)
    }
    // "<" escaped so the text can never close the script tag.
    script.textContent = JSON.stringify(data).replace(/</g, '\\u003c')
  }
}

function deepestData<T>(
  route: ActivatedRouteSnapshot,
  key: string
): T | undefined {
  let value: T | undefined
  for (let r: ActivatedRouteSnapshot | null = route; r; r = r.firstChild) {
    if (r.data[key]) {
      value = r.data[key] as T
    }
  }
  return value
}
