import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { Zone, ZoneRequest } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

@Injectable({ providedIn: 'root' })
export class AdminZonesService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/zones`

  list(): Observable<Zone[]> {
    return this.http.get<Zone[]>(this.url)
  }

  create(zone: ZoneRequest): Observable<Zone> {
    return this.http.post<Zone>(this.url, zone)
  }

  update(id: string, zone: ZoneRequest): Observable<Zone> {
    return this.http.put<Zone>(`${this.url}/${id}`, zone)
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`)
  }
}
