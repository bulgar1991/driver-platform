import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { ReportKind, SearchOptions, SearchQuery, SearchResult } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Admin → Search: rides by filters, and PDF reports of them. */
@Injectable({ providedIn: 'root' })
export class AdminSearchService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/search`

  options(): Observable<SearchOptions> {
    return this.http.get<SearchOptions>(`${this.url}/options`)
  }

  search(query: SearchQuery): Observable<SearchResult> {
    return this.http.get<SearchResult>(this.url, { params: toParams(query) })
  }

  report(query: SearchQuery, kind: ReportKind): Observable<Blob> {
    return this.http.get(`${this.url}/report`, {
      params: toParams(query).set('kind', kind),
      responseType: 'blob',
    })
  }
}

/** Empty filters are left out; lists repeat the key (statuses=New&statuses=Assigned). */
export function toParams(query: SearchQuery): HttpParams {
  let params = new HttpParams()
  for (const [key, value] of Object.entries(query)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        params = params.append(key, item)
      }
    } else if (
      value !== null &&
      value !== undefined &&
      value !== '' &&
      value !== false &&
      value !== 'Any'
    ) {
      params = params.set(key, String(value))
    }
  }
  return params
}
