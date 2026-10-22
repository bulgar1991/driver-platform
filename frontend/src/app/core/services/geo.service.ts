import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable, LOCALE_ID } from '@angular/core'
import { Observable } from 'rxjs'
import { siteLocale } from '../i18n/locales'
import { Place } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Address search in the countries served (Italy, France, Switzerland, Germany); predefined meeting points come first. */
@Injectable({ providedIn: 'root' })
export class GeoService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)
  /** Place names in the site language: "Mailand" on the German site, "Milano" on the Italian one. */
  private readonly language = siteLocale(inject(LOCALE_ID)).code

  search(query: string, lang = this.language): Observable<Place[]> {
    const params = new HttpParams().set('q', query).set('lang', lang)
    return this.http.get<Place[]>(`${this.apiUrl}/geo/search`, { params })
  }

  reverse(
    latitude: number,
    longitude: number,
    lang = this.language
  ): Observable<Place> {
    const params = new HttpParams()
      .set('lat', latitude)
      .set('lng', longitude)
      .set('lang', lang)
    return this.http.get<Place>(`${this.apiUrl}/geo/reverse`, { params })
  }
}
