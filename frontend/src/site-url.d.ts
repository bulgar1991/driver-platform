/** Public address of the site, set at build time: `ng build --define SITE_URL="'https://…'"`. */
declare const SITE_URL: string

/** The language at the root of the site ("it", "en"…), set at build time: `ng build --define SITE_LANGUAGE="'en'"`. */
declare const SITE_LANGUAGE: string

/** Where trips are served: "europe" or ISO country codes ("it,fr,ch,de"); `ng build --define SERVICE_AREA="'europe'"`. */
declare const SERVICE_AREA: string

/** The company's time zone (IANA, e.g. "Europe/Zurich"); `ng build --define TIME_ZONE="'Europe/Rome'"`. */
declare const TIME_ZONE: string

/** The site's currency, a three-letter ISO code ("EUR", "CHF"); `ng build --define CURRENCY="'CHF'"`. */
declare const CURRENCY: string
