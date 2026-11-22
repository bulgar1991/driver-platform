import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { ExpirationsResponse, RenewDeadlineRequest } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Admin → Expirations: every deadline of drivers and cars, and renewing one. */
@Injectable({ providedIn: 'root' })
export class AdminExpirationsService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/expirations`

  list(includeInactive = false): Observable<ExpirationsResponse> {
    const params = new HttpParams().set('includeInactive', includeInactive)
    return this.http.get<ExpirationsResponse>(this.url, { params })
  }

  renew(request: RenewDeadlineRequest): Observable<void> {
    return this.http.put<void>(`${this.url}/renew`, request)
  }
}
