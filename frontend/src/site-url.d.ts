/** Public address of the site, set at build time: `ng build --define SITE_URL="'https://…'"`. */
declare const SITE_URL: string

/** The language at the root of the site ("it", "en"…), set at build time: `ng build --define SITE_LANGUAGE="'en'"`. */
declare const SITE_LANGUAGE: string

/** Where trips are served: "europe" or ISO country codes ("it,fr,ch,de"); `ng build --define SERVICE_AREA="'europe'"`. */
declare const SERVICE_AREA: string
