import { DOCUMENT, isPlatformServer } from '@angular/common'
import { inject, InjectionToken, PLATFORM_ID } from '@angular/core'

/**
 * The site's public address without a trailing slash (https://magnusdrive.example), for canonical
 * links and social previews. Set at build time (the Docker build passes SITE_DOMAIN). Without it:
 * the browser's own address, or nothing while prerendering (links are then site-relative).
 */
export const SITE_URL_TOKEN = new InjectionToken<string>('SITE_URL', {
  providedIn: 'root',
  factory: () => {
    if (typeof SITE_URL === 'string' && SITE_URL) {
      return SITE_URL.replace(/\/+$/, '')
    }
    if (isPlatformServer(inject(PLATFORM_ID))) {
      return ''
    }
    return inject(DOCUMENT).defaultView?.location.origin ?? ''
  },
})
