/**
 * The site's currency (CURRENCY in deploy/.env → `ng build --define CURRENCY`): "EUR" by default,
 * "CHF" for a Swiss client. Tariffs are entered in it and prices shown in it; a booking keeps the
 * currency it was made in (it comes with the booking).
 */
export const SITE_CURRENCY =
  typeof CURRENCY === 'string' && /^[A-Za-z]{3}$/.test(CURRENCY.trim())
    ? CURRENCY.trim().toUpperCase()
    : 'EUR'

/** For labels: "Base fee (€)", "Base fee (CHF)". */
export const CURRENCY_LABEL = SITE_CURRENCY === 'EUR' ? '€' : SITE_CURRENCY
