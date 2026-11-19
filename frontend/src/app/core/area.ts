import { LatLng } from './models'

/**
 * Where trips are served, set per server (SERVICE_AREA in deploy/.env → `ng build --define
 * SERVICE_AREA`): "europe" (default) or ISO country codes such as "it,fr,ch,de". The backend
 * enforces it (address search, prices); here it picks the wording and the map's first view.
 */
const AREA =
  typeof SERVICE_AREA === 'string' && SERVICE_AREA.trim()
    ? SERVICE_AREA.trim().toLowerCase().replace(/\s+/g, '')
    : 'europe'

const FOUR_COUNTRIES = 'it,fr,ch,de'
const isFour =
  AREA.split(',').sort().join(',') ===
  FOUR_COUNTRIES.split(',').sort().join(',')

/** "across Europe", "in Italy, France, Switzerland and Germany", or "in the countries we serve". */
export const AREA_IN =
  AREA === 'europe'
    ? $localize`across Europe`
    : isFour
      ? $localize`in Italy, France, Switzerland and Germany`
      : $localize`in the countries we serve`

/** First view of the maps: the area served. */
export const AREA_BOUNDS: [LatLng, LatLng] = isFour
  ? [
      [37.5, -1.5],
      [53.5, 15],
    ]
  : [
      [36, -10],
      [60, 28],
    ]
