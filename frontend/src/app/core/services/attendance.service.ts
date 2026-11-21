import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  AttendanceCalendar,
  AttendanceEntry,
  AttendanceRequest,
  AttendanceType,
  AttendanceTypeRequest,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Attendance calendar of the staff. The kinds of event can only be changed by admins. */
@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/attendance`

  /** Dates yyyy-MM-dd; at most 400 days. */
  calendar(from: string, to: string): Observable<AttendanceCalendar> {
    const params = new HttpParams().set('from', from).set('to', to)
    return this.http.get<AttendanceCalendar>(this.url, { params })
  }

  set(request: AttendanceRequest): Observable<AttendanceEntry[]> {
    return this.http.put<AttendanceEntry[]>(this.url, request)
  }

  /** Removes every event of the person between the two dates. */
  clear(driverId: string, from: string, to: string): Observable<number> {
    return this.http.post<number>(`${this.url}/clear`, { driverId, from, to })
  }

  saveType(
    request: AttendanceTypeRequest,
    id?: string
  ): Observable<AttendanceType> {
    return id
      ? this.http.put<AttendanceType>(`${this.url}/types/${id}`, request)
      : this.http.post<AttendanceType>(`${this.url}/types`, request)
  }

  deleteType(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/types/${id}`)
  }
}
