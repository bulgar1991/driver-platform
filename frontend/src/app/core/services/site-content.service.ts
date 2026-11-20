import { HttpClient } from '@angular/common/http'
import {
  inject,
  Injectable,
  LOCALE_ID,
  PLATFORM_ID,
  signal,
} from '@angular/core'
import { isPlatformBrowser } from '@angular/common'
import { siteLocale } from '../i18n/locales'
import { ContentText, ImageAsset, SiteContent } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/**
 * Texts and pictures of the public website changed in Admin → Website content. Every place on the
 * site asks with a key and its built-in text or picture: what was changed in the panel is shown,
 * the built-in content otherwise (also while loading, on the prerendered page and when the API is
 * down).
 */
@Injectable({ providedIn: 'root' })
export class SiteContentService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID))
  private readonly language = siteLocale(inject(LOCALE_ID))
    .code as keyof ContentText
  private readonly content = signal<SiteContent | null>(null)
  private loading = false

  /** Once per visit, in the browser (the prerendered page keeps the built-in content). */
  load(): void {
    if (!this.browser || this.loading) {
      return
    }
    this.loading = true
    this.http.get<SiteContent>(`${this.apiUrl}/content`).subscribe({
      next: content => this.content.set(content),
      error: () => (this.loading = false),
    })
  }

  /** After a change in the panel: the site shows it at once. */
  set(content: SiteContent): void {
    this.content.set(content)
  }

  /** The text for this key in the site's language, or the built-in one. */
  text(key: string, builtIn: string): string {
    return this.content()?.texts[key]?.[this.language] || builtIn
  }

  /** The picture uploaded for this key, or the built-in one. */
  image(key: string, builtIn: ImageAsset): ImageAsset {
    const image = this.content()?.images[key]
    return image
      ? { src: image.url, width: image.width, height: image.height }
      : builtIn
  }
}
