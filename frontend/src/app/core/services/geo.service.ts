import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { Place } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Address search, limited to Italy; predefined meeting points come first. */
@Injectable({ providedIn: 'root' })
export class GeoService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)

  search(query: string, lang = 'en'): Observable<Place[]> {
    const params = new HttpParams().set('q', query).set('lang', lang)
    return this.http.get<Place[]>(`${this.apiUrl}/geo/search`, { params })
  }

  reverse(latitude: number, longitude: number, lang = 'en'): Observable<Place> {
    const params = new HttpParams()
      .set('lat', latitude)
      .set('lng', longitude)
      .set('lang', lang)
    return this.http.get<Place>(`${this.apiUrl}/geo/reverse`, { params })
  }
}
