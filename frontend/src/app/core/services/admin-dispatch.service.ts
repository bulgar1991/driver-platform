import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  AssignmentStatus,
  Availability,
  BookingDetail,
  BookingListItem,
  BookingQuery,
  BookingStatus,
  Dashboard,
  PagedResult,
  Shift,
  ShiftRequest,
  ShiftResult,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Dispatcher panel: bookings, shifts and the dashboard. */
@Injectable({ providedIn: 'root' })
export class AdminDispatchService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin`

  dashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${this.url}/dashboard`)
  }

  bookings(query: BookingQuery): Observable<PagedResult<BookingListItem>> {
    let params = new HttpParams()
    for (const [key, value] of Object.entries(query)) {
      if (
        value !== null &&
        value !== undefined &&
        value !== '' &&
        value !== false
      ) {
        params = params.set(key, String(value))
      }
    }
    return this.http.get<PagedResult<BookingListItem>>(`${this.url}/bookings`, {
      params,
    })
  }

  booking(id: string): Observable<BookingDetail> {
    return this.http.get<BookingDetail>(`${this.url}/bookings/${id}`)
  }

  setBookingStatus(
    id: string,
    status: BookingStatus,
    reason?: string
  ): Observable<BookingDetail> {
    return this.http.put<BookingDetail>(`${this.url}/bookings/${id}/status`, {
      status,
      reason,
    })
  }

  setBookingNotes(
    id: string,
    internalNotes: string
  ): Observable<BookingDetail> {
    return this.http.put<BookingDetail>(`${this.url}/bookings/${id}/notes`, {
      internalNotes,
    })
  }

  shifts(from: string, to: string): Observable<Shift[]> {
    const params = new HttpParams().set('from', from).set('to', to)
    return this.http.get<Shift[]>(`${this.url}/shifts`, { params })
  }

  availability(
    bookingIds: string[],
    shiftId?: string | null
  ): Observable<Availability> {
    let params = new HttpParams()
    for (const id of bookingIds) {
      params = params.append('bookingIds', id)
    }
    if (shiftId) {
      params = params.set('shiftId', shiftId)
    }
    return this.http.get<Availability>(`${this.url}/shifts/availability`, {
      params,
    })
  }

  createShift(request: ShiftRequest): Observable<ShiftResult> {
    return this.http.post<ShiftResult>(`${this.url}/shifts`, request)
  }

  updateShift(id: string, request: ShiftRequest): Observable<ShiftResult> {
    return this.http.put<ShiftResult>(`${this.url}/shifts/${id}`, request)
  }

  setShiftStatus(
    id: string,
    status: AssignmentStatus,
    reason?: string
  ): Observable<Shift> {
    return this.http.put<Shift>(`${this.url}/shifts/${id}/status`, {
      status,
      reason,
    })
  }
}
