import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { StatisticsResult } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Admin → Statistics: a period's numbers against the period just before it. */
@Injectable({ providedIn: 'root' })
export class AdminStatisticsService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/statistics`

  get(from: string, to: string): Observable<StatisticsResult> {
    return this.http.get<StatisticsResult>(this.url, { params: { from, to } })
  }
}
