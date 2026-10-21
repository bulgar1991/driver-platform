import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { DriverPortal, ReviewPage } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Public pages opened from links: the driver's shifts and the client's review. */
@Injectable({ providedIn: 'root' })
export class DriverPortalService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)

  get(token: string): Observable<DriverPortal> {
    return this.http.get<DriverPortal>(this.url(token))
  }

  confirm(token: string, shiftId: string): Observable<DriverPortal> {
    return this.http.post<DriverPortal>(
      `${this.url(token)}/shifts/${shiftId}/confirm`,
      null
    )
  }

  pickedUp(token: string, bookingId: string): Observable<DriverPortal> {
    return this.http.post<DriverPortal>(
      `${this.url(token)}/bookings/${bookingId}/picked-up`,
      null
    )
  }

  completed(token: string, bookingId: string): Observable<DriverPortal> {
    return this.http.post<DriverPortal>(
      `${this.url(token)}/bookings/${bookingId}/completed`,
      null
    )
  }

  review(token: string): Observable<ReviewPage> {
    return this.http.get<ReviewPage>(
      `${this.apiUrl}/reviews/${encodeURIComponent(token)}`
    )
  }

  submitReview(
    token: string,
    rating: number,
    comment: string
  ): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/reviews/${encodeURIComponent(token)}`,
      { rating, comment }
    )
  }

  private url(token: string): string {
    return `${this.apiUrl}/driver/${encodeURIComponent(token)}`
  }
}
