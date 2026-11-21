import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { CalendarData, Shift } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** The services calendar. */
@Injectable({ providedIn: 'root' })
export class AdminCalendarService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin`

  /** Office dates yyyy-MM-dd, inclusive; at most 62 days. */
  calendar(from: string, to: string): Observable<CalendarData> {
    const params = new HttpParams().set('from', from).set('to', to)
    return this.http.get<CalendarData>(`${this.url}/calendar`, { params })
  }

  shift(id: string): Observable<Shift> {
    return this.http.get<Shift>(`${this.url}/shifts/${id}`)
  }
}
