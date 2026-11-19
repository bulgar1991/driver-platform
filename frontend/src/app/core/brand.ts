import active from '../../brands/active.json'
import { PartnerLogo } from './models'

/**
 * The client the site is built for. One folder per client in src/brands/ (brand.json + icons);
 * before the build, `scripts/brand.mjs` copies the chosen one (`BRAND=<folder>`, the Docker build
 * argument from deploy/.env) to src/brands/active.json, so a site never contains another client's
 * brand. The committed active.json is Magnus Drive, used in development.
 */
export interface Brand {
  /** As written in titles, emails and the footer. */
  name: string
  /** Under the app icon on a phone. */
  shortName: string
  tagline: string
  /** Logo text in capitals; the accent word is drawn in gold. */
  logo: { text: string; accent: string }
  /** Shown in the footer and the partner section only when filled in. */
  contact: { email: string; phone: string; whatsapp: string; address: string }
  social: { label: string; url: string }[]
  /** Only with each partner's written permission; files in src/assets/partners/. */
  partnerLogos: PartnerLogo[]
}

export const BRAND: Brand = active
