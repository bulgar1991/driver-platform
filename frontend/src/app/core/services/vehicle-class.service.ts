import { HttpClient } from '@angular/common/http'
import { inject, Injectable, LOCALE_ID, signal } from '@angular/core'
import { Observable, tap } from 'rxjs'
import { siteLocale } from '../i18n/locales'
import { VehicleCategory, VehicleClass, VehicleClassNames } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/**
 * The tariff classes for the staff panel (cars, zones, bookings): names set in Admin → Pricing,
 * hidden ones included. Loaded once by the panel and again after a change in Pricing.
 */
@Injectable({ providedIn: 'root' })
export class VehicleClassService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/vehicle-classes`
  private readonly language = siteLocale(inject(LOCALE_ID))
    .code as keyof VehicleClassNames
  readonly classes = signal<VehicleClass[]>([])

  load(): Observable<VehicleClass[]> {
    return this.http.get<VehicleClass[]>(this.url).pipe(
      tap(list => {
        this.classes.set(list)
        for (const c of list) {
          const name = pickName(c.names, this.language)
          if (name) {
            CUSTOM_CLASS_NAMES[c.category] = name
          }
        }
      })
    )
  }
}

/** The panel's name for a class in a language, falling back to English; null when none was set. */
export function pickName(
  names: VehicleClassNames | null | undefined,
  language: keyof VehicleClassNames
): string | null {
  return names?.[language]?.trim() || names?.en?.trim() || null
}

/** Names set in Admin → Pricing, by class code (filled by VehicleClassService). */
export const CUSTOM_CLASS_NAMES: Record<VehicleCategory, string> = {}
